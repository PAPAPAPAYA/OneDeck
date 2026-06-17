using System;
using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using UnityEngine;

[RequireComponent(typeof(CombatInfoDisplayer))]
[RequireComponent(typeof(CombatFuncs))]
[RequireComponent(typeof(CardFactory))]
[RequireComponent(typeof(CombatLog))]
// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
	#region SINGLETON

	public static CombatManager Me;

	[Header("VISUALS")]
	[Tooltip("Optional override for ICombatVisuals. Drag a MonoBehaviour implementing ICombatVisuals here to inject a custom visual provider (e.g. NullCombatVisualsBehaviour for headless testing). If null, falls back to CombatUXManager.visuals.")]
	[SerializeField] private MonoBehaviour visualsOverride;

	/// <summary>
	/// Visual system interface. Logic layer should use this instead of CombatUXManager directly.
	/// Supports Inspector injection via visualsOverride; falls back to CombatUXManager.visuals if no override is set.
	/// </summary>
	public ICombatVisuals visuals
	{
		get
		{
			if (_visuals == null)
			{
				if (visualsOverride != null && visualsOverride is ICombatVisuals overrideVisuals)
					_visuals = overrideVisuals;
				else
					_visuals = CombatUXManager.visuals;
			}
			return _visuals;
		}
	}
	private ICombatVisuals _visuals;

	/// <summary>
	/// Test-only helper to inject a custom ICombatVisuals implementation.
	/// Clears the cached visuals reference so the new one takes effect immediately.
	/// </summary>
	public void SetVisualsOverride(ICombatVisuals visuals)
	{
		_visuals = visuals;
	}

	private void Awake()
	{
		Me = this;
		CombatAnimationSpeed.SpeedScale = combatAnimationSpeedScale;
		
		// Ensure RecorderAnimationPlayer singleton exists for effect-recorder-driven animation
		if (RecorderAnimationPlayer.me == null)
		{
			var go = new GameObject("RecorderAnimationPlayer");
			go.AddComponent<RecorderAnimationPlayer>();
		}

		// Ensure CostResultPresenter singleton exists for cost-failure feedback (shake + combat log)
		if (CostResultPresenter.me == null)
		{
			var go = new GameObject("CostResultPresenter");
			go.AddComponent<CostResultPresenter>();
		}
	}

	private void OnValidate()
	{
		CombatAnimationSpeed.SpeedScale = combatAnimationSpeedScale;
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

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public GameObject revealZone;

	[Header("FLOW")]
	public bool awaitingRevealConfirm = true;
	public BoolSO combatFinished; // identify if this session of combat is finished
	public int cardsRevealedThisRound; // tracks how many cards have been revealed this round (for display numbering)
	[Tooltip("Block player input when playing Stage/Bury animation")]
	public bool IsInputBlocked { get; private set; }
	private int _inputBlockCount = 0;

	[Header("ANIMATION LOCK")]
	[Tooltip("True while effect recorder animations are playing. Prevents RevealCards from auto-revealing next card too early.")]
	public bool isPlayingEffectAnimations;

	[Header("AUTO REVEAL")]
	[Tooltip("If true, all player confirmations inside combat phase are skipped automatically.")]
	public bool autoReveal;

	[Header("GLOBAL ANIMATION SPEED")]
	[Tooltip("Global speed scale for all Combat-phase card animations. 1 = normal, 2 = double speed.")]
	[SerializeField] private float combatAnimationSpeedScale = 1f;

	/// <summary>
	/// Request to block player input. Uses reference counting to handle concurrent animations.
	/// </summary>
	public void BlockInput(object requester)
	{
		_inputBlockCount++;
		IsInputBlocked = true;
	}

	/// <summary>
	/// Request to unblock player input. Reference count must reach zero before input is restored.
	/// </summary>
	public void UnblockInput(object requester)
	{
		_inputBlockCount = Mathf.Max(0, _inputBlockCount - 1);
		if (_inputBlockCount <= 0)
			IsInputBlocked = false;
	}

	/// <summary>
	/// Force reset input block state. Called on combat enter/exit.
	/// </summary>
	public void ResetInputBlock()
	{
		_inputBlockCount = 0;
		IsInputBlocked = false;
	}

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
		ResetInputBlock();
	}

	public void ExitCombat()
	{
		ResetInputBlock();
		
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
		var startCard = FindStartCardInstance();
		if (startCard != null)
			Destroy(startCard);
		
		// clean up tracking stats
		roundNumRef.value = 0;
		cardsRevealedThisRound = 0;
		totalCardsRevealed = 0;
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.chainNumber = 0;
		
		// clean up effect recorders
		if (EffectChainManager.Me != null)
		{
			for (int i = EffectChainManager.Me.transform.childCount - 1; i >= 0; i--)
			{
				var child = EffectChainManager.Me.transform.GetChild(i).gameObject;
				if (child.GetComponent<EffectRecorder>() != null)
				{
					Destroy(child);
				}
			}
			EffectChainManager.Me.closedEffectRecorders.Clear();
		}
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
			// Debug.LogError("[CombatManager] CardFactory is not available!");
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
		var startCardInstance = factory.CreateStartCard(startCardPrefab, playerDeckParent.transform);
		if (startCardInstance != null)
			combinedDeckZone.Add(startCardInstance);

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
		// print(msg);
		CombatLog.me?.Append(msg);
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
		// print(msg);
		CombatLog.me?.Append(msg);
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


	public void SetRaiseAfterShuffleOnNextReveal(bool value) => _raiseAfterShuffleOnNextReveal = value;
	public void ResetShuffleTrackersPublic() => ResetShuffleTrackers();

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
	/// <summary>
	/// Find Start Card instance in combinedDeckZone (or revealZone) by isStartCard flag.
	/// Replaces the removed _startCardInstance cached reference.
	/// </summary>
	public GameObject FindStartCardInstance()
	{
		// Check revealZone first (Start Card might be there during combat)
		if (revealZone != null)
		{
			var cs = revealZone.GetComponent<CardScript>();
			if (cs != null && cs.isStartCard)
				return revealZone;
		}
		foreach (var card in combinedDeckZone)
		{
			if (card == null) continue;
			var cs = card.GetComponent<CardScript>();
			if (cs != null && cs.isStartCard)
				return card;
		}
		return null;
	}

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

	/// <summary>
	/// Wait for active animation batches to idle, close the effect chain, then play captured recorder animations.
	/// </summary>
	private System.Collections.IEnumerator PlayRecorderAnimationsAndWait()
	{
		isPlayingEffectAnimations = true;
		// 1. Safety wait for active animation batches
		while (AnimationStateTracker.me != null && AnimationStateTracker.me.HasActiveBatch)
		{
			Debug.Log("[CombatManager] PlayRecorderAnimationsAndWait waiting for HasActiveBatch. Pending=" + AnimationStateTracker.me.PendingAnimations);
			yield return null;
		}

		// 2. Close the chain
		EffectChainManager.Me.CloseOpenedChain();

		try
		{
			// 3. Collect root recorders and play animations
			if (RecorderAnimationPlayer.me != null)
			{
				var roots = new List<GameObject>();
				if (EffectChainManager.Me != null && EffectChainManager.Me.closedEffectRecorders != null)
				{
					foreach (var rec in EffectChainManager.Me.closedEffectRecorders)
					{
						if (rec == null) continue;
						var recorder = rec.GetComponent<EffectRecorder>();
						bool isRoot = rec.transform.parent == EffectChainManager.Me.transform;
						Debug.Log("[CombatManager] Collecting recorder chainID=" + (recorder != null ? recorder.chainID.ToString() : "null")
							+ " card=" + (recorder != null && recorder.cardObject != null ? recorder.cardObject.name : "null")
							+ " animationPlayed=" + (recorder != null ? recorder.animationPlayed.ToString() : "null")
							+ " isRoot=" + isRoot
							+ " reqCount=" + (recorder != null ? recorder.animationRequests.Count.ToString() : "null"));
						if (recorder != null && !recorder.animationPlayed && rec.transform.parent == EffectChainManager.Me.transform)
						{
							roots.Add(rec);
						}
					}
				}

				if (roots.Count > 0)
				{
					yield return StartCoroutine(RecorderAnimationPlayer.me.PlayRecordersCoroutine(roots));
				}
			}
		}
		finally
		{
			// Mark all recorders in closedEffectRecorders as played to prevent replay on exception
			if (EffectChainManager.Me != null && EffectChainManager.Me.closedEffectRecorders != null)
			{
				foreach (var recObj in EffectChainManager.Me.closedEffectRecorders)
				{
					if (recObj == null) continue;
					var recorder = recObj.GetComponent<EffectRecorder>();
					if (recorder != null)
					{
						Debug.Log("[CombatManager] finally marking animationPlayed=true for chainID=" + recorder.chainID + " card=" + (recorder.cardObject != null ? recorder.cardObject.name : "null"));
						recorder.animationPlayed = true;
					}
				}
			}

			// Ensure input blocking is released
			ResetInputBlock();
			// NOTE: isPlayingEffectAnimations is reset AFTER UpdateAllPhysicalCardTargets below
		}

		// Wait for attack animations to finish before next reveal
		yield return StartCoroutine(WaitForAttackAnimationsBeforeNextReveal());

		// Ensure all physical cards tween to their final positions after recorder animations complete.
		if (visuals != null)
		{
			visuals.UpdateAllPhysicalCardTargets();
		}

		isPlayingEffectAnimations = false;

		Debug.Log("[CombatManager] PlayRecorderAnimationsAndWait COMPLETE");
		if (visuals != null)
		{
			string deckList = "";
			var ux = visuals as CombatUXManager;
			if (ux != null)
			{
				for (int i = 0; i < ux.physicalCardsInDeck.Count; i++)
				{
					var card = ux.physicalCardsInDeck[i];
					deckList += "[" + i + "]" + card.name + " pos=" + card.transform.position + " ";
				}
			}
			// Debug.Log("[CombatManager] Final deck state: " + deckList);
		}
	}

	private void RevealCards()
	{
		if (IsInputBlocked) return;
		
		// If attack animation is playing, wait
		if (visuals != null && visuals.IsPlayingAttackAnimation())
		{
			return;
		}

		_infoDisplayer.RefreshDeckInfo();

		// ========== Round start: automatically reveal Start Card (player sees it directly) ==========
		if (revealZone == null && cardsRevealedThisRound == 0)
		{
			// Guard: don't auto-reveal Start Card while effect recorder animations are playing
			if (isPlayingEffectAnimations)
			{
				return;
			}

			visuals.InstantiateAllPhysicalCards();
			
			// Reveal Start Card (it's at the bottom of the list but top of the actual deck)
			if (combinedDeckZone.Count > 0)
			{
				// If afterShuffle is pending, wait for reveal-zone movement to finish before raising it.
				// This prevents BOOSTER's emphasize/Stage animation from starting while the card is still
				// mid-flight from the deck to the reveal zone.
				if (_raiseAfterShuffleOnNextReveal)
				{
					isPlayingEffectAnimations = true;
					RevealNextCard(() =>
					{
						_raiseAfterShuffleOnNextReveal = false;
						GameEventStorage.me.afterShuffle.Raise();
						
						// VISUAL-FIX(2026-06-09): afterShuffle effect animations now play immediately via EffectRecorder flow
						//   Cause:    afterShuffle was raised synchronously in Round Start path but PlayRecorderAnimationsAndWait
						//             was never called, so captured animations sat unplayed until next player click.
						//   Affects:  CombatManager.RevealCards Round Start path, EffectChainManager, RecorderAnimationPlayer
						//   Regress:  Start Card shuffle → reveal BOOSTER (afterShuffle→Stage); verify Stage animation plays
						//             automatically without requiring a player click.
						//   Related:  PRD prd-aftershuffle-reveal-timing-fix-2026-06-08.md
						//
						// VISUAL-FIX(2026-06-09): BOOSTER overlaps deck cards because afterShuffle starts before reveal-zone move finishes
						//   Cause:    RevealNextCard starts a 0.3s DOTween to reveal zone, but afterShuffle.Raise fired immediately,
						//             so BOOSTER's emphasize + Stage animations started while BOOSTER was mid-flight.
						//   Fix:      RevealNextCard now accepts an onComplete callback via ICombatVisuals.MoveCardToRevealZone.
						//             Round Start path waits for the movement to finish before raising afterShuffle.
						//   Affects:  ICombatVisuals, CombatUXManager, CardPhysObjScript, CombatManager
						//   Regress:  Start Card shuffle → reveal BOOSTER; verify BOOSTER fully reaches reveal zone before
						//             emphasize/Stage animation starts and ends at the correct reveal-zone position.
						StartCoroutine(PlayRecorderAnimationsAndWait());
					});
				}
				else
				{
					RevealNextCard();
				}
				awaitingRevealConfirm = false; // Enter Start Card effect trigger phase
			}
			else if (_raiseAfterShuffleOnNextReveal)
			{
				// Edge case: no cards left to reveal but afterShuffle is pending
				_raiseAfterShuffleOnNextReveal = false;
				GameEventStorage.me.afterShuffle.Raise();
				StartCoroutine(PlayRecorderAnimationsAndWait());
			}

			return;
		}

		// ========== Phase 1: Wait to process current card and reveal next ==========
		if (awaitingRevealConfirm)
		{
			// Guard: don't advance state while effect recorder animations are playing
			if (isPlayingEffectAnimations)
			{
				return;
			}

			// Auto-reveal next card if current revealed card was removed from game (exiled/destroyed)
			if (revealZone == null && combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false;

				// 3. Trigger delayed afterShuffle event if pending
				// Moved here from RevealNextCard to ensure Start Card coroutine finishes first
				if (_raiseAfterShuffleOnNextReveal)
				{
					_raiseAfterShuffleOnNextReveal = false;
					GameEventStorage.me.afterShuffle.Raise();
				}

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
			if (!ShouldAutoConfirm()) return;
			CombatLog.me?.Clear();

			// 1. Put current card back to bottom of deck
			PutRevealedCardToBottom();

			// 2. Reveal next card (if any)
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // Enter effect trigger phase
			}

			// 3. Trigger delayed afterShuffle event if pending
			// Moved here from RevealNextCard to ensure Start Card coroutine finishes first
			if (_raiseAfterShuffleOnNextReveal)
			{
				_raiseAfterShuffleOnNextReveal = false;
				GameEventStorage.me.afterShuffle.Raise();
			}

			EffectChainManager.Me.CloseOpenedChain();
		}
		// ========== Phase 2: Wait to trigger current card effect ==========
		else
		{
			// VISUAL-FIX(2026-06-09): Prevent player from triggering next effect while afterShuffle animations play
			//   Cause:    Round Start path now calls PlayRecorderAnimationsAndWait after afterShuffle.Raise.
			//             Without this guard, a player click could start a second coroutine and race CloseOpenedChain.
			//   Affects:  CombatManager.RevealCards Phase 2
			//   Regress:  Start Card shuffle → reveal BOOSTER (afterShuffle→Stage); spam-click during Stage animation.
			//             Verify no duplicate effect trigger or chain corruption.
			//   Related:  PRD prd-aftershuffle-reveal-timing-fix-2026-06-08.md
			// Guard: don't trigger effect while recorder animations are playing
			if (isPlayingEffectAnimations)
			{
				return;
			}

			// Check if current card is valid
			if (revealZone == null)
			{
				awaitingRevealConfirm = true;
				return;
			}

			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to trigger effect";
			if (!ShouldAutoConfirm()) return;

			// Start Card special handling: trigger effect = shuffle + new round
			if (IsRevealedCardStartCard())
			{
				// Start Card does NOT trigger onMeRevealed/onAnyCardRevealed/onHostileCardRevealed.
				// It goes through CostNEffectContainer to create an EffectRecorder,
				// but skips the normal reveal-event broadcast.
				var container = revealZone.GetComponentInChildren<CostNEffectContainer>();
				container?.InvokeEffectEvent();
			}
			else
			{
				// Normal card triggers effect
				TriggerRevealedCardEffect();
			}
			
			awaitingRevealConfirm = true;

			
			// Wait for all attack animations to complete before allowing next operation
			StartCoroutine(PlayRecorderAnimationsAndWait());
		}
	}

	// ========== Helper Methods ==========

	private void RevealNextCard(Action onMoveToRevealZoneComplete = null)
	{
		var cardRevealed = combinedDeckZone[^1].GetComponent<CardScript>();
		revealZone = combinedDeckZone[^1];
		combinedDeckZone.RemoveAt(combinedDeckZone.Count - 1);

		// Physical movement: from deck to reveal zone
		visuals.MoveCardToRevealZone(cardRevealed.gameObject, onMoveToRevealZoneComplete);

		// Display info (don't trigger effect)
		cardsRevealedThisRound++;
		totalCardsRevealed++;
		_infoDisplayer.ShowCardInfo(cardRevealed, cardsRevealedThisRound, cardRevealed.myStatusRef == ownerPlayerStatusRef);
		_infoDisplayer.RefreshDeckInfo();
		
		// Check fatigue (based on reveal card count)
		CheckFatigueByRevealCount();

		// Record combat stats
		GetComponent<CombatStatsLogger>()?.OnCardRevealed(cardRevealed);

		// afterShuffle raising removed from here — moved to RevealCards Phase 1
		// if (_raiseAfterShuffleOnNextReveal)
		// {
		// 	_raiseAfterShuffleOnNextReveal = false;
		// 	GameEventStorage.me.afterShuffle.Raise();
		// }
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

	/// <summary>
	/// Called by RecorderAnimationPlayer via AnimationRequest.onComplete
	/// after Start Card shuffle animation finishes.
	/// </summary>
	public void OnStartCardShuffleAnimationComplete()
	{
		_infoDisplayer.RefreshDeckInfo();
		HandleNewRoundStart();
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
		if (!ShouldAutoConfirm()) return;

		combatFinished.value = true;
		visuals.ClearAllPhysicalCards();
	}

	/// <summary>
	/// Check if the combat should auto-confirm the current player input.
	/// Respects both the dedicated autoReveal flag and the legacy DeckTester.autoSpace flag.
	/// </summary>
	private bool ShouldAutoConfirm()
	{
		return Input.GetKeyDown(KeyCode.Space)
		       || Input.GetMouseButtonDown(0)
		       || autoReveal
		       || (DeckTester.me != null && DeckTester.me.autoSpace);
	}

	private bool IsRevealedCardStartCard()
	{
		if (revealZone == null) return false;
		var cardScript = revealZone.GetComponent<CardScript>();
		return cardScript != null && cardScript.isStartCard;
	}

	#region Custom Shuffle Order (Test Only)

	/// <summary>
	/// Apply custom deck order after shuffle for testing purposes.
	/// prefabOrder defines reveal order from top (first revealed) to bottom (last revealed).
	/// Cards not in the list stay at the bottom preserving original relative order.
	/// Start Card can be included in the list.
	/// </summary>
	public List<GameObject> ApplyCustomShuffleOrder(List<GameObject> currentDeck, List<GameObject> prefabOrder)
	{
		var matchedInRevealOrder = new List<GameObject>();
		var usedInstances = new HashSet<GameObject>();

		foreach (var prefab in prefabOrder)
		{
			if (prefab == null) continue;
			var prefabCs = prefab.GetComponent<CardScript>();
			if (prefabCs == null) continue;

			GameObject matchedInstance = null;
			foreach (var instance in currentDeck)
			{
				if (usedInstances.Contains(instance)) continue;
				var instanceCs = instance.GetComponent<CardScript>();
				if (instanceCs == null) continue;

				if (CardMatchesPrefab(prefabCs, instanceCs, prefab, instance))
				{
					matchedInstance = instance;
					break;
				}
			}

			if (matchedInstance != null)
			{
				matchedInRevealOrder.Add(matchedInstance);
				usedInstances.Add(matchedInstance);
			}
		}

		// Collect unmatched cards preserving original relative order
		var unmatched = new List<GameObject>();
		foreach (var card in currentDeck)
		{
			if (!usedInstances.Contains(card))
				unmatched.Add(card);
		}

		// Reverse matched list: reveal order [first->last] -> deck order [bottom->top]
		// combinedDeckZone: index 0 = bottom (last revealed), [^1] = top (first revealed)
		var matchedInDeckOrder = new List<GameObject>(matchedInRevealOrder);
		matchedInDeckOrder.Reverse();

		var result = new List<GameObject>();
		result.AddRange(unmatched);
		result.AddRange(matchedInDeckOrder);

		return result;
	}

	/// <summary>
	/// Check if an instantiated card matches a prefab for shuffle override purposes.
	/// </summary>
	private bool CardMatchesPrefab(CardScript prefabCs, CardScript instanceCs, GameObject prefab, GameObject instance)
	{
		// Start Card matching
		if (prefabCs.isStartCard && instanceCs.isStartCard)
			return true;

		// cardTypeID matching (primary)
		if (!string.IsNullOrEmpty(prefabCs.cardTypeID) && !string.IsNullOrEmpty(instanceCs.cardTypeID))
			return prefabCs.cardTypeID == instanceCs.cardTypeID;

		// Fallback: displayName or GameObject name
		string prefabName = !string.IsNullOrEmpty(prefabCs.displayName) ? prefabCs.displayName : prefab.name;
		string instanceName = !string.IsNullOrEmpty(instanceCs.displayName) ? instanceCs.displayName : instance.name;

		return prefabName == instanceName || instanceName.StartsWith(prefabName + " (");
	}

	#endregion
}
