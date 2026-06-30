using System;
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using DG.Tweening;
using UnityEngine;
using DefaultNamespace.Managers;

public class RecorderAnimationPlayer : MonoBehaviour
{
	public static RecorderAnimationPlayer me;

	private HashSet<CardScript> _baselineCards = new HashSet<CardScript>();
	private EffectRecorder _currentRecorder;

	void Awake()
	{
		me = this;
	}

	public IEnumerator PlayRecordersCoroutine(List<GameObject> rootRecorders)
	{
		TestManager.Log("[RecorderAnimationPlayer] PlayRecordersCoroutine START rootCount=" + rootRecorders.Count);
		AttackAnimationManager.me?.HoldDeckFocus();
		try
		{
			// Compute per-card display baselines before any animation plays so that
			// StatusEffectChange text updates can be applied incrementally per projectile.
			ComputeAndApplyDisplayBaselines(rootRecorders);

			foreach (var rootRecorder in rootRecorders)
			{
				if (rootRecorder == null)
				{
					TestManager.Log("[RecorderAnimationPlayer] Skipping null/destroyed root recorder.");
					continue;
				}
				var recorder = rootRecorder.GetComponent<EffectRecorder>();
				if (recorder == null || recorder.animationPlayed)
				{
					TestManager.Log("[RecorderAnimationPlayer] Skipping root recorder: recorder=" + (recorder == null ? "null" : "valid") + " played=" + (recorder != null ? recorder.animationPlayed.ToString() : "n/a"));
					continue;
				}
				yield return StartCoroutine(PlayRecorderCoroutine(recorder));
			}
		}
		finally
		{
			AttackAnimationManager.me?.ReleaseDeckFocus();

			// Commit display state for all cards that received a baseline so display
			// returns to live state after all animations complete.
			foreach (var card in _baselineCards)
			{
				if (card != null) card.CommitDisplayState();
			}
			_baselineCards.Clear();
		}
	}

