using System;
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
	/// Detect click again to restore card.
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
	/// The card's grid slot moved (shelf reflow after a purchase). While enlarged, the captured
	/// _originalPosition must track the new slot or RestoreCard would send the card to a stale
	/// position; when not enlarged the caller retargets the card directly, so nothing to do.
	/// </summary>
	public void NotifySlotMoved(Vector3 newSlotPosition)
	{
		if (_isEnlarged)
		{
			_originalPosition = newSlotPosition;
		}
	}

	/// <summary>
	/// Enlarge card.
	/// </summary>
	private void EnlargeCard()
	{
		if (_enlargeCooldown > 0) return;

		_originalPosition = _cardPhysObj.TargetPosition;
		_originalScale = _cardPhysObj.TargetScale;

		if (ShopUXManager.Instance != null)
		{
			float enlargeSize = ShopUXManager.Instance.physCardEnlargeSize;
			_cardPhysObj.SetTargetScale(new Vector3(enlargeSize, enlargeSize, enlargeSize));
			_cardPhysObj.SetTargetPosition(ShopUXManager.Instance.enlargedPosition);
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

		_isEnlarged = false;
		// Debug.Log("[ShopCardView] Card restored: " + (_cardPhysObj.cardImRepresenting != null ? _cardPhysObj.cardImRepresenting.gameObject.name : "null"));
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
