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
		// Debug.Log("[RecorderAnimationPlayer] PlayRecordersCoroutine START rootCount=" + rootRecorders.Count);
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
			// Debug.Log("[RecorderAnimationPlayer] Skipping emphasize: requests=" + recorder.animationRequests.Count + " card=" + cardName);
		}

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

	public IEnumerator PlayRequestCoroutine(AnimationRequest request)
	{
		if (request == null) yield break;
		if (CombatManager.Me == null) yield break;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;

		// ------------------------------------------------------------------
		// BUG FIX (E): Deck-move animations must play in normal deck layout.
		// If deck is currently focused/peeled, restore it before any card
		// position changes. Attack animations keep the focused state so
		// consecutive attacks can shift focus smoothly via TransitionFocus.
		// Do NOT remove this guard — removing it will regress the bug where
		// bury/stage animations play in the wrong peeled layout.
		// ------------------------------------------------------------------
		if (request.type == AnimationRequestType.MoveToBottomBatch ||
		    request.type == AnimationRequestType.MoveToTopBatch ||
		    request.type == AnimationRequestType.MoveToBottom ||
		    request.type == AnimationRequestType.MoveToTop ||
		    request.type == AnimationRequestType.MoveToIndex ||
		    request.type == AnimationRequestType.PopUp ||
		    request.type == AnimationRequestType.SlotIn ||
		    request.type == AnimationRequestType.MoveToPopUpPosition ||
		    request.type == AnimationRequestType.PopUpBatch ||
		    request.type == AnimationRequestType.SlotInBatch)
		{
			var combatUX = visuals as CombatUXManager;
			if (combatUX != null && combatUX.IsDeckFocused)
			{
				yield return combatUX.StartCoroutine(combatUX.RestoreDeckFocusCoroutine());
			}
		}
		// ------------------------------------------------------------------
		// END BUG FIX (E)
		// ------------------------------------------------------------------

		// Debug.Log("[RecorderAnimationPlayer] PlayRequest type=" + request.type + " target=" + (request.targetCard != null ? request.targetCard.name : (request.attackerCard != null ? request.attackerCard.name : "null")));
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
					// ApplyAnimationResult inserts them at index 0 in forward order,
					// so targetCards[i] ends up at (totalCount - 1 - i).
					// We intentionally do NOT use snapshotDeckSize offset here,
					// because Bury/Stage are absolute-position operations, not relative shifts.
					int correctedIndex = totalCount - 1 - i;
					correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
					// Debug.Log("[RecorderAnimationPlayer] MoveToBottomBatch calling MoveCardToIndex for " + card.name + " snapshotIndex=" + request.targetIndices[i] + " snapshotDeckSize=" + request.snapshotDeckSize + " currentCount=" + currentCount + " correctedIndex=" + correctedIndex);
					visuals.MoveCardToIndex(card, correctedIndex, request.duration, request.useArc, () =>
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
					// ApplyAnimationResult appends them in forward order,
					// so targetCards[i] ends up at (currentCount - totalCount + i).
					// We intentionally do NOT use snapshotDeckSize offset here,
					// because Bury/Stage are absolute-position operations, not relative shifts.
					int correctedIndex = currentCount - totalCount + i;
					correctedIndex = Mathf.Clamp(correctedIndex, 0, currentCount - 1);
					// Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch calling MoveCardToIndex for " + card.name + " snapshotIndex=" + request.targetIndices[i] + " snapshotDeckSize=" + request.snapshotDeckSize + " currentCount=" + currentCount + " correctedIndex=" + correctedIndex);
					visuals.MoveCardToIndex(card, correctedIndex, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
				// Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch DONE");
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

				request.onComplete?.Invoke();
				break;
			}
			case AnimationRequestType.StatusEffectProjectile:
			{
				if (request.attackerCard == null) break;

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
					onAllComplete: () => { done = true; }
				);
				yield return new WaitUntil(() => done);
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
		}
	}
}
