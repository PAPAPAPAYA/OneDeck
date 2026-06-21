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
		chainDepth = 0;
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
}