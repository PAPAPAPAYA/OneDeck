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
		Debug.Log("[RecorderAnimationPlayer] === Playing recorder chainID=" + recorder.chainID + " card=" + cardName + " requests=" + recorder.animationRequests.Count + " childCount=" + recorder.transform.childCount);

		// Emphasize the source card before playing its effect animations
		if (recorder.animationRequests.Count > 0 && recorder.cardObject != null)
		{
			yield return StartCoroutine(PlayEmphasizeAnimation(recorder.cardObject));
		}
		else
		{
			Debug.Log("[RecorderAnimationPlayer] Skipping emphasize: requests=" + recorder.animationRequests.Count + " card=" + cardName);
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
				visuals.ApplyAnimationResult(request);
				visuals.UpdateAllPhysicalCardTargets();
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (hasSnapshot)
					{
						visuals.MoveCardToIndex(card, request.targetIndices[i], request.duration, request.useArc, () =>
						{
							completedCount++;
							if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
						});
					}
					else
					{
						visuals.MoveCardToBottom(card, request.duration, request.useArc, () =>
						{
							completedCount++;
							if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
						});
					}
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
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
				Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch START targetCards=" + (request.targetCards != null ? request.targetCards.Count : 0));
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				bool hasSnapshot = request.targetIndices != null && request.targetIndices.Count == totalCount;
				for (int i = 0; i < totalCount; i++)
				{
					var card = request.targetCards[i];
					if (hasSnapshot)
					{
						Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch calling MoveCardToIndex for " + card.name + " index=" + request.targetIndices[i]);
						visuals.MoveCardToIndex(card, request.targetIndices[i], request.duration, request.useArc, () =>
						{
							completedCount++;
							if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
						});
					}
					else
					{
						Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch calling MoveCardToTop for " + card.name + " revealZone=" + (CombatManager.Me != null && CombatManager.Me.revealZone != null ? CombatManager.Me.revealZone.name : "null"));
						visuals.MoveCardToTop(card, request.duration, request.useArc, () =>
						{
							completedCount++;
							if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
						});
					}
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
				Debug.Log("[RecorderAnimationPlayer] MoveToTopBatch DONE");
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
				if (request.attackerCard == null || request.targetCard == null) break;
				var targetCardScript = request.targetCard.GetComponent<CardScript>();
				if (targetCardScript == null) break;

				bool done = false;
				visuals.PlayMultiStatusEffectProjectile(
					request.attackerCard,
					new List<CardScript> { targetCardScript },
					onEachComplete: null, // logic already resolved in logic phase
					onAllComplete: () => { done = true; }
				);
				yield return new WaitUntil(() => done);
				break;
			}
		}
	}
}
