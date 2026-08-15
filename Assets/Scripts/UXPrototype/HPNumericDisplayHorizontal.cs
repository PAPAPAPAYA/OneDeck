using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Horizontal numeric HP display: "12/20" in a row — current digits, a static
/// slash, then max digits at a smaller font (maxFontScale). Odometer digit-roll
/// strips per digit, same mechanics as HPNumericDisplay. Pure presentation;
/// no game-logic changes.
///
/// Kept from the vertical version: adaptive counting (HPNumericCounter), hit
/// shake + landing pop, digit-count growth (row re-centers with a glide).
/// NOT carried over: low-HP pulse, zero-out (no vertical divider to drop
/// through). Plain texts exist only for the Edit Mode preview — at runtime the
/// strips always render.
///
/// Current HP polls CombatInfoDisplayer's displayed-HP accessors (the same
/// queue-frozen values the HP text shows); max HP reads live from the side's
/// PlayerStatusSO (same accepted side effect as the vertical version).
/// Plan: plans/plan-hp-numeric-display-horizontal-2026-08-08.md
/// </summary>
public class HPNumericDisplayHorizontal : MonoBehaviour
{
	public enum Side
	{
		Player,
		Enemy
	}

	[Header("Wiring")]
	public Side side = Side.Player;
	public RectTransform displayRoot;
	public RectTransform currentRoot;
	public TMP_Text currentPlain;
	public RectTransform currentStrips;
	public TMP_Text slashText;
	public RectTransform maxRoot;
	public TMP_Text maxPlain;
	public RectTransform maxStrips;
	public GamePhaseSO gamePhaseRef;
	public Canvas canvas;

	[Header("Typography")]
	[Tooltip("Max (slash-right) digits render at em * maxFontScale; the slash renders at em.")]
	public float maxFontScale = 0.75f;
	[Tooltip("Gap between digit groups and the slash, in em.")]
	public float groupGapEm = 0.15f;

	[Header("Counting (demo constants, shared with HPNumericDisplay)")]
	public int stepMs = 50;
	public int targetCountMs = 500;
	public int easeOutPoints = 5;
	public int easeOutExtraMs = 35;
	public bool easeOutFinish = true;

	[Header("Digit roll (demo constants)")]
	public float rollBaseMs = 90f;
	public float rollPerStepMs = 50f;
	public float rollStaggerMs = 45f;

	[Header("Shake / landing pop (demo constants)")]
	public float shakeDuration = 0.32f;
	public int shakeVibrato = 10;
	public float landPopScale = 1.07f;
	public float landPopUpDuration = 0.063f;
	public float landPopDownDuration = 0.077f;

	[Header("States (demo constants)")]
	public float dividerGlideDuration = 0.24f;

	[Header("Edit Mode Preview")]
	[Tooltip("Edit Mode only: mirrors the Awake layout math and shows previewHp/previewHpMax so the real arrangement and digit sizes are visible in the Scene/Game view without entering Play Mode. Odometer strips are not built; plain text at the same font sizes represents the layout exactly.")]
	public bool editModePreview = true;
	public int previewHp = 12;
	public int previewHpMax = 20;

	private const int StripCycles = 3;
	private const int DigitsPerCycle = 10;

	private class CounterState
	{
		public int displayed;
		public int target;
		public float elapsed;
		public bool counting;
	}

	private class DigitStrip
	{
		public RectTransform slot;
		public RectTransform strip;
		public TMP_Text text;
		public int idx;
		public Tween tween;
		public float digitWidth;
		public float lineHeight;
	}

	private readonly CounterState _current = new CounterState();
	private readonly CounterState _max = new CounterState();
	private readonly List<DigitStrip> _currentDigitStrips = new List<DigitStrip>();
	private readonly List<DigitStrip> _maxDigitStrips = new List<DigitStrip>();

