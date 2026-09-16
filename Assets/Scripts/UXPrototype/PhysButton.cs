using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Port of the UIKitDemo §02 ".phys" interaction state machine (docs/UIUX_Guidelines.md §2):
/// rest -> hover (face lifts toward the top-left light, hard shadow stays anchored on the
/// ground) -> press (face lands on the shadow) -> activate on release inside; holding and
/// dragging out cancels (R6); release fires (R7); a disabled face is dim, shadowless, and
/// plays the once-per-pointer-enter refusal animation (§2.2).
///
/// Two render modes, both built around one invariant — the hit target never moves, only
/// the visuals do (a moving collider/rect under a stationary cursor flickers enter/exit):
/// - World: a static root (Collider2D sized to the face/shadow/travel envelope, OnMouse
///   input) plus a moving Visual group (face + label) and a grounded shadow sibling.
///   Release inside invokes SetWorldAction; used by the shop price button.
/// - UI: coexists with a uGUI Button on the same object. A static, transparent,
///   envelope-sized hitbox Image (PhysButtonPointerRelay) does the hit-testing; the
///   button graphic and label stop raycasting; release inside invokes the Button's own
///   onClick wiring (R7). The shadow is a sibling Image created by ConfigureUIButton.
///
/// Offsets are in face-local units; the caller converts design px per context (card-local
/// ≈ 3.2 units / 118 demo px, UI canvas reference px = 1).
/// </summary>
public class PhysButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	public enum PhysMode { World, UI }

	[Header("Mode")]
	[Tooltip("Auto-resolved at AddComponent time: Image present = UI, else World")]
	public PhysMode mode = PhysMode.World;

	[Header("Physical Params (Guidelines 2.1; face-local units)")]
	[Tooltip("Rest shadow offset rs (demo default 4 design px)")]
	public float restShadow = 0.1085f;
	[Tooltip("Hover lift toward the light hl")]
	public float hoverLift = 0.1085f;
	public float hoverDuration = 0.12f;
	[Tooltip("Press must stay shorter than hoverDuration (R5)")]
	public float pressDuration = 0.06f;

	[Header("Disabled Refusal (Guidelines 2.2; face-local units / seconds)")]
	public float denyShift = 0.1628f;
	public float denyOutDuration = 0.08f;
	public int denyWiggles = 1;
	public float denySwingDuration = 0.11f;
	[Range(0f, 1f)]
	[Tooltip("Swing amplitude = denyShift * this, decaying 0.55 per wiggle")]
	public float denyAmplitudeFactor = 0.4f;
	public float denyHoldDuration = 0.15f;
	public float denyRetreatDuration = 0.12f;

	[Header("Hover Text Swap (Guidelines 3.1; optional, empty = off)")]
	public TMP_Text label;
	[Tooltip("Shown while hovered; the rest text is owned by the caller and rewritten on unhover")]
	public string hoverText = "";

	private const float WiggleDecay = 0.55f;

	private Button _uiButton;
	private Image _uiImage;
	private Color _faceColor;
	private SpriteRenderer _faceRenderer;
	private Transform _shadow;
	private Transform _visualGroup;
	private RectTransform _uiHitbox;
	private Vector3 _faceBase;
	private Vector3 _shadowBase;
	private bool _basesCaptured;
	private Vector2 _faceOffset;
	private Tween _moveTween;
	private Coroutine _denyRoutine;
	private bool _denyPlayedThisEnter;
	private bool _pressed;
	private bool _externalDisabled;
	private bool _appliedDisabled;
	private Action _worldAction;

	/// <summary>True while the pointer is over the face. Callers holding the rest label text (live price) skip their rewrite while this is set.</summary>
	public bool IsPointerOver { get; private set; }

	/// <summary>World mode: the caller's SetDisabled. UI mode: merged with Button.interactable.</summary>
	public bool IsDisabled
	{
		get { return _externalDisabled || (_uiButton != null && !_uiButton.interactable); }
	}

	private void Awake()
	{
		_uiButton = GetComponent<Button>();
		_uiImage = GetComponent<Image>();
		if (_uiImage != null)
		{
			mode = PhysMode.UI;
			_faceColor = _uiImage.color;
		}
		else
		{
			// Self-renderer only (some callers put the face on the root); the shadow child
			// must never be picked up here — SetWorldFace assigns the face explicitly.
			_faceRenderer = GetComponent<SpriteRenderer>();
			if (_faceRenderer != null) _faceColor = _faceRenderer.color;
		}
	}

	private void Update()
	{
		if (mode == PhysMode.UI)
		{
			bool disabled = IsDisabled;
			if (disabled != _appliedDisabled)
			{
				_appliedDisabled = disabled;
				ApplyDisabledVisual(disabled);
			}
		}
		else if (_pressed && Input.GetMouseButtonUp(0))
		{
			// Deterministic release poll: OnMouseUp alone cannot distinguish an inside
			// release from a drag-out release, and R6/R7 hinge on that difference.
			FinishPress(IsPointerOver);
		}
	}

	private void LateUpdate()
	{
		// R2: disabled = shadowless, except while the refusal animation mirrors the face
		// offset (Guidelines 2.2). The UI shadow is a sibling, so it needs this sync
		// explicitly; for World it also covers the deny window.
		if (_shadow == null) return;
		bool visible = isActiveAndEnabled && (!IsDisabled || _denyRoutine != null);
		if (_shadow.gameObject.activeSelf != visible) _shadow.gameObject.SetActive(visible);
	}

	private void OnDisable()
	{
		StopDeny();
		if (_moveTween != null && _moveTween.IsActive()) _moveTween.Kill();
		_pressed = false;
		IsPointerOver = false;
		_denyPlayedThisEnter = false;
		if (_basesCaptured) ApplyOffset(Vector2.zero);
	}

	#region Configuration

	/// <summary>
	/// World mode: the ground-anchored shadow transform (a child of this object). The shadow
	/// never moves — during press the face lands on top of it, which reads as the demo's
	/// shadow collapse without animating it.
	/// </summary>
	public void SetShadowTransform(Transform shadow)
	{
		_shadow = shadow;
	}

	/// <summary>
	/// World mode: the group carrying face + label. This is what hover/press/deny move;
	/// the root stays put so its collider is a stable hit target — a moving collider under
	/// a stationary cursor would flicker enter/exit at the edges.
	/// </summary>
	public void SetVisualGroup(Transform visualGroup)
	{
		_visualGroup = visualGroup;
	}

	/// <summary>
	/// World mode: the face sprite as a CHILD of this object — never the renderer on the
	/// root, because ConfigureWorldFaceSize repositions the face onto the label's text
	/// bounds and the root itself must not move (the label lives at the root origin).
	/// </summary>
	public void SetWorldFace(SpriteRenderer face)
	{
		_faceRenderer = face;
		_faceColor = face != null ? face.color : Color.white;
	}

	/// <summary>World mode: caller-driven disabled state (UI mode derives from Button.interactable).</summary>
	public void SetDisabled(bool disabled)
	{
		_externalDisabled = disabled;
		bool now = IsDisabled;
		if (now != _appliedDisabled)
		{
			_appliedDisabled = now;
			ApplyDisabledVisual(now);
		}
	}

	/// <summary>World mode: what a release inside invokes. Swappable at runtime (shop price button flips buy/sell as the card changes side).</summary>
	public void SetWorldAction(Action action)
	{
		_worldAction = action;
	}

	/// <summary>UI mode bootstrap: canvas-px params + a shadow Image sibling behind the button graphic.</summary>
	public void ConfigureUIButton(float restShadowPx, float hoverLiftPx, float denyShiftPx)
	{
		mode = PhysMode.UI;
		restShadow = restShadowPx;
		hoverLift = hoverLiftPx;
		denyShift = denyShiftPx;
		_uiButton = GetComponent<Button>();
		_uiImage = GetComponent<Image>();
		if (_uiImage == null) return;
		_faceColor = _uiImage.color;

		if (_shadow == null && transform.parent != null)
		{
			GameObject shadowGo = new GameObject(gameObject.name + "_PhysShadow", typeof(Image));
			RectTransform shadowRt = shadowGo.GetComponent<RectTransform>();
			RectTransform faceRt = (RectTransform)transform;
			shadowRt.SetParent(transform.parent, false);
			shadowRt.anchorMin = faceRt.anchorMin;
			shadowRt.anchorMax = faceRt.anchorMax;
			shadowRt.pivot = faceRt.pivot;
			shadowRt.anchoredPosition = faceRt.anchoredPosition;
			shadowRt.sizeDelta = faceRt.sizeDelta;
			shadowRt.localRotation = faceRt.localRotation;
			shadowRt.localScale = faceRt.localScale;
			Image shadowImg = shadowGo.GetComponent<Image>();
			shadowImg.sprite = _uiImage.sprite;
			shadowImg.color = GameColorPalette.CardShadowColor;
			shadowImg.raycastTarget = false;
			shadowRt.SetSiblingIndex(transform.GetSiblingIndex());
			_shadow = shadowRt;
			CaptureBases();
			ApplyOffset(Vector2.zero); // land the shadow on the ground immediately, not stacked on the face
		}

		// Static hitbox: the button's own rect moves with the lift, which flickers
		// enter/exit at the edges. This non-moving, envelope-sized Image does all the
		// hit-testing; the button graphic and its label stop raycasting so every event
		// takes exactly one path (relay -> PhysButton -> onClick.Invoke on release).
		if (_uiHitbox == null && transform.parent != null)
		{
			RectTransform faceRt = (RectTransform)transform;
			GameObject hitGo = new GameObject(gameObject.name + "_PhysHitbox", typeof(Image));
			_uiHitbox = hitGo.GetComponent<RectTransform>();
			_uiHitbox.SetParent(transform.parent, false);
			_uiHitbox.anchorMin = faceRt.anchorMin;
			_uiHitbox.anchorMax = faceRt.anchorMax;
			_uiHitbox.pivot = faceRt.pivot;
			_uiHitbox.anchoredPosition = faceRt.anchoredPosition + new Vector2((restShadow - denyShift) / 2f, (denyShift - restShadow) / 2f);
			_uiHitbox.sizeDelta = faceRt.sizeDelta + new Vector2(restShadow + denyShift, restShadow + denyShift);
			_uiHitbox.localRotation = faceRt.localRotation;
			_uiHitbox.localScale = faceRt.localScale;
			Image hitImg = hitGo.GetComponent<Image>();
			hitImg.color = Color.clear;
			hitImg.raycastTarget = true;
			_uiHitbox.SetSiblingIndex(transform.GetSiblingIndex());
			hitGo.AddComponent<PhysButtonPointerRelay>().Init(this);
			_uiImage.raycastTarget = false;
			foreach (TMP_Text label in GetComponentsInChildren<TMP_Text>(true))
			{
				// A raycastable label would bubble pointer events to the Button alongside
				// the relay path and double-fire the click.
				label.raycastTarget = false;
			}
		}
	}

	/// <summary>
	/// World mode: face/shadow sliced size plus the root collider's envelope — the union of
	/// the face at rest, hover and press, the grounded shadow, and the deny excursion — so
	/// the pointer never leaves the hit area because the face moved away (edge flicker).
	/// </summary>
	public void ConfigureWorldFaceSize(Vector2 size, Vector2 center)
	{
		if (_faceRenderer != null)
		{
			_faceRenderer.size = size;
			Vector3 p = _faceRenderer.transform.localPosition;
			_faceRenderer.transform.localPosition = new Vector3(center.x, center.y, p.z);
		}
		if (_shadow != null)
		{
			SpriteRenderer shadowSr = _shadow.GetComponent<SpriteRenderer>();
			if (shadowSr != null) shadowSr.size = size;
			Vector3 sp = _shadow.localPosition;
			_shadow.localPosition = new Vector3(center.x + restShadow, center.y - restShadow, sp.z);
		}
		BoxCollider2D col = GetComponent<BoxCollider2D>();
		if (col != null)
		{
			col.size = size + new Vector2(restShadow + denyShift, restShadow + denyShift);
			col.offset = center + new Vector2((restShadow - denyShift) / 2f, (denyShift - restShadow) / 2f);
		}
	}

	#endregion

	#region Input

	private void OnMouseEnter()
	{
		PointerEnterCommon();
	}

	private void OnMouseExit()
	{
		PointerExitCommon();
	}

	private void OnMouseDown()
	{
		if (IsDisabled) return;
		_pressed = true;
		EnterPressVisual();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		PointerEnterCommon();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		PointerExitCommon();
	}

	public void OnPointerDown(PointerEventData eventData)
	{
		if (IsDisabled) return;
		_pressed = true;
		EnterPressVisual();
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		if (mode != PhysMode.UI || !_pressed) return;
		GameObject over = eventData.pointerCurrentRaycast.gameObject;
		bool fromOwnGraph = over != null && (over == gameObject || over.transform.IsChildOf(transform));
		FinishUiPress(InsideUi(over), fromOwnGraph);
	}

	private bool InsideUi(GameObject over)
	{
		if (over == null) return false;
		if (over == gameObject || over.transform.IsChildOf(transform)) return true;
		return _uiHitbox != null && (over == _uiHitbox.gameObject || over.transform.IsChildOf(_uiHitbox));
	}

	/// <summary>
	/// UI-mode release. <paramref name="fromOwnGraph"/> means the event bubbled from this
	/// object's own graphics (label path): the coexisting Button receives that same event
	/// chain and fires onClick natively, so invoking it here would double-fire. Releases
	/// over the static hitbox reach only the relay — this method then invokes the Button's
	/// wiring on the Button's behalf, still honoring interactable via IsDisabled.
	/// </summary>
	private void FinishUiPress(bool inside, bool fromOwnGraph)
	{
		_pressed = false;
		if (inside && !IsDisabled)
		{
			if (_uiButton != null && !fromOwnGraph)
			{
				_uiButton.onClick.Invoke();
			}
			else if (_uiButton == null && _worldAction != null)
			{
				_worldAction();
			}
		}
		RestoreHoverOrRest();
	}

	internal void UiRelayEnter()
	{
		PointerEnterCommon();
	}

	internal void UiRelayExit()
	{
		PointerExitCommon();
	}

	internal void UiRelayDown()
	{
		if (IsDisabled) return;
		_pressed = true;
		EnterPressVisual();
	}

	internal void UiRelayUp(PointerEventData eventData)
	{
		if (!_pressed) return;
		FinishUiPress(InsideUi(eventData.pointerCurrentRaycast.gameObject), false);
	}

	private void PointerEnterCommon()
	{
		IsPointerOver = true;
		_denyPlayedThisEnter = false;
		if (IsDisabled)
		{
			PlayDenyOncePerEnter();
			return;
		}
		// R6: re-entering while still held re-presses.
		if (_pressed)
		{
			EnterPressVisual();
			return;
		}
		EnterHoverVisual();
	}

	private void PointerExitCommon()
	{
		IsPointerOver = false;
		StopDeny();
		// Held-out: the release poll / OnPointerUp cancels (R6); keep the pressed pose meanwhile.
		if (_pressed) return;
		EnterRestVisual();
	}

	private void FinishPress(bool inside)
	{
		_pressed = false;
		if (inside && !IsDisabled && _worldAction != null)
		{
			_worldAction();
		}
		RestoreHoverOrRest();
	}

	private void RestoreHoverOrRest()
	{
		if (IsPointerOver && !IsDisabled) EnterHoverVisual();
		else EnterRestVisual();
	}

	#endregion

	#region Visual State Machine

	private Vector2 HoverVector
	{
		get { return new Vector2(-hoverLift, hoverLift); } // toward the top-left light
	}

	private Vector2 PressVector
	{
		get { return new Vector2(restShadow, -restShadow); } // lands on the shadow (R5)
	}

	private void EnterHoverVisual()
	{
		TweenOffsetTo(HoverVector, hoverDuration, Ease.OutBack);
		UpdateLabelSwap();
	}

	private void EnterRestVisual()
	{
		TweenOffsetTo(Vector2.zero, hoverDuration, Ease.OutQuad);
		UpdateLabelSwap();
	}

	private void EnterPressVisual()
	{
		TweenOffsetTo(PressVector, pressDuration, Ease.OutQuad);
	}

	private void ApplyDisabledVisual(bool disabled)
	{
		Color target = disabled ? GameColorPalette.CardFaceDimColor : _faceColor;
		if (_uiImage != null) _uiImage.color = target;
		if (_faceRenderer != null) _faceRenderer.color = target;
		if (disabled)
		{
			if (IsPointerOver) PlayDenyOncePerEnter();
		}
		else
		{
			StopDeny();
			RestoreHoverOrRest();
		}
	}

	private void PlayDenyOncePerEnter()
	{
		if (_denyPlayedThisEnter) return;
		_denyPlayedThisEnter = true;
		StopDeny();
		_denyRoutine = StartCoroutine(DenyRoutine());
	}

	/// <summary>
	/// Guidelines 2.2 refusal: slide out toward the light -> wiggle pairs with decaying
	/// amplitude around the out point -> recenter -> hold -> retreat. The shadow mirrors
	/// the face offset (ground-anchored) throughout via ApplyOffset.
	/// </summary>
	private IEnumerator DenyRoutine()
	{
		Vector2 outVec = new Vector2(-denyShift, denyShift);
		yield return TweenOffsetTo(outVec, denyOutDuration, Ease.OutQuad).WaitForCompletion();
		for (int k = 1; k <= denyWiggles; k++)
		{
			float amp = denyShift * denyAmplitudeFactor * Mathf.Pow(WiggleDecay, k - 1);
			yield return TweenOffsetTo(new Vector2(outVec.x + amp, outVec.y), denySwingDuration, Ease.Linear).WaitForCompletion();
			yield return TweenOffsetTo(new Vector2(outVec.x - amp, outVec.y), denySwingDuration, Ease.Linear).WaitForCompletion();
		}
		yield return TweenOffsetTo(outVec, denySwingDuration, Ease.Linear).WaitForCompletion();
		if (denyHoldDuration > 0f) yield return new WaitForSeconds(denyHoldDuration);
		yield return TweenOffsetTo(Vector2.zero, denyRetreatDuration, Ease.OutQuad).WaitForCompletion();
		_denyRoutine = null;
	}

	private void StopDeny()
	{
		if (_denyRoutine == null) return;
		StopCoroutine(_denyRoutine);
		_denyRoutine = null;
	}

	#endregion

	#region Offset Apply

	/// <summary>UI-mode only: the button rect itself is the moving visual, so its rest base is captured once.</summary>
	private void CaptureBases()
	{
		if (_basesCaptured) return;
		_faceBase = transform.localPosition;
		if (_shadow != null) _shadowBase = _shadow.localPosition;
		_basesCaptured = true;
	}

	/// <summary>
	/// Moves only the visuals, never the hit target: world mode shifts the Visual group
	/// (root collider stays put), UI mode shifts the button rect itself (the static hitbox
	/// Image does the hit-testing). The world shadow is a grounded sibling that never
	/// moves — during press the face lands on it, which reads as the shadow collapsing (R5).
	/// </summary>
	private void ApplyOffset(Vector2 offset)
	{
		_faceOffset = offset;
		if (_visualGroup != null)
		{
			_visualGroup.localPosition = offset; // group rest base is the local origin
			return;
		}
		CaptureBases();
		Vector2 flat = new Vector2(_faceBase.x, _faceBase.y) + offset;
		transform.localPosition = new Vector3(flat.x, flat.y, _faceBase.z);
		if (_shadow != null && mode == PhysMode.UI)
		{
			Vector2 ground = new Vector2(restShadow, -restShadow);
			Vector2 shadowFlat = new Vector2(_shadowBase.x, _shadowBase.y) + ground - offset;
			_shadow.localPosition = new Vector3(shadowFlat.x, shadowFlat.y, _shadowBase.z);
		}
	}

	private Tween TweenOffsetTo(Vector2 target, float duration, Ease ease)
	{
		if (_moveTween != null && _moveTween.IsActive()) _moveTween.Kill();
		_moveTween = DOTween.To(() => _faceOffset, ApplyOffset, target, duration).SetEase(ease);
		return _moveTween;
	}

	private void UpdateLabelSwap()
	{
		if (label == null || string.IsNullOrEmpty(hoverText)) return;
		if (IsPointerOver && !IsDisabled) label.text = hoverText;
		// The rest text is owned by the caller; it rewrites on the next unhover tick.
	}

	#endregion
}

/// <summary>
/// Forwards pointer events from PhysButton's static UI hitbox Image to its PhysButton.
/// Kept in this file on purpose: it is an implementation detail of the UI mode, never
/// wired in scenes or prefabs.
/// </summary>
public class PhysButtonPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	private PhysButton _owner;

	public void Init(PhysButton owner)
	{
		_owner = owner;
	}

	public void OnPointerEnter(PointerEventData eventData) { if (_owner != null) _owner.UiRelayEnter(); }
	public void OnPointerExit(PointerEventData eventData) { if (_owner != null) _owner.UiRelayExit(); }
	public void OnPointerDown(PointerEventData eventData) { if (_owner != null) _owner.UiRelayDown(); }
	public void OnPointerUp(PointerEventData eventData) { if (_owner != null) _owner.UiRelayUp(eventData); }
}
