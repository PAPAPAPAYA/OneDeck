using System;
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using DG.Tweening;
using UnityEngine;

public class RecorderAnimationPlayer : MonoBehaviour
{
	public static RecorderAnimationPlayer me;

	void Awake()
	{
		me = this;
	}

	public IEnumerator PlayRecordersCoroutine(List<GameObject> rootRecorders)
	{
		Debug.Log("[RecorderAnimationPlayer] PlayRecordersCoroutine START rootCount=" + rootRecorders.Count);
		AttackAnimationManager.me?.HoldDeckFocus();
		try
		{
			foreach (var rootRecorder in rootRecorders)
			{
				if (rootRecorder == null) continue;
				var recorder = rootRecorder.GetComponent<EffectRecorder>();
				if (recorder == null || recorder.animationPlayed) continue;
				yield return StartCoroutine(PlayRecorderCoroutine(recorder));
			}
		}
		finally
		{
			AttackAnimationManager.me?.ReleaseDeckFocus();
		}
	}

	public IEnumerator PlayRecorderCoroutine(EffectRecorder recorder)
	{
		if (recorder == null || recorder.animationPlayed) yield break;
		recorder.animationPlayed = true;

		string cardName = recorder.cardObject != null ? recorder.cardObject.name : "null";
		Debug.Log("[RecorderAnimationPlayer] PlayRecorderCoroutine chainID=" + recorder.chainID + " card=" + cardName + " animationPlayed SET TO TRUE");
		string reqSummary = "";
		for (int i = 0; i < recorder.animationRequests.Count; i++)
		{
			var r = recorder.animationRequests[i];
			reqSummary += "[" + i + "]" + (r != null ? r.type.ToString() : "null") + " ";
		}
		// Debug.Log("[RecorderAnimationPlayer] === Playing recorder chainID=" + recorder.chainID + " card=" + cardName + " requests=" + recorder.animationRequests.Count + " childCount=" + recorder.transform.childCount + " reqs=" + reqSummary);

		// Emphasize the source card before playing its effect animations
		if (recorder.animationRequests.Count > 0 && recorder.cardObject != null)
		{
			yield return StartCoroutine(PlayEmphasizeAnimation(recorder.cardObject));
		}
		else
		{
			Debug.Log("[RecorderAnimationPlayer] Skipping emphasize: requests=" + recorder.animationRequests.Count + " card=" + cardName);
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
			var childRecorder = child.GetComponent<EffectRecorder>();
			if (childRecorder != null && !childRecorder.animationPlayed)
			{
				yield return StartCoroutine(PlayRecorderCoroutine(childRecorder));
			}
		}
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
		float halfDuration = 0.25f;

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

	public IEnumerator PlayRequestCoroutine(AnimationRequest request)
	{
		if (request == null) yield break;
		if (CombatManager.Me == null) yield break;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

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
		Debug.Log("[RecorderAnimationPlayer] PlayRequest type=" + request.type + " target=" + (request.targetCard != null ? request.targetCard.name : (request.attackerCard != null ? request.attackerCard.name : "null")));
		switch (request.type)
		{
			case AnimationRequestType.Attack:
			{
				bool done = false;
				visuals.PlayAttackAnimation(request.attackerCard, request.isAttackingEnemy, request.onHit, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToBottom:
			{
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
				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				int currentCount = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.Count : 0;
				var combatUX = visuals as CombatUXManager;
				int physCount = combatUX != null ? combatUX.physicalCardsInDeck.Count : 0;
				// Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch deckCounts combined=" + currentCount + " physical=" + physCount);
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					// Bury (MoveToBottomBatch) sends cards to the absolute bottom of the physical deck.
					// We read actualPhysIndex from physicalCardsInDeck after ApplyAnimationResult,
					// because reactive effects (e.g. StageSelf) or pending slot-in cards may have altered deck order.
					// correctedIndex (totalCount - 1 - i) is kept as fallback only when physical card cannot be resolved.
					int correctedIndex = totalCount - 1 - i;
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
					Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch calling MoveCardToIndex card=" + card.name + " snapshotIndex=" + snapshotIdxStr + " correctedIndex=" + correctedIndex + " actualPhysIndex=" + actualPhysIndex + " targetIndex=" + targetIndex + " currentCount=" + currentCount + " physCount=" + physCount);
					visuals.MoveCardToIndex(card, targetIndex, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
				// Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch DONE");
				break;
			}
			case AnimationRequestType.MoveToTop:
			{
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
				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				int currentCount = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.Count : 0;
				var combatUX = visuals as CombatUXManager;
				int physCount = combatUX != null ? combatUX.physicalCardsInDeck.Count : 0;
				// Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch deckCounts combined=" + currentCount + " physical=" + physCount);
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					// Stage (MoveToTopBatch) sends cards to the absolute top of the physical deck.
					// We read actualPhysIndex from physicalCardsInDeck after ApplyAnimationResult,
					// because reactive effects or pending slot-in cards may have altered deck order.
					// correctedIndex (currentCount - totalCount + i) is kept as fallback only when physical card cannot be resolved.
					int correctedIndex = currentCount - totalCount + i;
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
					Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch calling MoveCardToIndex card=" + card.name + " snapshotIndex=" + snapshotIdxStr + " correctedIndex=" + correctedIndex + " actualPhysIndex=" + actualPhysIndex + " targetIndex=" + targetIndex + " currentCount=" + currentCount + " physCount=" + physCount);
					visuals.MoveCardToIndex(card, targetIndex, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
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
				var finalIndices = new List<int>();
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					int correctedIndex = currentCount - totalCount + i;
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
					Debug.Log("[RecorderAnimationPlayer] MoveToTopPopUpBatch card=" + card.name + " snapshotIndex=" + snapshotIdxStr + " correctedIndex=" + correctedIndex + " actualPhysIndex=" + actualPhysIndex + " targetIndex=" + targetIndex + " currentCount=" + currentCount);
					finalIndices.Add(targetIndex);
				}

				bool done = false;
				visuals.MoveCardToTopPopUpBatch(request.targetCards, finalIndices, request.duration, () => { done = true; });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToIndex:
			{
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToIndex(request.targetCard, request.targetIndex, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.Destroy:
			{
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
					targetCardScript.CommitDisplayState();
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
									targetCardScript.CommitDisplayState();
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

					// After projectile completes, commit display state for the source/target card
					// (for targets whose StatusEffectChange was deferred)
					if (request.targetCard != null)
					{
						var targetCardScript = request.targetCard.GetComponent<CardScript>();
						if (targetCardScript != null)
						{
							targetCardScript.CommitDisplayState();
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

						// Commit target card display state only after ALL source projectiles land
						var targetCardScript = request.targetCard.GetComponent<CardScript>();
						if (targetCardScript != null)
						{
							targetCardScript.CommitDisplayState();
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

				// After projectile completes, commit display state for all targets
				// (for targets whose StatusEffectChange was deferred)
				foreach (var targetCardScript in targetCardScripts)
				{
					if (targetCardScript != null)
					{
						targetCardScript.CommitDisplayState();
					}
				}
				break;
			}
			case AnimationRequestType.PopUp:
			{
				bool done = false;
				visuals.PopUpCard(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToPopUpPosition:
			{
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToPopUpPosition(request.targetCard, request.targetIndex, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.SlotIn:
			{
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
				foreach (var card in request.targetCards)
				{
					visuals.PopUpCard(card, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount)
							request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
				break;
			}
			case AnimationRequestType.SlotInBatch:
			{
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				foreach (var card in request.targetCards)
				{
					visuals.SlotInCard(card, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount)
							request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
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
