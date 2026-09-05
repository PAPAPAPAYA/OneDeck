using System;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;
using DefaultNamespace.Managers;

public class EffectChainManager : MonoBehaviour
{
	#region SINGLETON

	public static EffectChainManager Me;

	private void Awake()
	{
		Me = this;
	}

	#endregion
	[Header("NEED TO ASSIGN")]
	public GameObject effectRecorderPrefab;
	public IntSO sessionNumberRef;
	[Header("VIEW ONLY")]
	public int chainNumber;
	public GameObject currentEffectRecorder
	{
		get { return recorderStack.Count > 0 ? recorderStack[recorderStack.Count - 1] : null; }
	}
	public GameObject currentEffectRecorderParent; // tracks current chain parent
	public GameObject lastEffectObject; // tracks last effect inst
	public List<GameObject> openedEffectRecorders; // tracks opened effect containers
	public List<GameObject> closedEffectRecorders; // tracks closed effect containers
	public int chainDepth; // chain depth to prevent stack overflow, currently when depth reached 99 effect will not be processed
	
	// Stack to track nested recorder creation. Each InvokeEffectEvent pushes its recorder,
	// then pops it after effect execution. This prevents currentEffectRecorder from being
	// overwritten by synchronous reactive effects triggered during execution.
	private List<GameObject> recorderStack = new List<GameObject>();
	
	public void CheckShouldIStartANewChain(GameObject myCard, GameObject myEffectObj)
	{
		var shouldIMakeANewChain = false;
		bool sameCardDiffObj = false;
		
		if (openedEffectRecorders.Count == 0) // if no opened chains
		{
			shouldIMakeANewChain = true;
		}
		else
		{
			sameCardDiffObj = SameCardDifferentObject(myCard, myEffectObj);
			if (sameCardDiffObj)
			{
				shouldIMakeANewChain = true;
			}
		}

		if (shouldIMakeANewChain)
		{
			CloseOpenedChain();
			currentEffectRecorderParent = null;
		}
	}
	
	// check if opened chain contains same card, different effect object
	private bool SameCardDifferentObject(GameObject myCard, GameObject myEffectInst)
	{
		foreach (var chain in openedEffectRecorders)
		{
			var openedChainScript = chain.GetComponent<EffectRecorder>();
			bool sameCard = openedChainScript.cardObject.Equals(myCard);
			bool sameEffect = openedChainScript.effectObject.Equals(myEffectInst);

			if (sameCard && !sameEffect) // same card, different effect
			{
				return true;
			}
		}
		return false;
	}

	public void MakeANewEffectRecorder(GameObject myCard, GameObject myEffectInst)
	{
		// chainDepth must NOT reset here: it accumulates across nested invocations within one
		// chain generation so the >99 fuse in EffectCanBeInvoked can actually fire (it was dead
		// code while being reset per recorder). Per-chain-generation reset happens in CloseOpenedChain.
		chainNumber++;
		var newEffectChain = Instantiate(effectRecorderPrefab, transform);
		var newChainScript = newEffectChain.GetComponent<EffectRecorder>();
		newChainScript.sessionID = sessionNumberRef.value;
		newChainScript.chainID = chainNumber;
		newChainScript.cardObject = myCard;
		newChainScript.effectObject = myEffectInst;
		// Snapshot whether the source card is the currently revealed card. Some effects
		// (e.g. StartCardShuffleEffect) clear CombatManager.revealZone during execution,
		// so the animation phase cannot rely on the live value.
		newChainScript.sourceWasInRevealZone =
			CombatManager.Me != null &&
			CombatManager.Me.revealZone != null &&
			CombatManager.Me.revealZone == myCard;
		// Remember the recorder that was active before creating this one.
		// This ensures reactive effects are parented to the recorder that triggered them.
		var previousRecorder = currentEffectRecorder;
		
		recorderStack.Add(newEffectChain);
		openedEffectRecorders.Add(newEffectChain);
		
		bool isRoot = currentEffectRecorderParent == null;
		if (isRoot)
		{
			currentEffectRecorderParent = newEffectChain;
		}
		else
		{
			// Attach reactive effects as children of the recorder that triggered them,
			// instead of flattening everything under the chain root.
			var parentTransform = previousRecorder != null
				? previousRecorder.transform
				: currentEffectRecorderParent.transform;
			newEffectChain.transform.SetParent(parentTransform);
		}

		string parentName = isRoot ? "ROOT" : (previousRecorder != null ? previousRecorder.GetComponent<EffectRecorder>().chainID.ToString() : currentEffectRecorderParent.GetComponent<EffectRecorder>().chainID.ToString());
		TestManager.Log("[EffectChainManager] MakeANewEffectRecorder chain#" + chainNumber + " card=" + myCard.name + " effect=" + myEffectInst.name + " isRoot=" + isRoot + " parent=" + parentName + " stackSize=" + recorderStack.Count);
	}

