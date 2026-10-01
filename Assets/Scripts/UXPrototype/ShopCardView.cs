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
	// True once the authored prefab path supplies the baked Label (2026-09-30 label port):
	// the card's own price print is retired (kept hidden) and must not be toggled back on
	// by the shop-phase display code.
	private bool _priceLabelExternal;
	private string _lastPriceText;
	private Action _buyAction;
	private Action _sellAction;
	private DeckSizeIncreaseEffect _deckSizeEffect; // cached pricing probe, resolved once per card instead of per frame
	private bool _deckSizeEffectResolved;
	private UpgradeCapIncreaseEffect _upgradeCapEffect; // second meter family (upgrade-slot meter), same once-per-card caching
	private bool _upgradeCapEffectResolved;

	// Card-local design scale: demo card face is 118 px wide == 3.2 card-local units.
	private const float UNITS_PER_PX = 3.2f / 118f;
	private const float PRICE_FACE_PAD_X = 0.22f;
	private const float PRICE_FACE_PAD_Y = 0.14f;

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
		if (_cardPhysObj.cardPricePrint == null) return;

		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		bool shopPhase = phaseRef != null && phaseRef.Value() == EnumStorage.GamePhase.Shop;
		// plan-shop-reroll-flip-2026-09-30: face-down cards (reroll flip reveal) hide the
		// whole price block. cardPricePrint is a FlipRoot face element and hides with the
		// flip, but the price PhysButton is parented under FlipRoot too (EnsurePriceButton),
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
			if (!_priceLabelExternal) _cardPhysObj.cardPricePrint.gameObject.SetActive(false);
			if (_priceButton != null) _priceButton.gameObject.SetActive(false);
			// The dim face must never leak out of the unaffordable-shop-item state.
			_cardPhysObj.SetFaceDimmed(false);
			return;
		}

		if (!_priceLabelExternal) _cardPhysObj.cardPricePrint.gameObject.SetActive(true);
		PhysButton priceButton = EnsurePriceButton();
		priceButton.gameObject.SetActive(true);

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

		// The label is the PriceButton.prefab's baked one on the authored path (2026-09-30
		// label port) and the re-parented card print in the legacy build; both arrive as
		// PhysButton.label. Also compare the live label: the hover text swap rewrote it, so
		// the cached price string alone must not short-circuit the restore.
		TMP_Text label = _priceButton != null ? _priceButton.label : _cardPhysObj.cardPricePrint;
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
	/// lazily attached under the label in the legacy runtime build.
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
	/// Build the price button under the existing price print. Style authority is
	/// PriceButton.prefab (2026-09-29 prefab port; label baked into the prefab 2026-09-30)
	/// when wired — the card's own price print is then retired and the prefab's baked Label
	/// renders the price. The legacy runtime build (print re-parented as the label, sliced
	/// face sprite + hard shadow, BoxCollider2D on the face) stays as the fallback for old
	/// scenes / headless.
	/// </summary>
	private PhysButton EnsurePriceButton()
	{
		if (_priceButton != null) return _priceButton;

		Transform printT = _cardPhysObj.cardPricePrint.transform;
		const float printZ = -0.02f;

		// Static root = stable hitbox (envelope-sized in ConfigureWorldFaceSize); only the
		// Visual group moves for hover/press/deny. The shadow stays a direct child of the
		// root: it is grounded and never moves.
		GameObject rootGo;
		if (ShopUXManager.Instance != null && ShopUXManager.Instance.priceButtonPrefab != null)
		{
			// Authored prefab path (plan-shop-price-button-prefab-2026-09-29; label baked into
			// the prefab 2026-09-30): colors / feel / z layering and the price label are prefab
			// data. The card's own price print is retired (hidden on the card) instead of
			// re-parented; only per-card wiring happens here. Root placement mirrors the
			// legacy build below.
			Vector3 printLocalPos = printT.localPosition;
			rootGo = Instantiate(ShopUXManager.Instance.priceButtonPrefab, printT.parent, false);
			rootGo.transform.localPosition = printLocalPos;
			rootGo.transform.localRotation = Quaternion.identity;
			rootGo.transform.localScale = Vector3.one;

			PhysButton prefabButton = rootGo.GetComponent<PhysButton>();
			if (prefabButton == null || prefabButton.label == null)
			{
				// A prefab without a baked Label predates the 2026-09-30 label port — treat
				// it as unwired and fall through to the legacy runtime build.
				Debug.LogWarning("PriceButton.prefab has no baked Label — falling back to the legacy runtime price button build.", rootGo);
				Destroy(rootGo);
			}
			else
			{
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
				//   Affects:  ShopCardView.EnsurePriceButton prefab branch (ApplyDisabledVisual restore);
				//             the palette Apply first mirrors the reroll fix (capture the live palette
				//             color, not a stale one)
				//   Regress:  Play: unaffordable card dims -> afford it again -> face must return to
				//             ButtonFace, not white
				//   Related:  PhysButton.SetWorldFace / ApplyDisabledVisual, PriceButton.prefab,
				//             ShopSectionPanels.CreateRerollButton prefab branch (same fix, 09-29)
				SpriteRenderer prefabFaceSr = faceTr != null ? faceTr.GetComponent<SpriteRenderer>() : null;
				PaletteTint faceTint = prefabFaceSr != null ? prefabFaceSr.GetComponent<PaletteTint>() : null;
				if (faceTint != null) faceTint.Apply();
				prefabButton.SetWorldFace(prefabFaceSr);

				// The card's own price print stays on the card and off: the baked prefab label
				// (PhysButton.label) renders the price now.
				_priceLabelExternal = true;
				_cardPhysObj.cardPricePrint.gameObject.SetActive(false);

				_priceButton = prefabButton;
				return prefabButton;
			}
		}

		rootGo = new GameObject("PriceButton", typeof(BoxCollider2D));
		rootGo.transform.SetParent(printT.parent, false);
		rootGo.transform.localPosition = printT.localPosition;
		rootGo.transform.localRotation = Quaternion.identity;
		rootGo.transform.localScale = Vector3.one;

		GameObject visualGo = new GameObject("Visual");
		visualGo.transform.SetParent(rootGo.transform, false);

		GameObject shadowGo = new GameObject("Shadow", typeof(SpriteRenderer));
		shadowGo.transform.SetParent(rootGo.transform, false);
		shadowGo.transform.localPosition = new Vector3(0f, 0f, printZ + 0.08f);
		SpriteRenderer shadowSr = shadowGo.GetComponent<SpriteRenderer>();
		shadowSr.sprite = _cardPhysObj.cardFace != null ? _cardPhysObj.cardFace.sprite : null;
		shadowSr.drawMode = SpriteDrawMode.Sliced;
		shadowSr.color = GameColorPalette.CardShadowColor;

		GameObject faceGo = new GameObject("Face", typeof(SpriteRenderer));
		faceGo.transform.SetParent(visualGo.transform, false);
		faceGo.transform.localPosition = new Vector3(0f, 0f, printZ + 0.04f);
		SpriteRenderer faceSr = faceGo.GetComponent<SpriteRenderer>();
		faceSr.sprite = _cardPhysObj.cardFace != null ? _cardPhysObj.cardFace.sprite : null;
		faceSr.drawMode = SpriteDrawMode.Sliced;
		faceSr.color = GameColorPalette.ButtonFaceColor;

		// The print (and its PriceStrikeLine child) becomes the button label.
		printT.SetParent(visualGo.transform, false);
		printT.localPosition = new Vector3(0f, 0f, printZ);

		PhysButton button = rootGo.AddComponent<PhysButton>();
		button.SetShadowTransform(shadowGo.transform);
		button.SetWorldFace(faceSr);
		button.SetVisualGroup(visualGo.transform);
		button.restShadow = 4f * UNITS_PER_PX;
		button.hoverLift = 4f * UNITS_PER_PX;
		button.denyShift = 6f * UNITS_PER_PX;
		button.label = _cardPhysObj.cardPricePrint;

		_priceButton = button;
		return button;
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
