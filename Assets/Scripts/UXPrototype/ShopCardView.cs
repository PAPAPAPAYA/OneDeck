using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CardPhysObjScript))]
public class ShopCardView : MonoBehaviour
{
	private CardPhysObjScript _cardPhysObj;

	// Card enlarge related
	private Vector3 _originalPosition;
	private Vector3 _originalScale;
	private bool _isEnlarged = false;
	private bool _pressActive = false;
	private float _enlargeCooldown = 0f;
	private const float ENLARGE_COOLDOWN_TIME = 0.5f;

	// Card hover lift related (plan-shop-card-hover-lift-2026-10-01): shop-page cards
	// (shelf / deck band / upgrades row) lift diagonally toward the top-left light (the
	// PhysButton.HoverVector convention). The lift rides CardPhysObjScript's
	// TargetPosition system; _hoverBase is the REST pose captured at arm time
	// (TargetPosition carries the lift while hovered), and the retention test is anchored
	// on the rest bounds so the lift displacement can never flip the cursor test (combat
	// hover's flicker-loop lesson — no OnMouseExit).
	private bool _isHoverLifted = false;
	private Vector3 _hoverBase;
	private Bounds _hoverRestBounds;
	private Collider2D _hoverCollider;
	private Camera _hoverCamera;

	// Grounded-element pinning (完全分离, 2026-10-01): while the face is off its rest spot
	// for a face-only reason, grounded elements stay world-pinned at their rest spots (the
	// button's own "hit target never moves" invariant, extended to the card). The price
	// button pins through BOTH the hover lift AND the enlarge preview — the tag is a table
	// element: the face lifts off it, and the enlarged preview flies away from it (position
	// AND scale counter-held, so the tag never grows with the enlarged card); the big shadow
	// pins through the hover lift only — the enlarge flight carries it. Purchase / sell
	// reattach everything first so the pinned elements ride those flights.
	private bool _pinButtonActive = false;
	private Transform _pinButtonTr;
	private Vector3 _pinButtonRestLocal;
	private Vector3 _pinButtonRestLocalScale = Vector3.one;
	private Vector3 _pinButtonRestWorld;
	private Vector3 _pinButtonRestWorldScale = Vector3.one;
	private bool _pinShadowActive = false;
	private Transform _pinShadowTr;
	private Vector3 _pinShadowRestLocal;
	private Vector3 _pinShadowRestWorld;

	// Price button (UI kit §04: buy/sell = single click on the price button; long-press
	// removed 2026-09-16). Built lazily on the first shop-phase price display.
	private PhysButton _priceButton;
	private string _lastPriceText;
	private Action _buyAction;
	private Action _sellAction;
	private DeckSizeIncreaseEffect _deckSizeEffect; // cached pricing probe, resolved once per card instead of per frame
	private bool _deckSizeEffectResolved;
	private UpgradeCapIncreaseEffect _upgradeCapEffect; // second meter family (upgrade-slot meter), same once-per-card caching
	private bool _upgradeCapEffectResolved;

	private const float PRICE_FACE_PAD_X = 0.22f;
	private const float PRICE_FACE_PAD_Y = 0.14f;
	// Price button root z (the old CardPrice print's z): layering only, code-owned.
	private const float PRICE_BUTTON_Z = -0.02f;
	// Former CardPrice print position in PhysicalCard.prefab (node removed 2026-10-01);
	// only a fallback for when ShopUXManager is absent — its priceButtonPosition default
	// matches this value.
	private static readonly Vector2 DefaultPriceButtonPos = new Vector2(-0.27f, -3.51f);

	void OnEnable()
	{
		_cardPhysObj = GetComponent<CardPhysObjScript>();
		_buyAction = TryPurchase;
		_sellAction = TrySell;
	}

	void Update()
	{
		UpdatePriceDisplay();
		HandleClickToRestore();
		UpdateHoverLift();
		UpdateLiftPins();

		if (_enlargeCooldown > 0f)
		{
			_enlargeCooldown -= Time.deltaTime;
		}
	}

	#region Shop Display

	[Tooltip("Hide the price print (set for stacked duplicate copies; only the base card of a stack shows price)")]
	public bool suppressPriceDisplay = false;