	public bool EffectCanBeInvoked(string effectID)
	{
		// loop check: same CARD INSTANCE + same EFFECT OBJECT already processed in opened chains
		var currentRec = currentEffectRecorder.GetComponent<EffectRecorder>();
		var myCard = currentRec.cardObject;
		var myEffect = currentRec.effectObject;

		var invokedTimes = 0;
		string matchedChains = "";
		foreach (var chain in openedEffectRecorders)
		{
			var wipChainScript = chain.GetComponent<EffectRecorder>();
			// Match by GameObject reference (instance), not by effectID string
			if (wipChainScript.cardObject == myCard &&
			    wipChainScript.effectObject == myEffect &&
			    !string.IsNullOrEmpty(wipChainScript.processedEffectID))
			{
				invokedTimes++;
				matchedChains += "chain#" + wipChainScript.chainID + "[" + wipChainScript.effectObject.name + "];";
			}
		}

		bool canInvoke = !(invokedTimes > 0 || openedEffectRecorders.Count == 0) && chainDepth <= 99;

		// Diagnosability: log the gate values so a silently blocked invocation (no exception,
		// no effect) can be attributed to invokedTimes / openChains / chainDepth.
		TestManager.Log("[EffectChainManager] EffectCanBeInvoked effectID=[" + effectID + "] invokedTimes=" + invokedTimes + " openChains=" + openedEffectRecorders.Count + " chainDepth=" + chainDepth + " canInvoke=" + canInvoke);

		if (invokedTimes > 0 || openedEffectRecorders.Count == 0) // same card instance + effect already invoked in opened chains
		{
			return false;
		}

		if (chainDepth > 99)
		{
			TestManager.LogError("[EffectChainManager] ERROR: chain depth reached limit");
			return false;
		}

		currentRec.processedEffectID = effectID;
		chainDepth++;
		return true;
	}

	public void PopCurrentRecorder()
	{
		if (recorderStack.Count > 0)
		{
			var popped = recorderStack[recorderStack.Count - 1];
			var poppedRec = popped != null ? popped.GetComponent<EffectRecorder>() : null;
			string poppedName = poppedRec != null ? "chain#" + poppedRec.chainID + "[" + poppedRec.cardObject.name + "]" : "null";
			recorderStack.RemoveAt(recorderStack.Count - 1);
			var newCurrent = currentEffectRecorder;
			string newCurrentName = newCurrent != null ? "chain#" + newCurrent.GetComponent<EffectRecorder>().chainID + "[" + newCurrent.GetComponent<EffectRecorder>().cardObject.name + "]" : "null";
			TestManager.Log("[EffectChainManager] PopCurrentRecorder popped=" + poppedName + " newCurrent=" + newCurrentName + " stackSize=" + recorderStack.Count);
		}
	}

	public void CloseOpenedChain()
	{
		int count = openedEffectRecorders.Count;
		string closedChainInfo = "";
		int skippedCount = 0;
		foreach (var recorder in openedEffectRecorders)
		{
			if (recorder == null)
			{
				skippedCount++;
				continue;
			}
			var rec = recorder.GetComponent<EffectRecorder>();
			if (rec == null)
			{
				skippedCount++;
				continue;
			}
			// Guard against destroyed card/effect objects left in recorders.
			string cardName = rec.cardObject != null ? rec.cardObject.name : "null";
			string effectName = rec.effectObject != null ? rec.effectObject.name : "null";
			string reqSummary = "reqs=" + rec.animationRequests.Count;
			for (int i = 0; i < rec.animationRequests.Count; i++)
			{
				reqSummary += "[" + i + "]" + rec.animationRequests[i].type;
			}
			closedChainInfo += "chain#" + rec.chainID + "[" + cardName + "/" + effectName + "/" + reqSummary + "];";
		}
		if (count > 0)
			TestManager.Log("[EffectChainManager] CloseOpenedChain closing " + count + " recorders (skipped " + skippedCount + " destroyed): " + closedChainInfo);

		UtilityFuncManagerScript.CopyList(openedEffectRecorders, closedEffectRecorders, false);
		openedEffectRecorders.Clear();
		lastEffectObject = null; // also clear last effect object or else after shuffle if same card is revealed or after reveal if same card is legally revealed again, it won't go through
		chainDepth = 0;
		recorderStack.Clear();
		currentEffectRecorderParent = null;

	}

	#region Attack Segment Scope

	// LIFO stack of the container GO owning each open segment scope (per-segment attack events,
	// plans/plan-per-segment-attack-events-2026-09-05.md). Nested attacks (a reaction that itself
	// attacks) push their own entry so each scope end restores its own owner to lastEffectObject.
	private readonly List<GameObject> _segmentScopeOwnerEffects = new List<GameObject>();

	/// <summary>
	/// Begin an attack-segment scope: pushes a segment recorder as a child of the current
	/// in-progress recorder (or as a root when no chain is open). This segment's Attack capture
	/// and event reactions attach under it. Pair with EndAttackSegmentScope after the segment's
	/// event dispatch completes.
	/// </summary>
	public void BeginAttackSegmentScope(GameObject attackerCard, GameObject attackEffectObj)
	{
		_segmentScopeOwnerEffects.Add(currentEffectRecorder != null
			? currentEffectRecorder.GetComponent<EffectRecorder>()?.effectObject
			: null);
		MakeANewEffectRecorder(attackerCard, attackEffectObj);
	}

	/// <summary>
	/// End the attack-segment scope: pops the segment recorder, then moves every recorder opened
	/// during this segment that is not an in-progress invocation into closedEffectRecorders, so
	/// the same reactor can fire again on the next segment (the pair guard only scans OPENED
	/// chains). Transform parents are preserved — the animation tree still plays
	/// segment-by-segment under the attacker's recorder. lastEffectObject is restored to the
	/// attacking container so a card reacting to its own attack event via the same container
	/// stays blocked on every segment.
	/// </summary>
	public void EndAttackSegmentScope()
	{
		PopCurrentRecorder();
		for (int i = 0; i < openedEffectRecorders.Count; )
		{
			var rec = openedEffectRecorders[i];
			if (rec != null && !recorderStack.Contains(rec))
			{
				openedEffectRecorders.RemoveAt(i);
				closedEffectRecorders.Add(rec);
			}
			else
			{
				i++;
			}
		}
		if (_segmentScopeOwnerEffects.Count > 0)
		{
			int last = _segmentScopeOwnerEffects.Count - 1;
			lastEffectObject = _segmentScopeOwnerEffects[last];
			_segmentScopeOwnerEffects.RemoveAt(last);
		}
	}

	#endregion
}