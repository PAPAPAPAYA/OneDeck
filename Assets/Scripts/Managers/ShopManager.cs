using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;
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
	[Tooltip("Payday growth added per completed growth step.")]
	public int incomeGrowthPerStep = 2;
	[Min(1)]
	[Tooltip("Sessions per payday growth step. 1 = grow every session (legacy). A step lands at session N, 2N, ...")]
	public int sessionsPerIncomeStep = 1;
	[Tooltip("hpMax growth added per completed growth step, applied on top of hpMaxOg at shop entry.")]
	public int hpMaxGrowthPerStep = 2;
	[Min(1)]
	[Tooltip("Sessions per hpMax growth step.")]
	public int sessionsPerHpMaxStep = 1;
	[Tooltip("Deck size growth added per completed growth step, applied on top of deckSizeOg at shop entry.")]
	public int deckSizeGrowthPerStep = 1;
	[Min(1)]
	[Tooltip("Sessions per deck-size growth step.")]
	public int sessionsPerDeckSizeStep = 1;
	[Tooltip("Price of the first deck-slot purchase of a run; each prior purchase adds deckSlotPriceStep.")]
	public int deckSlotBasePrice = 4;
	[Tooltip("Price increase per already-made deck-slot purchase this run.")]
	public int deckSlotPriceStep = 2;
	[Tooltip("Run-persistent deck slot purchase counter (meter price + deckSize formula). Reset at run start.")]
	public IntSO deckSlotPurchasesRef;
	[Header("Utility Board Split (plan v2)")]
	[Tooltip("True = legacy board-type split (combat vs utility board roll). False (default, 2026-09-11) = mixed pool: utility and combat cards share one pool, no board-type roll (utilityBoardSlotCount and sessionUtilityBoardChances dormant).")]
	public bool splitUtilityCombatBoards = false;
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
	/// Roll odds (percent) of the active rarity weight table, for the shop chrome
	/// rarity chips. Zero total (or no table) yields 0/0/0.
	/// </summary>
	public void GetRarityOddsPercents(out float commonPct, out float uncommonPct, out float rarePct)
	{
		ShopRarityWeightSO active = GetActiveRarityWeightRef();
		if (active != null)
		{
			active.GetOddsPercents(out commonPct, out uncommonPct, out rarePct);
			return;
		}
		commonPct = uncommonPct = rarePct = 0f;
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
		return GetCardPrice(cardScript, cardScript != null ? cardScript.GetComponentInChildren<DeckSizeIncreaseEffect>(true) : null);
	}

	/// <summary>Overload for callers that cache the slot-effect probe (per-frame price displays).</summary>
	public int GetCardPrice(CardScript cardScript, DeckSizeIncreaseEffect slotEffect)
	{
		if (cardScript != null && slotEffect != null)
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
	public GameObject sectionIdentifier;

	private void Update()
	{
		if (gamePhaseRef.currentGamePhase != EnumStorage.GamePhase.Shop) return;

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
		bool isDeckSlotCard = cardToBuyScript.GetComponentInChildren<DeckSizeIncreaseEffect>(true) != null;
		if (isDeckSlotCard && deckSize != null && maxDeckSize != null && deckSize.value >= maxDeckSize.value)
		{
			return;
		}

		if (cardToBuyScript.occupiesDeckSlot) // if card player trying to buy consumes a deckSize slot
		{
			// Duplicate-slot rule: a copy of an already-owned cardTypeID costs no slot
			bool isFreeDuplicate = DuplicateCopiesShareSlot
				&& !string.IsNullOrEmpty(cardToBuyScript.cardTypeID)
				&& UtilityFuncManagerScript.DeckContainsCardType(playerDeckRef, cardToBuyScript.cardTypeID);
			if (!isFreeDuplicate)
			{
				int actualSize = UtilityFuncManagerScript.CountSlotOccupyingCards(playerDeckRef, DuplicateCopiesShareSlot);
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
				RunRecorder.OnCardBought(cardTypeID); // Async-PvP run journal (plan §2.6)
			}
		}
		ShopChrome.RefreshIfActive();

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
		if (!cardScript.physicalDeckCard) return; // non-physical cards cannot be sold
		purse.value += GetCardPrice(cardScript) / 2; // get the money
		playerDeckRef.deck.Remove(cardToSell); // remove it from player deck
		RefreshUtilityBonus();
		ApplyHpMaxFromDeck();
		
		// Notify ShopUXManager to handle sell animation
		if (physicalCardInstance != null)
		{
			ShopUXManager.Instance?.OnCardSold(physicalCardInstance, cardIndex);
		}

		ShopChrome.RefreshIfActive();
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
		RunRecorder.OnShopEnter(purse.value); // Async-PvP: goldEnter snapshot before payday (plan §2.6)
		ResetVisitCounters();
		RefreshUtilityBonus();
		ApplyBaselineGrowth();
		purse.value += GetCurrentPayday();
		RunRecorder.OnPayday(purse.value); // Async-PvP: goldAfterPayday snapshot before spending (plan §2.6)
		// process shop items and display
		GenerateShopItems();
		ApplyBoardDiscount(); // initial board rolls discounts too (2026-09-11 probability rework)
		// show + refresh the world chrome (payday / baseline growth are final by here)
		ShopChrome.ShowIfActive();
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

		ShopChrome.HideIfActive();
		sectionIdentifier.SetActive(false);
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

		// Full pipeline; reroll reruns all of it (board type re-rolled, reserved/chance rolls
		// re-rolled per board). Mixed pool (splitUtilityCombatBoards = false) skips the
		// board-type roll entirely.
		var board = ShopBoardPipeline.GenerateBoard(
			shopPoolRef != null ? shopPoolRef.deck : null,
			WeightOf,
			bonus,
			boardIndex,
			GetUtilityBoardChancePercent(),
			shopItemAmount + extraOptions,
			utilityBoardSlotCount + extraOptions,
			!splitUtilityCombatBoards,
			deckSizeAtCeiling,
			Rng.Channel(RngChannel.Shop));
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
					RunRecorder.OnCardOffered(cardTypeID, _currentBoardIsUtility); // Async-PvP run journal (plan §2.6)
				}
			}
		}
	}

	/// <summary>Free rerolls left this visit (utility bonus minus the ones used).</summary>
	public int FreeRerollsLeft => (_utilityBonus != null ? _utilityBonus.freeRerolls : 0) - _freeRerollsUsedThisVisit;

	/// <summary>Paid reroll price (0 when the ref is unwired).</summary>
	public int RerollPrice => RerollPriceRef != null ? RerollPriceRef.value : 0;

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

		// Free rerolls are consumed first and still count toward discount / reserved-slot rolls.
		if (isFree)
		{
			_freeRerollsUsedThisVisit++;
		}
		else
		{
			purse.value -= RerollPriceRef.value;
		}
		ShopChrome.RefreshIfActive();

		// First generate new shop item data
		GenerateShopItems();
		ApplyBoardDiscount();
		// record reroll
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.RecordReroll();
		}
		RunRecorder.OnReroll(); // Async-PvP run journal (plan §2.6)
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
	/// Paycheck the shop pays on entry: base payCheck + baseline growth + Income utility.
	/// Same formula as the EnterShop payday; shared by the payday and the live income display.
	/// </summary>
	public int GetCurrentPayday()
	{
		return UtilityShopBonus.ComputePayday(payCheck.value, GetSessionNum(), incomeGrowthPerStep, sessionsPerIncomeStep, _utilityBonus);
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
			deckSize.value = UtilityShopBonus.ComputeDeckSize(deckSize.valueOg, session, deckSizeGrowthPerStep, sessionsPerDeckSizeStep, purchases, ceiling);
			ShopUXManager.Instance?.SpawnAdditionalEmptySpaces();
		}
		ApplyHpMaxFromDeck();
	}

	/// <summary>
	/// hpMax = hpMaxOg + per-session baseline + sum(HP utility cards). Shop invariant:
	/// the player is always at full HP during the shop phase, so hp is set to the new
	/// max on every recompute (shop entry / buy / sell). Never lethal: hpMaxOg >= 1, bonuses >= 0.
	/// </summary>
	private void ApplyHpMaxFromDeck()
	{
		var status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
		if (status == null) return;
		status.hpMax = UtilityShopBonus.ComputeHpMax(status.hpMaxOg, GetSessionNum(), hpMaxGrowthPerStep, sessionsPerHpMaxStep, _utilityBonus);
		status.hp = status.hpMax;
	}

	private void ResetVisitCounters()
	{
		_freeRerollsUsedThisVisit = 0;
		_boardsGeneratedThisVisit = 0;
		_boardDiscounts.Clear();
	}

	/// <summary>
	/// Settles board discounts onto the freshly generated board (probability model 2026-09-11):
	/// every discount spec rolls its chance once per generated board - initial board included,
	/// EnterShop and Reroll both settle - and the summed percent-off lands on ONE random board
	/// card as a HALF PRICE ROUNDS UP gold-off. Discounts never accumulate across rerolls
	/// (board regenerates, dict cleared).
	/// </summary>
	private void ApplyBoardDiscount()
	{
		_boardDiscounts.Clear();
		if (_utilityBonus == null || currentShopItemDeckRef == null) return;
		int percentOff = UtilityShopBonus.RollBoardDiscountOffPercent(_utilityBonus, Rng.Channel(RngChannel.Shop));
		if (percentOff <= 0 || currentShopItemDeckRef.deck.Count == 0) return;
		int index = Rng.Next(RngChannel.Shop, currentShopItemDeckRef.deck.Count);
		var script = currentShopItemDeckRef.deck[index] != null ? currentShopItemDeckRef.deck[index].GetComponent<CardScript>() : null;
		if (script != null)
		{
			int off = UtilityShopBonus.GoldOffForPercent(GetCardPrice(script), percentOff);
			if (off > 0)
			{
				_boardDiscounts[script] = off;
			}
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