	/// <summary>
	/// Update price display, only shown in Shop Phase.
	/// </summary>
	private void UpdatePriceDisplay()
	{
		// Start cards / face-less prefabs never build a FlipRoot — no price button there.
		if (_cardPhysObj.FlipRoot == null) return;

		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		bool shopPhase = phaseRef != null && phaseRef.Value() == EnumStorage.GamePhase.Shop;
		// plan-shop-reroll-flip-2026-09-30: face-down cards (reroll flip reveal) hide the
		// whole price block. The price PhysButton lives under FlipRoot too (EnsurePriceButton),
		// so it must not show during any flip phase.
		// VISUAL-FIX(2026-09-30): the buy/price button squashed along with every reroll flip
		//   Cause:    EnsurePriceButton parents the button under printT.parent == FlipRoot
		//             (prefab and legacy branches alike), so the flip scaleX squash applied
		//             to it; and the isFaceUp guard flips state at the TWEEN START, so the
		//             button popped back over the card back during the first half of the
		//             face-up flip.
		//   Affects:  ShopCardView.UpdatePriceDisplay (visibility only; parenting unchanged)
		//   Regress:  Reroll: the price button must never be visible mid-flip and must be
		//             back on every card once the flip-up lands. Resting phases unchanged.
		//   Related:  CardPhysObjScript.isFlipPlaying, plan-shop-reroll-flip-2026-09-30
		bool showPrice = shopPhase && _cardPhysObj.cardImRepresenting != null && !suppressPriceDisplay
			&& _cardPhysObj.isFaceUp && !_cardPhysObj.isFlipPlaying;
		if (!showPrice)
		{
			if (_priceButton != null) _priceButton.gameObject.SetActive(false);
			// The dim face must never leak out of the unaffordable-shop-item state.
			_cardPhysObj.SetFaceDimmed(false);
			return;
		}

		PhysButton priceButton = EnsurePriceButton();
		if (priceButton == null) return;
		priceButton.gameObject.SetActive(true);
		// Live tuning: re-assert the authored placement on every price display pass, so
		// Inspector edits to ShopUXManager.priceButtonPosition apply on the next frame.
		ApplyPriceButtonPlacement(_priceButton.transform);

		int basePrice = ShopManager.me != null ? ResolveBasePrice() : 0;
		bool isShopItem = _cardPhysObj.shopItemIndex >= 0;
		int discountOff = isShopItem && ShopManager.me != null ? ShopManager.me.GetBoardDiscount(_cardPhysObj.cardImRepresenting) : 0;
		int displayPrice = isShopItem ? Mathf.Max(0, basePrice - discountOff) : basePrice / 2;

		bool affordable = !isShopItem
			|| (ShopManager.me != null && ShopManager.me.purse != null
				&& ShopManager.me.purse.value >= ShopManager.me.GetEffectiveBuyPrice(_cardPhysObj.cardImRepresenting));
		priceButton.SetDisabled(!affordable);
		_cardPhysObj.SetFaceDimmed(!affordable);
		priceButton.SetWorldAction(isShopItem ? _buyAction : _sellAction);
		priceButton.hoverText = isShopItem ? "买入" : "售出";

		if (discountOff > 0)
		{
			// Discounted board offer: struck-through base price + the reduced one. The <s> markup
			// is kept as metadata for PriceStrikeLine, which overlays the crisp diagonal line
			// (TMP's built-in strike line can never render opaque on this font, 2026-09-11).
			// The strike overlays the price text, so it hides while the hover text swap is up.
			string priceText = "<s>$" + basePrice + "</s> $" + displayPrice;
			SetPriceText(priceText);
			EnsureStrikeLine().SetVisible(!priceButton.IsPointerOver);
		}
		else
		{
			SetPriceText("$" + displayPrice);
			if (_strikeLine != null)
			{
				_strikeLine.SetVisible(false);
			}
		}
	}

	/// <summary>GetCardPrice probes for DeckSizeIncreaseEffect; this card's result never changes, so resolve once.</summary>
	private DeckSizeIncreaseEffect ResolveDeckSizeEffect()
	{
		if (!_deckSizeEffectResolved)
		{
			_deckSizeEffectResolved = true;
			_deckSizeEffect = _cardPhysObj.cardImRepresenting != null
				? _cardPhysObj.cardImRepresenting.GetComponentInChildren<DeckSizeIncreaseEffect>(true)
				: null;
		}
		return _deckSizeEffect;
	}