	private bool _wasInCombat;
	private Vector2 _rootBasePos;
	private float _em = 1f; // current digit em = currentPlain.fontSize in px.
	private float _maxEm = 1f; // max digit em = _em * maxFontScale.
	private float _digitWidth = 10f;
	private float _maxDigitWidth = 8f;
	private float _slashWidth = 10f;
	private float _stripLineSpacing;
	private int _fixedDigitCount = 1;
	private int _fixedMaxDigitCount = 1;
	private string _stripText;
	private bool _stripMetricsVerified;

	private Tween _popTween;
	private Tween _rowGlideTween;

	private void Awake()
	{
		if (displayRoot == null || currentRoot == null || currentPlain == null || currentStrips == null
			|| slashText == null || maxRoot == null || maxPlain == null || maxStrips == null || gamePhaseRef == null)
		{
			Debug.LogError("[HPNumericDisplayHorizontal] Missing serialized reference(s), disabling.");
			enabled = false;
			return;
		}
		if (canvas == null)
		{
			canvas = GetComponentInParent<Canvas>();
		}
		GameColorPalette palette = GameColorPalette.Me;
		if (palette != null && (side == Side.Player ? palette.hpNormalPlayer : palette.hpNormalEnemy) == null)
		{
			Debug.LogWarning("[HPNumericDisplayHorizontal] GameColorPalette hpNormal" + (side == Side.Player ? "Player" : "Enemy") + " not wired; color falls back to white.");
		}
		_em = currentPlain.fontSize;
		_maxEm = _em * maxFontScale;
		maxPlain.fontSize = _maxEm;
		slashText.fontSize = _em;
		slashText.text = "/";
		TMP_FontAsset fontAsset = currentPlain.font;
		// 1em line advance: same formula and rationale as HPNumericDisplay (the
		// lineSpacing is a font-relative percentage, so one value serves both the
		// current and the smaller max strips). VerifyStripMetrics re-measures.
		_stripLineSpacing = 100f * (fontAsset.faceInfo.pointSize - fontAsset.faceInfo.lineHeight) / fontAsset.faceInfo.pointSize;
		currentPlain.lineSpacing = _stripLineSpacing;
		maxPlain.lineSpacing = _stripLineSpacing;
		_digitWidth = currentPlain.GetPreferredValues("0").x;
		if (_digitWidth <= 0.01f)
		{
			_digitWidth = _em * 0.6f;
		}
		_maxDigitWidth = maxPlain.GetPreferredValues("0").x;
		if (_maxDigitWidth <= 0.01f)
		{
			_maxDigitWidth = _maxEm * 0.6f;
		}
		_slashWidth = slashText.GetPreferredValues("/").x;
		if (_slashWidth <= 0.01f)
		{
			_slashWidth = _em * 0.5f;
		}
		// Odometer strip content: three 0-9 cycles so rolls crossing 0/9 always have
		// room either way; canonical resting spot for digit d is line 10+d.
		var builder = new System.Text.StringBuilder(2 * DigitsPerCycle * StripCycles);
		for (int cycle = 0; cycle < StripCycles; cycle++)
		{
			for (int d = 0; d < DigitsPerCycle; d++)
			{
				builder.Append(d);
				if (cycle < StripCycles - 1 || d < DigitsPerCycle - 1)
				{
					builder.Append('\n');
				}
			}
		}
		_stripText = builder.ToString();
		StretchFull(currentPlain.rectTransform);
		StretchFull(maxPlain.rectTransform);
		StretchFull(currentStrips);
		StretchFull(maxStrips);
		currentPlain.alignment = TextAlignmentOptions.Center;
		maxPlain.alignment = TextAlignmentOptions.Center;
		slashText.alignment = TextAlignmentOptions.Center;
		currentPlain.enableWordWrapping = false;
		maxPlain.enableWordWrapping = false;
		slashText.enableWordWrapping = false;
		currentPlain.overflowMode = TextOverflowModes.Overflow;
		maxPlain.overflowMode = TextOverflowModes.Overflow;
		slashText.overflowMode = TextOverflowModes.Overflow;
		LayoutRoots();
		_rootBasePos = displayRoot.anchoredPosition;
		// Runtime renders the odometer strips only; the plain texts exist solely
		// for the Edit Mode preview (restored there on serialization).
		currentPlain.gameObject.SetActive(false);
		maxPlain.gameObject.SetActive(false);
		slashText.color = NormalColorValue;
		// Combat input is click-driven: no graphic of this display may intercept raycasts.
		foreach (Graphic graphic in displayRoot.GetComponentsInChildren<Graphic>(true))
		{
			graphic.raycastTarget = false;
		}
		displayRoot.gameObject.SetActive(false);
	}

