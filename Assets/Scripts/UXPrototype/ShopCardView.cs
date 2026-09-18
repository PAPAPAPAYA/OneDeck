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
		bool showPrice = shopPhase && _cardPhysObj.cardImRepresenting != null && !suppressPriceDisplay;
		if (!showPrice)
		{
			_cardPhysObj.cardPricePrint.gameObject.SetActive(false);
			if (_priceButton != null) _priceButton.gameObject.SetActive(false);
			// The dim face must never leak out of the unaffordable-shop-item state.
			_cardPhysObj.SetFaceDimmed(false);
			return;
		}

		_cardPhysObj.cardPricePrint.gameObject.SetActive(true);
		PhysButton priceButton = EnsurePriceButton();
		priceButton.gameObject.SetActive(true);

		int basePrice = ShopManager.me != null ? ShopManager.me.GetCardPrice(_cardPhysObj.cardImRepresenting, ResolveDeckSizeEffect()) : 0;
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

	/// <summary>
	/// Write the rest price text unless the pointer is on the price button (the hover
	/// text swap owns the label while hovered); rebuild the button face on change.
	/// </summary>
	private void SetPriceText(string priceText)
	{
		if (_priceButton != null && _priceButton.IsPointerOver) return;
		// Also compare the live label: the hover text swap rewrote it, so the cached
		// price string alone must not short-circuit the restore.
		if (priceText == _lastPriceText && _cardPhysObj.cardPricePrint.text == priceText) return;
		_lastPriceText = priceText;
		_cardPhysObj.cardPricePrint.text = priceText;
		_cardPhysObj.cardPricePrint.ForceMeshUpdate(false, false);
		ResizePriceButtonFace();
	}

	private PriceStrikeLine _strikeLine;

	/// <summary>Lazily attaches the solid diagonal strikethrough line to the price print.</summary>
	private PriceStrikeLine EnsureStrikeLine()
	{
		if (_strikeLine == null && _cardPhysObj.cardPricePrint != null)
		{
			var go = new GameObject("PriceStrikeLine");
			go.transform.SetParent(_cardPhysObj.cardPricePrint.transform, false);
			_strikeLine = go.AddComponent<PriceStrikeLine>();
		}
		return _strikeLine;
	}

	#endregion

	#region Price Button

	/// <summary>
	/// Build the price button around the existing price print: the print becomes the label,
	/// a sliced face sprite + hard shadow sit under it, and a BoxCollider2D covers the face.
	/// </summary>
	private PhysButton EnsurePriceButton()
	{
		if (_priceButton != null) return _priceButton;

		Transform printT = _cardPhysObj.cardPricePrint.transform;
		const float printZ = -0.02f;

		// Static root = stable hitbox (envelope-sized in ConfigureWorldFaceSize); only the
		// Visual group moves for hover/press/deny. The shadow stays a direct child of the
		// root: it is grounded and never moves.
		GameObject rootGo = new GameObject("PriceButton", typeof(BoxCollider2D));
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
		faceSr.color = GameColorPalette.OwnerCardColor;

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

	/// <summary>Face/shadow/collider sized from the label's text bounds (print-local x its 0.2 scale).</summary>
	private void ResizePriceButtonFace()
	{
		if (_priceButton == null || _cardPhysObj.cardPricePrint == null) return;

		TextMeshPro print = _cardPhysObj.cardPricePrint;
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
