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

		// Emphasize the source card before playing its effect animations
		if (recorder.animationRequests.Count > 0 && recorder.cardObject != null)
		{
			yield return StartCoroutine(PlayEmphasizeAnimation(recorder.cardObject));
		}
		else
		{
			TestManager.Log("[RecorderAnimationPlayer] Skipping emphasize: requests=" + recorder.animationRequests.Count + " card=" + cardName);
		}

		// Pre-scan to mark StatusEffectChange requests that should defer display commit
		// until their corresponding StatusEffectProjectile animation completes
		MarkDeferredDisplayCommits(recorder);

		// Play all requests of this effect instance sequentially
		foreach (var request in recorder.animationRequests)
		{
			if (request != null)
			{
				yield return StartCoroutine(PlayRequestCoroutine(request));
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
	/// Play emphasize animation (scale up then back to original) on the card that triggered the effect.
	/// </summary>
	private IEnumerator PlayEmphasizeAnimation(GameObject logicalCard)
	{
		if (logicalCard == null) yield break;
		if (CombatManager.Me == null) yield break;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

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
			physScript.isPlayingSpecialAnimation = false;
			AnimationStateTracker.me?.CompleteAnimation();
			done = true;
		});

		yield return new WaitUntil(() => done);
	}

	/// <summary>
	/// Pre-scan a recorder's animation requests to mark StatusEffectChange requests
	/// that should defer their display commit until the corresponding
	/// StatusEffectProjectile animation completes.
	/// </summary>
	private void MarkDeferredDisplayCommits(EffectRecorder recorder)
	{
		if (recorder == null || recorder.animationRequests == null) return;

		// Collect all targets that will receive a StatusEffectProjectile in this recorder
		var projectileTargets = new HashSet<GameObject>();
		foreach (var req in recorder.animationRequests)
		{
			if (req == null || req.type != AnimationRequestType.StatusEffectProjectile) continue;
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

				if (!request.deferDisplayCommit)
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
