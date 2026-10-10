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
///
/// 2026-09-19: the PLAYER side is also the shop-phase HP readout (shop top bar
/// reuses this component, repositioned via ShopTopBarLayout; combat anchor/scale
/// captured at Awake and restored on combat entry). Outside combat the display
/// queue is frozen at the last combat's values, so current HP reads the live
/// PlayerStatusSO instead. Enemy side stays combat-only.
/// 2026-09-21 world scroll (plan-shop-topbar-world-scroll-2026-09-21 §3.3): in a
/// SETTLED shop the canvas pill hides — the world copy lives in the ShopHudPage chrome
/// and scrolls with the page; during a driver transition (IsTransitioning) the canvas
/// pill stays visible and glides (shared-element flight), so it parks at the shop
/// anchor while hidden. Combat odometer/strips/shake/pop are untouched.
/// 2026-10-02 re-audit fixes 2/3/7 (docs/PhaseTransition.md): while the driver travels,
/// the pill flies in WORLD space (HudWorldFlight) between the shop chrome mirror spot
/// and its combat anchor — no scale tween; the enemy pill enters with a full-transDur
/// counter-direction slide from enemySlide above (started together with the camera
/// travel, demo :724-727) and stays visible through Result, sliding UP out on the
/// return descent (fix 3, demo :727).
///
/// 2026-10-07 HP NAME PLATE (plan-hp-name-plate-2026-10-07, docs/demo/HPNamePlateDemo.html):
/// the display becomes the merged plate — the digit row is wrapped by a sliced face
/// Image over the standard hard shadow (cardShadow) extended down-right into the
/// username band, replacing the per-side PlayerIcon/EnemyIcon + name labels
/// (CombatIconPresenter, retired). LayoutRoots lays out label row + face + shadow +
/// name; the slash renders full-size (2026-10-07 ruling); hit shake / landing pop move the
/// whole plate (they already move displayRoot). The name polls per frame with the
/// presenter's diff guard (PlayerIdentity.Username / OpponentDeckCache, "???" fallback)
/// and auto-shrinks to a floor before TMP ellipsis. All geometry is em-relative
/// (1em = current digit font size); colors come from the GameColorPalette "HP Name
/// Plate" group.
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
	[Tooltip("Plate face Image (sliced rounded sprite), child of displayRoot — sized/positioned by LayoutRoots.")]
	public Image faceImage;
	[Tooltip("Plate hard-shadow Image (sliced rounded sprite), child of displayRoot — extended down-right into the name band.")]
	public Image shadowImage;
	[Tooltip("Username TMP in the shadow band, child of displayRoot; polls PlayerIdentity/OpponentDeckCache.")]
	public TMP_Text nameLabel;
	[Tooltip("Static 'HP' label at the left of the digit row (full digit size); laid out by LayoutRoots.")]
	public TMP_Text hpLabel;

	[Header("Typography")]
	[Tooltip("Max digits render at em * maxFontScale (the slash always renders full-size).")]
	public float maxFontScale = 0.47f;
	[Tooltip("Gap between digit groups and the slash, in em.")]
	public float groupGapEm = 0f;

	[Header("Layout")]
	[Tooltip("Max digit group vertical offset from the row center, in em (positive = lower). Bottom-aligned-with-raise geometry: (raise - 0.5 + maxFontScale * 0.5) em.")]
	public float maxOffsetYEm = -0.215f;
	[Tooltip("Slash vertical offset from the row center, in em. The full-size slash shares the current digits' centerline (0).")]
	public float slashOffsetYEm = 0f;
	[Tooltip("Slash horizontal offset from its default slot between the digit groups, in em (positive = right).")]
	public float slashOffsetXEm = 0f;

	[Header("Plate (demo spec, em; 1em = current digit font size)")]
	[Tooltip("Face horizontal padding around the digit row.")]
	public float facePadXEm = 0.12f;
	[Tooltip("Face vertical padding (the 1em line box provides the air).")]
	public float facePadYEm = 0.01f;
	[Tooltip("Shadow overhang past the face's right edge (R0 light: shadow extends down-right).")]
	public float shadowExtendXEm = 0.16f;
	[Tooltip("Shadow extension below the face = the name band height.")]
	public float shadowExtendYEm = 0.41f;
	[Tooltip("Name band inset from the face's left edge.")]
	public float shadowInsetXEm = 0.11f;
	[Tooltip("Shadow top-edge inset below the face's top edge (0 = flush with the face, demo spec).")]
	public float shadowInsetYEm = 0f;
	[Tooltip("Gap between the HP label and the current digits, in em.")]
	public float labelGapEm = 0.11f;
	[Tooltip("Username font size in em (of the current digit size).")]
	public float nameScaleEm = 0.32f;
	[Tooltip("Username letter-spacing in em (of the current digit size).")]
	public float nameTrackingEm = 0.05f;
	[Tooltip("Auto-shrink floor for the username, as a fraction of nameScaleEm; beyond it TMP ellipsis takes over.")]
	[Range(0.2f, 1f)] public float nameShrinkFloor = 0.55f;
	[Tooltip("Username inset from the shadow's right edge; the demo value equals extend-x so the name's right edge lands on the face's right edge.")]
	public float nameSidePadEm = 0.16f;

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
	[Tooltip("Edit Mode only: sample username laid out in the shadow band (same shrink/ellipsis path as runtime).")]
	public string previewName = "???";

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
		public float slotOffsetY; // vertical offset keeping the visible digit line centered in its group box
	}

	private readonly CounterState _current = new CounterState();
	private readonly CounterState _max = new CounterState();
	private readonly List<DigitStrip> _currentDigitStrips = new List<DigitStrip>();
	private readonly List<DigitStrip> _maxDigitStrips = new List<DigitStrip>();

	private bool _wasVisible;
	// Last observed phase, driving re-placement on ANY phase change (see the
	// VISUAL-FIX(2026-09-19) block in Update). Initialized to Result — never the boot
	// phase — so the first Update always places (the convention the retired
	// CombatIconPresenter established).
	private EnumStorage.GamePhase _lastPhase = EnumStorage.GamePhase.Result;
	// World flight (fixes 2/3/7): owns this side's pill while the driver travels.
	private PhaseTransitionDriver.TransitionTravel _lastTravel = PhaseTransitionDriver.TransitionTravel.None;
	private HudWorldFlight _flight;
	// 2026-10-03 handoff seam: the flight completes INTO the world mirror — the canvas
	// copy eases into the mirror's shop scale over the flight's final window, then the
	// flight's onComplete swaps copies in the same frame. _handedOffToMirror keeps the
	// visibility rule from re-activating the canvas copy while the driver winds down.
	private bool _handedOffToMirror;
	private const float HandoffScaleWindow = 0.35f; // flight fraction used to ease between combat scale and the mirror's shop scale (opening window on departure, final window on return)
	private Vector2 _rootBasePos;
	// Shop-phase placement (2026-09-19): the component's own RectTransform is moved to
	// the shop top bar; the combat anchor/scale is captured at Awake and restored.
	private RectTransform _selfRt;
	private Vector2 _combatAnchoredPos;
	private Vector3 _combatScale = Vector3.one;
	private float _em = 1f; // current digit em = currentPlain.fontSize in px.
	private float _maxEm = 1f; // max digit em = _em * maxFontScale.
	private float _digitWidth = 10f;
	private float _maxDigitWidth = 8f;
	private float _slashWidth = 10f;
	private float _labelWidth; // measured 'HP' label advance at _em
	private float _stripLineSpacing;
	private float _glyphBlockEm = 1f; // font glyph block (ascent - descent) in em; digit mask slots size/offset by it
	private int _fixedDigitCount = 1;
	private int _fixedMaxDigitCount = 1;
	private string _stripText;
	private bool _stripMetricsVerified;

	private Tween _popTween;
	private Tween _rowGlideTween;
	private Tween _placementTween;
	private Tween _placementScaleTween;

	private void Awake()
	{
		if (displayRoot == null || currentRoot == null || currentPlain == null || currentStrips == null
			|| slashText == null || maxRoot == null || maxPlain == null || maxStrips == null || gamePhaseRef == null
			|| faceImage == null || shadowImage == null || nameLabel == null || hpLabel == null)
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
		_selfRt = transform as RectTransform;
		if (_selfRt != null)
		{
			_combatAnchoredPos = _selfRt.anchoredPosition;
			_combatScale = _selfRt.localScale;
		}
		_em = currentPlain.fontSize;
		_maxEm = _em * maxFontScale;
		maxPlain.fontSize = _maxEm;
		// VISUAL-FIX(2026-10-07): slash rendered small at the max group's scale, riding its raise
		//   Cause:    The parent plan specced the slash at _maxEm from the small mockup; the
		//             2026-10-07 ruling (new mockup) restores the pre-plate behavior — the
		//             separator between current and max renders at the FULL digit size.
		//   Affects:  HPNumericDisplayHorizontal (Awake + ApplyEditModePreview slash fontSize,
		//             slashOffsetYEm default), Tpl_HpNamePlate.prefab Slash child
		//   Regress:  Live read: slash fontSize == current digits' fontSize on both sides; slash
		//             centered on the digits' centerline; shop mirror shows the big slash;
		//             side-by-side with docs/demo/HPNamePlateDemo.html.
		//   Related:  plans/plan-hp-name-plate-fixes-2026-10-07.md §5.5
		// The slash renders full-size; measure AFTER the font size is set so the row math
		// uses the real glyph advance.
		slashText.fontSize = _em;
		slashText.text = "/";
		// The HP label participates in the row at full digit size.
		hpLabel.font = currentPlain.font;
		hpLabel.fontSize = _em;
		hpLabel.text = "HP";
		hpLabel.enableWordWrapping = false;
		hpLabel.overflowMode = TextOverflowModes.Overflow;
		hpLabel.color = NormalColorValue;
		_labelWidth = hpLabel.GetPreferredValues("HP").x;
		if (_labelWidth <= 0.01f)
		{
			_labelWidth = _em * 1.2f;
		}
		// The username rides the digits' bold font (CJK via its fallback chain); the
		// color comes from the plate name slot. Shrink/ellipsis handled in LayoutName.
		nameLabel.font = currentPlain.font;
		nameLabel.enableWordWrapping = false;
		TMP_FontAsset fontAsset = currentPlain.font;
		// 1em line advance: same formula and rationale as HPNumericDisplay (the
		// lineSpacing is a font-relative percentage, so one value serves both the
		// current and the smaller max strips). VerifyStripMetrics re-measures.
		_stripLineSpacing = 100f * (fontAsset.faceInfo.pointSize - fontAsset.faceInfo.lineHeight) / fontAsset.faceInfo.pointSize;
		currentPlain.lineSpacing = _stripLineSpacing;
		maxPlain.lineSpacing = _stripLineSpacing;
		// The font's glyph block (ascent - descent) is usually != 1em (RobotoCondensed-Bold: 1.172em);
		// the digit mask slots are sized/offset by it so runtime strips center like the preview (see
		// VISUAL-FIX(2026-08-15) in CreateStrip).
		_glyphBlockEm = fontAsset.faceInfo.pointSize > 0f
			? (fontAsset.faceInfo.ascentLine - fontAsset.faceInfo.descentLine) / fontAsset.faceInfo.pointSize
			: 1f;
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
		// Plate palette colors (face / hard shadow / name band) — single source,
		// same pattern as the digit ink above.
		faceImage.color = FaceColorValue;
		shadowImage.color = GameColorPalette.HpPlateShadowColor;
		nameLabel.color = NameColorValue;
		// Combat input is click-driven: no graphic of this display may intercept raycasts.
		foreach (Graphic graphic in displayRoot.GetComponentsInChildren<Graphic>(true))
		{
			graphic.raycastTarget = false;
		}
		displayRoot.gameObject.SetActive(false);
	}

	private void Update()
	{
		EnumStorage.GamePhase phase = gamePhaseRef.Value();
		bool inCombat = phase == EnumStorage.GamePhase.Combat;
		// Travel edges drive the world flight (fixes 2/3/7) BEFORE the phase placement
		// block, so the flight's combat-anchor snap wins over any same-frame placement.
		var travel = PhaseTransitionDriver.Travel;
		if (travel != _lastTravel)
		{
			OnTravelEdge(travel);
			_lastTravel = travel;
		}
		// The player side doubles as the shop top-bar HP readout (2026-09-19) and stays visible
		// through the Result phase (2026-09-21 phase transition rule, demo :27-28). 2026-09-21
		// world-scroll handoff (plan-shop-topbar-world-scroll-2026-09-21 §3.3): in a SETTLED
		// shop the canvas pill hides (the world copy in the chrome shows); during a driver
		// transition it stays visible and world-flies. 2026-10-02 re-audit fix 3: the ENEMY
		// side also stays visible through Result and while a travel world-flies it.
		// VISUAL-FIX(2026-09-21): landing in the shop left the canvas pill stacked over the new
		//   world pill, and the shop -> combat flight lost its start point
		//   Cause:    The player side was visible in every settled shop, and the old
		//             ExitVisiblePhase always restored the COMBAT anchor — so the canvas copy
		//             hovered over ShopPageHud, and when 离开商店 flipped the phase the glide
		//             started already home instead of from the shop top bar.
		//   Affects:  HPNumericDisplayHorizontal player side (shop phase + shop->combat glide).
		//   Regress:  Result -> Shop landing: canvas pill hides on the landing poll (the world
		//             pill is the only one visible); it stays parked at the SHOP anchor while
		//             hidden. 离开商店 / Space from a settled shop: canvas pill glides shop
		//             anchor -> combat anchor. Buy/sell HP utility in a settled shop: the WORLD
		//             pill counts up/down; combat odometer/shake/pop untouched. Driver bypass
		//             (config off / headless): same-frame hard cut, no glide.
		bool visible = side == Side.Player
			? inCombat || phase == EnumStorage.GamePhase.Result
				|| (phase == EnumStorage.GamePhase.Shop && PhaseTransitionDriver.IsTransitioning && !_handedOffToMirror)
			: (inCombat && !PhaseTransitionDriver.SuppressCombatCanvasUI)
				|| phase == EnumStorage.GamePhase.Result
				|| travel != PhaseTransitionDriver.TransitionTravel.None;
		// VISUAL-FIX(2026-09-19): Leaving the shop for combat left the player pill at the shop
		//   Cause:    Placement was applied only from EnterVisiblePhase, which fires on an
		//             invisible->visible edge. The player side is visible in BOTH Shop and
		//             Combat, so _wasVisible stayed true across the switch and the display kept
		//             the anchor/scale of the phase it entered from (combat->shop was masked by
		//             the Result phase in between, which does cross an edge).
		//   Affects:  HPNumericDisplayHorizontal player side (shop top-bar reuse).
		//   Regress:  Enter Shop, leave to Combat: the pill must return to (290, 89) / scale 0.8
		//             bottom-left while the enemy pill sits at (-290, -89) / 0.8 top-right.
		//             Result -> Shop must still place it at the ShopTopBarLayout anchor.
		//             (Updated 2026-09-21: the PLAYER side now STAYS visible through Result —
		//             phase-transition rule, demo PhaseTransitionDemo.html:27-28 — so Result no
		//             longer hides it; the enemy side still hides outside Combat. Since the
		//             2026-09-21 world-scroll handoff it hides in a SETTLED shop instead — see
		//             the VISUAL-FIX(2026-09-21) block in Update.)
		if (phase != _lastPhase)
		{
			_lastPhase = phase;
			ApplyPhasePlacement(side == Side.Player && phase == EnumStorage.GamePhase.Shop);
		}
		if (visible && !_wasVisible)
		{
			EnterVisiblePhase(!inCombat);
		}
		else if (!visible && _wasVisible)
		{
			ExitVisiblePhase();
		}
		_wasVisible = visible;
		if (!visible)
		{
			return;
		}

		// Username poll: the player side in every visible phase; the enemy side only
		// in Combat (the same gating the retired CombatIconPresenter used).
		if (side == Side.Player || inCombat)
		{
			RefreshNameLabel();
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

	// Silent sync to the current displayed values on entering a visible phase: no
	// tweens, no effects, so the first frame never plays a phantom damage/heal.
	private void EnterVisiblePhase(bool shopPhase)
	{
		ApplyPhasePlacement(shopPhase);
		CleanupVisuals();
		int hp = GetDisplayedHp();
		int hpMax = GetLiveMaxHp();
		_fixedDigitCount = DigitCount(hp);
		_fixedMaxDigitCount = DigitCount(hpMax);
		LayoutRoots();
		RefreshNameLabel();
		SetCounterInstant(_current, true, hp);
		SetCounterInstant(_max, false, hpMax);
		displayRoot.gameObject.SetActive(true);
		// Enemy HUD counter-direction entrance: driven by the driver's Travel edge
		// (OnTravelEdge — full-transDur world slide, fixes 2/3); no anchored variant here.
	}

	private void ExitVisiblePhase()
	{
		CleanupVisuals();
		KillTween(ref _placementTween);
		KillTween(ref _placementScaleTween);
		displayRoot.gameObject.SetActive(false);
		// 2026-09-21 world-scroll handoff: hiding in a SETTLED shop parks the pill at the SHOP
		// anchor (not the combat anchor), so the next 离开商店 trigger finds the shared-element
		// flight's start point at the shop top bar. Any other exit (combat end) keeps the
		// combat-anchor restore, so the next combat entry finds the transform untouched even
		// if a phase is skipped.
		bool parkAtShop = gamePhaseRef.Value() == EnumStorage.GamePhase.Shop;
		if (_selfRt != null)
		{
			_selfRt.anchoredPosition = parkAtShop
				? ShopTopBarLayout.ShopAnchorHpDisplay(canvas)
				: _combatAnchoredPos;
			_selfRt.localScale = parkAtShop ? Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay : _combatScale;
		}
	}

	private void ApplyPhasePlacement(bool shopPhase)
	{
		if (_selfRt == null)
		{
			return;
		}
		// While a travel owns the pill (world flight, fixes 2/3/7) the anchored placement
		// stands down — the flight writes .position and anchoredPosition tracks it.
		if (PhaseTransitionDriver.Travel != PhaseTransitionDriver.TransitionTravel.None
			&& _flight != null && _flight.Active)
		{
			return;
		}
		// Selection dwell (plan-opponent-select-page-2026-10-06 §4.4): the page is alive and
		// the pill must HOLD its select-page world spot — the shop-phase placement below
		// would snap it back to the shop top bar anchor.
		if (OpponentSelectPage.Instance != null)
		{
			return;
		}
		Vector2 targetPos = shopPhase
			? ShopTopBarLayout.ShopAnchorHpDisplay(canvas)
			: _combatAnchoredPos;
		Vector3 targetScale = shopPhase ? Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay : _combatScale;
		var cfg = PhaseTransitionConfigSO.Me;
		if (PhaseTransitionDriver.IsTransitioning && cfg != null)
		{
			// Fallback glide (no chrome mirror -> no world flight, headless corner).
			KillTween(ref _placementTween);
			KillTween(ref _placementScaleTween);
			_placementTween = cfg.ApplyEase(_selfRt.DOAnchorPos(targetPos, cfg.transDur).SetUpdate(UpdateType.Normal, true));
			_placementScaleTween = cfg.ApplyEase(_selfRt.DOScale(targetScale, cfg.transDur).SetUpdate(UpdateType.Normal, true));
			return;
		}
		KillTween(ref _placementTween);
		KillTween(ref _placementScaleTween);
		_selfRt.anchoredPosition = targetPos;
		_selfRt.localScale = targetScale;
	}

	/// <summary>
	/// Travel edges drive the world flight (fixes 2/3/7): the player pill detaches from the
	/// shop top bar (world mirror spot) and flies to its combat anchor and back; the enemy
	/// pill slides in from / out to enemySlide above its combat home, full transDur, together
	/// with the camera travel. Without the chrome mirror (headless corner) no flight starts
	/// and the legacy anchored placement/glide applies.
	/// </summary>
	private void OnTravelEdge(PhaseTransitionDriver.TransitionTravel travel)
	{
		var cfg = PhaseTransitionConfigSO.Me;
		if (cfg == null || _selfRt == null) return;
		// Player shop home = the built page's HpPill world center (single placement source,
		// plan-shop-mirror-prefab §3.4); no mirror -> no flight (fallback glide handles it).
		// Pre-declared: the && short-circuit leaves the out unassigned on the enemy side.
		Vector3 shopHome = Vector3.zero;
		bool mirrorOk = side == Side.Player && ShopChrome.TryGetNamePlateWorldCenter(out shopHome);
		switch (travel)
		{
			case PhaseTransitionDriver.TransitionTravel.ToSelection:
			{
				// Selection leg1 (plan-opponent-select-page-2026-10-06 §4.4): the player plate
				// hands off to its combat home one page EARLY (LandingPageY = the selection
				// page) — identical mechanics to the ToCombat flight, one page lower. The
				// enemy side does NOTHING: no enemy combat HUD exists on the selection page
				// (the candidates carry their own plates); the enemy pill enters on the leg2
				// ToCombat edge.
				if (side != Side.Player) break;
				if (!mirrorOk) return;
				_handedOffToMirror = false;
				Vector3 selectionHome = PhaseFlightPlanner.HudHomeAtPage(SnapToCombatAnchor(), PhaseTransitionDriver.LandingPageY, CanvasPlaneY);
				_selfRt.localScale = Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay;
				KillTween(ref _placementScaleTween);
				_placementScaleTween = _selfRt.DOScale(_combatScale, cfg.transDur * HandoffScaleWindow)
					.SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
				_flight = EnsureFlight();
				_flight.Begin(shopHome, selectionHome, cfg.transDur, cfg);
				ShopChrome.SetMirrorsActive(false);
				break;
			}
			case PhaseTransitionDriver.TransitionTravel.ToCombat:
			{
				if (side == Side.Player)
				{
					// Selection leg2 (plan §4.4): the plate already sits at its select-page
					// home and RIDES the rig (the canvas plane travels with the camera) — a
					// second mirror flight would teleport it back to the hidden shop spot.
					if (OpponentSelectPage.Instance != null) break;
					// Mirror check BEFORE the snap: the fallback glide needs the parked shop anchor intact.
					if (!mirrorOk) return;
					_handedOffToMirror = false;
					Vector3 to = PhaseFlightPlanner.HudHomeAtPage(SnapToCombatAnchor(), PhaseTransitionDriver.LandingPageY, CanvasPlaneY);
					// VISUAL-FIX(2026-10-03): pill popped 0.41 -> 0.8 at travel start
					//   Cause:    SnapToCombatAnchor wrote the combat scale instantly while the
					//             replaced mirror renders at ShopMirrorScale.canvasShopScale.
					//   Affects:  HPNumericDisplayHorizontal.OnTravelEdge (player, both travels)
					//   Regress:  离开商店: the pill starts at the mirror's size and grows into
					//             the combat scale over the opening window — no size pop at the
					//             band; Result->Shop: symmetric shrink into the band.
					//   Related:  docs/PhaseTransition.md (HUD world flights), ShopMirrorScale
					_selfRt.localScale = Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay;
					KillTween(ref _placementScaleTween);
					_placementScaleTween = _selfRt.DOScale(_combatScale, cfg.transDur * HandoffScaleWindow)
						.SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					_flight = EnsureFlight();
					_flight.Begin(shopHome, to, cfg.transDur, cfg);
					// Presenter-driven mirror hide (replaces the driver's synchronous pre-phase
					// hide): the canvas copy is already active and world-locked at the mirror
					// spot, so the swap has no blank frame regardless of script execution order.
					ShopChrome.SetMirrorsActive(false);
				}
				else
				{
					// Entrance (fix 2): slide DOWN in from enemySlide above; the world position
					// keeps the pill outside the viewport until the camera's arrival, like the demo.
					Vector3 to = PhaseFlightPlanner.HudHomeAtPage(SnapToCombatAnchor(), PhaseTransitionDriver.LandingPageY, CanvasPlaneY);
					_selfRt.localScale = _combatScale; // was SnapToCombatAnchor's job before 2026-10-03
					_flight = EnsureFlight();
					_flight.Begin(to + Vector3.up * SlideWorld(cfg), to, cfg.transDur, cfg);
				}
				break;
			}
			case PhaseTransitionDriver.TransitionTravel.ToShop:
			{
				if (side == Side.Player)
				{
					if (!mirrorOk) return;
					_handedOffToMirror = false;
					Vector3 from = PhaseFlightPlanner.HudHomeAtPage(_selfRt.position, PhaseTransitionDriver.LandingPageY, CanvasPlaneY);
					// Shrink into the mirror's shop scale over the final window so the swap
					// below lands size-matched (the old landing swap popped 0.8 -> 0.41).
					_selfRt.localScale = _combatScale;
					KillTween(ref _placementScaleTween);
					_placementScaleTween = _selfRt.DOScale(Vector3.one * ShopTopBarLayout.ShopScaleHpDisplay, cfg.transDur * HandoffScaleWindow)
						.SetDelay(cfg.transDur * (1f - HandoffScaleWindow)).SetEase(Ease.OutQuad).SetUpdate(UpdateType.Normal, true);
					_flight = EnsureFlight();
					var captured = this;
					_flight.Begin(from, shopHome, cfg.transDur, cfg, () =>
					{
						// Same-frame swap at flight end: the mirror shows, the canvas copy
						// hides, and the settled-shop rule must not re-activate it while the
						// driver winds down (its own SetMirrorsActive(true) stays as a
						// defensive idempotent call).
						ShopChrome.SetMirrorsActive(true);
						captured._handedOffToMirror = true;
						if (captured.displayRoot != null) captured.displayRoot.gameObject.SetActive(false);
					});
				}
				else
				{
					// Exit (fix 3): slides UP out while the camera descends (demo :727 -enemySlide).
					Vector3 home = PhaseFlightPlanner.HudHomeAtPage(_selfRt.position, PhaseTransitionDriver.LandingPageY, CanvasPlaneY);
					_flight = EnsureFlight();
					_flight.Begin(home, home + Vector3.up * SlideWorld(cfg), cfg.transDur, cfg);
				}
				break;
			}
			default:
				_handedOffToMirror = false;
				if (_flight != null) _flight.Kill();
				break;
		}
	}

	private HudWorldFlight EnsureFlight()
	{
		if (_flight == null) _flight = new HudWorldFlight(_selfRt, canvas);
		return _flight;
	}

	/// <summary>Snaps the pill to its combat anchor POSITION (the home math needs it) and returns its world position. Scale is no longer written here (2026-10-03 handoff seam): the flight endpoints own scale — player flights ease between the mirror's shop scale and combat scale, enemy flights set combat scale explicitly at their call sites. Runs for BOTH sides: a shop-park can have left the transform at the shop anchor.</summary>
	private Vector3 SnapToCombatAnchor()
	{
		KillTween(ref _placementTween);
		KillTween(ref _placementScaleTween);
		_selfRt.anchoredPosition = _combatAnchoredPos;
		return _selfRt.position;
	}

	/// <summary>
	/// The carrying canvas root's current world Y — the flight-home basis (HudHomeAtPage).
	/// NOT the camera rig: the canvas root lags the rig within the travel-start frame
	/// (VISUAL-FIX(2026-10-03), ex-CombatIconPresenter); the rig Y remains only as the
	/// no-canvas fallback.
	/// </summary>
	private float CanvasPlaneY => canvas != null ? canvas.transform.position.y : HudWorldFlight.RigY;

	/// <summary>Enemy HUD slide distance in world units (demo enemySlide 140 px on the 740 px page).</summary>
	private static float SlideWorld(PhaseTransitionConfigSO cfg)
	{
		return PhaseFlightPlanner.PxToWorld(cfg.enemySlideDemoPx, PhaseFlightPlanner.PageHeightWorld(ShopTopBarLayout.MainOrthoSize));
	}

	private void LateUpdate()
	{
		// World-flight re-projection runs after every Update placement write, so the rendered
		// pose is the world-locked one.
		if (_flight != null) _flight.Tick();
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

	// Row: [HP label] gap [current digits] gap [/] gap [max digits], centered on the
	// displayRoot pivot (0.5, 0.5). Each digit group is a top-pivot box whose single
	// visible strip line lands on the row center (group top sits at +lineHeight/2).
	// The plate (face Image + extended shadow + name band) is laid out around the
	// same row by LayoutPlate, so the whole element shares one geometry source.
	private void LayoutRoots()
	{
		float gap = _em * groupGapEm;
		float labelGap = _em * labelGapEm;
		float width = _fixedDigitCount * _digitWidth;
		float maxWidth = _fixedMaxDigitCount * _maxDigitWidth;
		float total = _labelWidth + labelGap + width + gap + _slashWidth + gap + maxWidth;
		displayRoot.sizeDelta = new Vector2(total, _em);
		float x = -total * 0.5f;
		if (hpLabel != null)
		{
			hpLabel.rectTransform.sizeDelta = new Vector2(_labelWidth, _em);
			hpLabel.rectTransform.anchoredPosition = new Vector2(x + _labelWidth * 0.5f, 0f);
		}
		float rowX = x + _labelWidth + labelGap;
		currentRoot.sizeDelta = new Vector2(width, _em);
		currentRoot.anchoredPosition = new Vector2(rowX, _em * 0.5f);
		float slashX = rowX + width + gap;
		slashText.rectTransform.sizeDelta = new Vector2(_slashWidth, _em);
		slashText.rectTransform.anchoredPosition = new Vector2(slashX + _slashWidth * 0.5f + slashOffsetXEm * _em, slashOffsetYEm * _em);
		maxRoot.sizeDelta = new Vector2(maxWidth, _maxEm);
		maxRoot.anchoredPosition = new Vector2(slashX + _slashWidth + gap, _maxEm * 0.5f + maxOffsetYEm * _em);
		LayoutPlate(total);
	}

	// The plate silhouette around the digit row (demo HPNamePlateDemo.html §3.1):
	// a face Image padded around the row, and the hard-shadow Image extended
	// down-right past it — right overhang (extend-x) and bottom name band
	// (extend-y) with the band inset from the face's left edge; shadowInsetYEm
	// additionally drops the slab's top edge below the face's top (0 = demo's
	// flush top). The name label is right-aligned inside the band; band top ==
	// face bottom by construction.
	// All rects are center-anchored children of displayRoot ((0.5,0.5)/(0.5,0.5))
	// except the name's (1,0.5) pivot, which pins its right edge.
	private void LayoutPlate(float totalRowWidth)
	{
		if (faceImage == null || shadowImage == null)
		{
			return; // pre-plate scene state (Awake would have disabled us anyway)
		}
		float padX = _em * facePadXEm;
		float padY = _em * facePadYEm;
		float extX = _em * shadowExtendXEm;
		float extY = _em * shadowExtendYEm;
		float inset = _em * shadowInsetXEm;
		float insetY = _em * shadowInsetYEm;
		float faceW = totalRowWidth + 2f * padX;
		float faceH = _em + 2f * padY;
		RectTransform faceRt = faceImage.rectTransform;
		faceRt.sizeDelta = new Vector2(faceW, faceH);
		faceRt.anchoredPosition = Vector2.zero;
		RectTransform shadowRt = shadowImage.rectTransform;
		shadowRt.sizeDelta = new Vector2(faceW - inset + extX, faceH - insetY + extY);
		// VISUAL-FIX(2026-10-07): name band flush with the face's left edge, right overhang ~4x too thin
		//   Cause:    The slab center formula wrote (extX - inset) — the inset sign was flipped, so the
		//             slab spanned [faceLeft, faceRight + (extX - inset)] instead of the demo spec
		//             [faceLeft + inset, faceRight + extX].
		//   Affects:  HPNumericDisplayHorizontal.LayoutPlate (canvas plate + Edit Mode preview)
		//   Regress:  Live rect read: shadow slab edges == [faceLeft + inset, faceRight + extX] on
		//             both sides; name band top == face bottom unchanged; side-by-side with
		//             docs/demo/HPNamePlateDemo.html §3.1.
		//   Related:  plans/plan-hp-name-plate-fixes-2026-10-07.md §3
		shadowRt.anchoredPosition = new Vector2((extX + inset) * 0.5f, -(insetY + extY) * 0.5f);
		if (nameLabel != null)
		{
			RectTransform nameRt = nameLabel.rectTransform;
			// Right edge = shadow right edge - name side pad; with nameSidePadEm ==
			// shadowExtendXEm (demo) that lands exactly on the face's right edge.
			float nameRight = faceW * 0.5f + extX - _em * nameSidePadEm;
			float bandW = nameRight - (-faceW * 0.5f + inset);
			nameRt.sizeDelta = new Vector2(bandW, extY);
			nameRt.anchoredPosition = new Vector2(nameRight, -(faceH * 0.5f + extY * 0.5f));
			LayoutName(nameLabel.text);
		}
	}

	// Username layout: measure at the demo scale, auto-shrink toward the floor
	// (nameShrinkFloor of nameScaleEm) while it overflows the band, then hand the
	// remainder to TMP ellipsis. Tracking is em-of-current-digit-size in the demo,
	// while TMP characterSpacing is 1/100 em of the text's own font size — convert.
	private void LayoutName(string value)
	{
		if (nameLabel == null || string.IsNullOrEmpty(value))
		{
			return;
		}
		float baseSize = _em * nameScaleEm;
		nameLabel.text = value;
		nameLabel.overflowMode = TextOverflowModes.Overflow;
		nameLabel.fontSize = baseSize;
		nameLabel.characterSpacing = nameTrackingEm * _em / baseSize * 100f;
		float boxW = nameLabel.rectTransform.sizeDelta.x;
		if (boxW <= 0.01f)
		{
			return;
		}
		float w = nameLabel.GetPreferredValues(value).x;
		if (w > boxW)
		{
			nameLabel.fontSize = Mathf.Max(baseSize * nameShrinkFloor, baseSize * (boxW / w));
			nameLabel.characterSpacing = nameTrackingEm * _em / nameLabel.fontSize * 100f;
			w = nameLabel.GetPreferredValues(value).x;
		}
		nameLabel.overflowMode = w > boxW ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
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

	// Per-side plate colors ("HP Name Plate" group): face fill and username ink.
	private Color FaceColorValue => side == Side.Player ? GameColorPalette.HpPlateFacePlayerColor : GameColorPalette.HpPlateFaceEnemyColor;
	private Color NameColorValue => side == Side.Player ? GameColorPalette.HpPlateNamePlayerColor : GameColorPalette.HpPlateNameEnemyColor;

	// Name sources — the same poll contract the retired CombatIconPresenter used:
	// diff-guarded per frame; the enemy name can arrive shortly after the phase
	// switch (ghost deck injection), so polling beats a one-shot entry write.
	private const string UnknownName = "???";
	private string _lastName;

	private string ResolveNameValue()
	{
		if (side == Side.Player)
		{
			string playerName = PlayerIdentity.Username;
			return string.IsNullOrEmpty(playerName) ? UnknownName : playerName;
		}
		string enemyName = OpponentDeckCache.Current != null ? OpponentDeckCache.Current.username : null;
		return string.IsNullOrEmpty(enemyName) ? UnknownName : enemyName;
	}

	/// <summary>Diff-guarded username poll; writes text, colors and the shrink/ellipsis layout on change.</summary>
	private void RefreshNameLabel()
	{
		if (nameLabel == null)
		{
			return;
		}
		string value = ResolveNameValue();
		if (_lastName == value)
		{
			return;
		}
		_lastName = value;
		nameLabel.color = NameColorValue;
		LayoutName(value);
	}

	private int GetDisplayedHp()
	{
		if (CombatManager.Me == null)
		{
			return 0;
		}
		PlayerStatusSO status = side == Side.Player ? CombatManager.Me.ownerPlayerStatusRef : CombatManager.Me.enemyPlayerStatusRef;
		if (status == null)
		{
			return 0;
		}
		// Outside combat the display queue is frozen at the last combat's values; the
		// shop readout shows the live status HP (shop phase = full-HP invariant).
		if (gamePhaseRef.Value() != EnumStorage.GamePhase.Combat)
		{
			return status.hp;
		}
		if (CombatInfoDisplayer.me == null)
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
		// VISUAL-FIX(2026-08-15): Runtime digit strips render ~11px lower than the Edit Mode plain-text preview
		//   Cause:     The mask slot was 1em tall with the strip line top-aligned to it, but the font's glyph
		//              block (ascent - descent, 1.172em for RobotoCondensed-Bold) is taller than 1em, so TMP
		//              Center alignment (preview) and the top-anchored strip line land the digits
		//              (glyphBlockEm - 1) * lineHeight / 2 apart vertically.
		//   Affects:   HPNumericDisplayHorizontal digit strips (current + max groups)
		//   Regress:   Edit Mode preview (previewHp=12/20) vs Play Mode combat digits: vertical centers must
		//              match; digit-count growth (99->100) and hit shake/landing pop must be unchanged.
		float blockHeight = lineHeight * _glyphBlockEm;
		var slotGo = new GameObject("Digit", typeof(RectTransform));
		var slotRt = (RectTransform)slotGo.transform;
		slotRt.SetParent(container, false);
		slotRt.anchorMin = new Vector2(0.5f, 1f);
		slotRt.anchorMax = new Vector2(0.5f, 1f);
		slotRt.pivot = new Vector2(0.5f, 1f);
		slotRt.sizeDelta = new Vector2(digitWidth, blockHeight);
		slotRt.anchoredPosition = new Vector2(0f, (blockHeight - lineHeight) * 0.5f);
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
		var entry = new DigitStrip { slot = slotRt, strip = stripRt, text = tmp, idx = Canonical(0), digitWidth = digitWidth, lineHeight = lineHeight, slotOffsetY = (blockHeight - lineHeight) * 0.5f };
		SetStripY(entry, entry.idx);
		return entry;
	}

	private void RepositionStrips(List<DigitStrip> strips)
	{
		for (int i = 0; i < strips.Count; i++)
		{
			float x = (i - (strips.Count - 1) * 0.5f) * strips[i].digitWidth;
			strips[i].slot.anchoredPosition = new Vector2(x, strips[i].slotOffsetY);
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
			float block = measured * _glyphBlockEm;
			entry.slotOffsetY = (block - measured) * 0.5f;
			entry.slot.sizeDelta = new Vector2(entry.digitWidth, block);
			entry.slot.anchoredPosition = new Vector2(0f, entry.slotOffsetY);
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
		// The plate silhouette grows with the row: capture face/shadow widths so they
		// glide to the new size in the same window instead of snapping with LayoutRoots.
		Vector2 faceSize = faceImage != null ? faceImage.rectTransform.sizeDelta : Vector2.zero;
		Vector2 shadowSize = shadowImage != null ? shadowImage.rectTransform.sizeDelta : Vector2.zero;
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
		Sequence glide = DOTween.Sequence()
			.Append(currentRoot.DOAnchorPosX(newCurX, dividerGlideDuration).SetEase(Ease.OutQuad))
			.Join(slashText.rectTransform.DOAnchorPosX(newSlashX, dividerGlideDuration).SetEase(Ease.OutQuad))
			.Join(maxRoot.DOAnchorPosX(newMaxX, dividerGlideDuration).SetEase(Ease.OutQuad));
		if (faceImage != null)
		{
			Vector2 newFaceSize = faceImage.rectTransform.sizeDelta;
			faceImage.rectTransform.sizeDelta = faceSize;
			glide.Join(DOTween.To(() => faceImage.rectTransform.sizeDelta, v => faceImage.rectTransform.sizeDelta = v, newFaceSize, dividerGlideDuration).SetEase(Ease.OutQuad));
		}
		if (shadowImage != null)
		{
			Vector2 newShadowSize = shadowImage.rectTransform.sizeDelta;
			shadowImage.rectTransform.sizeDelta = shadowSize;
			glide.Join(DOTween.To(() => shadowImage.rectTransform.sizeDelta, v => shadowImage.rectTransform.sizeDelta = v, newShadowSize, dividerGlideDuration).SetEase(Ease.OutQuad));
		}
		_rowGlideTween = ApplySpeed(glide);
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
		// Plate: the slash and the HP label render at the full digit size (2026-10-07 ruling).
		slashText.fontSize = _em;
		slashText.text = "/";
		if (hpLabel != null)
		{
			hpLabel.font = currentPlain.font;
			hpLabel.fontSize = _em;
			hpLabel.text = "HP";
			hpLabel.enableWordWrapping = false;
			hpLabel.overflowMode = TextOverflowModes.Overflow;
			_labelWidth = hpLabel.GetPreferredValues("HP").x;
			if (_labelWidth <= 0.01f)
			{
				_labelWidth = _em * 1.2f;
			}
		}
		else
		{
			_labelWidth = 0f;
		}
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
			_slashWidth = _maxEm * 0.5f;
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
		if (hpLabel != null) hpLabel.color = NormalColorValue;
		if (faceImage != null) faceImage.color = FaceColorValue;
		if (shadowImage != null) shadowImage.color = GameColorPalette.HpPlateShadowColor;
		if (nameLabel != null)
		{
			nameLabel.color = NameColorValue;
			LayoutName(string.IsNullOrEmpty(previewName) ? "???" : previewName);
		}
	}
#endif
}
