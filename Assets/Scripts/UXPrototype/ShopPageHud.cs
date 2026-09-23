using DG.Tweening;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// World-space shop-page avatar + HP pill (plan-shop-topbar-world-scroll-2026-09-21 §3.2).
// The whole shop top bar is PAGE content (ShopChrome 2026-09-21 world scroll); the canvas
// combat HUD pieces hide in a settled shop, so this component mirrors them as world objects
// parented under the ShopChrome root — show/hide and wheel scroll follow the chrome for
// free. Mirror, don't redesign: at Bootstrap the live canvas pieces (CombatIconPresenter
// playerIcon hierarchy, player-side HPNumericDisplayHorizontal displayRoot hierarchy) are
// walked and their sprites/colors/fonts copied, with every canvas px converted through
// CanvasPxToWorld so the settled shop renders pixel-identical at scroll 0.
//
// The HP digits are ONE world TMP "hp/hpMax" (no odometer strips, no RectMask2D): the shop
// invariant keeps hp at hpMax, but HP utility buy/sell moves hpMax mid-visit (user ruling
// 2026-09-21), so value changes play a plain integer count-up/down tween (~0.3 s, text
// rewritten per tick) — the visible feedback is the number climbing, matching the canvas
// pill minus the strip mechanics. Refreshed from ShopChrome.Refresh (diff-guarded, same
// pattern as the chips). The name label polls PlayerIdentity.Username with the presenter's
// diff-guard convention. Pure presentation; nothing interactive (no colliders — the
// world-only input rule, one physics pipeline via ShopInputGate, is untouched).
public class ShopPageHud : MonoBehaviour
{
	private const string HudName = "Shop Page Hud";
	// World TMP sizing: 1 font point of em height = 0.1 world units (TMP world-space
	// convention; consistent with the ShopChrome chip calibration of ~0.13 u per point).
	private const float TmpWorldUnitsPerEm = 0.1f;
	private const float CountUpSeconds = 0.3f;
	// Layered z offsets (chrome-root local): larger z = farther from the camera = behind, so
	// the canvas sibling draw order (back to front) maps to descending z.
	private const float ZStep = 0.01f;
	private const float ZText = 0.01f;

	private static ShopPageHud _instance;
	public static ShopPageHud Instance => _instance;

	private TextMeshPro _nameLabel;
	private TextMeshPro _hpValue;
	private string _lastUsername;
	private int _targetHp = int.MinValue;
	private int _targetHpMax = int.MinValue;
	private int _shownHp;
	private int _shownHpMax;
	private Tween _countTween;
	// Live-tuning flag (ShopLayoutConfigSO.OnValidate → ApplyMirrorLayout): set mid-travel,
	// consumed by the next active Update — the chrome root is inactive during the camera
	// flight, so the rebuild naturally waits for the shop to be visible again.
	private bool _mirrorDirty;

	/// <summary>
	/// Pure canvas px -> world units conversion (plan §3.2 calibration; EditMode goldens in
	/// ShopPageHudTests): screenPx = canvasPx x localScale x canvasScaleFactor, and the ortho
	/// camera maps Screen.height screen px onto 2 x orthoSize world units.
	/// </summary>
	public static float CanvasPxToWorld(float canvasPx, float localScale, float canvasScaleFactor, float screenH, float orthoSize)
	{
		if (screenH <= 0f) return 0f;
		return canvasPx * localScale * canvasScaleFactor / screenH * (2f * orthoSize);
	}

