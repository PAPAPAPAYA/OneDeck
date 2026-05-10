using System;
using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
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
		string rootInfo = "";
		foreach (var r in rootRecorders)
		{
			if (r == null) continue;
			var rec = r.GetComponent<EffectRecorder>();
			if (rec != null) rootInfo += "chain#" + rec.chainID + "[" + rec.cardObject.name + "/" + rec.effectObject.name + "];";
		}


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
		
		string reqSummary = "";
		foreach (var r in recorder.animationRequests)
		{
			if (r != null) reqSummary += r.type.ToString() + ";";
		}


		// Play all requests of this effect instance sequentially without interleaving children
		for (int reqIndex = 0; reqIndex < recorder.animationRequests.Count; reqIndex++)
		{
			var request = recorder.animationRequests[reqIndex];
			if (request != null)

			
			yield return StartCoroutine(PlayRequestCoroutine(request));
		}

		// After all requests of this effect instance are done, recurse into children (effect-instance-boundary interleave)
		if (recorder.transform.childCount > 0)
		{

		}
		
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

	public IEnumerator PlayRequestCoroutine(AnimationRequest request)
	{
		if (request == null) yield break;
		if (CombatManager.Me == null) yield break;
		var visuals = CombatManager.Me.visuals;
		if (visuals == null) yield break;
		
		string attackerName = request.attackerCard != null ? request.attackerCard.name : "null";
		string targetName = request.targetCard != null ? request.targetCard.name : (request.targetCards != null ? "count=" + request.targetCards.Count : "null");


		switch (request.type)
		{
			case AnimationRequestType.Attack:
			{
				bool done = false;
				visuals.PlayAttackAnimation(request.attackerCard, request.isAttackingEnemy, request.onHit, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				while (!done) yield return null;
				break;
			}
			case AnimationRequestType.MoveToBottom:
			{
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToBottom(request.targetCard, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				while (!done) yield return null;
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
				while (completedCount < totalCount) yield return null;
				break;
			}
			case AnimationRequestType.MoveToTop:
			{
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToTop(request.targetCard, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				while (!done) yield return null;
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
				while (completedCount < totalCount) yield return null;
				break;
			}
			case AnimationRequestType.MoveToIndex:
			{
				visuals.UpdateAllPhysicalCardTargets();
				bool done = false;
				visuals.MoveCardToIndex(request.targetCard, request.targetIndex, request.duration, request.useArc, () => { done = true; if (request.onComplete != null) request.onComplete(); });
				while (!done) yield return null;
				break;
			}
		}
		

	}
}
