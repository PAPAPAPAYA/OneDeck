using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Numeric HP display: big fraction (current over a thick divider over max), one
/// component instance per side. Pure presentation; no game-logic changes.
///
/// Current HP polls CombatInfoDisplayer's displayed-HP accessors (the same
/// queue-frozen values the HP text shows) so the count starts exactly when a hit
/// lands. Max HP has no display queue anywhere, so it is read live from the side's
/// PlayerStatusSO (accepted side effect: the denominator moves immediately while a
/// queue-frozen numerator catches up when the hit lands).
///
/// Motion design validated in docs/demo/HPNumericDisplayDemo.html.
/// Plan: plans/plan-hp-numeric-display-2026-07-19.md
/// </summary>
public class HPNumericDisplay : MonoBehaviour
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
	public Image divider;
	public RectTransform maxRoot;
	public TMP_Text maxPlain;
	public RectTransform maxStrips;
	public GamePhaseSO gamePhaseRef;
	public Canvas canvas;

	[Header("Mode")]
	[Tooltip("false = plain text; true = odometer digit-roll strips. The same tick loop drives both.")]
	public bool digitRoll = false;

	[Header("Feature toggles")]
	[Tooltip("Low-HP red pulse (current <= 25% max and > 0). Toggling off mid-pulse restores the color next frame.")]
	public bool enableLowHpPulse = true;
	[Tooltip("Zero-out: drop through the divider, gray settle, divider flash. Toggling off mid-state cancels and restores next frame.")]
	public bool enableZeroOut = true;

	[Header("Colors")]
	public Color normalColor = new Color(0.078f, 0.078f, 0.078f); // demo #141414
	public Color lowHpColor = new Color(0.753f, 0.224f, 0.169f); // demo #c0392b
	public Color zeroGrayColor = new Color(0.541f, 0.541f, 0.565f); // demo #8a8a90

	[Header("Counting (demo constants)")]
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
	public float lowHpThreshold = 0.25f;
	public float lowHpPulseHalfDuration = 0.575f;
	public float zeroDropEm = 0.8f;
	public float zeroDropDuration = 0.38f;
	public float zeroSettleEm = 0.08f;
	public float zeroSettleDuration = 0.2f;
	public float zeroFlashDuration = 0.42f;
	public float stateColorFadeDuration = 0.25f;
	public float dividerGlideDuration = 0.24f;

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
	}

	private readonly CounterState _current = new CounterState();
	private readonly CounterState _max = new CounterState();
	private readonly List<DigitStrip> _currentDigitStrips = new List<DigitStrip>();
	private readonly List<DigitStrip> _maxDigitStrips = new List<DigitStrip>();
	private readonly List<Tween> _pulseTweens = new List<Tween>();

	private bool _wasInCombat;
	private Vector2 _rootBasePos;
	private Vector2 _currentRootBasePos;
	private float _em = 1f; // 1 em = fontSize in px; unit for all em-based offsets.
	private float _digitWidth = 10f;
	private float _stripLineHeight = 1f;
	private float _stripLineSpacing;
	private int _fixedDigitCount = 1;
	private string _stripText;
	private bool _stripMetricsVerified;
	private CanvasGroup _currentCanvasGroup;

	private Tween _popTween;
	private bool _lowPulsing;
	private bool _zeroActive;
	private Sequence _zeroDropSequence;
	private Tween _dividerWidthTween;

	private void Awake()
	{
		if (displayRoot == null || currentRoot == null || currentPlain == null || currentStrips == null
			|| divider == null || maxRoot == null || maxPlain == null || maxStrips == null || gamePhaseRef == null)
		{
			Debug.LogError("[HPNumericDisplay] Missing serialized reference(s), disabling.");
			enabled = false;
			return;
		}
		if (canvas == null)
		{
			canvas = GetComponentInParent<Canvas>();
		}
		_currentCanvasGroup = currentRoot.GetComponent<CanvasGroup>();
		if (_currentCanvasGroup == null)
		{
			_currentCanvasGroup = currentRoot.gameObject.AddComponent<CanvasGroup>();
		}
		_em = currentPlain.fontSize;
		TMP_FontAsset fontAsset = currentPlain.font;
		// 1em line advance: TMP's line advance is
		// faceInfo.lineHeight * fontSize/pointSize + lineSpacing * fontSize/100
		// (verified empirically 2026-07-19), so 100*(pointSize-lineHeight)/pointSize
		// makes the advance exactly fontSize. This corrects the plan's raw
		// (pointSize - lineHeight) form, which is off by the 100/pointSize factor.
		// VerifyStripMetrics re-measures and wins if a font still misbehaves.
		_stripLineSpacing = 100f * (fontAsset.faceInfo.pointSize - fontAsset.faceInfo.lineHeight) / fontAsset.faceInfo.pointSize;
		currentPlain.lineSpacing = _stripLineSpacing;
		maxPlain.lineSpacing = _stripLineSpacing;
		_stripLineHeight = _em;
		_digitWidth = currentPlain.GetPreferredValues("0").x;
		if (_digitWidth <= 0.01f)
		{
			_digitWidth = _em * 0.6f;
		}
		// Odometer strip content: three 0-9 cycles so rolls crossing 0/9 always have
		// room either way; the canonical resting spot for digit d is line 10+d
		// (middle cycle) and snaps back invisibly after each roll.
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
		SetTopCenter(currentRoot);
		SetTopCenter(maxRoot);
		SetTopCenter(divider.rectTransform);
		StretchFull(currentPlain.rectTransform);
		StretchFull(maxPlain.rectTransform);
		StretchFull(currentStrips);
		StretchFull(maxStrips);
		LayoutRoots();
		_rootBasePos = displayRoot.anchoredPosition;
		_currentRootBasePos = currentRoot.anchoredPosition;
		currentPlain.gameObject.SetActive(!digitRoll);
		currentStrips.gameObject.SetActive(digitRoll);
		maxPlain.gameObject.SetActive(!digitRoll);
		maxStrips.gameObject.SetActive(digitRoll);
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

		// Classify from this side's displayed-HP delta (same rule as the compare bar):
		// drop -> shake scaled by the damage; rise -> count only.
		int delta = hp - _current.target;
		if (delta < 0)
		{
			PlayShake(-delta);
		}
		SetCounterTarget(_current, true, hp);
		SetCounterTarget(_max, false, hpMax);

		TickCounter(_current, true);
		TickCounter(_max, false);

		UpdateZeroState();
		UpdateLowPulse(hp, hpMax);
		CheckDigitGrowth(hp, hpMax);
	}

	private void OnDisable()
	{
		CleanupVisuals();
	}

	// ------------------------------------------------------------------ phases

	// Silent sync to the current displayed values on combat entry: no tweens, no
	// effects, so the first frame never plays a phantom damage/heal from defaults.
	private void EnterCombat()
	{
		CleanupVisuals();
		int hp = GetDisplayedHp();
		int hpMax = GetLiveMaxHp();
		// Fixed digit count reserves room for an overheal above hpMax without
		// overflowing the layout.
		_fixedDigitCount = Mathf.Max(DigitCount(hp), DigitCount(hpMax));
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
		_zeroActive = false;
		_lowPulsing = false;
		KillTween(ref _popTween);
		KillTween(ref _dividerWidthTween);
		if (_zeroDropSequence != null && _zeroDropSequence.IsActive())
		{
			_zeroDropSequence.Kill();
		}
		_zeroDropSequence = null;
		KillPulseTweens();
		displayRoot.DOKill();
		displayRoot.anchoredPosition = _rootBasePos;
		displayRoot.localScale = Vector3.one;
		displayRoot.localEulerAngles = Vector3.zero;
		currentRoot.DOKill();
		currentRoot.anchoredPosition = _currentRootBasePos;
		currentRoot.localScale = Vector3.one;
		_currentCanvasGroup.DOKill();
		_currentCanvasGroup.alpha = 1f;
		divider.DOKill();
		SetCurrentColorInstant(normalColor);
		SetDividerInstant(normalColor, 1f);
		KillAllStripTweens();
		_current.counting = false;
		_max.counting = false;
	}

	// ------------------------------------------------------------------ layout

	private void LayoutRoots()
	{
		float width = _fixedDigitCount * _digitWidth;
		float em = _em;
		displayRoot.sizeDelta = new Vector2(width, em * 2.23f);
		currentRoot.sizeDelta = new Vector2(width, em);
		currentRoot.anchoredPosition = Vector2.zero;
		divider.rectTransform.sizeDelta = new Vector2(width, em * 0.11f);
		divider.rectTransform.anchoredPosition = new Vector2(0f, -(em * 1.06f));
		maxRoot.sizeDelta = new Vector2(width, em);
		maxRoot.anchoredPosition = new Vector2(0f, -(em * 1.23f));
	}

	private static void SetTopCenter(RectTransform rt)
	{
		rt.anchorMin = new Vector2(0.5f, 1f);
		rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
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
		// frame as the hit shake, not one tick behind it (demo fix).
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
		ShowInstant(isCurrent, value);
	}

	private void ShowValue(bool isCurrent, int value, int direction)
	{
		if (digitRoll)
		{
			SetStripsValue(StripsFor(isCurrent), isCurrent, value, direction);
		}
		else
		{
			GetPlain(isCurrent).text = value.ToString();
		}
	}

	private void ShowInstant(bool isCurrent, int value)
	{
		if (digitRoll)
		{
			SnapStrips(StripsFor(isCurrent), isCurrent, value);
			VerifyStripMetrics();
		}
		else
		{
			GetPlain(isCurrent).text = value.ToString();
		}
	}

	private TMP_Text GetPlain(bool isCurrent)
	{
		return isCurrent ? currentPlain : maxPlain;
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
				.DOAnchorPosY(k * _stripLineHeight, dur)
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

	// Digit slots grow/shrink from the left, exactly like the demo's unshift/shift.
	private void EnsureStripCount(List<DigitStrip> strips, bool isCurrent, int count)
	{
		RectTransform container = isCurrent ? currentStrips : maxStrips;
		while (strips.Count < count)
		{
			strips.Insert(0, CreateStrip(container));
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

	private DigitStrip CreateStrip(RectTransform container)
	{
		var slotGo = new GameObject("Digit", typeof(RectTransform));
		var slotRt = (RectTransform)slotGo.transform;
		slotRt.SetParent(container, false);
		slotRt.anchorMin = new Vector2(0.5f, 1f);
		slotRt.anchorMax = new Vector2(0.5f, 1f);
		slotRt.pivot = new Vector2(0.5f, 1f);
		slotRt.sizeDelta = new Vector2(_digitWidth, _stripLineHeight);
		slotRt.anchoredPosition = Vector2.zero;
		slotGo.AddComponent<RectMask2D>();

		var stripGo = new GameObject("Strip", typeof(RectTransform));
		var stripRt = (RectTransform)stripGo.transform;
		stripRt.SetParent(slotRt, false);
		stripRt.anchorMin = new Vector2(0.5f, 1f);
		stripRt.anchorMax = new Vector2(0.5f, 1f);
		stripRt.pivot = new Vector2(0.5f, 1f);
		stripRt.sizeDelta = new Vector2(_digitWidth, _stripLineHeight * DigitsPerCycle * StripCycles);
		stripRt.anchoredPosition = Vector2.zero;

		var tmp = stripGo.AddComponent<TextMeshProUGUI>();
		tmp.text = _stripText;
		tmp.font = currentPlain.font;
		tmp.fontSize = _em;
		tmp.fontStyle = currentPlain.fontStyle;
		tmp.alignment = TextAlignmentOptions.Top;
		tmp.lineSpacing = _stripLineSpacing;
		tmp.enableWordWrapping = false;
		tmp.overflowMode = TextOverflowModes.Overflow;
		tmp.raycastTarget = false;
		tmp.color = _zeroActive ? zeroGrayColor : normalColor;
		var entry = new DigitStrip { slot = slotRt, strip = stripRt, text = tmp, idx = Canonical(0) };
		SetStripY(entry, entry.idx);
		return entry;
	}

	private void RepositionStrips(List<DigitStrip> strips)
	{
		for (int i = 0; i < strips.Count; i++)
		{
			float x = (i - (strips.Count - 1) * 0.5f) * _digitWidth;
			strips[i].slot.anchoredPosition = new Vector2(x, 0f);
		}
	}

	private void SetStripY(DigitStrip entry, int lineIndex)
	{
		Vector2 pos = entry.strip.anchoredPosition;
		pos.y = lineIndex * _stripLineHeight;
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
	// advance; if the font metrics misbehave, the measured advance takes over for
	// mask heights and strip positioning (plan section 3 fallback).
	private void VerifyStripMetrics()
	{
		if (_stripMetricsVerified || !digitRoll)
		{
			return;
		}
		TMP_Text probe = null;
		if (_currentDigitStrips.Count > 0)
		{
			probe = _currentDigitStrips[0].text;
		}
		else if (_maxDigitStrips.Count > 0)
		{
			probe = _maxDigitStrips[0].text;
		}
		if (probe == null)
		{
			return;
		}
		_stripMetricsVerified = true;
		probe.ForceMeshUpdate();
		if (probe.textInfo.lineCount < 2)
		{
			return;
		}
		// The line-to-line advance (baseline delta) is what positions digits;
		// lineInfo.lineHeight excludes lineSpacing and is the wrong metric here.
		float measured = probe.textInfo.lineInfo[0].baseline - probe.textInfo.lineInfo[1].baseline;
		if (Mathf.Abs(measured - _stripLineHeight) <= 0.5f)
		{
			return;
		}
		Debug.LogWarning("[HPNumericDisplay] Strip line advance " + measured + " differs from 1em (" + _stripLineHeight + "); using the measured value.");
		_stripLineHeight = measured;
		foreach (DigitStrip entry in _currentDigitStrips)
		{
			ApplyStripMetrics(entry);
		}
		foreach (DigitStrip entry in _maxDigitStrips)
		{
			ApplyStripMetrics(entry);
		}
	}

	private void ApplyStripMetrics(DigitStrip entry)
	{
		entry.slot.sizeDelta = new Vector2(_digitWidth, _stripLineHeight);
		entry.strip.sizeDelta = new Vector2(_digitWidth, _stripLineHeight * DigitsPerCycle * StripCycles);
		SetStripY(entry, entry.idx);
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
		if (isCurrent && enableZeroOut && settledValue == 0)
		{
			return; // the zero-out drop is the feedback there (demo ties the skip to zeroFx).
		}
		// Kill only the previous pop (never a running shake; the shake's own root
		// DOKill is what restarts the pop on a new hit).
		KillTween(ref _popTween);
		displayRoot.localScale = Vector3.one;
		_popTween = ApplySpeed(DOTween.Sequence()
			.Append(displayRoot.DOScale(landPopScale, landPopUpDuration).SetEase(Ease.OutQuad))
			.Append(displayRoot.DOScale(1f, landPopDownDuration).SetEase(Ease.OutQuad)));
	}

	private void UpdateZeroState()
	{
		bool atZero = enableZeroOut && _current.displayed == 0;
		if (atZero && !_zeroActive)
		{
			PlayZeroEnter();
		}
		else if (!atZero && _zeroActive)
		{
			CancelZero();
		}
	}

	private void PlayZeroEnter()
	{
		_zeroActive = true;
		foreach (TMP_Text text in CurrentColorTargets())
		{
			text.DOKill();
			ApplySpeed(text.DOColor(zeroGrayColor, stateColorFadeDuration));
		}
		divider.DOKill();
		ApplySpeed(divider.DOColor(zeroGrayColor, stateColorFadeDuration));
		// Drop through the divider while fading and shrinking.
		_zeroDropSequence = DOTween.Sequence();
		_zeroDropSequence.Append(currentRoot.DOAnchorPosY(_currentRootBasePos.y - zeroDropEm * _em, zeroDropDuration).SetEase(Ease.InCubic));
		_zeroDropSequence.Join(_currentCanvasGroup.DOFade(0f, zeroDropDuration).SetEase(Ease.InCubic));
		_zeroDropSequence.Join(currentRoot.DOScale(0.9f, zeroDropDuration).SetEase(Ease.InCubic));
		_zeroDropSequence.AppendCallback(() =>
		{
			if (!_zeroActive)
			{
				return;
			}
			// Release the drop (snap back to the natural slot), then settle the gray
			// "0" back in from slightly above.
			currentRoot.localScale = Vector3.one;
			currentRoot.anchoredPosition = _currentRootBasePos + new Vector2(0f, zeroSettleEm * _em);
			_currentCanvasGroup.alpha = 0f;
			ApplySpeed(currentRoot.DOAnchorPosY(_currentRootBasePos.y, zeroSettleDuration).SetEase(Ease.OutCubic));
			ApplySpeed(_currentCanvasGroup.DOFade(1f, zeroSettleDuration).SetEase(Ease.OutQuad));
		});
		ApplySpeed(_zeroDropSequence);
		// Divider snap-flash x2.
		float seg = zeroFlashDuration / 4f;
		ApplySpeed(DOTween.Sequence()
			.Append(divider.DOFade(0.15f, seg))
			.Append(divider.DOFade(1f, seg))
			.Append(divider.DOFade(0.25f, seg))
			.Append(divider.DOFade(1f, seg)));
	}

	private void CancelZero()
	{
		_zeroActive = false;
		if (_zeroDropSequence != null && _zeroDropSequence.IsActive())
		{
			_zeroDropSequence.Kill();
		}
		_zeroDropSequence = null;
		currentRoot.DOKill();
		currentRoot.anchoredPosition = _currentRootBasePos;
		currentRoot.localScale = Vector3.one;
		_currentCanvasGroup.DOKill();
		_currentCanvasGroup.alpha = 1f;
		divider.DOKill();
		SetDividerInstant(normalColor, 1f);
		// Restore instantly; UpdateLowPulse restarts the pulse from here if still low.
		SetCurrentColorInstant(normalColor);
	}

	private void UpdateLowPulse(int hp, int hpMax)
	{
		bool shouldPulse = enableLowHpPulse && hp > 0 && hp <= Mathf.Max(1, Mathf.FloorToInt(hpMax * lowHpThreshold));
		if (shouldPulse == _lowPulsing)
		{
			return;
		}
		_lowPulsing = shouldPulse;
		if (shouldPulse)
		{
			foreach (TMP_Text text in CurrentColorTargets())
			{
				_pulseTweens.Add(ApplySpeed(text.DOColor(lowHpColor, lowHpPulseHalfDuration).SetLoops(-1, LoopType.Yoyo)));
			}
		}
		else
		{
			KillPulseTweens();
			SetCurrentColorInstant(_zeroActive ? zeroGrayColor : normalColor);
		}
	}

	private void KillPulseTweens()
	{
		for (int i = 0; i < _pulseTweens.Count; i++)
		{
			if (_pulseTweens[i] != null && _pulseTweens[i].IsActive())
			{
				_pulseTweens[i].Kill();
			}
		}
		_pulseTweens.Clear();
	}

	private IEnumerable<TMP_Text> CurrentColorTargets()
	{
		if (!digitRoll)
		{
			yield return currentPlain;
		}
		else
		{
			foreach (DigitStrip entry in _currentDigitStrips)
			{
				yield return entry.text;
			}
		}
	}

	private void SetCurrentColorInstant(Color color)
	{
		foreach (TMP_Text text in CurrentColorTargets())
		{
			text.DOKill();
			text.color = color;
		}
	}

	private void SetDividerInstant(Color color, float alpha)
	{
		divider.DOKill();
		Color c = color;
		c.a = alpha;
		divider.color = c;
	}

	// Digit-count growth (e.g. max HP 99 -> 100, or an overheal past the reserved
	// width): rebuild the fixed root widths and glide the divider to the new number
	// width in the same frame, so the relayout and the glide land together.
	private void CheckDigitGrowth(int hp, int hpMax)
	{
		int needed = Mathf.Max(DigitCount(hp), DigitCount(hpMax));
		if (needed <= _fixedDigitCount)
		{
			return;
		}
		float startWidth = divider.rectTransform.sizeDelta.x; // live, possibly mid-glide
		_fixedDigitCount = needed;
		float newWidth = _fixedDigitCount * _digitWidth;
		LayoutRoots();
		divider.rectTransform.sizeDelta = new Vector2(startWidth, divider.rectTransform.sizeDelta.y);
		KillTween(ref _dividerWidthTween);
		_dividerWidthTween = ApplySpeed(DOTween.To(
			() => divider.rectTransform.sizeDelta.x,
			x => divider.rectTransform.sizeDelta = new Vector2(x, divider.rectTransform.sizeDelta.y),
			newWidth, dividerGlideDuration).SetEase(Ease.OutQuad));
		Color c = divider.color;
		c.a = 0.35f;
		divider.color = c;
		ApplySpeed(divider.DOFade(1f, dividerGlideDuration).SetEase(Ease.OutQuad));
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
}