	/// <summary>
	/// Canvas scale factor without the first-render timing hazard: canvas.scaleFactor is only
	/// recomputed on the first willRenderCanvases pass (ShopUXManager.Start runs before it),
	/// so derive it from the CanvasScaler exactly the way ScaleWithScreenSize does (exponential
	/// lerp of the width/height factors — same formula as the UI package source).
	/// </summary>
	public static float ComputeCanvasScaleFactor(Canvas canvas)
	{
		if (canvas == null) return 1f;
		CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
		if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize
			|| scaler.referenceResolution.x <= 0f || scaler.referenceResolution.y <= 0f)
		{
			return canvas.scaleFactor > 0.001f ? canvas.scaleFactor : 1f;
		}
		float logW = Mathf.Log(Screen.width / scaler.referenceResolution.x, 2f);
		float logH = Mathf.Log(Screen.height / scaler.referenceResolution.y, 2f);
		return Mathf.Pow(2f, Mathf.Lerp(logW, logH, scaler.matchWidthOrHeight));
	}

	/// <summary>
	/// Builds the hud once (idempotent) under the chrome root. Sources are the live canvas
	/// pieces; a missing source skips that block with a warning (the rest still works).
	/// </summary>
	public static ShopPageHud Bootstrap(Transform chromeRoot)
	{
		if (_instance != null) return _instance;
		GameObject root = new GameObject(HudName);
		root.transform.SetParent(chromeRoot, false);
		_instance = root.AddComponent<ShopPageHud>();
		_instance.Build();
		return _instance;
	}

	private void Build()
	{
		Camera cam = Camera.main;
		if (cam == null)
		{
			TestManager.LogWarning("[ShopPageHud] Camera.main missing at Build — world avatar/HP skipped");
			return;
		}
		GameObject iconSource = FindPlayerIconSource();
		if (iconSource != null)
		{
			float unit = ShopTopBarLayout.PlayerIconShopScale * ComputeCanvasScaleFactor(iconSource.GetComponentInParent<Canvas>())
				* (2f * cam.orthographicSize / Screen.height);
			BuildAvatar(iconSource.transform, unit, cam);
		}

		HPNumericDisplayHorizontal pillSource = FindPlayerPillSource();
		if (pillSource != null)
		{
			float unit = ShopTopBarLayout.HpDisplayShopScale * ComputeCanvasScaleFactor(pillSource.GetComponentInParent<Canvas>())
				* (2f * cam.orthographicSize / Screen.height);
			BuildHpPill(pillSource, unit, cam);
		}

		InitHpText();
	}

	/// <summary>
	/// Live-tuning path (ShopLayoutConfigSO.OnValidate): rebuilds the world avatar/HP
	/// mirrors so retuned viewport anchors / shop scales apply immediately. Mid-travel it
	/// only flags — the canvas source pieces are being tweened then, and mirroring them
	/// would bake half-flown offsets into the copies.
	/// </summary>
	public void ApplyMirrorLayout()
	{
		if (PhaseTransitionDriver.IsTransitioning)
		{
			_mirrorDirty = true;
			return;
		}
		RebuildMirrors();
	}

	private void RebuildMirrors()
	{
		KillTween(ref _countTween);
		for (int i = transform.childCount - 1; i >= 0; i--)
		{
			Destroy(transform.GetChild(i).gameObject);
		}
		_nameLabel = null;
		_hpValue = null;
		Build();
	}

	// ------------------------------------------------------------------ avatar mirror

	private void BuildAvatar(Transform iconSource, float unit, Camera cam)
	{
		Vector2 center = new Vector2(
			ShopTopBarLayout.ChromeLocalXFromLeftOffset(ShopTopBarLayout.PlayerIconXFromLeftEdge, cam.orthographicSize * cam.aspect),
			ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.PlayerIconViewportY, cam));
		GameObject avatar = new GameObject("Avatar");
		avatar.transform.SetParent(transform, false);
		avatar.transform.localPosition = center;

		int count = iconSource.childCount;
		for (int i = 0; i < count; i++)
		{
			Transform child = iconSource.GetChild(i);
			if (!child.gameObject.activeSelf) continue; // e.g. PhysicalCardShadow stays hidden
			Image img = child.GetComponent<Image>();
			TMP_Text srcTmp = child.GetComponent<TMP_Text>();
			if (img == null && srcTmp == null) continue;
			GameObject go = new GameObject(child.name);
			go.transform.SetParent(avatar.transform, false);
			Vector2 off = GetAnchoredOffset(child);
			go.transform.localPosition = new Vector3(off.x * unit, off.y * unit, (count - 1 - i) * ZStep + ZText);
			if (img != null)
			{
				MirrorSprite(go, img, unit);
			}
			else
			{
				_nameLabel = go.AddComponent<TextMeshPro>();
				CopyText(_nameLabel, srcTmp, unit, GameColorPalette.IconNameLabelColor);
			}
		}
	}

	// ------------------------------------------------------------------ HP pill mirror

	private void BuildHpPill(HPNumericDisplayHorizontal pillSource, float unit, Camera cam)
	{
		Vector2 center = new Vector2(
			ShopTopBarLayout.ChromeLocalXFromLeftOffset(ShopTopBarLayout.HpDisplayXFromLeftEdge, cam.orthographicSize * cam.aspect),
			ShopTopBarLayout.ViewportToChromeLocalY(ShopTopBarLayout.HpDisplayViewportY, cam));
		GameObject pill = new GameObject("HpPill");
		pill.transform.SetParent(transform, false);
		// displayRoot rides at its own anchored offset inside the (zero-size) pill root; the
		// runtime LayoutRoots centers the digit row on the displayRoot pivot, so the value
		// text below goes at the displayRoot center too.
		Vector2 dispOff = GetAnchoredOffset(pillSource.displayRoot != null ? pillSource.displayRoot.transform : null);
		pill.transform.localPosition = center + new Vector2(dispOff.x * unit, dispOff.y * unit);

		int count = pillSource.displayRoot != null ? pillSource.displayRoot.childCount : 0;
		for (int i = 0; i < count; i++)
		{
			Transform child = pillSource.displayRoot.GetChild(i);
			if (!child.gameObject.activeSelf) continue;
			// The digit groups (CurrentRoot / Slash / MaxRoot) collapse into ONE value TMP
			// centered on the displayRoot pivot — no odometer strips in the world pill.
			if (child.name == "CurrentRoot" || child.name == "Slash" || child.name == "MaxRoot") continue;
			Image img = child.GetComponent<Image>();
			TMP_Text srcTmp = child.GetComponent<TMP_Text>();
			if (img == null && srcTmp == null) continue;
			GameObject go = new GameObject(child.name);
			go.transform.SetParent(pill.transform, false);
			Vector2 off = GetAnchoredOffset(child);
			go.transform.localPosition = new Vector3(off.x * unit, off.y * unit, (count - 1 - i) * ZStep + ZText);
			if (img != null)
			{
				MirrorSprite(go, img, unit);
			}
			else
			{
				// "HP" prefix word
				TextMeshPro word = go.AddComponent<TextMeshPro>();
				CopyText(word, srcTmp, unit, GameColorPalette.HpNormalPlayerColor);
			}
		}

		_hpValue = new GameObject("HpValue").AddComponent<TextMeshPro>();
		_hpValue.transform.SetParent(pill.transform, false);
		_hpValue.transform.localPosition = new Vector3(0f, 0f, ZText);
		_hpValue.font = pillSource.currentPlain.font;
		_hpValue.fontSharedMaterial = pillSource.currentPlain.font.material;
		_hpValue.fontStyle = pillSource.currentPlain.fontStyle;
		_hpValue.fontSize = pillSource.currentPlain.fontSize * unit / TmpWorldUnitsPerEm;
		_hpValue.alignment = TextAlignmentOptions.Center;
		_hpValue.enableWordWrapping = false;
		_hpValue.overflowMode = TextOverflowModes.Overflow;
		_hpValue.rectTransform.sizeDelta = new Vector2(360f * unit, pillSource.currentPlain.fontSize * 1.2f * unit);
		_hpValue.color = GameColorPalette.HpNormalPlayerColor;
		_hpValue.raycastTarget = false;
	}

	private void InitHpText()
	{
		PlayerStatusSO status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
		if (status == null) return;
		_targetHp = status.hp;
		_targetHpMax = Mathf.Max(1, status.hpMax);
		_shownHp = _targetHp;
		_shownHpMax = _targetHpMax;
		WriteHpText();
	}

	// ------------------------------------------------------------------ refresh

	/// <summary>
	/// Catch-up refresh (from ShopChrome.Refresh on buy/sell/reroll/show): re-reads the live
	/// full-HP pair and plays the count tween only on an actual change.
	/// </summary>
	public void Refresh()
	{
		if (_hpValue == null) return;
		PlayerStatusSO status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
		if (status == null) return;
		int hp = status.hp;
		int hpMax = Mathf.Max(1, status.hpMax);
		if (hp == _targetHp && hpMax == _targetHpMax) return;
		_targetHp = hp;
		_targetHpMax = hpMax;
		if (hp == _shownHp && hpMax == _shownHpMax) return;
		PlayCountUp(hp, hpMax);
	}

	private void PlayCountUp(int hp, int hpMax)
	{
		KillTween(ref _countTween);
		int fromHp = _shownHp;
		int fromMax = _shownHpMax;
		ShopPageHud self = this;
		_countTween = DOTween.To(() => 0f, v =>
		{
			self._shownHp = Mathf.RoundToInt(Mathf.Lerp(fromHp, hp, v));
			self._shownHpMax = Mathf.RoundToInt(Mathf.Lerp(fromMax, hpMax, v));
			self.WriteHpText();
		}, 1f, CountUpSeconds).OnComplete(() =>
		{
			self._shownHp = hp;
			self._shownHpMax = hpMax;
			self.WriteHpText();
		});
	}

	private void WriteHpText()
	{
		if (_hpValue != null) _hpValue.text = _shownHp + "/" + _shownHpMax;
	}

	private void Update()
	{
		// Deferred mirror rebuild lands here (ApplyMirrorLayout sets the flag mid-travel).
		if (_mirrorDirty && !PhaseTransitionDriver.IsTransitioning)
		{
			_mirrorDirty = false;
			RebuildMirrors();
		}
		PollUsername();
	}

	// Diff-guarded so the TMP mesh only rebuilds on an actual change (presenter convention).
	private void PollUsername()
	{
		if (_nameLabel == null) return;
		string username = PlayerIdentity.Username;
		if (string.IsNullOrEmpty(username)) username = "???";
		if (_lastUsername != username)
		{
			_lastUsername = username;
			_nameLabel.text = username;
		}
	}

	private void OnDisable()
	{
		// The chrome root deactivates on shop exit / mid-travel; a running count tween would
		// keep mutating the text invisibly. The next visible Refresh re-syncs from scratch.
		KillTween(ref _countTween);
	}

	private void OnDestroy()
	{
		if (_instance == this) _instance = null;
	}

	// ------------------------------------------------------------------ helpers

	/// <summary>
	/// Border-exact sliced world sizing (VISUAL-FIX 2026-09-21, see MirrorSprite): a canvas
	/// sliced Image renders its 9-slice border at border/spritePPU x referencePPU local px,
	/// while a SpriteRenderer's sliced border is border/spritePPU x transform.localScale
	/// (independent of sr.size). Splitting the target size as sizeDelta x childScale / refPPU
	/// on sr.size and refPPU x unit on localScale keeps the final size identical AND makes
	/// the border thickness land at exactly canvas-border-px x unit.
	/// </summary>
	public static Vector2 SlicedWorldSize(Vector2 canvasSize, Vector2 childScale, float refPpu)
	{
		if (refPpu <= 0.0001f) refPpu = 100f;
		return new Vector2(canvasSize.x * childScale.x / refPpu, canvasSize.y * childScale.y / refPpu);
	}

	/// <summary>Companion scale for SlicedWorldSize; see its summary for the derivation.</summary>
	public static float SlicedWorldScale(float refPpu, float unit)
	{
		if (refPpu <= 0.0001f) refPpu = 100f;
		return refPpu * unit;
	}

	// VISUAL-FIX(2026-09-21): World avatar/HP pill rendered bloated vs the canvas originals
	//   Cause:    (1) MirrorSprite sized by sizeDelta only and dropped the child
	//             RectTransform's localScale — the avatar's `image` child (scale 0.9) filled
	//             the frame edge-to-edge, erasing the cream margin. (2) The 9-slice border
	//             proportion came out ~2x the canvas (see SlicedWorldSize's summary).
	//   Affects:  ShopPageHud.MirrorSprite (avatar frame/image/shadows, HP pill bg/shadow).
	//   Regress:  Shop top bar at scroll 0 must pixel-match the canvas shop bar: the dark
	//             image keeps its cream frame margin; corner radius matches combat exactly.
	//   Related:  ShopPageHudTests B1/B2 goldens; GameScene PlayerIcon `image` scale 0.9.
	private static void MirrorSprite(GameObject go, Image source, float unit)
	{
		SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
		sr.sprite = source.sprite;
		sr.color = source.color;
		Vector3 childScale = source.rectTransform.localScale;
		float refPpu = 100f;
		Canvas canvas = source.canvas;
		if (canvas != null && canvas.referencePixelsPerUnit > 0.0001f) refPpu = canvas.referencePixelsPerUnit;
		if (source.type == Image.Type.Sliced && source.sprite != null)
		{
			sr.drawMode = SpriteDrawMode.Sliced;
			sr.size = SlicedWorldSize(source.rectTransform.sizeDelta, childScale, refPpu);
			float s = SlicedWorldScale(refPpu, unit);
			go.transform.localScale = new Vector3(s, s, 1f);
			return;
		}
		// Non-sliced sources have no border to preserve: scale the native sprite rect
		// straight onto the target size.
		sr.drawMode = SpriteDrawMode.Simple;
		if (source.sprite != null && source.sprite.pixelsPerUnit > 0.0001f)
		{
			Vector2 native = source.sprite.rect.size / source.sprite.pixelsPerUnit;
			if (native.x > 0.0001f && native.y > 0.0001f)
			{
				go.transform.localScale = new Vector3(
					source.rectTransform.sizeDelta.x * childScale.x * unit / native.x,
					source.rectTransform.sizeDelta.y * childScale.y * unit / native.y, 1f);
			}
		}
	}

	private static void CopyText(TextMeshPro target, TMP_Text source, float unit, Color color)
	{
		Vector3 childScale = source.rectTransform.localScale;
		target.font = source.font;
		target.fontSharedMaterial = source.font.material;
		target.fontStyle = source.fontStyle;
		target.fontSize = source.fontSize * unit * childScale.x / TmpWorldUnitsPerEm;
		target.alignment = source.alignment;
		target.enableWordWrapping = false;
		target.overflowMode = TextOverflowModes.Overflow;
		target.rectTransform.sizeDelta = new Vector2(
			source.rectTransform.sizeDelta.x * unit * childScale.x,
			source.rectTransform.sizeDelta.y * unit * childScale.y);
		target.text = source.text;
		target.color = color;
		target.raycastTarget = false;
	}

	// Canvas children of these blocks are anchored (0.5, 0.5), so anchoredPosition == the
	// local offset from the block center.
	private static Vector2 GetAnchoredOffset(Transform t)
	{
		RectTransform rt = t as RectTransform;
		return rt != null ? rt.anchoredPosition : Vector2.zero;
	}

	private static GameObject FindPlayerIconSource()
	{
		CombatIconPresenter presenter = FindObjectOfType<CombatIconPresenter>();
		if (presenter == null || presenter.playerIcon == null)
		{
			TestManager.LogWarning("[ShopPageHud] CombatIconPresenter/playerIcon not found — world avatar skipped");
			return null;
		}
		return presenter.playerIcon;
	}

	private static HPNumericDisplayHorizontal FindPlayerPillSource()
	{
		foreach (HPNumericDisplayHorizontal display in FindObjectsOfType<HPNumericDisplayHorizontal>(true))
		{
			if (display.side == HPNumericDisplayHorizontal.Side.Player
				&& display.displayRoot != null && display.currentPlain != null)
			{
				return display;
			}
		}
		TestManager.LogWarning("[ShopPageHud] player-side HPNumericDisplayHorizontal not found — world HP pill skipped");
		return null;
	}

	private static void KillTween(ref Tween tween)
	{
		if (tween != null && tween.IsActive()) tween.Kill();
		tween = null;
	}
}