	public IEnumerator PlayRecorderCoroutine(EffectRecorder recorder)
	{
		if (recorder == null || recorder.animationPlayed) yield break;
		if (recorder.gameObject == null)
		{
			TestManager.Log("[RecorderAnimationPlayer] PlayRecorderCoroutine: recorder GameObject was destroyed, skipping.");
			yield break;
		}
		recorder.animationPlayed = true;
		var previousRecorder = _currentRecorder;
		_currentRecorder = recorder;

		string cardName = recorder.cardObject != null ? recorder.cardObject.name : "null";
		TestManager.Log("[RecorderAnimationPlayer] PlayRecorderCoroutine chainID=" + recorder.chainID + " card=" + cardName + " animationPlayed SET TO TRUE");
		string reqSummary = "";
		for (int i = 0; i < recorder.animationRequests.Count; i++)
		{
			var r = recorder.animationRequests[i];
			reqSummary += "[" + i + "]" + (r != null ? r.type.ToString() : "null") + " ";
		}

		// VISUAL-FIX(2026-06-30): Off-reveal source cards stay at popup peak during effect animation
		//   Cause:    Cards activated from the deck were popped up, emphasized, then immediately
		//             slotted back in before the recorder's own effect animations ran. Many effects
		//             then captured their own PopUp/PopUpBatch, causing a second popup/slotin cycle.
		//   Fix:      Pop the source card up, play emphasize/shake while it stays at peak, then play
		//             the recorder's effect animations. Skip redundant PopUp/PopUpBatch requests for
		//             cards already at peak. Slot the source card back in once after its own requests
		//             finish, unless an effect already slotted it in.
		//   Affects:  RecorderAnimationPlayer, CombatUXManager, CardPhysObjScript, EffectRecorder, EffectChainManager, CostNEffectContainer
		//   Regress:  Trigger a reactive deck effect (e.g. afterShuffle StageSelf, onMeGotPower);
		//             verify popup -> emphasize -> stay at peak -> effect animation -> slotin.
		//             Trigger a cost-fail reaction; verify popup -> shake -> slotin (no emphasize).
		//             Reveal Start Card and verify it does NOT popup before its shuffle animation.

		// VISUAL-FIX(2026-06-30): Chained off-reveal attacks popup the next card before focus transition
		//   Cause:    RecorderAnimationPlayer popped the source card up before playing its requests.
		//             The Attack request then called FocusOnCardCoroutine/TransitionFocusCoroutine,
		//             so the deck transition happened after the popup, making the next attack look
		//             like it started while the deck was still adjusting.
		//   Affects:  RecorderAnimationPlayer, CombatUXManager, AttackAnimationManager
		//   Regress:  Trigger a chain of two off-reveal attacks (e.g. BOOSTER StageSelf ->
		//             two GOBLIN_CHARGE_TEAM OnMeStaged). Verify the focus transition to the
		//             second card completes before its popup starts.
		//   Related:  Card_GOBLIN_CHARGE_TEAM, Card_BOOSTER
		bool sourceNeedsPopup = recorder.animationRequests.Count > 0
			&& recorder.cardObject != null
			&& !recorder.sourceWasInRevealZone;

		if (sourceNeedsPopup)
		{
			// Focus deck on the source card before popping it up.
			// This lets the smart focus transition finish first, so the next attack
			// does not appear to start while the deck is still transitioning.
			var sourceCardScript = recorder.cardObject.GetComponent<CardScript>();
			if (sourceCardScript != null && CombatManager.Me != null)
			{
				var visuals = CombatManager.Me.visuals;
				var combatUX = visuals as CombatUXManager;
				if (combatUX != null && combatUX.enablePeelDeck)
				{
					yield return combatUX.StartCoroutine(combatUX.FocusOnCardCoroutine(sourceCardScript));
				}
			}

			yield return StartCoroutine(PlayOffRevealPopupCoroutine(recorder.cardObject));
		}

		// Play the source-card feedback while it is still popped up.
		// Success recorders emphasize; cost-fail recorders shake.
		if (recorder.cardObject != null)
		{
			if (recorder.isCostFailRecorder)
			{
				// Play the single shake request while the card is still at the popup peak.
				var shakeRequest = recorder.animationRequests.Count > 0 ? recorder.animationRequests[0] : null;
				if (shakeRequest != null)
				{
					yield return StartCoroutine(PlayRequestCoroutine(shakeRequest));
				}
			}
			else if (recorder.animationRequests.Count > 0)
			{
				yield return StartCoroutine(PlayEmphasizeAnimation(recorder.cardObject));
			}
			else
			{
				TestManager.Log("[RecorderAnimationPlayer] Skipping emphasize: requests=" + recorder.animationRequests.Count + " card=" + cardName);
			}
		}

		// Pre-scan to mark StatusEffectChange requests that should defer display commit
		// until their corresponding StatusEffectProjectile animation completes
		MarkDeferredDisplayCommits(recorder);

		// Play all remaining requests of this effect instance sequentially.
		// Skip redundant popups for cards already at the popup peak (e.g. the source card
		// popped up above, or a card already lifted by an earlier request in this recorder).
		foreach (var request in recorder.animationRequests)
		{
			if (request == null) continue;

			// Shake was already played above for cost-fail recorders.
			if (recorder.isCostFailRecorder && request.type == AnimationRequestType.Shake)
				continue;

			if (request.type == AnimationRequestType.PopUp)
			{
				var phys = GetPhysicalCardScript(request.targetCard);
				if (phys != null && phys.isPoppedUp)
					continue;
			}
			else if (request.type == AnimationRequestType.PopUpBatch)
			{
				var filtered = new List<GameObject>();
				if (request.targetCards != null)
				{
					foreach (var card in request.targetCards)
					{
						if (card == null) continue;
						var phys = GetPhysicalCardScript(card);
						if (phys != null && phys.isPoppedUp)
							continue;
						filtered.Add(card);
					}
				}
				if (filtered.Count == 0) continue;
				request.targetCards = filtered;
			}

			yield return StartCoroutine(PlayRequestCoroutine(request));
		}

		// Return the source card to the deck once its own effect animations are done.
		// If the effect already slotted it in (e.g. StatusEffect SlotInBatch / Stage Batch),
		// isPoppedUp will be false and we skip the redundant slot-in.
		if (sourceNeedsPopup && recorder.cardObject != null)
		{
			var sourcePhys = GetPhysicalCardScript(recorder.cardObject);
			if (sourcePhys != null && sourcePhys.isPoppedUp)
			{
				yield return StartCoroutine(SlotInSourceCardCoroutine(recorder.cardObject));
			}
		}

		// After all requests of this effect instance are done, recurse into children (effect-instance-boundary interleave)
		for (int i = 0; i < recorder.transform.childCount; i++)
		{
			var child = recorder.transform.GetChild(i);
			if (child == null) continue;
			var childRecorder = child.GetComponent<EffectRecorder>();
			if (childRecorder != null && !childRecorder.animationPlayed)
			{
				yield return StartCoroutine(PlayRecorderCoroutine(childRecorder));
			}
		}

		_currentRecorder = previousRecorder;
	}

