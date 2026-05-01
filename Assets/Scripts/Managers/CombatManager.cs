using System;
using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using UnityEngine;

[RequireComponent(typeof(CombatInfoDisplayer))]
[RequireComponent(typeof(CombatFuncs))]
[RequireComponent(typeof(CardFactory))]
// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
	#region SINGLETON

	public static CombatManager Me;

	/// <summary>
	/// Visual system interface. Logic layer should use this instead of CombatUXManager directly.
	/// Lazily initialized to avoid dependency on Awake execution order.
	/// </summary>
	public ICombatVisuals visuals
	{
		get
		{
			if (_visuals == null)
				_visuals = CombatUXManager.visuals;
			return _visuals;
		}
	}
	private ICombatVisuals _visuals;

	private void Awake()
	{
		Me = this;
	}

	/// <summary>
	/// Event raised when damage is dealt and attack animation should play.
	/// Parameters: attackerCard, isAttackingEnemy, onHit, onComplete
	/// </summary>
	public event Action<GameObject, bool, Action, Action> onDamageDealt;

	/// <summary>
	/// Raise onDamageDealt event. Called by HPAlterEffect to request attack animation.
	/// </summary>
	public void RaiseDamageDealtEvent(GameObject attackerCard, bool isAttackingEnemy, Action onHit, Action onComplete)
	{
		onDamageDealt?.Invoke(attackerCard, isAttackingEnemy, onHit, onComplete);
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

	[Header("STATUS EFFECT EVENT")]
	[Tooltip("Tracks the last card that received any status effect for reaction effects")]
	public CardScript lastCardGotStatusEffect;

	[Header("POWER EVENT")]
	[Tooltip("Tracks the last card that received Power status effect for reaction effects")]
	public CardScript lastCardGotPower;

	[Header("SHUFFLE EVENT TIMING")]
	[Tooltip("Delay afterShuffle event until the first card is revealed after shuffle")]
	private bool _raiseAfterShuffleOnNextReveal;

	#region Enter and exit funcs

	public void EnterCombat()
	{
		currentCombatState = EnumStorage.CombatState.GatherDeckLists;
		combatFinished.value = false;
	}

	public void ExitCombat()
	{
		// Stop all attack animations
		visuals?.StopAllAnimations();
		
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
			case EnumStorage.CombatState.Reveal:
				RevealCards();
				break;
		}
	}

	public void GatherDecks() // collect player and enemy decks and instantiate cards
	{
		combinedDeckZone.Clear();

		// Use CardFactory for consistent logical card creation
		var factory = CardFactory.me;
		if (factory == null)
		{
			Debug.LogError("[CombatManager] CardFactory is not available!");
			return;
		}

		foreach (var card in playerDeck.deck)
		{
			var cardInstance = factory.CreateLogicalCard(card, ownerPlayerStatusRef, enemyPlayerStatusRef, playerDeckParent.transform);
			if (cardInstance != null)
				combinedDeckZone.Add(cardInstance);
		}

		foreach (var card in enemyDeck.deck)
		{
			var cardInstance = factory.CreateLogicalCard(card, enemyPlayerStatusRef, ownerPlayerStatusRef, enemyDeckParent.transform);
			if (cardInstance != null)
				combinedDeckZone.Add(cardInstance);
		}

		// Instantiate Start Card and add to the bottom of deck
		_startCardInstance = factory.CreateStartCard(startCardPrefab, playerDeckParent.transform);
		if (_startCardInstance != null)
			combinedDeckZone.Add(_startCardInstance);

		_infoDisplayer.RefreshDeckInfo();
		GameEventStorage.me.beforeRoundStart.Raise(); // timepoint

		// Record player deck snapshot (for win rate stats) - query directly from playerDeck, no need to wait for instantiation
		TestWriteRead.CardWinRateTracker.Me?.RecordPlayerDeckSnapshot(playerDeck.deck);

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	private void CheckFatigueNAddFatigue()
	{
		// Fatigue check based on round number (called before Start Card shuffle)
		if (roundNumRef.value <= overtimeRoundThreshold) return;
		
		var msg = $"<color=red>疲劳!</color> 回合{roundNumRef.value} > {overtimeRoundThreshold}";
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
		
		var msg = $"<color=red>疲劳!</color> 已揭示{totalCardsRevealed}张卡牌";
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


	private void ResetShuffleTrackers()
	{
		if (ValueTrackerManager.me == null) return;
		if (ValueTrackerManager.me.ownerCardsBuriedCountRef != null)
			ValueTrackerManager.me.ownerCardsBuriedCountRef.value = 0;
		if (ValueTrackerManager.me.enemyCardsBuriedCountRef != null)
			ValueTrackerManager.me.enemyCardsBuriedCountRef.value = 0;
		if (ValueTrackerManager.me.stagedOwnerRef != null)
			ValueTrackerManager.me.stagedOwnerRef.value = 0;
		if (ValueTrackerManager.me.stagedEnemyRef != null)
			ValueTrackerManager.me.stagedEnemyRef.value = 0;
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
		while (visuals != null && visuals.HasPendingAnimations())
		{
			yield return null;
		}
	}

	private void RevealCards()
	{
		if (blockPlayerInput) return;
		
		// If attack animation is playing, wait
		if (visuals != null && visuals.IsPlayingAttackAnimation())
		{
			return;
		}

		_infoDisplayer.RefreshDeckInfo();

		// ========== Round start: automatically reveal Start Card (player sees it directly) ==========
		if (revealZone == null && cardsRevealedThisRound == 0)
		{
			visuals.InstantiateAllPhysicalCards();
			
			// Reveal Start Card (it's at the bottom of the list but top of the actual deck)
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
			// Auto-reveal next card if current revealed card was removed from game (exiled/destroyed)
			if (revealZone == null && combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false;
				EffectChainManager.Me.CloseOpenedChain();
				return;
			}

			// Combat end check
			if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				HandleCombatFinished();
				return;
			}

			// Prompt text
			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to reveal next card";
			
			visuals.InstantiateAllPhysicalCards();
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
		visuals.MoveCardToRevealZone(cardRevealed.gameObject);

		// Display info (don't trigger effect)
		cardsRevealedThisRound++;
		totalCardsRevealed++;
		_infoDisplayer.ShowCardInfo(cardRevealed, cardsRevealedThisRound, cardRevealed.myStatusRef == ownerPlayerStatusRef);
		_infoDisplayer.RefreshDeckInfo();
		
		// Check fatigue (based on reveal card count)
		CheckFatigueByRevealCount();

		// Record combat stats
		GetComponent<CombatStatsLogger>()?.OnCardRevealed(cardRevealed);

		// Trigger delayed afterShuffle event if pending
		if (_raiseAfterShuffleOnNextReveal)
		{
			_raiseAfterShuffleOnNextReveal = false;
			GameEventStorage.me.afterShuffle.Raise();
		}
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
		visuals.MoveRevealedCardToBottom(cardToBottom);
	}

	private void TriggerStartCardEffect()
	{
		if (revealZone == null) return;

		var startCard = revealZone;
		revealZone = null;

		// Add Start Card back to deck (it is currently in revealZone, not in combinedDeckZone)
		combinedDeckZone.Add(startCard);

		// Execute logical shuffle first, determine each card's position
		combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);

		// Play animation based on known shuffle result
		// Start Card flies directly from Reveal Zone to new position, other cards fly from old to new position
		visuals.PlayShuffleAnimation(startCard, combinedDeckZone, () =>
		{
			// After animation completes, refresh UI and prepare delayed afterShuffle
			_infoDisplayer.RefreshDeckInfo();
			_raiseAfterShuffleOnNextReveal = true; // Delay afterShuffle until first card reveal
			ResetShuffleTrackers();

			// New round start
			HandleNewRoundStart();
		});
	}

	private void HandleNewRoundStart()
	{
		// Round number increment
		roundNumRef.value++;
		cardsRevealedThisRound = 0;
		_infoDisplayer.ClearInfo();
		
		// Physical card reset
		visuals.ReviveAllPhysicalCards();
		
		// Round start event
		GameEventStorage.me.beforeRoundStart.Raise();
	}

	private void HandleCombatFinished()
	{
		if (combatFinished.value) return;

		_infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\nTAP / SPACE to continue";
		if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

		combatFinished.value = true;
		visuals.ClearAllPhysicalCards();
	}

	private bool IsRevealedCardStartCard()
	{
		if (revealZone == null) return false;
		var cardScript = revealZone.GetComponent<CardScript>();
		return cardScript != null && cardScript.isStartCard;
	}
}