	/// <summary>Same once-per-card caching for the upgrade-slot meter family.</summary>
	private UpgradeCapIncreaseEffect ResolveUpgradeCapEffect()
	{
		if (!_upgradeCapEffectResolved)
		{
			_upgradeCapEffectResolved = true;
			_upgradeCapEffect = _cardPhysObj.cardImRepresenting != null
				? _cardPhysObj.cardImRepresenting.GetComponentInChildren<UpgradeCapIncreaseEffect>(true)
				: null;
		}
		return _upgradeCapEffect;
	}

	/// <summary>
	/// Base price of the represented card: deck-slot meter price, then upgrade-slot meter
	/// price, then the rarity table. Mirrors ShopManager.GetCardPrice's single-arg priority
	/// while keeping the once-per-card probe caching.
	/// </summary>
	private int ResolveBasePrice()
	{
		var slotEffect = ResolveDeckSizeEffect();
		if (slotEffect != null) return ShopManager.me.GetCardPrice(_cardPhysObj.cardImRepresenting, slotEffect);
		var capEffect = ResolveUpgradeCapEffect();
		if (capEffect != null) return ShopManager.me.GetUpgradeCapMeterPrice(capEffect);
		return ShopManager.me.GetCardPrice(_cardPhysObj.cardImRepresenting, null);
	}

	/// <summary>
	/// Write the rest price text unless the pointer is on the price button (the hover
	/// text swap owns the label while hovered); rebuild the button face on change.
	/// </summary>
	private void SetPriceText(string priceText)
	{
		if (_priceButton != null && _priceButton.IsPointerOver) return;

		// The label is the PriceButton.prefab's baked one (2026-09-30 label port), arriving
		// as PhysButton.label. Also compare the live label: the hover text swap rewrote it,
		// so the cached price string alone must not short-circuit the restore.
		if (_priceButton == null) return;
		TMP_Text label = _priceButton.label;
		if (label == null) return;
		if (priceText == _lastPriceText && label.text == priceText) return;
		_lastPriceText = priceText;
		label.text = priceText;
		label.ForceMeshUpdate(false, false);
		ResizePriceButtonFace();
	}

	private PriceStrikeLine _strikeLine;

	/// <summary>
	/// Find the strikethrough line: baked under the prefab label (2026-09-30 label port),
	/// lazily attached under it on first use for prefabs predating the bake.
	/// </summary>
	private PriceStrikeLine EnsureStrikeLine()
	{
		if (_strikeLine == null && _priceButton != null && _priceButton.label != null)
		{
			_strikeLine = _priceButton.label.GetComponentInChildren<PriceStrikeLine>(true);
			if (_strikeLine == null)
			{
				var go = new GameObject("PriceStrikeLine");
				go.transform.SetParent(_priceButton.label.transform, false);
				_strikeLine = go.AddComponent<PriceStrikeLine>();
			}
		}
		return _strikeLine;
	}

	#endregion

	#region Price Button

