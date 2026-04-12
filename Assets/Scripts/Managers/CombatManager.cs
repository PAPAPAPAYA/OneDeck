using System;
using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using UnityEngine;

[RequireComponent(typeof(CombatInfoDisplayer))]
[RequireComponent(typeof(CombatFuncs))]
// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
	#region SINGLETON

	public static CombatManager Me;

	private void Awake()
	{
		Me = this;
	}

	#endregion

	[Header("PHASE AND STATE REFS")]
	public GamePhaseSO currentGamePhaseRef;
	public EnumStorage.CombatState currentCombatState;

	[Header("PLAYER STATUS REFS")]
	public PlayerStatusSO ownerPlayerStatusRef;
	public PlayerStatusSO enemyPlayerStatusRef;

	[Header("DECK REFS")]
	public DeckSO playerDeck;
	public DeckSO enemyDeck;
	public GameObject playerDeckParent;
	public GameObject enemyDeckParent;

	[Header("START CARD")]
	public GameObject startCardPrefab; // Start Card prefab
	private GameObject _startCardInstance; // Start Card instance (at the bottom of deck)
	[Tooltip("If true, Start Card is removed directly after triggering, not shuffled into deck")]
	public bool removeStartCardInsteadOfShuffle = false;

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public GameObject revealZone;

	[Header("FLOW")]
	public bool awaitingRevealConfirm = true;
	public BoolSO combatFinished; // identify if this session of combat is finished
	public int cardsRevealedThisRound; // tracks how many cards have been revealed this round (for display numbering)
	[Tooltip("Block player input when playing Stage/Bury animation")]
	public bool blockPlayerInput = false;

	[Header("SUPPLEMENT COMPONENTS")]
	private CombatInfoDisplayer _infoDisplayer;
	private CombatFuncs _combatFuncs;

	[Header("OVERTIME")]
	public IntSO roundNumRef;
	public int overtimeRoundThreshold;
	public GameObject cardToAddWhenOvertime;
	[Tooltip("Add this amount of fatigue to both players")]
	public int fatigueAmount;
	
	[Header("FATIGUE BY REVEAL COUNT")]
	[Tooltip("Fatigue trigger threshold based on total cards revealed (0 means disabled)")]
	public int fatigueRevealThreshold;
	[Tooltip("Total cards revealed count")]
	public int totalCardsRevealed;

	[Header("POWER EVENT")]
	[Tooltip("Tracks the last card that received Power status effect for reaction effects")]
	public CardScript lastCardGotPower;

	#region Enter and exit funcs

	public void EnterCombat()
	{
		currentCombatState = EnumStorage.CombatState.GatherDeckLists;
		combatFinished.value = false;
	}

	public void ExitCombat()
	{
		// Stop all attack animations
		AttackAnimationManager.me?.StopAllAttackAnimations();
		
		// clean up ui
		_infoDisplayer.ClearInfo();
		// clean up combined deck
		foreach (var cardInstance in combinedDeckZone)
		{
			Destroy(cardInstance);
		}

		combinedDeckZone.Clear();
		Destroy(revealZone);
		revealZone = null;
		
		// clean up start card
		if (_startCardInstance != null)
		{
			Destroy(_startCardInstance);
			_startCardInstance = null;
		}
		
		// clean up tracking stats
		roundNumRef.value = 0;
		cardsRevealedThisRound = 0;
		totalCardsRevealed = 0;
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.chainNumber = 0;
	}

	#endregion

	private void OnEnable()
	{
		_infoDisplayer = GetComponent<CombatInfoDisplayer>();
		_combatFuncs = GetComponent<CombatFuncs>();
	}

	private void Update()
	{
		if (currentGamePhaseRef.Value() != EnumStorage.GamePhase.Combat) return;
		switch (currentCombatState)
		{
			case EnumStorage.CombatState.GatherDeckLists:
				GatherDecks();
				break;
			case EnumStorage.CombatState.ShuffleDeck:
				CheckFatigueNAddFatigue(); // Fatigue check based on round number
				Shuffle();
				break;
			case EnumStorage.CombatState.Reveal:
				RevealCards();
				break;
		}
	}

	public void GatherDecks() // collect player and enemy decks and instantiate cards
	{
		combinedDeckZone.Clear();
		foreach (var card in playerDeck.deck)
		{
			var cardInstance = Instantiate(card, playerDeckParent.transform);
			cardInstance.name = cardInstance.name.Replace("(Clone)", "");
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = ownerPlayerStatusRef;
			cardInstanceScript.theirStatusRef = enemyPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		foreach (var card in enemyDeck.deck)
		{
			var cardInstance = Instantiate(card, enemyDeckParent.transform);
			cardInstance.name = cardInstance.name.Replace("(Clone)", "");
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = enemyPlayerStatusRef;
			cardInstanceScript.theirStatusRef = ownerPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		// Instantiate Start Card and add to the bottom of deck
		_startCardInstance = Instantiate(startCardPrefab, playerDeckParent.transform);
		_startCardInstance.name = "Start Card";
		var startCardScript = _startCardInstance.GetComponent<CardScript>();
		if (startCardScript != null)
		{
			startCardScript.isStartCard = true;
		}
		combinedDeckZone.Add(_startCardInstance);

		_infoDisplayer.RefreshDeckInfo();
		GameEventStorage.me.beforeRoundStart.Raise(); // timepoint

		// Record player deck snapshot (for win rate stats) - query directly from playerDeck, no need to wait for instantiation
		TestWriteRead.CardWinRateTracker.Me?.RecordPlayerDeckSnapshot(playerDeck.deck);

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	private void CheckFatigueNAddFatigue()
	{
		// Fatigue check based on round number (called in ShuffleDeck phase)
		if (roundNumRef.value <= overtimeRoundThreshold) return;
		
		var msg = $"<color=red>FATIGUE!</color> Round {roundNumRef.value} > {overtimeRoundThreshold}";
		print(msg);
		_infoDisplayer.effectResultString.value += msg + "\n";
		AddFatigueCards();
	}
	
	private void CheckFatigueByRevealCount()
	{
		// Reveal count-based fatigue check (called each reveal)
		// disabled when threshold
		if (fatigueRevealThreshold <= 0) return;
		if (totalCardsRevealed < fatigueRevealThreshold) return;
		// Only trigger when revealed card count equals threshold exactly (to avoid duplicate triggers)
		if (totalCardsRevealed != fatigueRevealThreshold) return;
		
		var msg = $"<color=red>FATIGUE!</color> Revealed {totalCardsRevealed} cards";
		print(msg);
		_infoDisplayer.effectResultString.value += msg + "\n";
		AddFatigueCards();
	}
	
	private void AddFatigueCards()
	{
		// add fatigue to owner side
		for (var i = 0; i < fatigueAmount; i++)
		{
			_combatFuncs.AddCardInTheMiddleOfCombat(cardToAddWhenOvertime, true);
		}

		// add fatigue to enemy side
		for (var i = 0; i < fatigueAmount; i++)
		{
			_combatFuncs.AddCardInTheMiddleOfCombat(cardToAddWhenOvertime, false);
		}
	}

	public void Shuffle()
	{
		// Shuffle directly (Start Card is already in the deck)
		combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);

		_infoDisplayer.RefreshDeckInfo();
		GameEventStorage.me.afterShuffle.Raise(); // TIMEPOINT: after shuffle

		CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
		CombatUXManager.me.UpdateAllPhysicalCardTargets();

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	public int GetCurrentDeckSize()
	{
		return combinedDeckZone.Count;
	}

	/// <summary>
	/// Check if card should be skipped by effects (Start Card and other neutral cards)
	/// Unified entry point for easy extension of other neutral card types
	/// </summary>
	public static bool ShouldSkipEffectProcessing(CardScript card)
	{
		if (card == null) return true;
		return card.IsNeutralCard;
	}

	/// <summary>
	/// Get the count of cards that actually participate in effect calculation in the deck (excluding Start Card)
	/// </summary>
	public int GetEffectiveDeckSize()
	{
		int count = 0;
		foreach (var card in combinedDeckZone)
		{
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript != null && !cardScript.IsNeutralCard)
			{
				count++;
			}
		}
		return count;
	}

	public void ResetCardsRevealedCount()
	{
		cardsRevealedThisRound = 0;
	}

	/// <summary>
	/// Wait for attack animation to complete before revealing next
	/// </summary>
	private System.Collections.IEnumerator WaitForAttackAnimationsBeforeNextReveal()
	{
		// If there are pending attack animations, wait
		while (AttackAnimationManager.me != null && AttackAnimationManager.me.HasPendingAnimations())
		{
			yield return null;
		}
	}

	private void RevealCards()
	{
		if (blockPlayerInput) return;
		
		// If attack animation is playing, wait
		if (AttackAnimationManager.me != null && AttackAnimationManager.me.isPlayingAttackAnimation)
		{
			return;
		}

		_infoDisplayer.RefreshDeckInfo();

		// ========== Round start: automatically reveal Start Card (player sees it directly) ==========
		if (revealZone == null && cardsRevealedThisRound == 0)
		{
			CombatUXManager.me.InstantiateAllPhysicalCards();
			
			// Reveal Start Card (it's at the bottom of the deck)
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // Enter Start Card effect trigger phase
			}
			return;
		}

		// ========== Phase 1: Wait to process current card and reveal next ==========
		if (awaitingRevealConfirm)
		{
			// Combat end check
			if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				HandleCombatFinished();
				return;
			}

			// Prompt text
			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to reveal next card";
			
			CombatUXManager.me.InstantiateAllPhysicalCards();
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
			_infoDisplayer.effectResultString.value = "";

			// 1. Put current card back to bottom of deck
			PutRevealedCardToBottom();

			// 2. Reveal next card (if any)
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // Enter effect trigger phase
			}

			EffectChainManager.Me.CloseOpenedChain();
		}
		// ========== Phase 2: Wait to trigger current card effect ==========
		else
		{
			// Check if current card is valid
			if (revealZone == null)
			{
				awaitingRevealConfirm = true;
				return;
			}

			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to trigger effect";
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

			// Start Card special handling: trigger effect = shuffle + new round
			if (IsRevealedCardStartCard())
			{
				TriggerStartCardEffect();
			}
			else
			{
				// Normal card triggers effect
				TriggerRevealedCardEffect();
			}
			
			awaitingRevealConfirm = true;
			EffectChainManager.Me.CloseOpenedChain();
			
			// Wait for all attack animations to complete before allowing next operation
			StartCoroutine(WaitForAttackAnimationsBeforeNextReveal());
		}
	}

	// ========== Helper Methods ==========

	private void RevealNextCard()
	{
		var cardRevealed = combinedDeckZone[^1].GetComponent<CardScript>();
		revealZone = combinedDeckZone[^1];
		combinedDeckZone.RemoveAt(combinedDeckZone.Count - 1);

		// Physical movement: from deck to reveal zone
		var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(cardRevealed);
		if (physicalCard != null)
		{
			CombatUXManager.me.MovePhysicalCardToRevealZone(physicalCard);
		}

		// Display info (don't trigger effect)
		cardsRevealedThisRound++;
		totalCardsRevealed++;
		_infoDisplayer.ShowCardInfo(cardRevealed, cardsRevealedThisRound, cardRevealed.myStatusRef == ownerPlayerStatusRef);
		_infoDisplayer.RefreshDeckInfo();
		
		// Check fatigue (based on reveal card count)
		CheckFatigueByRevealCount();

		// Record combat stats
		GetComponent<CombatStatsLogger>()?.OnCardRevealed(cardRevealed);
	}

	private void TriggerRevealedCardEffect()
	{
		if (revealZone == null) return;
		
		var cardScript = revealZone.GetComponent<CardScript>();
		if (cardScript == null) return;

		GameEventStorage.me.onAnyCardRevealed.Raise();
		GameEventStorage.me.onMeRevealed.RaiseSpecific(revealZone);
		
		// Check if it's an enemy curse card
		if (GameEventStorage.me.curseCardTypeID != null &&
			    !string.IsNullOrEmpty(GameEventStorage.me.curseCardTypeID.value) &&
		    cardScript.cardTypeID == GameEventStorage.me.curseCardTypeID.value)
		{
			if (cardScript.myStatusRef == enemyPlayerStatusRef)
			{
				GameEventStorage.me.onEnemyCurseCardRevealed.RaiseOwner();
			}
			else
			{
				GameEventStorage.me.onEnemyCurseCardRevealed.RaiseOpponent();
			}
		}

		// Check if it's a hostile card
		if (cardScript.myStatusRef == enemyPlayerStatusRef) // card belongs to enemy
		{
			GameEventStorage.me.onHostileCardRevealed.RaiseOwner();
		}
		else // card belongs to session owner (player)
		{
			GameEventStorage.me.onHostileCardRevealed.RaiseOpponent();
		}
		
		_infoDisplayer.RefreshDeckInfo();
	}

	private void PutRevealedCardToBottom()
	{
		if (revealZone == null) return;

		var cardToBottom = revealZone;
		revealZone = null;

		// Put back to bottom of deck (index 0)
		combinedDeckZone.Insert(0, cardToBottom);
		CombatUXManager.me.MoveRevealedCardToBottom(cardToBottom);
	}

	private void TriggerStartCardEffect()
	{
		if (revealZone == null) return;

		var startCard = revealZone;
		revealZone = null;

		// Determine animation and subsequent handling based on config
		if (removeStartCardInsteadOfShuffle)
		{
			// Remove Start Card from deck (it won't participate in Shuffle)
			combinedDeckZone.Remove(startCard);
			
			// Play simultaneously: Start Card exit animation + other cards Shuffle animation
			CombatUXManager.me.PlayStartCardExitWithShuffleAnimation(startCard, combinedDeckZone, () =>
			{
				// Destroy logical card
				Destroy(startCard);
				_startCardInstance = null;
				
				// Logically execute Shuffle (Start Card is already gone)）
				combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);
				
				// Refresh UI display
				_infoDisplayer.RefreshDeckInfo();
				GameEventStorage.me.afterShuffle.Raise();
				
				// New round start
				HandleNewRoundStart();
			});
		}
		else
		{
			// ========== Plan B: Execute logical shuffle first, then play animation ==========
			
			// 1. First add Start Card to deck (prepare to participate in shuffle)
			// Start Card is currently not in combinedDeckZone (it's in revealZone), needs to be added back
			combinedDeckZone.Add(startCard);
			
			// 2. [Key] Execute logical shuffle first, determine each card's position
			combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);
			
			// 3. Play animation based on known shuffle result
			// Start Card flies directly from Reveal Zone to new position, other cards fly from old to new position
			CombatUXManager.me.PlayStartCardShuffleAnimation(startCard, combinedDeckZone, () =>
			{
				// After animation completes, refresh UI and trigger event
				_infoDisplayer.RefreshDeckInfo();
				GameEventStorage.me.afterShuffle.Raise();
				
				// New round start
				HandleNewRoundStart();
			});
		}
	}

	private void HandleNewRoundStart()
	{
		// Round number increment
		roundNumRef.value++;
		cardsRevealedThisRound = 0;
		_infoDisplayer.ClearInfo();
		
		// Physical card reset
		CombatUXManager.me.ReviveAllPhysicalCards();
		
		// Round start event
		GameEventStorage.me.beforeRoundStart.Raise();
	}

	private void HandleCombatFinished()
	{
		if (combatFinished.value) return;

		_infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\nTAP / SPACE to continue";
		if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

		combatFinished.value = true;
		CombatUXManager.me.ClearAllPhysicalCards();
	}

	private bool IsRevealedCardStartCard()
	{
		if (revealZone == null) return false;
		var cardScript = revealZone.GetComponent<CardScript>();
		return cardScript != null && cardScript.isStartCard;
	}
}