	/// <summary>
	/// Helper to get the CardPhysObjScript for a logical card via the current visuals.
	/// </summary>
	private CardPhysObjScript GetPhysicalCardScript(GameObject logicalCard)
	{
		if (logicalCard == null || CombatManager.Me == null) return null;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) return null;
		var physicalCard = visuals.GetPhysicalCard(logicalCard);
		if (physicalCard == null) return null;
		return physicalCard.GetComponent<CardPhysObjScript>();
	}

	/// <summary>
	/// Play emphasize animation (scale up then back to original) on the card that triggered the effect.
	/// </summary>
	private IEnumerator PlayEmphasizeAnimation(GameObject logicalCard)
	{
		if (logicalCard == null) yield break;
		if (CombatManager.Me == null) yield break;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

		TestManager.Log("[RecorderAnimationPlayer] PlayEmphasizeAnimation START card=" + logicalCard.name + " time=" + Time.time);

		GameObject physicalCard = visuals.GetPhysicalCard(logicalCard);
		if (physicalCard == null) yield break;

		var physScript = physicalCard.GetComponent<CardPhysObjScript>();
		if (physScript == null) yield break;

		AnimationStateTracker.me?.RegisterAnimation();
		physScript.isPlayingSpecialAnimation = true;

		bool done = false;
		Vector3 originalScale = physicalCard.transform.localScale;
		Vector3 targetScale = originalScale * 1.2f;
		float halfDuration = CombatAnimationSpeed.ScaleDuration(0.25f);

		Sequence seq = DOTween.Sequence();
		seq.Append(physicalCard.transform.DOScale(targetScale, halfDuration).SetEase(Ease.OutQuad));
		seq.Append(physicalCard.transform.DOScale(originalScale, halfDuration).SetEase(Ease.OutQuad));
		seq.OnComplete(() =>
		{
			// VISUAL-FIX(2026-06-30): Keep popped-up source cards at peak through effect animation.
			//   If the card is still in popup state, do not clear isPlayingSpecialAnimation here;
			//   SlotInCard / MoveCardWithAnimation will clear it when the card returns to deck.
			if (!physScript.isPoppedUp)
				physScript.isPlayingSpecialAnimation = false;
			AnimationStateTracker.me?.CompleteAnimation();
			done = true;
		});

		yield return new WaitUntil(() => done);
	}

	/// <summary>
	/// Pop the source card up from the deck so the player can see which card is about to trigger.
	/// The caller is responsible for slotting it back in and for verifying the card was not the
	/// revealed card when the recorder was created.
	/// </summary>
	private IEnumerator PlayOffRevealPopupCoroutine(GameObject logicalCard)
	{
		if (logicalCard == null) yield break;
		if (CombatManager.Me == null) yield break;

		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

		TestManager.Log("[RecorderAnimationPlayer] PlayOffRevealPopupCoroutine START card=" + logicalCard.name + " time=" + Time.time);

		bool popupDone = false;
		visuals.PopUpCard(logicalCard, () => popupDone = true);
		yield return new WaitUntil(() => popupDone);

		TestManager.Log("[RecorderAnimationPlayer] PlayOffRevealPopupCoroutine END card=" + logicalCard.name + " time=" + Time.time);
	}

	/// <summary>
	/// Slot the source card back into the deck after its popup feedback (emphasize/shake) has
	/// finished playing.
	/// </summary>
	private IEnumerator SlotInSourceCardCoroutine(GameObject logicalCard)
	{
		if (logicalCard == null) yield break;
		if (CombatManager.Me == null) yield break;

		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

		bool slotInDone = false;
		visuals.SlotInCard(logicalCard, () => slotInDone = true);
		yield return new WaitUntil(() => slotInDone);
	}

	/// <summary>
	/// Pre-scan a recorder's animation requests to mark StatusEffectChange requests
	/// that should defer their display commit until the corresponding
	/// StatusEffectProjectile animation completes.
	/// </summary>
	private void MarkDeferredDisplayCommits(EffectRecorder recorder)
	{
		if (recorder == null || recorder.animationRequests == null) return;

		// Collect all targets that will receive a StatusEffectProjectile in this recorder.
		// For reverse projectiles (absorb/transfer), the source cards listed in attackerCards
		// are the ones losing the status effect, so they must also defer.
		var projectileTargets = new HashSet<GameObject>();
		foreach (var req in recorder.animationRequests)
		{
			if (req == null || req.type != AnimationRequestType.StatusEffectProjectile) continue;
			if (req.reverseProjectile && req.attackerCards != null)
			{
				foreach (var a in req.attackerCards)
				{
					if (a != null)
						projectileTargets.Add(a);
				}
			}
			if (req.targetCard != null)
				projectileTargets.Add(req.targetCard);
			if (req.targetCards != null)
			{
				foreach (var t in req.targetCards)
				{
					if (t != null)
						projectileTargets.Add(t);
				}
			}
		}

		if (projectileTargets.Count == 0) return;

		// Mark StatusEffectChange requests whose target will receive a projectile
		foreach (var req in recorder.animationRequests)
		{
			if (req == null || req.type != AnimationRequestType.StatusEffectChange) continue;
			if (req.targetCard != null && projectileTargets.Contains(req.targetCard))
			{
				req.deferDisplayCommit = true;
			}
		}
	}

	/// <summary>
	/// Apply display deltas for StatusEffectChange requests that should commit when a
	/// projectile spawns (consume/transfer source cards). This is called before the
	/// projectile flight begins so the source card text updates at the start of the animation.
	/// </summary>
	private void ApplySpawnDeltasForProjectile(EffectRecorder recorder, AnimationRequest projectileRequest)
	{
		if (recorder == null || recorder.animationRequests == null || projectileRequest == null) return;

		// Collect all cards involved in this projectile request.
		var involvedCards = new HashSet<GameObject>();
		if (projectileRequest.attackerCard != null)
			involvedCards.Add(projectileRequest.attackerCard);
		if (projectileRequest.attackerCards != null)
		{
			foreach (var a in projectileRequest.attackerCards)
			{
				if (a != null)
					involvedCards.Add(a);
			}
		}
		if (projectileRequest.targetCard != null)
			involvedCards.Add(projectileRequest.targetCard);
		if (projectileRequest.targetCards != null)
		{
			foreach (var t in projectileRequest.targetCards)
			{
				if (t != null)
					involvedCards.Add(t);
			}
		}

		if (involvedCards.Count == 0) return;

		foreach (var req in recorder.animationRequests)
		{
			if (req == null || req.type != AnimationRequestType.StatusEffectChange) continue;
			if (!req.applyDisplayDeltaOnProjectileSpawn) continue;
			if (req.displayDeltaApplied) continue;
			if (req.targetCard == null) continue;
			if (!involvedCards.Contains(req.targetCard)) continue;

			var targetCardScript = req.targetCard.GetComponent<CardScript>();
			if (targetCardScript != null)
			{
				targetCardScript.ApplyDisplayDelta(req.statusEffect, req.statusEffectDelta);
				req.displayDeltaApplied = true;
			}
		}
	}

	// VISUAL-FIX(2026-06-21): Consume/transfer source cards should update status text when projectile spawns
	//   Cause:    All StatusEffectChange deltas were deferred until the projectile landed, so cards
	//             losing status effects (consume/transfer) appeared to still have the effect until
	//             the projectile finished flying. Players expected the source card text to update
	//             the moment the status effect left the card.
	//   Fix:      Add applyDisplayDeltaOnProjectileSpawn flag. For consume/transfer source cards,
	//             RecorderAnimationPlayer applies the negative delta immediately when the projectile
	//             is spawned. Cards receiving status effects keep the existing deferred-until-land
	//             behavior.
	//   Affects:  AnimationRequest, RecorderAnimationPlayer, EffectScript, ConsumeStatusEffect,
	//             TransferStatusEffectEffect
	//   Regress:  Reveal OVERCHARGED_SUMMONER (ConsumeOwnStatusEffect), POWER_TRANSFER
	//             (ConsumeRandomEnemyCardsStatusEffect), CROW_CROWD/POWER_SIPHONER
	//             (TransferStatusEffectEffect). Check: source card status text updates at the
	//             start of projectile flight; target card status text still updates after landing.

	// VISUAL-FIX(2026-06-20): Per-projectile status effect display commit
	//   Cause:    CommitDisplayState() copied the full current myStatusEffects list, so when
	//             multiple effects gave status effects to the same card (e.g. PowerReactionEffect),
	//             the first projectile landing refreshed the text to include all pending layers.
	//   Fix:      Track statusEffectDelta per StatusEffectChange request, compute a pre-animation
	//             baseline, and apply deltas incrementally as each StatusEffectProjectile completes.
	//   Affects:  AnimationRequest, EffectScript, CardScript, RecorderAnimationPlayer, ConsumeStatusEffect
	//   Regress:  Reveal SACRIFICIAL_SWORD with POWER_CRAVER and WEAPON_SPIRIT in deck. Check that
	//             POWER_CRAVER's Power text shows 1 after the first projectile, then 2 after the
	//             WEAPON_SPIRIT reaction projectile lands.
	//   Related:  PRD power-reaction-status-effect-delta-commit-2026-06-20

	/// <summary>
	/// Pre-scan the entire recorder tree and compute a per-card display baseline.
	/// The baseline equals the current myStatusEffects minus all pending statusEffectDeltas,
	/// so GetStatusEffectsForDisplay() returns the state before any pending animations.
	/// </summary>
	private void ComputeAndApplyDisplayBaselines(List<GameObject> rootRecorders)
	{
		_baselineCards.Clear();

		var allRecorders = new List<EffectRecorder>();
		foreach (var root in rootRecorders)
		{
			if (root == null) continue;
			var recorder = root.GetComponent<EffectRecorder>();
			CollectAllRecorders(recorder, allRecorders);
		}

		var pendingDeltas = new Dictionary<CardScript, Dictionary<EnumStorage.StatusEffect, int>>();
		foreach (var recorder in allRecorders)
		{
			if (recorder == null || recorder.animationRequests == null) continue;
			foreach (var req in recorder.animationRequests)
			{
				if (req == null || req.type != AnimationRequestType.StatusEffectChange) continue;
				if (req.targetCard == null) continue;
				var targetCardScript = req.targetCard.GetComponent<CardScript>();
				if (targetCardScript == null) continue;

				if (!pendingDeltas.ContainsKey(targetCardScript))
					pendingDeltas[targetCardScript] = new Dictionary<EnumStorage.StatusEffect, int>();
				if (!pendingDeltas[targetCardScript].ContainsKey(req.statusEffect))
					pendingDeltas[targetCardScript][req.statusEffect] = 0;
				pendingDeltas[targetCardScript][req.statusEffect] += req.statusEffectDelta;

				_baselineCards.Add(targetCardScript);
			}
		}

		foreach (var cardEntry in pendingDeltas)
		{
			var card = cardEntry.Key;
			var baseline = new List<EnumStorage.StatusEffect>(card.myStatusEffects);
			foreach (var effectEntry in cardEntry.Value)
			{
				CardScript.ApplyStatusEffectDeltaToList(baseline, effectEntry.Key, -effectEntry.Value);
			}
			card.SetDisplayBaseline(baseline);
		}
	}

	/// <summary>
	/// Recursively collect all EffectRecorders in the recorder tree (depth-first).
	/// </summary>
	private void CollectAllRecorders(EffectRecorder recorder, List<EffectRecorder> result)
	{
		if (recorder == null) return;
		result.Add(recorder);
		for (int i = 0; i < recorder.transform.childCount; i++)
		{
			var child = recorder.transform.GetChild(i);
			if (child == null) continue;
			var childRecorder = child.GetComponent<EffectRecorder>();
			if (childRecorder != null)
				CollectAllRecorders(childRecorder, result);
		}
	}

	/// <summary>
	/// Apply all still-deferred StatusEffectChange deltas for the given target in the given recorder.
	/// Each delta is applied exactly once.
	/// </summary>
	private void ApplyDeferredDeltasForTarget(EffectRecorder recorder, CardScript targetCardScript)
	{
		if (recorder == null || targetCardScript == null) return;
		foreach (var req in recorder.animationRequests)
		{
			if (req == null || req.type != AnimationRequestType.StatusEffectChange) continue;
			if (req.displayDeltaApplied) continue;
			if (!req.deferDisplayCommit) continue;
			if (req.targetCard != targetCardScript.gameObject) continue;

			targetCardScript.ApplyDisplayDelta(req.statusEffect, req.statusEffectDelta);
			req.displayDeltaApplied = true;
		}
	}

	public IEnumerator PlayRequestCoroutine(AnimationRequest request)
	{
		if (request == null) yield break;
		if (CombatManager.Me == null) yield break;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

		// Diagnostic log: report whether key referenced objects are alive before playing this request.
		string targetState = "n/a";
		if (request.targetCard != null) targetState = request.targetCard.name + "(alive)";
		else if (request.targetCards != null) targetState = "batch[" + request.targetCards.Count + "]";
		string attackerState = request.attackerCard != null ? request.attackerCard.name + "(alive)" : "n/a";
		TestManager.Log("[RecorderAnimationPlayer] PlayRequest type=" + request.type + " target=" + targetState + " attacker=" + attackerState);

		// VISUAL-FIX(2026-05-18): Deck-move animations play in wrong peeled/focused layout
		//   Cause:    When deck is focused/peeled, bury/stage animations use peeled positions
		//             instead of normal deck layout, causing cards to fly to wrong spots
		//   Affects:  RecorderAnimationPlayer, MoveToBottomBatch, MoveToTopBatch, MoveToIndex, PopUp
		//   Regress:  Reveal any card with Bury or Stage while deck is focused (e.g. after clicking a card)
		//   Related:  Any card with BuryNextXCards or StageSelf
		if (request.type == AnimationRequestType.MoveToBottomBatch ||
		    request.type == AnimationRequestType.MoveToTopBatch ||
		    request.type == AnimationRequestType.MoveToBottom ||
		    request.type == AnimationRequestType.MoveToTop ||
		    request.type == AnimationRequestType.MoveToIndex ||
		    request.type == AnimationRequestType.PopUp ||
		    request.type == AnimationRequestType.SlotIn ||
		    request.type == AnimationRequestType.MoveToPopUpPosition ||
		    request.type == AnimationRequestType.PopUpBatch ||
		    request.type == AnimationRequestType.SlotInBatch ||
		    request.type == AnimationRequestType.MoveToTopPopUpBatch)
		{
			var combatUX = visuals as CombatUXManager;
			if (combatUX != null && combatUX.IsDeckFocused)
			{
				yield return combatUX.StartCoroutine(combatUX.RestoreDeckFocusCoroutine());
			}
		}
		TestManager.Log("[RecorderAnimationPlayer] PlayRequest type=" + request.type + " target=" + (request.targetCard != null ? request.targetCard.name : (request.attackerCard != null ? request.attackerCard.name : "null")));
		switch (request.type)
		{
			case AnimationRequestType.Attack:
			{
				if (request.attackerCard == null) break;
				bool done = false;
				visuals.PlayAttackAnimation(request.attackerCard, request.isAttackingEnemy, request.onHit, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToBottom:
			{
				if (request.targetCard == null) break;
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToBottom(request.targetCard, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToBottomBatch:
			{
				// Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch START targetCards=" + (request.targetCards != null ? request.targetCards.Count : 0) + " snapshot=" + (request.targetIndices != null ? string.Join(",", request.targetIndices) : "null") + " snapshotDeckSize=" + request.snapshotDeckSize);
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				int filteredCount = 0;
				for (int i = 0; i < totalCount; i++)
				{
					if (request.targetCards[i] != null) filteredCount++;
				}
				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				int currentCount = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.Count : 0;
				var combatUX = visuals as CombatUXManager;
				int physCount = combatUX != null ? combatUX.physicalCardsInDeck.Count : 0;
				// Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch deckCounts combined=" + currentCount + " physical=" + physCount);
				int processedIndex = 0;
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (card == null)
					{
						TestManager.Log("[RecorderAnimationPlayer] MoveToBottomBatch skipping destroyed card at index " + i);
						continue;
					}
					// Bury (MoveToBottomBatch) sends cards to the absolute bottom of the physical deck.
					// We read actualPhysIndex from physicalCardsInDeck after ApplyAnimationResult,
					// because reactive effects (e.g. StageSelf) or pending slot-in cards may have altered deck order.
					// correctedIndex (filteredCount - 1 - processedIndex) is kept as fallback only when physical card cannot be resolved.
					int correctedIndex = filteredCount - 1 - processedIndex;
					processedIndex++;
					correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
					int actualPhysIndex = -1;
					var combatUX2 = visuals as CombatUXManager;
					if (combatUX2 != null)
					{
						var phys = combatUX2.GetPhysicalCard(card);
						if (phys != null) actualPhysIndex = combatUX2.GetPhysicalCardDeckIndex(phys);
					}
					int targetIndex = actualPhysIndex >= 0 ? actualPhysIndex : correctedIndex;
					string snapshotIdxStr = (hasSnapshot && request.targetIndices != null && i < request.targetIndices.Count) ? request.targetIndices[i].ToString() : "null";
					TestManager.Log("[RecorderAnimationPlayer] MoveToBottomBatch calling MoveCardToIndex card=" + card.name + " snapshotIndex=" + snapshotIdxStr + " correctedIndex=" + correctedIndex + " actualPhysIndex=" + actualPhysIndex + " targetIndex=" + targetIndex + " currentCount=" + currentCount + " physCount=" + physCount);
					visuals.MoveCardToIndex(card, targetIndex, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= filteredCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= filteredCount);
				// Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch DONE");
				break;
			}
			case AnimationRequestType.MoveToTop:
			{
				if (request.targetCard == null) break;
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToTop(request.targetCard, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToTopBatch:
			{
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				// Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch START targetCards=" + (request.targetCards != null ? request.targetCards.Count : 0) + " snapshotDeckSize=" + request.snapshotDeckSize);
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				int filteredCount = 0;
				for (int i = 0; i < totalCount; i++)
				{
					if (request.targetCards[i] != null) filteredCount++;
				}
				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				int currentCount = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.Count : 0;
				var combatUX = visuals as CombatUXManager;
				int physCount = combatUX != null ? combatUX.physicalCardsInDeck.Count : 0;
				// Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch deckCounts combined=" + currentCount + " physical=" + physCount);
				int processedIndex = 0;
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (card == null)
					{
						TestManager.Log("[RecorderAnimationPlayer] MoveToTopBatch skipping destroyed card at index " + i);
						continue;
					}
					// Stage (MoveToTopBatch) sends cards to the absolute top of the physical deck.
					// We read actualPhysIndex from physicalCardsInDeck after ApplyAnimationResult,
					// because reactive effects or pending slot-in cards may have altered deck order.
					// correctedIndex (currentCount - filteredCount + processedIndex) is kept as fallback only when physical card cannot be resolved.
					int correctedIndex = currentCount - filteredCount + processedIndex;
					processedIndex++;
					correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
					int actualPhysIndex = -1;
					var combatUX2 = visuals as CombatUXManager;
					if (combatUX2 != null)
					{
						var phys = combatUX2.GetPhysicalCard(card);
						if (phys != null) actualPhysIndex = combatUX2.GetPhysicalCardDeckIndex(phys);
					}
					int targetIndex = actualPhysIndex >= 0 ? actualPhysIndex : correctedIndex;
					string snapshotIdxStr = (hasSnapshot && request.targetIndices != null && i < request.targetIndices.Count) ? request.targetIndices[i].ToString() : "null";
					TestManager.Log("[RecorderAnimationPlayer] MoveToTopBatch calling MoveCardToIndex card=" + card.name + " snapshotIndex=" + snapshotIdxStr + " correctedIndex=" + correctedIndex + " actualPhysIndex=" + actualPhysIndex + " targetIndex=" + targetIndex + " currentCount=" + currentCount + " physCount=" + physCount);
					visuals.MoveCardToIndex(card, targetIndex, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= filteredCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= filteredCount);
				// Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch DONE");
				break;
			}
			case AnimationRequestType.MoveToTopPopUpBatch:
			{
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();

				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;

				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				int currentCount = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.Count : 0;

				// Build final indices by reading actualPhysIndex from physicalCardsInDeck after ApplyAnimationResult.
				// Fallback to correctedIndex (currentCount - totalCount + i) if physical card cannot be resolved.
				// NOTE: We pass these finalIndices into MoveCardToTopPopUpBatch rather than letting it look up
				// indices itself, because the two-phase parallel animation needs every card's final deck
				// position before Phase 1 starts (to compute pop-up peaks). Other batch types call
				// MoveCardToIndex per-card, so they resolve indices individually inside the loop.
				var filteredCards = new List<GameObject>();
				int filteredCount = 0;
				for (int i = 0; i < totalCount; i++)
				{
					if (request.targetCards[i] != null) filteredCount++;
				}
				var finalIndices = new List<int>();
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (card == null)
					{
						TestManager.Log("[RecorderAnimationPlayer] MoveToTopPopUpBatch skipping destroyed card at index " + i);
						continue;
					}
					int correctedIndex = currentCount - filteredCount + filteredCards.Count;
					correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
					int actualPhysIndex = -1;
					var combatUX2 = visuals as CombatUXManager;
					if (combatUX2 != null)
					{
						var phys = combatUX2.GetPhysicalCard(card);
						if (phys != null) actualPhysIndex = combatUX2.GetPhysicalCardDeckIndex(phys);
					}
					int targetIndex = actualPhysIndex >= 0 ? actualPhysIndex : correctedIndex;
					string snapshotIdxStr = (hasSnapshot && request.targetIndices != null && i < request.targetIndices.Count) ? request.targetIndices[i].ToString() : "null";
					TestManager.Log("[RecorderAnimationPlayer] MoveToTopPopUpBatch card=" + card.name + " snapshotIndex=" + snapshotIdxStr + " correctedIndex=" + correctedIndex + " actualPhysIndex=" + actualPhysIndex + " targetIndex=" + targetIndex + " currentCount=" + currentCount);
					filteredCards.Add(card);
					finalIndices.Add(targetIndex);
				}

				bool done = false;
				visuals.MoveCardToTopPopUpBatch(filteredCards, finalIndices, request.duration, () => { done = true; });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToIndex:
			{
				if (request.targetCard == null) break;
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToIndex(request.targetCard, request.targetIndex, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.Destroy:
			{
				if (request.targetCard == null) break;
				visuals.ApplyAnimationResult(request);
				bool done = false;
				visuals.DestroyCardWithAnimation(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.StatusEffectChange:
			{
				if (request.targetCard == null) break;
				var targetCardScript = request.targetCard.GetComponent<CardScript>();
				if (targetCardScript == null) break;

				if (request.statusEffectParticlePrefab != null)
				{
					visuals.PlayStatusEffectParticle(
						targetCardScript,
						request.statusEffectParticlePrefab,
						request.statusEffectParticleYOffset,
						Mathf.Abs(request.statusEffectAmount));
				}

				if (request.statusEffect == EnumStorage.StatusEffect.Infected ||
				    request.statusEffect == EnumStorage.StatusEffect.Power)
				{
					visuals.ApplyStatusTint(targetCardScript, request.statusEffect);
				}

				if (!request.deferDisplayCommit && !request.displayDeltaApplied)
				{
					targetCardScript.ApplyDisplayDelta(request.statusEffect, request.statusEffectDelta);
					request.displayDeltaApplied = true;
				}

				request.onComplete?.Invoke();
				break;
			}
			case AnimationRequestType.StatusEffectProjectile:
			{
				if (request.attackerCard == null && (request.attackerCards == null || request.attackerCards.Count == 0)) break;

				// Consume/transfer source cards update display as soon as the projectile spawns.
				ApplySpawnDeltasForProjectile(_currentRecorder, request);

				// VISUAL-FIX(2026-06-12): Support projectile flying to a custom world position
				//   Cause:    ConsumeOwnStatusEffect needed a projectile that flies to statusEffectConsumePos
				//             instead of to another card, but StatusEffectProjectile only supported card targets.
				//   Affects:  AnimationRequest, ICombatVisuals, CombatUXManager, RecorderAnimationPlayer, ConsumeStatusEffect
				//   Regress:  Reveal a card whose effect calls ConsumeOwnStatusEffect (e.g. OVERCHARGED_SUMMONER)
				//             Check: card pops up, projectile flies toward statusEffectConsumePos, then slots back in.
				// VISUAL-FIX(2026-06-13): ConsumeHostileCursePower needs projectiles from multiple targets to a custom position
				//   Cause:    ConsumeHostileCursePower consumes Power from multiple enemy curse cards and the
				//             absorbed power should fly toward statusEffectConsumePos, but StatusEffectProjectile
				//             only supported single-source-to-position or target-to-giver-card paths.
				//   Affects:  CurseEffect, EffectScript, CombatUXManager, RecorderAnimationPlayer, AnimationRequest
				//   Regress:  Reveal CURSE_SUMMONER or PREMATURE when multiple enemy curse cards carry Power
				//             Check: all target curse cards pop up together, projectiles fly from each target
				//             toward statusEffectConsumePos in parallel, status text updates after projectiles land,
				//             then all targets slot back in together.
				if (request.customProjectileEndPosition.HasValue)
				{
					// Multi-target reverse absorption to a custom world position.
					if (request.reverseProjectile && request.targetCards != null && request.targetCards.Count > 0)
					{
						var customTargetCardScripts = new List<CardScript>();
						foreach (var t in request.targetCards)
						{
							if (t == null) continue;
							var cs = t.GetComponent<CardScript>();
							if (cs != null) customTargetCardScripts.Add(cs);
						}

						if (customTargetCardScripts.Count > 0)
						{
							bool multiCustomDone = false;
							visuals.PlayMultiStatusEffectProjectile(
								request.attackerCard,
								customTargetCardScripts,
								onEachComplete: null, // logic already resolved in logic phase
								onAllComplete: () => { multiCustomDone = true; },
								customStaggerDelay: null,
								projectileCount: request.projectileCount,
								projectileStartRandomOffsetRange: request.projectileStartRandomOffsetRange.sqrMagnitude > 0f ? request.projectileStartRandomOffsetRange : (Vector2?)null,
								projectileStartTimeStaggerRange: request.projectileStartTimeStaggerRange.sqrMagnitude > 0f ? request.projectileStartTimeStaggerRange : (Vector2?)null,
								reverseDirection: true,
								customEndPosition: request.customProjectileEndPosition.Value,
								projectileCountsPerTarget: request.projectileCountsPerTarget
							);
							yield return new WaitUntil(() => multiCustomDone);

							foreach (var targetCardScript in customTargetCardScripts)
							{
								if (targetCardScript != null)
								{
									ApplyDeferredDeltasForTarget(_currentRecorder, targetCardScript);
								}
							}
						}
						break;
					}

					// Single-source projectile to a custom world position.
					bool customDone = false;
					visuals.PlayStatusEffectProjectileToPosition(
						request.attackerCard,
						request.customProjectileEndPosition.Value,
						onComplete: () => { customDone = true; },
						projectileCount: request.projectileCount,
						projectileStartRandomOffsetRange: request.projectileStartRandomOffsetRange.sqrMagnitude > 0f ? request.projectileStartRandomOffsetRange : (Vector2?)null,
						projectileStartTimeStaggerRange: request.projectileStartTimeStaggerRange.sqrMagnitude > 0f ? request.projectileStartTimeStaggerRange : (Vector2?)null
					);
					yield return new WaitUntil(() => customDone);

					// After projectile completes, apply deferred display deltas for the source/target card.
					if (request.targetCard != null)
					{
						var targetCardScript = request.targetCard.GetComponent<CardScript>();
						if (targetCardScript != null)
						{
							ApplyDeferredDeltasForTarget(_currentRecorder, targetCardScript);
						}
					}
					break;
				}

				// VISUAL-FIX(2026-06-13): Support multiple source cards projecting toward a single target card
				//   Cause:    TransferStatusEffectEffect moves status effects from several source cards
				//             to one target card, but StatusEffectProjectile only supported one attacker
				//             or one attacker to many targets. We need many attackers to one target.
				//   Affects:  AnimationRequest, RecorderAnimationPlayer, CombatUXManager, EffectScript,
				//             TransferStatusEffectEffect
				//   Regress:  Reveal CROW_CROWD or POWER_SIPHONER with multiple source cards carrying status
				//             Check: all source cards pop up, a single projectile (or one per layer) flies
				//             from each source to the target in parallel, target display commits only after
				//             the last projectile lands, then all cards slot back in.
				if (request.attackerCards != null && request.attackerCards.Count > 0 && request.targetCard != null)
				{
					var sourceCardScripts = new List<CardScript>();
					foreach (var s in request.attackerCards)
					{
						if (s == null) continue;
						var cs = s.GetComponent<CardScript>();
						if (cs != null) sourceCardScripts.Add(cs);
					}

					if (sourceCardScripts.Count > 0)
					{
						bool multiSourceDone = false;
						visuals.PlayMultiStatusEffectProjectile(
							request.targetCard,
							sourceCardScripts,
							onEachComplete: null, // logic already resolved in logic phase
							onAllComplete: () => { multiSourceDone = true; },
							customStaggerDelay: null,
							projectileCount: request.projectileCount,
							projectileStartRandomOffsetRange: request.projectileStartRandomOffsetRange.sqrMagnitude > 0f ? request.projectileStartRandomOffsetRange : (Vector2?)null,
							projectileStartTimeStaggerRange: request.projectileStartTimeStaggerRange.sqrMagnitude > 0f ? request.projectileStartTimeStaggerRange : (Vector2?)null,
							reverseDirection: true,
							customEndPosition: null,
							projectileCountsPerTarget: request.projectileCountsPerTarget
						);
						yield return new WaitUntil(() => multiSourceDone);

						// Apply deferred display deltas for the target card only after ALL source projectiles land.
						var targetCardScript = request.targetCard.GetComponent<CardScript>();
						if (targetCardScript != null)
						{
							ApplyDeferredDeltasForTarget(_currentRecorder, targetCardScript);
						}
					}
					break;
				}

				// Build target list: prefer batch list, fall back to single targetCard
				var targetCardScripts = new List<CardScript>();
				if (request.targetCards != null && request.targetCards.Count > 0)
				{
					foreach (var t in request.targetCards)
					{
						if (t == null) continue;
						var cs = t.GetComponent<CardScript>();
						if (cs != null) targetCardScripts.Add(cs);
					}
				}
				else if (request.targetCard != null)
				{
					var cs = request.targetCard.GetComponent<CardScript>();
					if (cs != null) targetCardScripts.Add(cs);
				}

				if (targetCardScripts.Count == 0) break;

				bool done = false;
				visuals.PlayMultiStatusEffectProjectile(
					request.attackerCard,
					targetCardScripts,
					onEachComplete: null, // logic already resolved in logic phase
					onAllComplete: () => { done = true; },
					customStaggerDelay: null,
					projectileCount: request.projectileCount,
					projectileStartRandomOffsetRange: request.projectileStartRandomOffsetRange.sqrMagnitude > 0f ? request.projectileStartRandomOffsetRange : (Vector2?)null,
					projectileStartTimeStaggerRange: request.projectileStartTimeStaggerRange.sqrMagnitude > 0f ? request.projectileStartTimeStaggerRange : (Vector2?)null,
					reverseDirection: request.reverseProjectile,
					customEndPosition: null,
					projectileCountsPerTarget: request.projectileCountsPerTarget
				);
				yield return new WaitUntil(() => done);

				// After projectile completes, apply deferred display deltas for all targets.
				foreach (var targetCardScript in targetCardScripts)
				{
					if (targetCardScript != null)
					{
						ApplyDeferredDeltasForTarget(_currentRecorder, targetCardScript);
					}
				}
				break;
			}
			case AnimationRequestType.PopUp:
			{
				if (request.targetCard == null) break;
				bool done = false;
				visuals.PopUpCard(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToPopUpPosition:
			{
				if (request.targetCard == null) break;
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToPopUpPosition(request.targetCard, request.targetIndex, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.SlotIn:
			{
				if (request.targetCard == null) break;
				bool done = false;
				visuals.SlotInCard(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.PopUpBatch:
			{
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				int filteredCount = 0;
				for (int i = 0; i < totalCount; i++)
				{
					if (request.targetCards[i] != null) filteredCount++;
				}
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (card == null)
					{
						TestManager.Log("[RecorderAnimationPlayer] PopUpBatch skipping destroyed card at index " + i);
						continue;
					}
					visuals.PopUpCard(card, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= filteredCount)
							request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= filteredCount);
				break;
			}
			case AnimationRequestType.SlotInBatch:
			{
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				int filteredCount = 0;
				for (int i = 0; i < totalCount; i++)
				{
					if (request.targetCards[i] != null) filteredCount++;
				}
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (card == null)
					{
						TestManager.Log("[RecorderAnimationPlayer] SlotInBatch skipping destroyed card at index " + i);
						continue;
					}
					visuals.SlotInCard(card, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= filteredCount)
							request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= filteredCount);
				break;
			}
			case AnimationRequestType.Shuffle:
			{
				// Sync physical deck order before animation starts
				visuals.ApplyAnimationResult(request);

				bool done = false;
				visuals.PlayShuffleAnimation(request.sourceCard, request.targetCards, () =>
				{
					done = true;
					if (request.onComplete != null) request.onComplete();
				});
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.Shake:
			{
				if (request.targetCard == null) break;
				GameObject physicalCard = visuals.GetPhysicalCard(request.targetCard);
				if (physicalCard != null)
				{
					var physScript = physicalCard.GetComponent<CardPhysObjScript>();
					if (physScript != null)
					{
						bool done = false;
						physScript.PlayCustomShake(() => { done = true; });
						yield return new WaitUntil(() => done);
					}
				}
				break;
			}
		}
	}
}