	/// <summary>
	/// Build the price button under the card's FlipRoot. Style authority is
	/// PriceButton.prefab (2026-09-29 prefab port; label baked into the prefab 2026-09-30;
	/// the legacy runtime build was removed 2026-10-01 together with the CardPrice print):
	/// colors / feel / z layering and the price label are prefab data; only per-card wiring
	/// happens here. Root placement comes from ShopUXManager.priceButtonPosition
	/// (card-local X/Y, live-tunable).
	/// </summary>
	private PhysButton EnsurePriceButton()
	{
		if (_priceButton != null) return _priceButton;

		if (ShopUXManager.Instance == null || ShopUXManager.Instance.priceButtonPrefab == null)
		{
			Debug.LogWarning("ShopUXManager.priceButtonPrefab is not wired — no buy/sell price button will be shown on shop cards.", _cardPhysObj);
			return null;
		}

		// Static root = stable hitbox (envelope-sized in ConfigureWorldFaceSize); only the
		// Visual group moves for hover/press/deny. The shadow stays a direct child of the
		// root: it is grounded and never moves.
		GameObject rootGo = Instantiate(ShopUXManager.Instance.priceButtonPrefab, _cardPhysObj.FlipRoot, false);
		rootGo.transform.localRotation = Quaternion.identity;
		rootGo.transform.localScale = Vector3.one;
		ApplyPriceButtonPlacement(rootGo.transform);

		PhysButton prefabButton = rootGo.GetComponent<PhysButton>();
		if (prefabButton == null || prefabButton.label == null)
		{
			Debug.LogWarning("PriceButton.prefab has no PhysButton + baked Label — price button cannot be shown.", rootGo);
			Destroy(rootGo);
			return null;
		}

		Transform shadowTr = rootGo.transform.Find("Shadow");
		Transform faceTr = rootGo.transform.Find("Visual/Face");
		// Card-face sprite stays a code copy (plan §3.1-5): the per-card shape source; the
		// prefab's baked sprite is only a placeholder. PaletteTint owns the colors.
		if (_cardPhysObj.cardFace != null)
		{
			if (shadowTr != null) shadowTr.GetComponent<SpriteRenderer>().sprite = _cardPhysObj.cardFace.sprite;
			if (faceTr != null) faceTr.GetComponent<SpriteRenderer>().sprite = _cardPhysObj.cardFace.sprite;
		}

		// VISUAL-FIX(2026-09-29): prefab price button face turned white after disabled -> re-enable
		//   Cause:    PhysButton._faceColor (the re-enable restore color) is captured only in
		//             SetWorldFace; prefab instances carry _faceRenderer serialized but nobody
		//             called the setter, so _faceColor kept its Color.white default.
		//   Affects:  ShopCardView.EnsurePriceButton (ApplyDisabledVisual restore); the palette
		//             Apply first mirrors the reroll fix (capture the live palette color, not
		//             a stale one)
		//   Regress:  Play: unaffordable card dims -> afford it again -> face must return to
		//             ButtonFace, not white
		//   Related:  PhysButton.SetWorldFace / ApplyDisabledVisual, PriceButton.prefab,
		//             ShopSectionPanels.CreateRerollButton prefab branch (same fix, 09-29)
		SpriteRenderer prefabFaceSr = faceTr != null ? faceTr.GetComponent<SpriteRenderer>() : null;
		PaletteTint faceTint = prefabFaceSr != null ? prefabFaceSr.GetComponent<PaletteTint>() : null;
		if (faceTint != null) faceTint.Apply();
		prefabButton.SetWorldFace(prefabFaceSr);

		_priceButton = prefabButton;
		return prefabButton;
	}

	/// <summary>
	/// Root X/Y placement of the buy/sell button, card-local
	/// (ShopUXManager.priceButtonPosition). Re-asserted on every shop-phase display pass so
	/// Inspector edits apply live; the compare keeps the steady state write-free.
	/// </summary>
	private void ApplyPriceButtonPlacement(Transform buttonRoot)
	{
		// While the lift/enlarge pin holds THIS button to its rest world spot, the
		// authored-placement re-assert would fight the pin every frame. The pin's reattach
		// lands on the arm-time local and the next unpinned pass re-asserts live Inspector
		// edits. A button built mid-flight (never captured) keeps the normal re-assert.
		if (_pinButtonActive && _pinButtonTr == buttonRoot) return;
		Vector2 pos = ShopUXManager.Instance != null ? ShopUXManager.Instance.priceButtonPosition : DefaultPriceButtonPos;
		Vector3 desired = new Vector3(pos.x, pos.y, PRICE_BUTTON_Z);
		if (buttonRoot.localPosition != desired) buttonRoot.localPosition = desired;
	}

	/// <summary>Face/shadow/collider sized from the button label's text bounds (label-local x its scale).</summary>
	private void ResizePriceButtonFace()
	{
		if (_priceButton == null || _priceButton.label == null) return;

		TMP_Text print = _priceButton.label;
		float s = print.transform.localScale.x;
		Bounds b = print.textBounds;
		Vector2 size = new Vector2(b.size.x * s + PRICE_FACE_PAD_X, b.size.y * s + PRICE_FACE_PAD_Y);
		Vector2 center = new Vector2(b.center.x * s, b.center.y * s);
		_priceButton.ConfigureWorldFaceSize(size, center);
	}

	#endregion

	#region Shop Input

	/// <summary>
	/// Detect click again to restore card. The enlarge modal holds the shop input gate
	/// itself, so the gate must not gate the dismiss — the only allowed click while
	/// enlarged. The transition driver's own block (it owns camera and cards mid-flight)
	/// still suppresses the dismiss.
	/// </summary>
	private void HandleClickToRestore()
	{
		if (!_isEnlarged) return;

		if (Input.GetMouseButtonDown(0) && !ShopInputGate.Blocked)
		{
			RestoreCard();
			_enlargeCooldown = ENLARGE_COOLDOWN_TIME;
			_pressActive = false; // the click was consumed by restore, don't re-enlarge on release
		}
	}

