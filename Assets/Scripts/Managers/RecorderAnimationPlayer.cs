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

		// Emphasize the source card before playing its effect animations
		if (recorder.animationRequests.Count > 0 && recorder.cardObject != null)
		{
			yield return StartCoroutine(PlayEmphasizeAnimation(recorder.cardObject));
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
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToBottom(request.targetCard, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToBottomBatch:
			{
				visuals.UpdateAllPhysicalCardTargets();
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				foreach (var card in request.targetCards)
				{
					visuals.MoveCardToBottom(card, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
				break;
			}
			case AnimationRequestType.MoveToTop:
			{
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToTop(request.targetCard, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.MoveToTopBatch:
			{
				visuals.UpdateAllPhysicalCardTargets();
				int completedCount = 0;
				int totalCount = request.targetCards != null ? request.targetCards.Count : 0;
				if (totalCount == 0) break;
				foreach (var card in request.targetCards)
				{
					visuals.MoveCardToTop(card, request.duration, request.useArc, () =>
					{
						completedCount++;
						if (request.onComplete != null && completedCount >= totalCount) request.onComplete();
					});
				}
				yield return new WaitUntil(() => completedCount >= totalCount);
				break;
			}
			case AnimationRequestType.MoveToIndex:
			{
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToIndex(request.targetCard, request.targetIndex, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
			case AnimationRequestType.Destroy:
			{
				bool done = false;
				visuals.DestroyCardWithAnimation(request.targetCard, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				yield return new WaitUntil(() => done);
				break;
			}
		}
	}
}
