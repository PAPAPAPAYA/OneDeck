using UnityEngine;

[RequireComponent(typeof(CardPhysObjScript))]
public class ShopCardView : MonoBehaviour
{
	private CardPhysObjScript _cardPhysObj;

	// Long press purchase related
	private bool _isHolding = false;
	private float _holdTimer = 0f;

	// Card enlarge related
	private Vector3 _originalPosition;
	private Vector3 _originalScale;
	private bool _isEnlarged = false;
	private bool _hasClickProcessed = false;
	private float _enlargeCooldown = 0f;
	private const float ENLARGE_COOLDOWN_TIME = 0.5f;

	void OnEnable()
	{
		_cardPhysObj = GetComponent<CardPhysObjScript>();
	}

	void Update()
	{
		UpdatePriceDisplay();
		HandleHoldToBuy();
		HandleClickToRestore();

		if (_enlargeCooldown > 0f)
		{
			_enlargeCooldown -= Time.deltaTime;
		}
	}

	#region Shop Display

	/// <summary>
	/// Update price display, only shown in Shop Phase.
	/// </summary>
	private void UpdatePriceDisplay()
	{
		if (_cardPhysObj.cardPricePrint == null) return;

		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		if (phaseRef == null || phaseRef.Value() != EnumStorage.GamePhase.Shop)
		{
			_cardPhysObj.cardPricePrint.gameObject.SetActive(false);
			return;
		}

		if (_cardPhysObj.cardImRepresenting == null)
		{
			_cardPhysObj.cardPricePrint.gameObject.SetActive(false);
			return;
		}

		_cardPhysObj.cardPricePrint.gameObject.SetActive(true);

		int displayPrice = _cardPhysObj.shopItemIndex >= 0
			? _cardPhysObj.cardImRepresenting.price.value
			: _cardPhysObj.cardImRepresenting.price.value / 2;
		_cardPhysObj.cardPricePrint.text = "<color=yellow>$" + displayPrice + "</color>";
	}

	#endregion

	#region Shop Input

	/// <summary>
	/// Handle long press buy/sell logic.
	/// </summary>
	private void HandleHoldToBuy()
	{
		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		if (phaseRef == null || phaseRef.Value() != EnumStorage.GamePhase.Shop)
			return;

		if (_isHolding)
		{
			_holdTimer += Time.deltaTime;

			if (_holdTimer >= _cardPhysObj.holdTimeRequired)
			{
				if (_cardPhysObj.shopItemIndex >= 0)
				{
					TryPurchase();
				}
				else if (_cardPhysObj.shopItemIndex == -1)
				{
					TrySell();
				}
				_isHolding = false;
				_holdTimer = 0f;
			}
		}
	}

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

	/// <summary>
	/// Detect click again to restore card.
	/// </summary>
	private void HandleClickToRestore()
	{
		if (!_isEnlarged) return;

		if (Input.GetMouseButtonDown(0))
		{
			RestoreCard();
			_enlargeCooldown = ENLARGE_COOLDOWN_TIME;
		}
	}

	private void OnMouseDown()
	{
		GamePhaseSO phaseRef = _cardPhysObj.currentGamePhaseRef;
		if (phaseRef != null && phaseRef.Value() == EnumStorage.GamePhase.Shop)
		{
			_isHolding = true;
			_holdTimer = 0f;
			_hasClickProcessed = false;
			_cardPhysObj.StartCardShake();
		}
	}

	private void OnMouseUp()
	{
		if (_isHolding && _holdTimer < _cardPhysObj.holdTimeRequired && !_hasClickProcessed)
		{
			EnlargeCard();
			_hasClickProcessed = true;
		}

		_isHolding = false;
		_holdTimer = 0f;
		_cardPhysObj.StopCardShake();
	}

	private void OnMouseExit()
	{
		_isHolding = false;
		_holdTimer = 0f;
		_cardPhysObj.StopCardShake();
	}

	#endregion

	#region Card Enlarge

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
		Debug.Log("[ShopCardView] Card enlarged: " + (_cardPhysObj.cardImRepresenting != null ? _cardPhysObj.cardImRepresenting.gameObject.name : "null"));
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
		Debug.Log("[ShopCardView] Card restored: " + (_cardPhysObj.cardImRepresenting != null ? _cardPhysObj.cardImRepresenting.gameObject.name : "null"));
	}

	#endregion
}