	private void OnMouseDown()
	{
		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		if (phaseRef != null && phaseRef.Value() == EnumStorage.GamePhase.Shop && !ShopInputGate.Blocked)
		{
			_pressActive = true;
		}
	}

	private void OnMouseUp()
	{
		// Short click = enlarge preview (UIKitDemo §04: the card body previews, it never transacts).
		if (_pressActive)
		{
			EnlargeCard();
		}
		_pressActive = false;
	}

	private void OnMouseExit()
	{
		_pressActive = false;
	}

	#endregion

	#region Card Hover Lift

	private void OnMouseEnter()
	{
		TryArmHoverLift();
	}

	/// <summary>
	/// Arm the hover lift: every gate must pass while the card is AT REST (the position-tween
	/// gate also guarantees the rest-pose capture below is not mid-flight — entry flight,
	/// purchase flight to the deck, sell flight). The card then rises to base + hoverLiftY with
	/// a slight scale bump, OutBack (PhysButton hover feel).
	/// </summary>
	private void TryArmHoverLift()
	{
		if (_isHoverLifted || _isEnlarged) return;
		if (ShopUXManager.Instance == null || !ShopUXManager.Instance.hoverLiftEnabled) return;
		if (_enlargeCooldown > 0f) return;
		if (ShopInputGate.Blocked || PhaseTransitionDriver.IsTransitioning) return;
		if (_cardPhysObj.IsPositionTweenPlaying) return;
		if (!_cardPhysObj.isFaceUp || _cardPhysObj.isFlipPlaying) return;

		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		if (phaseRef == null || phaseRef.Value() != EnumStorage.GamePhase.Shop) return;

		if (_hoverCollider == null) _hoverCollider = GetComponent<Collider2D>();
		if (_hoverCollider == null) return; // no collider: no retention rect, never arm

		_hoverBase = _cardPhysObj.TargetPosition;
		_hoverRestBounds = _hoverCollider.bounds;
		_isHoverLifted = true;
		// Grounded elements captured at arm: the card is at rest here, so the current world
		// position IS the rest world position. The price button is lazily built by the price
		// display pass, so it may not exist yet on a fresh card — it then simply rides this
		// lift session (re-armed lifts pin it normally).
		CaptureButtonPin();
		_pinShadowTr = null;
		SpriteRenderer bigShadow = _cardPhysObj.bigShadowRenderer;
		if (bigShadow != null && bigShadow.gameObject.activeInHierarchy)
		{
			_pinShadowTr = bigShadow.transform;
			_pinShadowRestLocal = _pinShadowTr.localPosition;
			_pinShadowRestWorld = _pinShadowTr.position;
			_pinShadowActive = true;
		}
		ReassertLiftedPose();
	}

	/// <summary>
	/// Capture the price button's rest pose for world-pinning. Only valid while the card is at
	//  rest (a mid-flight capture would freeze the tag in mid-air), so a click during the entry
	/// flight leaves the tag riding that session. Already-active pin = capture is a no-op and
	/// the existing pin carries over seamlessly (e.g. lift → enlarge, restore → re-hover).
	/// </summary>
	private void CaptureButtonPin()
	{
		if (_pinButtonActive) return;
		if (_priceButton == null || !_priceButton.gameObject.activeInHierarchy) return;
		if (_cardPhysObj.IsPositionTweenPlaying) return;
		_pinButtonTr = _priceButton.transform;
		_pinButtonRestLocal = _pinButtonTr.localPosition;
		_pinButtonRestLocalScale = _pinButtonTr.localScale;
		_pinButtonRestWorld = _pinButtonTr.position;
		_pinButtonRestWorldScale = _pinButtonTr.lossyScale;
		_hoverBase = _cardPhysObj.TargetPosition;
		_pinButtonActive = true;
	}