	private void Update()
	{
		bool inCombat = gamePhaseRef.Value() == EnumStorage.GamePhase.Combat;
		if (inCombat && !_wasInCombat)
		{
			EnterCombat();
		}
		else if (!inCombat && _wasInCombat)
		{
			ExitCombat();
		}
		_wasInCombat = inCombat;
		if (!inCombat)
		{
			return;
		}

		int hp = GetDisplayedHp();
		int hpMax = GetLiveMaxHp();

		// Same classification rule as the vertical version: drop -> shake scaled
		// by the damage; rise -> count only.
		int delta = hp - _current.target;
		if (delta < 0)
		{
			PlayShake(-delta);
		}
		SetCounterTarget(_current, true, hp);
		SetCounterTarget(_max, false, hpMax);

		TickCounter(_current, true);
		TickCounter(_max, false);

		CheckDigitGrowth(hp, hpMax);
	}

	private void OnEnable()
	{
#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			SubscribeColorEvents();
		}
#endif
	}

	private void OnDisable()
	{
		CleanupVisuals();
#if UNITY_EDITOR
		UnsubscribeColorEvents();
#endif
	}

	private void OnDestroy()
	{
#if UNITY_EDITOR
		UnsubscribeColorEvents();
#endif
	}

#if UNITY_EDITOR
	private bool _colorEventsSubscribed;

	// Subscribed from OnEnable and OnValidate so edit-mode live updates do not
	// depend on lifecycle timing (scene load, recompile, re-enter edit mode).
	private void SubscribeColorEvents()
	{
		if (_colorEventsSubscribed)
		{
			return;
		}
		_colorEventsSubscribed = true;
		ColorSO.Changed += OnEditorColorChanged;
		GameColorPalette.Changed += OnEditorColorChanged;
	}

	private void UnsubscribeColorEvents()
	{
		if (!_colorEventsSubscribed)
		{
			return;
		}
		_colorEventsSubscribed = false;
		ColorSO.Changed -= OnEditorColorChanged;
		GameColorPalette.Changed -= OnEditorColorChanged;
	}

	// A palette-side asset/field change re-applies the Edit Mode preview so HUD
	// colors live-update while tuning GameColorPalette (or a ColorSO asset).
	private void OnEditorColorChanged(ColorSO changed)
	{
		UnityEditor.EditorApplication.delayCall += ApplyEditModePreview;
	}

	private void OnEditorColorChanged()
	{
		UnityEditor.EditorApplication.delayCall += ApplyEditModePreview;
	}
