using System;
using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;

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
	public GameObject currentEffectRecorder; // tracks current effect container being processed
	public GameObject currentEffectRecorderParent; // tracks current chain parent
	public GameObject lastEffectObject; // tracks last effect inst
	public List<GameObject> openedEffectRecorders; // tracks opened effect containers
	public List<GameObject> closedEffectRecorders; // tracks closed effect containers
	public int chainDepth; // chain depth to prevent stack overflow, currently when depth reached 99 effect will not be processed
	
	public void CheckShouldIStartANewChain(GameObject myCard, GameObject myEffectObj)
	{
		var shouldIMakeANewChain = false;
		
		if (openedEffectRecorders.Count == 0) // if no opened chains
		{
			shouldIMakeANewChain = true;
		}
		else
		{
			if (SameCardDifferentObject(myCard, myEffectObj))
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
			if (openedChainScript.cardObject.Equals(myCard) && !openedChainScript.effectObject.Equals(myEffectInst)) // same card, different effect
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
		newChainScript.open = true;
		currentEffectRecorder = newEffectChain;
		openedEffectRecorders.Add(newEffectChain);
		
		if (currentEffectRecorderParent == null)
		{
			currentEffectRecorderParent = newEffectChain;
		}
		else
		{
			newEffectChain.transform.SetParent(currentEffectRecorderParent.transform);
		}
	}

	public bool EffectCanBeInvoked(string effectID)
	{
		// loop check (if same effect has already been invoked in the same chain)
		var invokedTimes = 0;
		foreach (var chain in openedEffectRecorders)
		{
			var wipChainScript = chain.GetComponent<EffectRecorder>();
			if (wipChainScript.processedEffectID == effectID)
			{
				invokedTimes++;
			}
		}

		if (invokedTimes > 0 || openedEffectRecorders.Count == 0) // same effect already invoked in opened chains
		{
			return false;
		}

		if (chainDepth > 99)
		{
			Debug.LogError("ERROR: chain depth reached limit");
			return false;
		}

		currentEffectRecorder.GetComponent<EffectRecorder>().processedEffectID = effectID;
		chainDepth++;
		return true;
	}

	public void CloseOpenedChain()
	{
		foreach (var recorder in openedEffectRecorders)
		{
			recorder.GetComponent<EffectRecorder>().open = false;
		}

		UtilityFuncManagerScript.CopyList(openedEffectRecorders, closedEffectRecorders, false);
		openedEffectRecorders.Clear();
		lastEffectObject = null; // also clear last effect object or else after shuffle if same card is revealed or after reveal if same card is legally revealed again, it won't go through
	}
}