	/// <summary>
	/// Per-frame hover poll. OnMouseExit is deliberately NOT used to end the lift (the lifted
	/// card moves under the cursor — the combat hover's flicker-loop lesson): the cursor is
	/// tested against the REST-pose retention rect every frame instead. Two release flavors:
	/// another owner took the target (enlarge / manager gone) → silent release, the pinned
	/// elements reattach and ride; transient gates or the cursor leaving (incl. the price
	/// button claiming the cursor — 完全分离: hovering the tag drops the card) → settle home.
	/// </summary>
	private void UpdateHoverLift()
	{
		if (!_isHoverLifted) return;

		if (_isEnlarged || ShopUXManager.Instance == null)
		{
			// The enlarge state owns the target now (EnlargeCard already reattached the
			// shadow so it rides the preview flight); the price button pin carries over
			// into the enlarge hold — the tag stays on its shelf spot.
			_isHoverLifted = false;
			return;
		}

		if (ShopInputGate.Blocked || PhaseTransitionDriver.IsTransitioning
			|| !_cardPhysObj.isFaceUp || _cardPhysObj.isFlipPlaying
			|| !IsCursorInHoverRetention() || PriceButtonOwnsCursor())
		{
			DropHoverLift();
			return;
		}

		// Live tuning: re-assert the lifted position when hoverLift changed mid-hover. The
		// compare keeps the steady state write-free (ApplyPriceButtonPlacement pattern).
		ReassertLiftedPose();
	}

	/// <summary>True while the cursor is on the price button — the tag owns the interaction and the card settles back home (完全分离).</summary>
	private bool PriceButtonOwnsCursor()
	{
		return _priceButton != null && _priceButton.gameObject.activeInHierarchy && _priceButton.IsPointerOver;
	}

	private void ReassertLiftedPose()
	{
		float lift = ShopUXManager.Instance.hoverLift;
		Vector3 liftedPosition = _hoverBase + new Vector3(-lift, lift, 0f);
		if (_cardPhysObj.TargetPosition != liftedPosition)
		{
			_cardPhysObj.SetTargetPosition(liftedPosition, Ease.OutBack, ShopUXManager.Instance.hoverLiftDuration);
		}
	}

	private void DropHoverLift()
	{
		_isHoverLifted = false;
		_cardPhysObj.SetTargetPosition(_hoverBase, Ease.OutQuad, ShopUXManager.Instance.hoverLiftDuration);
	}

	/// <summary>
	/// Grounded-element pins, per frame. Each pin holds while the face is off its rest spot
	/// for ITS face-only reason, and reattaches the moment that reason ends:
	///   button — hover lift, enlarge preview, or the glide home from either (the tag stays
	///   on its shelf spot through all of it);
	///   shadow — hover lift / drop-back glide only (the enlarge flight carries the shadow).
	/// </summary>
	private void UpdateLiftPins()
	{
		if (PhaseTransitionDriver.IsTransitioning)
		{
			// The transition driver owns the flight — reattach so the pinned elements ride it
			// instead of fighting it for their transform.
			ReattachAllPins();
			return;
		}

		if (_pinButtonActive)
		{
			if (_isHoverLifted || _isEnlarged || _cardPhysObj.IsPositionTweenPlaying)
			{
				if (_pinButtonTr != null)
				{
					_pinButtonTr.position = _pinButtonRestWorld;
					// Counter-scale: the tag rides FlipRoot, so the enlarged card's scale would
					// grow it — divide it back out to hold the captured rest world scale.
					Vector3 parentLossy = _pinButtonTr.parent != null ? _pinButtonTr.parent.lossyScale : Vector3.one;
					_pinButtonTr.localScale = new Vector3(
						_pinButtonRestWorldScale.x / Mathf.Max(Mathf.Abs(parentLossy.x), 0.0001f),
						_pinButtonRestWorldScale.y / Mathf.Max(Mathf.Abs(parentLossy.y), 0.0001f),
						_pinButtonRestWorldScale.z / Mathf.Max(Mathf.Abs(parentLossy.z), 0.0001f));
				}
			}
			else
			{
				ReattachButtonPin();
			}
		}

		if (_pinShadowActive)
		{
			bool faceOffRest = _isHoverLifted
				|| (_cardPhysObj.IsPositionTweenPlaying && _cardPhysObj.TargetPosition == _hoverBase);
			if (faceOffRest)
			{
				if (_pinShadowTr != null) _pinShadowTr.position = _pinShadowRestWorld;
			}
			else
			{
				ReattachShadowPin();
			}
		}
	}

	private void ReattachButtonPin()
	{
		_pinButtonActive = false;
		if (_pinButtonTr != null)
		{
			_pinButtonTr.localPosition = _pinButtonRestLocal;
			_pinButtonTr.localScale = _pinButtonRestLocalScale;
		}
	}

	private void ReattachShadowPin()
	{
		_pinShadowActive = false;
		if (_pinShadowTr != null) _pinShadowTr.localPosition = _pinShadowRestLocal;
	}