#endif

	// ------------------------------------------------------------------ phases

	// Silent sync to the current displayed values on combat entry: no tweens, no
	// effects, so the first frame never plays a phantom damage/heal from defaults.
	private void EnterCombat()
	{
		CleanupVisuals();
		int hp = GetDisplayedHp();
		int hpMax = GetLiveMaxHp();
		_fixedDigitCount = DigitCount(hp);
		_fixedMaxDigitCount = DigitCount(hpMax);
		LayoutRoots();
		SetCounterInstant(_current, true, hp);
		SetCounterInstant(_max, false, hpMax);
		displayRoot.gameObject.SetActive(true);
	}

	private void ExitCombat()
	{
		CleanupVisuals();
		displayRoot.gameObject.SetActive(false);
	}

	private void CleanupVisuals()
	{
		if (currentRoot == null)
		{
			return; // Awake disabled the component for missing references.
		}
		KillTween(ref _popTween);
		KillTween(ref _rowGlideTween);
		displayRoot.DOKill();
		displayRoot.anchoredPosition = _rootBasePos;
		displayRoot.localScale = Vector3.one;
		displayRoot.localEulerAngles = Vector3.zero;
		currentRoot.DOKill();
		maxRoot.DOKill();
		slashText.rectTransform.DOKill();
		KillAllStripTweens();
		_current.counting = false;
		_max.counting = false;
	}

	// ------------------------------------------------------------------ layout

	// Row: [current digits] gap [/] gap [max digits], centered on the displayRoot
	// pivot (0.5, 0.5). Each digit group is a top-pivot box whose single visible
	// strip line lands on the row center (group top sits at +lineHeight/2).
	private void LayoutRoots()
	{
		float gap = _em * groupGapEm;
		float width = _fixedDigitCount * _digitWidth;
		float maxWidth = _fixedMaxDigitCount * _maxDigitWidth;
		float total = width + gap + _slashWidth + gap + maxWidth;
		displayRoot.sizeDelta = new Vector2(total, _em);
		float x = -total * 0.5f;
		currentRoot.sizeDelta = new Vector2(width, _em);
		currentRoot.anchoredPosition = new Vector2(x, _em * 0.5f);
		float slashX = x + width + gap;
		slashText.rectTransform.sizeDelta = new Vector2(_slashWidth, _em);
		slashText.rectTransform.anchoredPosition = new Vector2(slashX + _slashWidth * 0.5f, 0f);
		maxRoot.sizeDelta = new Vector2(maxWidth, _maxEm);
		maxRoot.anchoredPosition = new Vector2(slashX + _slashWidth + gap, _maxEm * 0.5f);
	}

	private static void StretchFull(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	private static int DigitCount(int value)
	{
		value = Mathf.Abs(value);
		int digits = 1;
		while (value >= 10)
		{
			value /= 10;
			digits++;
		}
		return digits;
	}

	// ------------------------------------------------------------------ polling

	// Per-side normal color from the palette ("HP Bar / Numeric" group).
	private Color NormalColorValue => side == Side.Player ? GameColorPalette.HpNormalPlayerColor : GameColorPalette.HpNormalEnemyColor;

	private int GetDisplayedHp()
	{
		if (CombatInfoDisplayer.me == null || CombatManager.Me == null)
		{
			return 0;
		}
		if (side == Side.Player && CombatManager.Me.ownerPlayerStatusRef == null)
		{
			return 0;
		}
		if (side == Side.Enemy && CombatManager.Me.enemyPlayerStatusRef == null)
		{
			return 0;
		}
		return side == Side.Player ? CombatInfoDisplayer.me.GetDisplayedOwnerHp() : CombatInfoDisplayer.me.GetDisplayedEnemyHp();
	}

	private int GetLiveMaxHp()
	{
		if (CombatManager.Me == null)
		{
			return 1;
		}
		PlayerStatusSO status = side == Side.Player ? CombatManager.Me.ownerPlayerStatusRef : CombatManager.Me.enemyPlayerStatusRef;
		return status != null ? Mathf.Max(1, status.hpMax) : 1;
	}

	// ------------------------------------------------------------------ counter

	private void SetCounterTarget(CounterState counter, bool isCurrent, int value)
	{
		counter.target = value;
		// First step runs synchronously so the number starts moving in the same
		// frame as the hit shake, not one tick behind it (same fix as vertical).
		if (!counter.counting && counter.displayed != counter.target)
		{
			counter.counting = true;
			counter.elapsed = 0f;
			StepCounter(counter, isCurrent);
		}
	}

	private void TickCounter(CounterState counter, bool isCurrent)
	{
		if (!counter.counting)
		{
			return;
		}
		counter.elapsed += Time.deltaTime * CombatAnimationSpeed.SpeedScale;
		int guard = 0;
		while (counter.counting && guard < 20)
		{
			int remaining = Mathf.Abs(counter.target - counter.displayed);
			float delaySec = HPNumericCounter.StepDelay(remaining, easeOutFinish, stepMs, easeOutPoints, easeOutExtraMs) / 1000f;
			if (counter.elapsed < delaySec)
			{
				break;
			}
			counter.elapsed -= delaySec;
			StepCounter(counter, isCurrent);
			guard++;
		}
	}

	private void StepCounter(CounterState counter, bool isCurrent)
	{
		if (counter.displayed == counter.target)
		{
			counter.counting = false;
			counter.elapsed = 0f;
			return;
		}
		int direction = counter.displayed < counter.target ? 1 : -1;
		int remaining = Mathf.Abs(counter.target - counter.displayed);
		int step = Mathf.Min(remaining, HPNumericCounter.StepSizeFor(remaining, stepMs, targetCountMs, easeOutPoints));
		counter.displayed += direction * step;
		ShowValue(isCurrent, counter.displayed, direction);
		if (counter.displayed == counter.target)
		{
			counter.counting = false;
			counter.elapsed = 0f;
			PlayLandPop(isCurrent, counter.displayed);
		}
	}

	private void SetCounterInstant(CounterState counter, bool isCurrent, int value)
	{
		counter.displayed = value;
		counter.target = value;
		counter.counting = false;
		counter.elapsed = 0f;
		SnapStrips(StripsFor(isCurrent), isCurrent, value);
		VerifyStripMetrics();
	}

	private void ShowValue(bool isCurrent, int value, int direction)
	{
		SetStripsValue(StripsFor(isCurrent), isCurrent, value, direction);
	}

	private List<DigitStrip> StripsFor(bool isCurrent)
	{
		return isCurrent ? _currentDigitStrips : _maxDigitStrips;
	}

	// ------------------------------------------------------------------ digit roll

	private static int Canonical(int digit)
	{
		return DigitsPerCycle + digit;
	}

	private void SetStripsValue(List<DigitStrip> strips, bool isCurrent, int value, int direction)
	{
		string s = value.ToString();
		EnsureStripCount(strips, isCurrent, s.Length);
		RepositionStrips(strips);
		for (int i = 0; i < s.Length; i++)
		{
			DigitStrip entry = strips[i];
			int fromRight = s.Length - 1 - i;
			int targetDigit = s[i] - '0';
			// Base the roll on the canonical spot of the digit the strip is committed
			// to; mid-flight re-aims still start visually from wherever the strip is.
			int baseIdx = Canonical(entry.idx % DigitsPerCycle);
			int currentDigit = baseIdx - DigitsPerCycle;
			KillStripTween(entry);
			if (targetDigit == currentDigit)
			{
				entry.idx = baseIdx;
				SetStripY(entry, baseIdx);
				continue;
			}
			int k;
			if (direction > 0)
			{
				k = targetDigit > currentDigit ? Canonical(targetDigit) : 2 * DigitsPerCycle + targetDigit;
			}
			else
			{
				k = targetDigit < currentDigit ? Canonical(targetDigit) : targetDigit;
			}
			int steps = Mathf.Abs(k - baseIdx);
			float dur = (rollBaseMs + steps * rollPerStepMs) / 1000f;
			float delaySec = fromRight * rollStaggerMs / 1000f;
			entry.idx = k;
			int snap = Canonical(targetDigit);
			DigitStrip captured = entry;
			// Unity y points up (CSS y points down): the strip rides at +k * lineHeight.
			entry.tween = ApplySpeed(entry.strip
				.DOAnchorPosY(k * entry.lineHeight, dur)
				.SetEase(Ease.OutCubic)
				.SetDelay(delaySec)
				.OnComplete(() =>
				{
					captured.idx = snap;
					SetStripY(captured, snap);
				}));
		}
	}

	private void SnapStrips(List<DigitStrip> strips, bool isCurrent, int value)
	{
		string s = value.ToString();
		EnsureStripCount(strips, isCurrent, s.Length);
		RepositionStrips(strips);
		for (int i = 0; i < s.Length; i++)
		{
			DigitStrip entry = strips[i];
			KillStripTween(entry);
			entry.idx = Canonical(s[i] - '0');
			SetStripY(entry, entry.idx);
		}
	}

	// Digit slots grow/shrink from the left, exactly like the vertical version.
	private void EnsureStripCount(List<DigitStrip> strips, bool isCurrent, int count)
	{
		RectTransform container = isCurrent ? currentStrips : maxStrips;
		bool isMaxGroup = !isCurrent;
		while (strips.Count < count)
		{
			strips.Insert(0, CreateStrip(container, isMaxGroup));
		}
		while (strips.Count > count)
		{
			DigitStrip removed = strips[0];
			strips.RemoveAt(0);
			KillStripTween(removed);
			if (removed.slot != null)
			{
				Destroy(removed.slot.gameObject);
			}
		}
	}

	private DigitStrip CreateStrip(RectTransform container, bool isMaxGroup)
	{
		float digitWidth = isMaxGroup ? _maxDigitWidth : _digitWidth;
		float lineHeight = isMaxGroup ? _maxEm : _em;
		float fontSize = isMaxGroup ? _maxEm : _em;
		var slotGo = new GameObject("Digit", typeof(RectTransform));
		var slotRt = (RectTransform)slotGo.transform;
		slotRt.SetParent(container, false);
		slotRt.anchorMin = new Vector2(0.5f, 1f);
		slotRt.anchorMax = new Vector2(0.5f, 1f);
		slotRt.pivot = new Vector2(0.5f, 1f);
		slotRt.sizeDelta = new Vector2(digitWidth, lineHeight);
		slotRt.anchoredPosition = Vector2.zero;
		slotGo.AddComponent<RectMask2D>();

		var stripGo = new GameObject("Strip", typeof(RectTransform));
		var stripRt = (RectTransform)stripGo.transform;
		stripRt.SetParent(slotRt, false);
		stripRt.anchorMin = new Vector2(0.5f, 1f);
		stripRt.anchorMax = new Vector2(0.5f, 1f);
		stripRt.pivot = new Vector2(0.5f, 1f);
		stripRt.sizeDelta = new Vector2(digitWidth, lineHeight * DigitsPerCycle * StripCycles);
		stripRt.anchoredPosition = Vector2.zero;

		var tmp = stripGo.AddComponent<TextMeshProUGUI>();
		tmp.text = _stripText;
		tmp.font = currentPlain.font;
		tmp.fontSize = fontSize;
		tmp.fontStyle = currentPlain.fontStyle;
		tmp.alignment = TextAlignmentOptions.Top;
		tmp.lineSpacing = _stripLineSpacing;
		tmp.enableWordWrapping = false;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.raycastTarget = false;
		tmp.color = NormalColorValue;
		var entry = new DigitStrip { slot = slotRt, strip = stripRt, text = tmp, idx = Canonical(0), digitWidth = digitWidth, lineHeight = lineHeight };
		SetStripY(entry, entry.idx);
		return entry;
	}

	private void RepositionStrips(List<DigitStrip> strips)
	{
		for (int i = 0; i < strips.Count; i++)
		{
			float x = (i - (strips.Count - 1) * 0.5f) * strips[i].digitWidth;
			strips[i].slot.anchoredPosition = new Vector2(x, 0f);
		}
	}

	private void SetStripY(DigitStrip entry, int lineIndex)
	{
		Vector2 pos = entry.strip.anchoredPosition;
		pos.y = lineIndex * entry.lineHeight;
		entry.strip.anchoredPosition = pos;
	}

	private static void KillStripTween(DigitStrip entry)
	{
		if (entry.tween != null && entry.tween.IsActive())
		{
			entry.tween.Kill();
		}
		entry.tween = null;
	}

	private void KillAllStripTweens()
	{
		foreach (DigitStrip entry in _currentDigitStrips)
		{
			KillStripTween(entry);
			entry.text.DOKill();
		}
		foreach (DigitStrip entry in _maxDigitStrips)
		{
			KillStripTween(entry);
			entry.text.DOKill();
		}
	}

	// One-time runtime check that the lineSpacing formula really produced a 1em line
	// advance per group; if the font metrics misbehave, the measured advance takes
	// over for mask heights and strip positioning (same fallback as vertical).
	private void VerifyStripMetrics()
	{
		if (_stripMetricsVerified)
		{
			return;
		}
		_stripMetricsVerified = true;
		ProbeStripMetrics(_currentDigitStrips);
		ProbeStripMetrics(_maxDigitStrips);
	}

	private void ProbeStripMetrics(List<DigitStrip> strips)
	{
		if (strips.Count == 0)
		{
			return;
		}
		TMP_Text probe = strips[0].text;
		probe.ForceMeshUpdate();
		if (probe.textInfo.lineCount < 2)
		{
			return;
		}
		// The line-to-line advance (baseline delta) is what positions digits;
		// lineInfo.lineHeight excludes lineSpacing and is the wrong metric here.
		float measured = probe.textInfo.lineInfo[0].baseline - probe.textInfo.lineInfo[1].baseline;
		if (Mathf.Abs(measured - strips[0].lineHeight) <= 0.5f)
		{
			return;
		}
		Debug.LogWarning("[HPNumericDisplayHorizontal] Strip line advance " + measured + " differs from 1em (" + strips[0].lineHeight + "); using the measured value.");
		foreach (DigitStrip entry in strips)
		{
			entry.lineHeight = measured;
			entry.slot.sizeDelta = new Vector2(entry.digitWidth, measured);
			entry.strip.sizeDelta = new Vector2(entry.digitWidth, measured * DigitsPerCycle * StripCycles);
			SetStripY(entry, entry.idx);
		}
	}

	// ------------------------------------------------------------------ effects

	private void PlayShake(int damage)
	{
		float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
		if (scaleFactor <= 0.0001f)
		{
			scaleFactor = 1f;
		}
		float amplitudePx = Mathf.Min(10f, 2f + damage * 0.3f);
		float rotationDeg = Mathf.Min(2.2f, amplitudePx * 0.22f);
		float amplitude = amplitudePx / scaleFactor;
		// The root-level DOKill intentionally also kills a running landing pop:
		// shake and pop share the root, and restart-on-new-hit is desired for both.
		displayRoot.DOKill();
		displayRoot.anchoredPosition = _rootBasePos;
		displayRoot.localEulerAngles = Vector3.zero;
		displayRoot.localScale = Vector3.one;
		ApplySpeed(displayRoot.DOShakePosition(shakeDuration, new Vector3(amplitude, amplitude * 0.2f, 0f), shakeVibrato, 0f, false, true));
		ApplySpeed(displayRoot.DOShakeRotation(shakeDuration, new Vector3(0f, 0f, rotationDeg), shakeVibrato, 0f, true));
	}

	private void PlayLandPop(bool isCurrent, int settledValue)
	{
		// Kill only the previous pop (never a running shake; the shake's own root
		// DOKill is what restarts the pop on a new hit).
		KillTween(ref _popTween);
		displayRoot.localScale = Vector3.one;
		_popTween = ApplySpeed(DOTween.Sequence()
			.Append(displayRoot.DOScale(landPopScale, landPopUpDuration).SetEase(Ease.OutQuad))
			.Append(displayRoot.DOScale(1f, landPopDownDuration).SetEase(Ease.OutQuad)));
	}

	// Digit-count growth (e.g. max HP 99 -> 100, or an overheal past the reserved
	// width): rebuild the row, re-center each digit group, and glide the group
	// x-positions so the re-centering lands smoothly in the same frame.
	private void CheckDigitGrowth(int hp, int hpMax)
	{
		int needed = DigitCount(hp);
		int neededMax = DigitCount(hpMax);
		if (needed <= _fixedDigitCount && neededMax <= _fixedMaxDigitCount)
		{
			return;
		}
		Vector2 curPos = currentRoot.anchoredPosition;
		Vector2 slashPos = slashText.rectTransform.anchoredPosition;
		Vector2 maxPos = maxRoot.anchoredPosition;
		_fixedDigitCount = needed;
		_fixedMaxDigitCount = neededMax;
		LayoutRoots();
		RepositionStrips(_currentDigitStrips);
		RepositionStrips(_maxDigitStrips);
		float newCurX = currentRoot.anchoredPosition.x;
		float newSlashX = slashText.rectTransform.anchoredPosition.x;
		float newMaxX = maxRoot.anchoredPosition.x;
		currentRoot.anchoredPosition = curPos;
		slashText.rectTransform.anchoredPosition = slashPos;
		maxRoot.anchoredPosition = maxPos;
		KillTween(ref _rowGlideTween);
		_rowGlideTween = ApplySpeed(DOTween.Sequence()
			.Append(currentRoot.DOAnchorPosX(newCurX, dividerGlideDuration).SetEase(Ease.OutQuad))
			.Join(slashText.rectTransform.DOAnchorPosX(newSlashX, dividerGlideDuration).SetEase(Ease.OutQuad))
			.Join(maxRoot.DOAnchorPosX(newMaxX, dividerGlideDuration).SetEase(Ease.OutQuad)));
	}

	// ------------------------------------------------------------------ helpers

	private static void KillTween(ref Tween tween)
	{
		if (tween != null && tween.IsActive())
		{
			tween.Kill();
		}
		tween = null;
	}

	// timeScale (not ScaleDuration) so SetDelay-based staggers scale with the global
	// combat animation speed together with the durations. Applied to standalone
	// tweens or whole sequences (never to tweens nested inside a sequence).
	private static T ApplySpeed<T>(T tween) where T : Tween
	{
		tween.timeScale = CombatAnimationSpeed.SpeedScale;
		return tween;
	}

	// ------------------------------------------------------------------ edit mode preview

	// Edit Mode preview: mirrors the Awake layout math (same constants, same seam)
	// and writes sample values so the real arrangement and digit sizes are visible
	// in the Scene/Game view without entering Play Mode. Odometer strips are not
	// built; plain text at the same font sizes represents the layout exactly.
	// Saved scene values are inert: Awake and combat entry fully rebuild layout and
	// text at runtime.
	// Deferred: touching the RectTransform inside OnValidate raises
	// OnRectTransformDimensionsChange via SendMessage, which Unity forbids there.
	private void OnValidate()
	{
#if UNITY_EDITOR
		if (!Application.isPlaying)
		{
			SubscribeColorEvents();
		}
		if (Application.isPlaying || !editModePreview || displayRoot == null || currentRoot == null
			|| currentPlain == null || currentStrips == null || slashText == null
			|| maxRoot == null || maxPlain == null || maxStrips == null)
		{
			return;
		}
		UnityEditor.EditorApplication.delayCall += ApplyEditModePreview;
#endif
	}

#if UNITY_EDITOR
	private void ApplyEditModePreview()
	{
		if (Application.isPlaying || !editModePreview || displayRoot == null || currentRoot == null
			|| currentPlain == null || currentStrips == null || slashText == null
			|| maxRoot == null || maxPlain == null || maxStrips == null)
		{
			return;
		}
		_em = currentPlain.fontSize;
		_maxEm = _em * maxFontScale;
		maxPlain.fontSize = _maxEm;
		slashText.fontSize = _em;
		slashText.text = "/";
		_digitWidth = currentPlain.GetPreferredValues("0").x;
		if (_digitWidth <= 0.01f)
		{
			_digitWidth = _em * 0.6f;
		}
		_maxDigitWidth = maxPlain.GetPreferredValues("0").x;
		if (_maxDigitWidth <= 0.01f)
		{
			_maxDigitWidth = _maxEm * 0.6f;
		}
		_slashWidth = slashText.GetPreferredValues("/").x;
		if (_slashWidth <= 0.01f)
		{
			_slashWidth = _em * 0.5f;
		}
		_fixedDigitCount = DigitCount(previewHp);
		_fixedMaxDigitCount = DigitCount(previewHpMax);
		StretchFull(currentPlain.rectTransform);
		StretchFull(maxPlain.rectTransform);
		StretchFull(currentStrips);
		StretchFull(maxStrips);
		LayoutRoots();
		currentPlain.text = previewHp.ToString();
		maxPlain.text = previewHpMax.ToString();
		currentPlain.gameObject.SetActive(true);
		maxPlain.gameObject.SetActive(true);
		currentPlain.color = NormalColorValue;
		maxPlain.color = NormalColorValue;
		slashText.color = NormalColorValue;
	}
#endif
}
