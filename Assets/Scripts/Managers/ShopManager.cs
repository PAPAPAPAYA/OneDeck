using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class ShopManager : MonoBehaviour
{
	#region singleton
	public static ShopManager me;
	private void Awake()
	{
		me = this;
	}
	#endregion

	[Serializable]
	public class SessionRarityWeightEntry
	{
		[Tooltip("This entry takes effect from this session number onward (inclusive).")]
		public int startSession;
		public ShopRarityWeightSO rarityWeightRef;
	}

	[Serializable]
	public class SessionBoardChanceEntry
	{
		[Tooltip("This entry takes effect from this session number onward (inclusive).")]
		public int startSession;
		[Tooltip("Chance percent (0-100) that a generated board is a utility board.")]
		public float utilityBoardChancePercent = 10f;
	}

	[Header("flow ref")]
	public GamePhaseSO gamePhaseRef;
	public IntSO sessionNum;

	[Header("player ref")]
	public DeckSO playerDeckRef;
	public IntSO deckSize;
	public IntSO maxDeckSize;
	[Tooltip("ON: copies sharing a cardTypeID take a single deck slot (first copy only) and stack in the shop display.")]
	public BoolSO duplicateCopiesShareSlotRef;
	public IntSO purse;

	/// <summary>
	/// True when the duplicate-copies-share-slot rule is enabled (null-safe).
	/// </summary>
	public bool DuplicateCopiesShareSlot
	{
		get { return duplicateCopiesShareSlotRef != null && duplicateCopiesShareSlotRef.value; }
	}

	[Header("shop")]
	public DeckSO shopPoolRef;
	public DeckSO currentShopItemDeckRef;
	public ShopRarityWeightSO rarityWeightRef;
	[Tooltip("Session-based rarity weight overrides. Matches the entry with the highest startSession <= current sessionNum. Falls back to rarityWeightRef if none match.")]
	public List<SessionRarityWeightEntry> sessionRarityWeights;
	[Range(1, 6)]
	public int shopItemAmount;
	public IntSO payCheck;
	public IntSO RerollPriceRef;
	[Tooltip("Price for Common rarity cards, resolved from CardScript.rarity")]
	public IntSO CommonPriceRef;
	[Tooltip("Price for Uncommon rarity cards, resolved from CardScript.rarity")]
	public IntSO UncommonPriceRef;
	[Tooltip("Price for Rare rarity cards, resolved from CardScript.rarity")]
	public IntSO RarePriceRef;
	[TextArea]
	[Tooltip("button prompts and other general info")]
	public string phaseInfo;
	public bool sellMode = false; // if it's not sell mode then its buy mode

	[Header("Utility Baseline Growth (plan v2)")]
	[Tooltip("Payday bonus added per session number.")]
	public int incomePerSession = 2;
	[Tooltip("hpMax added per session number, applied on top of hpMaxOg at shop entry.")]
	public int hpMaxPerSession = 2;
	[Tooltip("Deck size added per session number, applied on top of deckSizeOg at shop entry.")]
	public int deckSizePerSession = 1;
	[Tooltip("Price of the first deck-slot purchase of a run; each prior purchase adds deckSlotPriceStep.")]
	public int deckSlotBasePrice = 4;
	[Tooltip("Price increase per already-made deck-slot purchase this run.")]
	public int deckSlotPriceStep = 2;
	[Tooltip("Run-persistent deck slot purchase counter (meter price + deckSize formula). Reset at run start.")]
	public IntSO deckSlotPurchasesRef;
	[Header("Utility Board Split (plan v2)")]
	[Tooltip("Session-based utility board chance (board split roll per generation). Matches the entry with the highest startSession <= current sessionNum. No match = pipeline built-in default (10%).")]
	public List<SessionBoardChanceEntry> sessionUtilityBoardChances = new List<SessionBoardChanceEntry>
	{
		new SessionBoardChanceEntry { startSession = 1, utilityBoardChancePercent = 10f },
		new SessionBoardChanceEntry { startSession = 3, utilityBoardChancePercent = 15f },
		new SessionBoardChanceEntry { startSession = 5, utilityBoardChancePercent = 20f },
	};
	[Tooltip("Generic offer slots on a utility board (before extraShopOptions). Combat boards keep using shopItemAmount.")]
	public int utilityBoardSlotCount = 3;
	private UtilityShopBonus.Bonus _utilityBonus;
	private int _rerollsThisVisit;
	private int _freeRerollsUsedThisVisit;
	private int _boardsGeneratedThisVisit;
	private bool _currentBoardIsUtility;
	private readonly Dictionary<CardScript, int> _boardDiscounts = new Dictionary<CardScript, int>();

	/// <summary>True when the most recently generated board is a utility board (shop UX visual marker reads this).</summary>
	public bool CurrentBoardIsUtility => _currentBoardIsUtility;

	[Tooltip("Store instantiated cards when purchased, destroy uniformly when exiting shop")]
	private List<GameObject> _boughtCardInstances = new List<GameObject>();

	/// <summary>
	/// Resolves the active rarity weight table based on the current session number.
	/// Returns the matching session override if available; otherwise falls back to rarityWeightRef.
	/// </summary>
	private ShopRarityWeightSO GetActiveRarityWeightRef()
	{
		if (sessionRarityWeights != null && sessionRarityWeights.Count > 0 && sessionNum != null)
		{
			ShopRarityWeightSO bestMatch = null;
			int bestStart = int.MinValue;
			foreach (var entry in sessionRarityWeights)
			{
				if (entry.rarityWeightRef == null) continue;
				if (entry.startSession <= sessionNum.value && entry.startSession > bestStart)
				{
					bestMatch = entry.rarityWeightRef;
					bestStart = entry.startSession;
				}
			}
			if (bestMatch != null) return bestMatch;
		}
		return rarityWeightRef;
	}

	/// <summary>
	/// Resolves the utility board chance for the current session (mirrors GetActiveRarityWeightRef).
	/// Returns -1 when the table is empty or no entry matches, so ShopBoardPipeline applies its
	/// built-in default - scene deserialization wipes the list's field initializer, therefore the
	/// fallback must live in the pipeline, not here.
	/// </summary>
	private float GetUtilityBoardChancePercent()
	{
		if (sessionUtilityBoardChances != null && sessionUtilityBoardChances.Count > 0 && sessionNum != null)
		{
			float bestChance = 0f;
			int bestStart = int.MinValue;
			foreach (var entry in sessionUtilityBoardChances)
			{
				if (entry == null) continue;
				if (entry.startSession <= sessionNum.value && entry.startSession > bestStart)
				{
					bestChance = entry.utilityBoardChancePercent;
					bestStart = entry.startSession;
				}
			}
			if (bestStart != int.MinValue) return bestChance;
		}
		return -1f;
	}

	/// <summary>
	/// Resolves a card's shop price from its prefab rarity (Common/Uncommon/Rare price refs).
	/// Deck-slot meter cards override this with the escalating meter price
	/// (base + step per already-made purchase this run). Returns 0 with a warning if the
	/// matching ref is not wired. Display (ShopCardView), buy and sell prices all funnel here.
	/// </summary>
	public int GetCardPrice(CardScript cardScript)
	{
		if (cardScript != null && cardScript.GetComponent<DeckSizeIncreaseEffect>() != null)
		{
			int purchases = deckSlotPurchasesRef != null ? deckSlotPurchasesRef.value : 0;
			return UtilityShopBonus.GetDeckSlotPrice(deckSlotBasePrice, deckSlotPriceStep, purchases);
		}
		IntSO priceRef = cardScript.rarity switch
		{
			EnumStorage.Rarity.Uncommon => UncommonPriceRef,
			EnumStorage.Rarity.Rare => RarePriceRef,
			_ => CommonPriceRef,
		};
		if (priceRef == null)
		{
			Debug.LogWarning($"[ShopManager] {cardScript.rarity}PriceRef not wired; returning price 0 for card '{cardScript.GetDisplayName()}'", this);
			return 0;
		}
		return priceRef.value;
	}

	[Header("UI objects")]
	public TextMeshProUGUI phaseInfoDisplay;
	public TextMeshProUGUI deckInfoDisplay;
	public TextMeshProUGUI shopInfoDisplay;
	public TextMeshProUGUI playerStatsDisplay;
	public GameObject rerollButton;
	public GameObject rerollButtonBg;
	public GameObject exitButton;
	public GameObject sectionIdentifier;
	private string _deckInfoStr = "Your Deck: \n\n";
	private string _shopInfoStr = "Shop: \n\n";

	private void Update()
	{
		if (gamePhaseRef.currentGamePhase != EnumStorage.GamePhase.Shop) return;
		//ShowDeck();
		//ShowShopItems();
		//ShowShopTips();
		ShowPlayerStats();

		// toggle sell/buy mode
		if (Input.GetKeyDown(KeyCode.S))
		{
			//sellMode = !sellMode;
		}

		// reroll
		if (Input.GetKeyDown(KeyCode.R))
		{
			//Reroll();
		}

		/*
		if (!sellMode) // buy mode TEMP
		{
			if (Input.GetKeyDown(KeyCode.Alpha1))
			{
				BuyFunc(0);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha2))
			{
				BuyFunc(1);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha3))
			{
				BuyFunc(2);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha4))
			{
				BuyFunc(3);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha5))
			{
				BuyFunc(4);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha6))
			{
				BuyFunc(5);
			}
		}
		else // sell mode TEMP
		{
			if (Input.GetKeyDown(KeyCode.Alpha1))
			{
				SellFunc(0);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha2))
			{
				SellFunc(1);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha3))
			{
				SellFunc(2);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha4))
			{
				SellFunc(3);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha5))
			{
				SellFunc(4);
			}
			else if (Input.GetKeyDown(KeyCode.Alpha6))
			{
				SellFunc(5);
			}
		}*/
	}

	public void BuyFunc(int itemIndex)
	{
		if (currentShopItemDeckRef.deck.Count - 1 < itemIndex) return; // check if item index valid
		var cardToBuy = currentShopItemDeckRef.deck[itemIndex]; // store card player tyring to buy
		var cardToBuyScript = cardToBuy.GetComponent<CardScript>();

		// Deck-slot meter card (v2): ceiling reached = stop selling (pipeline stops offering too;
		// this guards copies already sitting on the current board).
		bool isDeckSlotCard = cardToBuyScript.GetComponent<DeckSizeIncreaseEffect>() != null;
		if (isDeckSlotCard && deckSize != null && maxDeckSize != null && deckSize.value >= maxDeckSize.value)
		{
			return;
		}

		if (cardToBuyScript.takeUpSpace) // if card player trying to buy takes up space in deck
		{
			// Duplicate-slot rule: a copy of an already-owned cardTypeID costs no slot
			bool isFreeDuplicate = DuplicateCopiesShareSlot
				&& !string.IsNullOrEmpty(cardToBuyScript.cardTypeID)
				&& UtilityFuncManagerScript.DeckContainsCardType(playerDeckRef, cardToBuyScript.cardTypeID);
			if (!isFreeDuplicate)
			{
				int actualSize = UtilityFuncManagerScript.CountCardsTakingUpSpace(playerDeckRef, DuplicateCopiesShareSlot);
				if (actualSize >= deckSize.value) return; // check if player deck not full
			}
		}
		int buyPrice = GetEffectiveBuyPrice(cardToBuyScript);
		if (purse.value < buyPrice) return; // check if affordable
		purse.value -= buyPrice; // pay the price

		// Deck-slot meter card never enters the deck (self-exile): its onMeBought effect bumps the
		// run purchase counter + deckSize; the shop-entry formula reproduces the same deck size.
		if (!isDeckSlotCard)
		{
			// Add the card to player deck regardless of whether it takes up space
			playerDeckRef.deck.Add(cardToBuy);
			RefreshUtilityBonus();
			ApplyHpMaxFromDeck();
		}

		currentShopItemDeckRef.deck.Remove(cardToBuy); // remove it from current shop item list

		// Instantiate a temporary copy to fire onMeBought effects (e.g. IncreaseHpMax).
		// Non-space cards will still be destroyed on shop exit and skipped in combat.
		var cardToBuyInst = Instantiate(cardToBuy, transform);
		cardToBuyInst.GetComponent<CardScript>().myStatusRef = CombatManager.Me.ownerPlayerStatusRef;
		GameEventStorage.me?.onMeBought?.RaiseSpecific(cardToBuyInst); // buy timepoint: instantiate so it register as a listener
		_boughtCardInstances.Add(cardToBuyInst); // Add to list, destroy uniformly when exiting shop

		// record card bought (board type = the board the offer is sitting on)
		if (ShopStatsManager.Me != null)
		{
			var cardScript = cardToBuy.GetComponent<CardScript>();
			var cardTypeID = cardScript?.cardTypeID;
			if (!string.IsNullOrEmpty(cardTypeID))
			{
				ShopStatsManager.Me.RecordCardBought(cardTypeID, cardScript.GetDisplayName(), _currentBoardIsUtility);
			}
		}
		GatherPlayerDeckInfo();
		UpdateShopItemInfo();

		// Plan step 5: emphasize pulse on the bought card's deck instance when a utility
		// passive's effect (re)applies via the recompute (payday-time application happens
		// before deck instances exist, so the pulse only fires on buy/sell).
		if (cardToBuyScript.isPassive && cardToBuyScript.utilityKind != EnumStorage.UtilityKind.None)
		{
			ShopUXManager.Instance?.PulsePlayerCard(cardToBuyScript);
		}

		// Notify ShopUXManager to handle visual updates after purchase
		ShopUXManager.Instance?.OnCardPurchased(itemIndex);
	}
	public void SellFunc(int cardIndex, GameObject physicalCardInstance = null)
	{
		if (playerDeckRef.deck.Count - 1 < cardIndex) return; // check if card index valid
		var cardToSell = playerDeckRef.deck[cardIndex]; // store card player tyring to sell
		var cardScript = cardToSell.GetComponent<CardScript>();
		if (!cardScript.takeUpSpace) return; // non-space cards cannot be sold
		purse.value += GetCardPrice(cardScript) / 2; // get the money
		playerDeckRef.deck.Remove(cardToSell); // remove it from player deck
		RefreshUtilityBonus();
		ApplyHpMaxFromDeck();
		
		// Notify ShopUXManager to handle sell animation
		if (physicalCardInstance != null)
		{
			ShopUXManager.Instance?.OnCardSold(physicalCardInstance, cardIndex);
		}
		
		GatherPlayerDeckInfo();
		UpdateShopItemInfo();
	}

	public void EnterShop()
	{
		// Clean up possible residual instances (just in case)
		if (_boughtCardInstances.Count > 0)
		{
			foreach (var cardInst in _boughtCardInstances)
			{
				if (cardInst != null)
				{
					Destroy(cardInst);
				}
			}
			_boughtCardInstances.Clear();
		}
		
		// payday + baseline growth (utility recompute first: deckSize, hpMax, then payday)
		ResetVisitCounters();
		RefreshUtilityBonus();
		ApplyBaselineGrowth();
		purse.value += UtilityShopBonus.ComputePayday(payCheck.value, GetSessionNum(), incomePerSession, _utilityBonus);
		// process shop items and display
		GenerateShopItems();
		UpdateShopItemInfo();
		// process player deck and display
		GatherPlayerDeckInfo();
		// show reroll button
		rerollButton.SetActive(true);
		rerollButtonBg.SetActive(true);
		UpdateRerollButtonLabel();
		// show exit button
		exitButton.SetActive(true);
		// show section identifiers
		sectionIdentifier.SetActive(true);
		// record shop visit
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.RecordShopVisit();
		}
		// Note: No need to Flush here, Flush when exiting shop
	}

	public void ExitShop()
	{
		// DIAG-LOG(2026-08-08): tracing why the shop Exit button may appear dead
		TestManager.Log("[ShopButton] ExitShop() called. phase=" + (gamePhaseRef != null ? gamePhaseRef.currentGamePhase.ToString() : "null"));
		// Ensure statistics are saved
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.Flush();
		}

		// Destroy all cards instantiated during purchase
		foreach (var cardInst in _boughtCardInstances)
		{
			if (cardInst != null)
			{
				Destroy(cardInst);
			}
		}
		_boughtCardInstances.Clear();

		deckInfoDisplay.text = "";
		shopInfoDisplay.text = "";
		phaseInfoDisplay.text = "";
		playerStatsDisplay.text = "";
		rerollButton.SetActive(false);
		rerollButtonBg.SetActive(false);
		exitButton.SetActive(false);
		sectionIdentifier.SetActive(false);
	}

	private void GatherPlayerDeckInfo()
	{
		_deckInfoStr = "Your Deck:\n\n";
		//deckInstList.Clear();
		int displayIndex = 1;
		for (var i = 0; i < playerDeckRef.deck.Count; i++)
		{
			var card = playerDeckRef.deck[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.takeUpSpace) continue; // if card doesn't take up space, skip it
			_deckInfoStr +=
				"#" + displayIndex + " <size=+2><b>" + // number
				card.name + // name
				"</b></size>: " + GameColorPalette.Me.highlight.OpenTag + "$" + GetCardPrice(cardScript) / 2 + "</color>" + // price
				"\n" + cardScript.GetCardDescForDisplay() + "\n\n"; // desc
			displayIndex++;
		}
	}

	private void GenerateShopItems()
	{
		currentShopItemDeckRef.deck.Clear();
		_boardsGeneratedThisVisit++;
		int boardIndex = _boardsGeneratedThisVisit - 1; // first board of the visit = 0

		var bonus = _utilityBonus;
		var activeRarityWeightRef = GetActiveRarityWeightRef();

		// Weight layer: session table (this rarity) x shopRollWeightMultiplier x owned RarityWeight utility mults.
		float WeightOf(CardScript cardScript)
		{
			float sessionWeight = activeRarityWeightRef != null ? activeRarityWeightRef.GetWeight(cardScript.rarity) : 1f;
			float utilityMult = 1f;
			if (bonus != null && bonus.rarityWeightMults != null && bonus.rarityWeightMults.TryGetValue(cardScript.rarity, out float mult))
			{
				utilityMult = mult;
			}
			return sessionWeight * cardScript.shopRollWeightMultiplier * utilityMult;
		}

		int extraOptions = bonus != null ? bonus.extraShopOptions : 0;
		int ceiling = maxDeckSize != null ? maxDeckSize.value : int.MaxValue;
		bool deckSizeAtCeiling = deckSize != null && deckSize.value >= ceiling;

		// Full pipeline; reroll reruns all of it (board type re-rolled, boardIndex advances cadence).
		var board = ShopBoardPipeline.GenerateBoard(
			shopPoolRef != null ? shopPoolRef.deck : null,
			WeightOf,
			bonus,
			boardIndex,
			GetUtilityBoardChancePercent(),
			shopItemAmount + extraOptions,
			utilityBoardSlotCount + extraOptions,
			deckSizeAtCeiling,
			new System.Random());
		_currentBoardIsUtility = board.isUtilityBoard;

		foreach (var card in board.cards)
		{
			currentShopItemDeckRef.deck.Add(card);
			// record card appeared (board type feeds the stats utility-board share)
			if (ShopStatsManager.Me != null)
			{
				var cardScript = card.GetComponent<CardScript>();
				var cardTypeID = cardScript?.cardTypeID;
				if (!string.IsNullOrEmpty(cardTypeID))
				{
					ShopStatsManager.Me.RecordCardAppeared(cardTypeID, cardScript.GetDisplayName(), _currentBoardIsUtility);
				}
			}
		}
	}

	private void UpdateShopItemInfo()
	{
		// Plan step 5 readability: board-type marker (plan §1 "utility 板需视觉标识") + the
		// effective rarity weight table (placeholder for the hover tips).
		string header = "Shop:\n\n";
		if (_currentBoardIsUtility)
		{
			header += GameColorPalette.Me.heal.OpenTag + "◆ 奇物架 — 本架只陈列奇物</color>\n\n";
		}
		var weightRef = GetActiveRarityWeightRef();
		if (weightRef != null && weightRef.entries != null && weightRef.entries.Count > 0)
		{
			var parts = new System.Collections.Generic.List<string>();
			foreach (var e in weightRef.entries)
			{
				parts.Add(e.rarity + " x" + e.weight);
			}
			header += "<size=60%>权重 " + string.Join(" / ", parts) + "</size>\n\n";
		}
		_shopInfoStr = header;
		for (var i = 0; i < currentShopItemDeckRef.deck.Count; i++)
		{
			var card = currentShopItemDeckRef.deck[i];
			var cardScript = card.GetComponent<CardScript>();
			_shopInfoStr +=
				"#" + (i + 1) + " <size=+2><b>" + // number
				card.name + // name
				"</b></size>: " + GameColorPalette.Me.highlight.OpenTag + "$" + GetCardPrice(cardScript) + "</color>" + // price
				"\n" + cardScript.GetCardDescForDisplay() + "\n\n"; // desc
		}
	}

	private void ShowDeck()
	{
		deckInfoDisplay.text = _deckInfoStr;
	}

	private void ShowShopItems()
	{
		shopInfoDisplay.text = _shopInfoStr;
	}

	private void ShowShopTips()
	{
		string currentMode = sellMode ? GameColorPalette.Me.highlight.OpenTag + "Selling</color>" : GameColorPalette.Me.highlight.OpenTag + "Buying</color>";
		phaseInfoDisplay.text = phaseInfo + " Current: " + currentMode;
								
	}
	private void ShowPlayerStats()
	{
		int freeLeft = (_utilityBonus != null ? _utilityBonus.freeRerolls : 0) - _freeRerollsUsedThisVisit;
		playerStatsDisplay.text =
			"HP Max: " + GameColorPalette.Me.heal.OpenTag + CombatManager.Me.ownerPlayerStatusRef.hpMax + "</color>" +
			"\nYou have: " + GameColorPalette.Me.highlight.OpenTag + "$" + purse.value + "</color> (+$12/combat)" +
			(freeLeft > 0 ? "\nFree Rerolls: " + GameColorPalette.Me.heal.OpenTag + freeLeft + "</color>" : "");
	}

	/// <summary>
	/// Plan step 5: reroll button shows whether the next reroll is free (and how many remain).
	/// </summary>
	private void UpdateRerollButtonLabel()
	{
		if (rerollButton == null) return;
		var label = rerollButton.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
		if (label == null) return;
		int freeLeft = (_utilityBonus != null ? _utilityBonus.freeRerolls : 0) - _freeRerollsUsedThisVisit;
		label.text = freeLeft > 0
			? "Reroll: 免费 x" + freeLeft
			: "Reroll: $" + (RerollPriceRef != null ? RerollPriceRef.value : 0);
	}

	public void Reroll()
	{
		// DIAG-LOG(2026-08-08): tracing why the shop Reroll button may appear dead
		TestManager.Log("[ShopButton] Reroll() clicked. phase=" + (gamePhaseRef != null ? gamePhaseRef.currentGamePhase.ToString() : "null") + " purse=" + (purse != null ? purse.value : -1) + " price=" + (RerollPriceRef != null ? RerollPriceRef.value : -1));
		int freeLeft = (_utilityBonus != null ? _utilityBonus.freeRerolls : 0) - _freeRerollsUsedThisVisit;
		bool isFree = freeLeft > 0;
		if (!isFree && (RerollPriceRef == null || purse.value < RerollPriceRef.value))
		{
			TestManager.Log("[ShopButton] Reroll() early return: cost not met (free left=" + freeLeft + "). purse=" + (purse != null ? purse.value : -1) + " price=" + (RerollPriceRef != null ? RerollPriceRef.value : -1));
			return;
		}

		// Free rerolls are consumed first and still count toward discount / reserved-slot cadence.
		_rerollsThisVisit++;
		if (isFree)
		{
			_freeRerollsUsedThisVisit++;
		}
		else
		{
			purse.value -= RerollPriceRef.value;
		}

		// First generate new shop item data
		GenerateShopItems();
		ApplyBoardDiscount();
		UpdateShopItemInfo();
		UpdateRerollButtonLabel();
		// record reroll
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.RecordReroll();
		}
		TestManager.Log("[ShopButton] Reroll() succeeded (free=" + isFree + "). shopItems=" + (currentShopItemDeckRef != null ? currentShopItemDeckRef.deck.Count : -1) + " ShopUXManager.Instance=" + (ShopUXManager.Instance != null ? "exists" : "NULL"));

		// Notify ShopUXManager to handle reroll animation and regenerate physical cards
		ShopUXManager.Instance?.OnReroll();
	}

	/// <summary>
	/// Recomputes utility contributions from the current player deck. Call after any deck change.
	/// </summary>
	private void RefreshUtilityBonus()
	{
		_utilityBonus = UtilityShopBonus.Compute(playerDeckRef != null ? playerDeckRef.deck : null);
	}

	private int GetSessionNum()
	{
		return sessionNum != null ? sessionNum.value : 0;
	}

	/// <summary>
	/// Applies baseline growth at shop entry: deckSize formula (deckSizeOg + per-session +
	/// purchases, clamped to the static maxDeckSize ceiling) and the hpMax recompute.
	/// </summary>
	private void ApplyBaselineGrowth()
	{
		int session = GetSessionNum();
		if (deckSize != null)
		{
			int purchases = deckSlotPurchasesRef != null ? deckSlotPurchasesRef.value : 0;
			int ceiling = maxDeckSize != null ? maxDeckSize.value : 16;
			deckSize.value = UtilityShopBonus.ComputeDeckSize(deckSize.valueOg, session, deckSizePerSession, purchases, ceiling);
			ShopUXManager.Instance?.SpawnAdditionalEmptySpaces();
		}
		ApplyHpMaxFromDeck();
	}

	/// <summary>
	/// hpMax = hpMaxOg + per-session baseline + sum(HP utility cards); hp clamps down if above
	/// max. Never lethal: hpMaxOg >= 1 and bonuses are >= 0.
	/// </summary>
	private void ApplyHpMaxFromDeck()
	{
		var status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
		if (status == null) return;
		status.hpMax = UtilityShopBonus.ComputeHpMax(status.hpMaxOg, GetSessionNum(), hpMaxPerSession, _utilityBonus);
		status.hp = Mathf.Min(status.hp, status.hpMax);
	}

	private void ResetVisitCounters()
	{
		_rerollsThisVisit = 0;
		_freeRerollsUsedThisVisit = 0;
		_boardsGeneratedThisVisit = 0;
		_boardDiscounts.Clear();
	}

	/// <summary>
	/// Settles reroll discounts onto the freshly generated board: every discount spec whose
	/// cadence hits this reroll number adds its gold-off onto ONE random board card. Discounts
	/// never accumulate across rerolls (board regenerates, dict cleared) and never apply to the
	/// initial board of a visit (only Reroll() settles).
	/// </summary>
	private void ApplyBoardDiscount()
	{
		_boardDiscounts.Clear();
		if (_utilityBonus == null || currentShopItemDeckRef == null) return;
		int totalOff = 0;
		foreach (var spec in _utilityBonus.rerollDiscounts)
		{
			if (spec.everyRerolls > 0 && _rerollsThisVisit % spec.everyRerolls == 0)
			{
				totalOff += spec.goldOff;
			}
		}
		if (totalOff <= 0 || currentShopItemDeckRef.deck.Count == 0) return;
		int index = Random.Range(0, currentShopItemDeckRef.deck.Count);
		var script = currentShopItemDeckRef.deck[index] != null ? currentShopItemDeckRef.deck[index].GetComponent<CardScript>() : null;
		if (script != null)
		{
			_boardDiscounts[script] = totalOff;
		}
	}

	/// <summary>Active board discount (gold off) for this card, 0 when none. Read by ShopCardView for the struck-through price display; buy settlement uses GetEffectiveBuyPrice.</summary>
	public int GetBoardDiscount(CardScript cardScript)
	{
		return cardScript != null && _boardDiscounts.TryGetValue(cardScript, out int off) ? off : 0;
	}

	/// <summary>Buy price with any active board discount applied.</summary>
	public int GetEffectiveBuyPrice(CardScript cardScript)
	{
		int price = GetCardPrice(cardScript);
		if (cardScript != null && _boardDiscounts.TryGetValue(cardScript, out int off))
		{
			price = Mathf.Max(0, price - off);
		}
		return price;
	}
}