	/// <summary>
	/// Reattach every pinned element to the card: they ride whatever takes over next
	/// (purchase flight, sell flight, transition). Never writes the card target — the
	/// caller / new owner owns it.
	/// </summary>
	private void ReattachAllPins()
	{
		_isHoverLifted = false;
		ReattachButtonPin();
		ReattachShadowPin();
	}

	/// <summary>
	/// Cursor still inside the card's REST bounds (sampled at arm, shifted by NotifySlotMoved
	/// on reflow) expanded by hoverRetentionMargin. The cursor is projected onto the rest
	/// pose's z plane, so the lift displacement never changes what the cursor "hits".
	/// </summary>
	private bool IsCursorInHoverRetention()
	{
		if (_hoverCollider == null)
		{
			_hoverCollider = GetComponent<Collider2D>();
			if (_hoverCollider == null) return true; // cannot test: keep the lift (combat fallback)
		}
		if (_hoverCamera == null)
		{
			_hoverCamera = Camera.main;
			if (_hoverCamera == null) return true;
		}
		Vector3 screenPos = Input.mousePosition;
		screenPos.z = _hoverCamera.WorldToScreenPoint(_hoverRestBounds.center).z;
		Vector3 worldPos = _hoverCamera.ScreenToWorldPoint(screenPos);
		float margin = ShopUXManager.Instance.hoverRetentionMargin;
		Bounds retention = _hoverRestBounds;
		retention.Expand(margin * 2f);
		return retention.Contains(worldPos);
	}

	/// <summary>
	/// True while the hover lift is active. ShopUXManager reflow paths check this to sync the
	/// lift base (NotifySlotMoved) instead of retargeting the card flat.
	/// </summary>
	public bool IsHoverLifted
	{
		get { return _isHoverLifted; }
	}

	/// <summary>
	/// Target ownership handoff (purchase / sell / transition): end the lift WITHOUT writing
	/// the target — the new owner (flight, relayout, transition driver) retargets from here.
	/// The pinned (grounded) elements reattach and ride the new owner's flight.
	/// </summary>
	public void NotifyTargetTaken()
	{
		ReattachAllPins();
	}

	#endregion

	#region Card Enlarge

	/// <summary>
	/// True while this card is hover-enlarged; its target position is owned by the enlarge state
	/// (relayout must not move it, or RestoreCard would send it to a stale position).
	/// </summary>
	public bool IsEnlarged
	{
		get { return _isEnlarged; }
	}

	/// <summary>
	/// The card's grid slot moved (shelf reflow after a purchase, deck/utility reflow). While
	/// enlarged, the captured _originalPosition must track the new slot or RestoreCard would
	/// send it to a stale position. While hover-lifted, the lift base and retention rect must
	/// track it too so the card keeps riding its new slot (target = new base + lift). When
	/// neither, only the base is kept fresh for a later hover; the caller retargets directly.
	/// </summary>
	public void NotifySlotMoved(Vector3 newSlotPosition)
	{
		Vector3 delta = newSlotPosition - _hoverBase;
		if (_isEnlarged)
		{
			_originalPosition = newSlotPosition;
		}
		if (_isHoverLifted)
		{
			_hoverRestBounds.center += delta;
			ReassertLiftedPose();
		}
		if (_pinButtonActive) _pinButtonRestWorld += delta;
		if (_pinShadowActive) _pinShadowRestWorld += delta;
		_hoverBase = newSlotPosition;
	}

