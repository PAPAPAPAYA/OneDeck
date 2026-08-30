using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class BuryEffect : EffectScript
{
	private List<GameObject> _combinedDeck;

	[Header("Tag Configuration")]
	public List<EnumStorage.Tag> tagsToCheck;

	[Header("Self Exclusion")]
	[Tooltip("If true, the source card will not be selected when burying multiple cards")]
	public bool excludeSelf = true;

	[Header("Based on IntSO")]
	[Tooltip("IntSO used when this card belongs to the owner/player")]
	public IntSO ownerIntSO;
	[Tooltip("IntSO used when this card belongs to the enemy")]
	public IntSO enemyIntSO;

	[Header("Start Card Boundary")]
	[Tooltip("If true, BuryNextXCards ignores the Start Card boundary: the Start Card itself and cards below it become valid targets")]
	// TEST-ONLY(2026-07-31): Added to test Start Card burial behavior in BuryNextXCards.
	// Only affects BuryNextXCards. Revisit/remove once the test concludes.
	public bool ignoreStartCardBoundary = false;

	[Header("Creature Filter (4.0 step-5)")]
	[Tooltip("Narrow BuryMyCards/BuryTheirCards pools (and the max/min attack pickers) to creatures or non-creatures")]
	public EffectScript.EffectCreatureFilter creatureFilter = EffectScript.EffectCreatureFilter.Any;
	[Tooltip("For the max/min attack pickers: true = friendly cards, false = enemy cards")]
	public bool targetFriendly = true;

	/// <summary>
	/// Get card owner's color tag (delegates to base palette-aware helper)
	/// </summary>
	private string GetCardColorTag(GameObject card)
	{
		var cardStatus = card.GetComponent<CardScript>().myStatusRef;
		return GetCardOwnerColor(cardStatus);
	}

	/// <summary>
	/// Get current card's color tag
	/// </summary>
	private string GetMyCardColorTag()
	{
		return GetMyCardOwnerColor();
	}

	/// <summary>
	/// Get card's index in combinedDeck
	/// </summary>
	private int GetCardIndexInCombinedDeck(GameObject card)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		return _combinedDeck.IndexOf(card);
	}

	/// <summary>
	/// Check if card is at bottom of deck (index = 0)
	/// </summary>
	private bool IsCardAtBottom(GameObject card)
	{
		int index = GetCardIndexInCombinedDeck(card);
		return index == 0;
	}

	/// <summary>
	/// Find the current index of the Start Card in combinedDeck.
	/// Returns -1 if no Start Card is present.
	/// </summary>
	private int GetStartCardIndex()
	{
		_combinedDeck = combatManager.combinedDeckZone;
		for (int i = 0; i < _combinedDeck.Count; i++)
		{
			var cardScript = _combinedDeck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
				return i;
		}
		return -1;
	}

	/// <summary>
	/// Check if card is below the Start Card in deck order (index < startCardIndex).
	/// Always returns false when no Start Card is found.
	/// </summary>
	private bool IsCardBelowStartCard(GameObject card)
	{
		int startCardIndex = GetStartCardIndex();
		if (startCardIndex < 0) return false;
		return GetCardIndexInCombinedDeck(card) < startCardIndex;
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardToBury = transform.parent.gameObject;
		// If already at bottom, no need to bury
		if (IsCardAtBottom(cardToBury)) return;
		// Do not bury cards below the Start Card boundary
		if (IsCardBelowStartCard(cardToBury)) return;
		var cardsToBury = new List<GameObject> { cardToBury };
		BuryChosenCards(cardsToBury, 1);
	}

	/// <summary>
	/// Check if card's tags intersect with tagsToCheck list
	/// </summary>
	private bool CardHasAnyMatchingTag(CardScript cardScript)
	{
		if (tagsToCheck == null || tagsToCheck.Count == 0) return false;
		foreach (var tag in tagsToCheck)
		{
			if (cardScript.myTags.Contains(tag)) return true;
		}
		return false;
	}

	public void BuryCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have any of the specified tags and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!CardHasAnyMatchingTag(cardScript) || IsCardAtBottom(card) || cardScript.isMinion || CombatManager.ShouldSkipEffectProcessing(cardScript) || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryMyCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the bottom
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || (excludeSelf && card == myCard) || IsCardBelowStartCard(card) || !PassesCreatureFilter(cardScript))
			{
				myCards.RemoveAt(i);
			}
		}

		myCards = UtilityFuncManagerScript.ShuffleList(myCards);
		BuryChosenCards(myCards, amount);
	}

	private bool PassesCreatureFilter(CardScript cardScript)
	{
		if (creatureFilter == EffectScript.EffectCreatureFilter.Creature && !cardScript.isCreature) return false;
		if (creatureFilter == EffectScript.EffectCreatureFilter.NonCreature && cardScript.isCreature) return false;
		return true;
	}

	/// <summary>
	/// Bury as many top-of-deck cards as this card's current attack (MILLBLADE
	/// "攻击；每有1攻击力，埋葬卡组顶1卡").
	/// </summary>
	public void BuryNextXCards_BasedOnAttack()
	{
		BuryNextXCards(myCardScript != null ? myCardScript.GetAttack() : 0);
	}

	/// <summary>
	/// Bury the 1 card with the highest current attack on the side given by targetFriendly
	/// (KINGSLAYER "埋葬1攻击力最高敌方"). Ties are broken randomly.
	/// </summary>
	public void BuryCardWithMaxAttack()
	{
		var card = CardScript.FindCardWithMaxAttack(combatManager.combinedDeckZone, combatManager.revealZone,
			c => IsBuryable(c) && CardOnTargetSide(c));
		if (card == null) return;
		BuryChosenCards(new List<GameObject> { card.gameObject }, 1);
	}

	/// <summary>
	/// Bury the 1 card with the lowest current attack on the side given by targetFriendly
	/// (SACRIFICE_WEAKEST "埋葬1攻击力最低友方"). Positive-attack cards only, ties random.
	/// </summary>
	public void BuryCardWithMinAttack()
	{
		var card = CardScript.FindCardWithMinAttack(combatManager.combinedDeckZone, combatManager.revealZone,
			c => IsBuryable(c) && CardOnTargetSide(c) && c.GetAttack() > 0);
		if (card == null) return;
		BuryChosenCards(new List<GameObject> { card.gameObject }, 1);
	}

	/// <summary>
	/// Bury N friendly cards where N = baseCount - (this round's friendly burials of MY cards,
	/// victim-side counter: my sacrificed + enemy-buried both count) (DECIMATION
	/// "埋葬6友方，本回合每埋葬过1友方，埋葬数-1"). Negative clamps to 0.
	/// </summary>
	public void BuryMyCards_CountBasedOnBuried(int baseCount)
	{
		int buriedThisRound = 0;
		if (ValueTrackerManager.me != null && myCardScript != null &&
		    myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
		{
			buriedThisRound = ValueTrackerManager.me.ownerCardsBuriedCountRef != null
				? ValueTrackerManager.me.ownerCardsBuriedCountRef.value : 0;
		}
		BuryMyCards(baseCount - buriedThisRound);
	}

	private bool CardOnTargetSide(CardScript cardScript)
	{
		return targetFriendly
			? cardScript.myStatusRef == myCardScript.myStatusRef
			: cardScript.myStatusRef != myCardScript.myStatusRef;
	}

	private bool IsBuryable(CardScript cardScript)
	{
		if (cardScript == null || CombatManager.ShouldSkipEffectProcessing(cardScript)) return false;
		if (cardScript.isMinion || cardScript.isPassive) return false;
		if (!PassesCreatureFilter(cardScript)) return false;
		if (excludeSelf && cardScript == myCardScript) return false;
		if (IsCardAtBottom(cardScript.gameObject) || IsCardBelowStartCard(cardScript.gameObject)) return false;
		return true;
	}

	public void BuryMyCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have any of the specified tags, belong to this card's owner, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!CardHasAnyMatchingTag(cardScript) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var theirCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, theirCards, true);

		// Filter cards that belong to the opponent and are not at the bottom
		for (int i = theirCards.Count - 1; i >= 0; i--)
		{
			var card = theirCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || IsCardBelowStartCard(card) || !PassesCreatureFilter(cardScript))
			{
				theirCards.RemoveAt(i);
			}
		}

		theirCards = UtilityFuncManagerScript.ShuffleList(theirCards);
		BuryChosenCards(theirCards, amount);
	}

	public void BuryTheirCardsWithTag(int amount)
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var cardsWithTag = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, cardsWithTag, true);

		// Filter cards that have any of the specified tags, belong to the opponent, and are not at the bottom
		for (int i = cardsWithTag.Count - 1; i >= 0; i--)
		{
			var card = cardsWithTag[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!CardHasAnyMatchingTag(cardScript) || CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.myStatusRef == myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || IsCardBelowStartCard(card))
			{
				cardsWithTag.RemoveAt(i);
			}
		}

		cardsWithTag = UtilityFuncManagerScript.ShuffleList(cardsWithTag);
		BuryChosenCards(cardsWithTag, amount);
	}

	public void BuryTheirCards_BasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		BuryTheirCards(intSO.value);
	}

	public void BuryMyCards_BasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		BuryMyCards(intSO.value);
	}

	public void BuryAllMyCards()
	{
		_combinedDeck = combatManager.combinedDeckZone;
		var myCards = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_combinedDeck, myCards, true);

		// Filter cards that belong to this card's owner and are not at the bottom
		for (int i = myCards.Count - 1; i >= 0; i--)
		{
			var card = myCards[i];
			var cardScript = card.GetComponent<CardScript>();
			if (CombatManager.ShouldSkipEffectProcessing(cardScript) || cardScript.isPassive || cardScript.myStatusRef != myCardScript.myStatusRef || IsCardAtBottom(card) || cardScript.isMinion || (excludeSelf && card == myCard) || IsCardBelowStartCard(card))
			{
				myCards.RemoveAt(i);
			}
		}

		BuryChosenCards(myCards, myCards.Count);
	}

	/// <summary>
	/// Bury the next X cards in deck order (cards before this card in combined deck, i.e. closer to bottom).
	/// Iterates backwards from the current card's position and buries each valid target.
	/// Skips cards that should be ignored, are minions, are already at the bottom, or are below the Start Card.
	/// If this card is in the reveal zone, starts from the bottom of the deck instead.
	/// TEST-ONLY(2026-07-31): when ignoreStartCardBoundary is on, the Start Card itself and cards
	/// below it also become valid targets, and the below-Start-Card source guard is bypassed.
	/// </summary>
	/// <param name="amount">Number of cards to bury</param>
	public void BuryNextXCards(int amount)
	{
		// Entry log BEFORE the amount guard: an amount<=0 silent return must be visible here.
		TestManager.Log("[BuryEffect] BuryNextXCards ENTER amount=" + amount + " myCard=" + (myCard != null ? myCard.name : "null"));
		if (amount <= 0) return;
		_combinedDeck = combatManager.combinedDeckZone;
		TestManager.Log("[BuryEffect] BuryNextXCards START amount=" + amount + " myCard=" + myCard.name + " inReveal=" + (combatManager.revealZone != null && combatManager.revealZone == myCard) + " deckCount=" + _combinedDeck.Count);
		int startIndex;
		// 4.0 passive cards live below the Start Card permanently. Their 埋葬卡组顶N卡 targets
		// the live-zone deck top, so route them through the top-start branch; the below-Start-Card
		// source guard exists to stop grave cards digging the grave and must not apply to passives
		// (RELIC_CHAIN_BURIAL regression 2026-08-30).
		if ((combatManager.revealZone != null && combatManager.revealZone == myCard) || (myCardScript != null && myCardScript.isPassive))
		{
			startIndex = _combinedDeck.Count - 1;
		}
		else
		{
			int currentIndex = -1;
			for (int i = 0; i < _combinedDeck.Count; i++)
			{
				if (_combinedDeck[i] == myCard)
				{
					currentIndex = i;
					break;
				}
			}
			if (currentIndex < 0) return;
			// If this card is already below the Start Card, it cannot bury anything toward the bottom
			// TEST-ONLY(2026-07-31): bypassed when ignoreStartCardBoundary is on.
			if (!ignoreStartCardBoundary && IsCardBelowStartCard(myCard))
			{
				TestManager.Log("[BuryEffect] BuryNextXCards blocked: source below Start Card");
				return;
			}
			startIndex = currentIndex - 1;
		}
		int startCardIndex = GetStartCardIndex();
		// TEST-ONLY(2026-07-31): ignoreStartCardBoundary lets the loop dig past the Start Card down to index 0.
		int loopLowerBound = !ignoreStartCardBoundary && startCardIndex >= 0 ? startCardIndex : 0;
		var cardsToBury = new List<GameObject>();
		int cardsFound = 0;
		for (int i = startIndex; i >= loopLowerBound && cardsFound < amount; i--)
		{
			var targetCard = _combinedDeck[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();
			if (targetCardScript == null) continue;
			// TEST-ONLY(2026-07-31): ignoreStartCardBoundary lets the neutral Start Card become a valid target.
			if (!ignoreStartCardBoundary && CombatManager.ShouldSkipEffectProcessing(targetCardScript)) continue;
			if (targetCardScript.isPassive) continue; // 4.0 passive cards are immovable, even past the Start Card boundary
			if (targetCardScript.isMinion) continue;
			if (IsCardAtBottom(targetCard)) continue;
			cardsToBury.Add(targetCard);
			cardsFound++;
		}
		if (cardsToBury.Count > 0)
		{
			TestManager.Log("[BuryEffect] BuryNextXCards found cardsToBury=" + cardsToBury.Count + " cards=" + string.Join(",", cardsToBury.ConvertAll(c => c.name)));
			BuryChosenCards(cardsToBury, cardsToBury.Count);
		}
		else
		{
			TestManager.Log("[BuryEffect] BuryNextXCards found NO cards to bury");
		}
	}

	private void BuryChosenCards(List<GameObject> cardsToBury, int amount)
	{
		amount = Mathf.Clamp(amount, 0, cardsToBury.Count);
		if (amount == 0) return;

		// 1. First modify logical list, and collect successfully moved cards
		var buriedCards = new List<GameObject>();
		for (var i = 0; i < amount; i++)
		{
			var targetCard = cardsToBury[i];
			var targetCardScript = targetCard.GetComponent<CardScript>();

			if (_combinedDeck.Contains(targetCard))
			{
				_combinedDeck.Remove(targetCard);
				_combinedDeck.Insert(0, targetCard);  // Insert at bottom
				buriedCards.Add(targetCard);
				
				// Track buried counts
				if (ValueTrackerManager.me != null)
				{
					if (targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
					{
						if (ValueTrackerManager.me.ownerCardsBuriedCountRef != null)
							ValueTrackerManager.me.ownerCardsBuriedCountRef.value++;
					}
					else
					{
						if (ValueTrackerManager.me.enemyCardsBuriedCountRef != null)
							ValueTrackerManager.me.enemyCardsBuriedCountRef.value++;
					}

					// 4.0 E4: causer-based per-round creature-burial counters (RELIC_TALLY).
					// The victim-side counters above count cards buried OF a side regardless of
					// burier; these count burials CAUSED by each side — my sacrificed creatures
					// count for me, enemy-caused burials never do. Neutral sources are skipped.
					if (targetCardScript.isCreature && myCardScript != null && myCardScript.myStatusRef != null)
					{
						if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
						{
							if (ValueTrackerManager.me.creaturesBuriedByOwnerThisRoundRef != null)
								ValueTrackerManager.me.creaturesBuriedByOwnerThisRoundRef.value++;
						}
						else
						{
							if (ValueTrackerManager.me.creaturesBuriedByEnemyThisRoundRef != null)
								ValueTrackerManager.me.creaturesBuriedByEnemyThisRoundRef.value++;
						}
					}
				}

				// Per-card result stats: source-side friendly/enemy split + victim TimesBuried
				CombatPerCardStatsTracker.Me?.RecordBury(myCardScript, targetCardScript);
				
				string myColor = GetMyCardColorTag();
				string targetColor = GetCardColorTag(targetCard);
				AppendLog("// [<color=" + myColor + ">" + myCard.gameObject.name + "</color>]将[<color=" + targetColor + ">" +
					targetCardScript.gameObject.name + "</color>]埋入牌库底端");
			}
		}
		
		// VISUAL-FIX(2026-06-13): Remove logic-phase deck sync in Bury to keep animation indices consistent
		//   Cause:    SyncPhysicalCardsWithCombinedDeck in logic phase pre-moves physical cards to final
		//             positions, corrupting snapshot indices for a preceding consume effect's SlotInBatch
		//             and causing distance-zero tweens for the bury animation itself.
		//   Fix:      Physical deck reordering is deferred to RecorderAnimationPlayer via ApplyAnimationResult.
		//   Affects:  BuryEffect, ApplyAnimationResult, RecorderAnimationPlayer
		//   Regress:  StoneShell / grave_punch: verify PopUpBatch + MoveToBottomBatch animate with visible movement.
		//   Related:  PRD stage-sync-removal-ju-on-slot-in-2026-06-13

		// VISUAL-FIX(2026-05-15): Bury-then-Stage reactive chain causes wrong animation target index
		//   Cause:    onMeBuried -> StageSelf modifies deck order AFTER bury logic but BEFORE
		//             animation playback; without snapshot the animation uses stale indices
		//   Affects:  BuryEffect, StageEffect, reactive chains, ApplyAnimationResult
		//   Regress:  Reveal StoneShell (BuryNext2Cards) then reveal RisingFlame (StageSelf on bury)
		//   Related:  Card_StoneShell, Card_RisingFlame
		// R2 (PRD 4.5): bounce candidates — buried cards with life left will return to the
		// queue tail instead of resting in the grave. Life is consumed AFTER reactive
		// resolution below (a staged/exiled/destroyed card does not bounce and keeps its life).
		var bounceCandidates = new List<GameObject>();
		var bounceRequests = new List<AnimationRequest>();
		foreach (var card in buriedCards)
		{
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript != null && cardScript.currentLife > 0)
				bounceCandidates.Add(card);
		}

		var buriedTargetIndices = new List<int>();
		foreach (var card in buriedCards)
		{
			int idx = _combinedDeck.IndexOf(card);
			buriedTargetIndices.Add(idx >= 0 ? idx : 0);
		}

		// VISUAL-FIX(2026-06-10): Bury animation not played when buried card triggers reactive effects
		//   Cause:    BuryChosenCards captured AnimationRequests AFTER raising onMeBuried, but
		//             reactive effects (e.g. counter -> add a copy) called CloseOpenedChain,
		//             destroying the current recorder before requests were written.
		//   Affects:  BuryEffect, EffectChainManager, RecorderAnimationPlayer
		//   Regress:  Deck: grave_punch, slime, start card. Reveal grave_punch (BuryNextXCards).
		//             Verify slime plays PopUp + MoveToBottomBatch animation visibly.
		//   Related:  grave_punch, slime
		// 2. Capture animation requests BEFORE raising events, because reactive effects
		// (e.g. onMeBuried -> counter -> add a copy) may call CloseOpenedChain and destroy
		// the current recorder before we get a chance to write our requests.
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		string recorderInfo = recorder != null ? "chain#" + recorder.chainID + "[" + recorder.cardObject.name + "]" : "null";
		string reqInfo = "BuryBatch cards=" + buriedCards.Count + " indices=" + string.Join(",", buriedTargetIndices) + " deckSize=" + _combinedDeck.Count;
		TestManager.Log("[BuryEffect] Capture request to recorder=" + recorderInfo + " " + reqInfo);
		if (recorder != null)
		{
			// PopUp so player can see which cards are being buried
			recorder.animationRequests.Add(new AnimationRequest {
				type = AnimationRequestType.PopUpBatch,
				targetCards = new List<GameObject>(buriedCards)
			});

			// Bounce candidates skip MoveToBottomBatch: they fly to the queue tail instead
			// (single MoveToIndex requests, played sequentially after the batch).
			var nonBounceCards = new List<GameObject>();
			var nonBounceIndices = new List<int>();
			for (int i = 0; i < buriedCards.Count; i++)
			{
				if (bounceCandidates.Contains(buriedCards[i])) continue;
				nonBounceCards.Add(buriedCards[i]);
				nonBounceIndices.Add(buriedTargetIndices[i]);
			}
			if (nonBounceCards.Count > 0)
			{
				recorder.animationRequests.Add(new AnimationRequest {
					type = AnimationRequestType.MoveToBottomBatch,
					targetCards = nonBounceCards,
					targetIndices = nonBounceIndices,
					snapshotDeckSize = _combinedDeck.Count,
					duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
					useArc = true
				});
			}

			// targetIndex is a placeholder here: the final queue-tail index is patched after
			// reactive resolution (4.5) so the animation matches the post-reaction deck state.
			foreach (var card in bounceCandidates)
			{
				var bounceRequest = new AnimationRequest {
					type = AnimationRequestType.MoveToIndex,
					targetCard = card,
					targetIndex = 0,
					duration = CombatUXManager.me != null ? CombatUXManager.me.deckMoveArcDuration : 0.5f,
					useArc = true
				};
				recorder.animationRequests.Add(bounceRequest);
				bounceRequests.Add(bounceRequest);
			}
		}

		// 3. Raise events in logic phase
		foreach (var buriedCard in buriedCards)
		{
			GameEventStorage.me.onMeBuried.RaiseSpecific(buriedCard);
			GameEventStorage.me.onAnyCardBuried.Raise();
			var buriedCardScript = buriedCard.GetComponent<CardScript>();
			if (buriedCardScript != null && GameEventStorage.me.onFriendlyCardBuried != null)
			{
				if (buriedCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOwner();
				}
				else
				{
					GameEventStorage.me.onFriendlyCardBuried.RaiseOpponent();
				}
			}
		}

		// 4. R2 bounce (PRD 4.5): applied AFTER reactive resolution — only when the card is
		// still in the grave (not staged, exiled, or destroyed by reactions). The buried card
		// stays at index 0 while its events resolve, so CheckCost_IndexBeforeStartCard and
		// Linger semantics are unaffected. Bounced cards stack at the queue tail LIFO.
		if (bounceCandidates.Count > 0)
		{
			_combinedDeck = combatManager.combinedDeckZone; // reactions may have reassigned the list
			int startCardIndex = GetStartCardIndex();
			if (startCardIndex >= 0)
			{
				foreach (var card in bounceCandidates)
				{
					int idx = GetCardIndexInCombinedDeck(card);
					if (idx < 0 || idx >= startCardIndex) continue; // gone, or staged above the Start Card
					var cardScript = card.GetComponent<CardScript>();
					if (cardScript == null) continue;
					cardScript.currentLife--;
					_combinedDeck.RemoveAt(idx);
					_combinedDeck.Insert(startCardIndex + 1, card); // inserts above the Start Card don't move it
					foreach (var req in bounceRequests)
					{
						if (req.targetCard == card)
							req.targetIndex = _combinedDeck.IndexOf(card);
					}
				}
			}
		}
	}
}