	/// <summary>
	/// Enlarge card.
	/// </summary>
	private void EnlargeCard()
	{
		if (_enlargeCooldown > 0) return;

		// Capture the REST pose: while hover-lifted, TargetPosition carries the lift offset,
		// and RestoreCard must land on the un-lifted slot. The lift ends here; the big shadow
		// reattaches and rides the preview flight, while the price button stays grounded —
		// its pin carries over into the enlarge hold (a direct click without a prior hover
		// captures the pin now, before the flight tweens start).
		_originalPosition = _isHoverLifted ? _hoverBase : _cardPhysObj.TargetPosition;
		_originalScale = _cardPhysObj.TargetScale;
		if (_isHoverLifted)
		{
			_isHoverLifted = false;
			ReattachShadowPin();
		}
		CaptureButtonPin();

		if (ShopUXManager.Instance != null)
		{
			float enlargeSize = ShopUXManager.Instance.physCardEnlargeSize;
			_cardPhysObj.SetTargetScale(new Vector3(enlargeSize, enlargeSize, enlargeSize));
			// The preview lands on the authored center PLUS the current scroll travel, so a
			// preview started from a scrolled view still lands on the screen center
			// (enlargedPosition stays the authored center at rest scroll; x/z tunable there).
			Vector3 enlargeTarget = ShopUXManager.Instance.enlargedPosition;
			enlargeTarget.y += ShopUXManager.Instance.CameraScrollOffsetY;
			_cardPhysObj.SetTargetPosition(enlargeTarget);
			// Draw the whole card subtree above the chrome band while the preview is up
			// (VISUAL-FIX(2026-10-02) in CardPhysObjScript.SetEnlargedSorting). Skipped when
			// no manager exists — that path has no chrome to beat either.
			_cardPhysObj.SetEnlargedSorting(true, ShopUXManager.Instance.enlargedSortingOrder);
		}
		else
		{
			_cardPhysObj.SetTargetScale(new Vector3(2f, 2f, 2f));
			_cardPhysObj.SetTargetPosition(Vector3.zero);
		}

		_isEnlarged = true;

		// DIAGNOSTIC: log dynamic damage resolution state when player enlarges a shop card.
		if (_cardPhysObj != null && _cardPhysObj.cardImRepresenting != null)
		{
			_cardPhysObj.cardImRepresenting.LogDynamicDamageDiagnostics("ShopEnlarge");
		}
	}

	/// <summary>
	/// Restore card to original state.
	/// </summary>
	public void RestoreCard()
	{
		if (!_isEnlarged) return;

		_cardPhysObj.SetTargetPosition(_originalPosition);
		_cardPhysObj.SetTargetScale(_originalScale);
		// Drop the enlarge sorting boost before the flight home so the landing pose sorts
		// like any other shelf card again (chrome above shelf cards restored).
		_cardPhysObj.SetEnlargedSorting(false);

		_isEnlarged = false;
		// Debug.Log("[ShopCardView] Card restored: " + (_cardPhysObj.cardImRepresenting != null ? _cardPhysObj.cardImRepresenting.gameObject.name : "null"));
	}

	/// <summary>
	/// The camera scroll rig moved by deltaY (world units, post-clamp). While enlarged, shift
	/// the preview by the same delta so it stays glued to the screen center. Only the live
	/// pose moves — _originalPosition stays on its world-fixed slot for RestoreCard. Normally
	/// the wheel is gated off during a preview (ShopUXManager.blockScrollWhileEnlarged); this
	/// follower is the gate-off mode and the guard for any other rig mover.
	/// </summary>
	public void NotifyScrollDelta(float deltaY)
	{
		if (!_isEnlarged || _cardPhysObj == null || Mathf.Approximately(deltaY, 0f)) return;

		Vector3 target = _cardPhysObj.TargetPosition;
		target.y += deltaY;

		if (_cardPhysObj.IsPositionTweenPlaying)
		{
			// Enlarge flight still playing: retarget and let the tween carry the shift.
			_cardPhysObj.SetTargetPosition(target);
		}
		else
		{
			// Landed: translate synchronously with the rig move — same frame, zero offset.
			Vector3 pos = _cardPhysObj.transform.position;
			pos.y += deltaY;
			_cardPhysObj.transform.position = pos;
			_cardPhysObj.UpdateTargetPositionOnly(target);
		}
	}

	#endregion

	/// <summary>
	/// Try to purchase this card.
	/// </summary>
	private void TryPurchase()
	{
		if (ShopManager.me != null)
		{
			ShopManager.me.BuyFunc(_cardPhysObj.shopItemIndex);
		}
	}

	/// <summary>
	/// Try to sell this card.
	/// </summary>
	private void TrySell()
	{
		if (ShopManager.me == null || _cardPhysObj.cardImRepresenting == null) return;

		int cardIndex = GetPlayerCardIndex();
		if (cardIndex >= 0)
		{
			ShopManager.me.SellFunc(cardIndex, this.gameObject);
		}
	}

	/// <summary>
	/// Get the index of this card in player deck.
	/// </summary>
	private int GetPlayerCardIndex()
	{
		if (ShopManager.me == null || _cardPhysObj.cardImRepresenting == null) return -1;

		var playerDeck = ShopManager.me.playerDeckRef;
		if (playerDeck == null || playerDeck.deck == null) return -1;

		for (int i = 0; i < playerDeck.deck.Count; i++)
		{
			if (playerDeck.deck[i] == _cardPhysObj.cardImRepresenting.gameObject)
			{
				return i;
			}
		}
		return -1;
	}
}
