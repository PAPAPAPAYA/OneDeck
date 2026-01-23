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

	public GameObject effectChainPrefab;
	public IntSO sessionNumberRef;
	public int chainNumber;
	public GameObject currentEffectChain;
	public GameObject currentWipChain;
	// tracks last effect inst
	public GameObject lastEffectInst;
	public List<GameObject> openedChains;
	public List<GameObject> closedChains;
	public int chainDepth = 0;

	#region WIP
	public void MakeANewWipChain(GameObject myCard, GameObject myEffectInst)
	{
		var newEffectChain = Instantiate(effectChainPrefab, transform);
		var newChainScript = newEffectChain.GetComponent<EffectChain>();
		newChainScript.sessionID = sessionNumberRef.value;
		newChainScript.chainID = chainNumber;
		newChainScript.cardObject = myCard;
		newChainScript.effectObject = myEffectInst;
		newChainScript.open = true;
		if (currentWipChain != null) // there is already a chain before this one
		{
			if (IsSameChain(myCard, myEffectInst))
			{
				currentWipChain.GetComponent<EffectChain>().subChain.Add(newEffectChain);
				newChainScript.parentChain = currentWipChain;
				chainDepth++;
			}
			else
			{
				foreach (var chain in openedChains)
				{
					var openedChainScript = chain.GetComponent<EffectChain>();
					if (newChainScript.cardObject.Equals(myCard) && !newChainScript.effectObject.Equals(myEffectInst)) // same card, different effect
					{
						openedChainScript.subChain.Add(newEffectChain);
						newChainScript.parentChain = chain;
					}
				}
				CloseChains();
			}
		}
		currentWipChain = newEffectChain;
		openedChains.Add(newEffectChain);
	}

	// todo: parent chain is wrongly assigned, not comprehensive enough
	private bool IsSameChain(GameObject myCard, GameObject  myEffectInst)
	{
		var isSameChain = true;
		foreach (var chain in openedChains)
		{
			var chainScript = chain.GetComponent<EffectChain>();
			if (chainScript.cardObject.Equals(myCard) && !chainScript.effectObject.Equals(myEffectInst)) // same card, different effect
			{
				isSameChain = false;
			}
		}
		return isSameChain;
	}
	
	public bool WipEffectCanBeInvoked(string effectID)
	{
		// loop check (if same effect has already been invoked in the same chain)
		var invokedTimes = 0;
		foreach (var chain in openedChains)
		{
			var wipChainScript = chain.GetComponent<EffectChain>();
			if (wipChainScript.processedEffectIDs.Contains(effectID))
			{
				invokedTimes++;
			}
		}
		if (invokedTimes > 0 || openedChains.Count == 0) // same effect already invoked in opened chains
		{
			return false;
		}
		if (chainDepth > 99)
		{
			Debug.LogError("ERROR: chain depth reached limit");
			return false;
		}
		currentWipChain.GetComponent<EffectChain>().processedEffectIDs.Add(effectID);
		return true;
	}

	public void CloseChains()
	{
		foreach (var chain in openedChains)
		{
			chain.GetComponent<EffectChain>().open = false;
		}
		UtilityFuncManagerScript.CopyList(openedChains, closedChains, false);
		openedChains.Clear();
	}
	#endregion
	
	
	private void MakeANewEffectChain()
	{
		var effectChain = Instantiate(effectChainPrefab, transform);
		effectChain.GetComponent<EffectChain>().sessionID = sessionNumberRef.value;
		effectChain.GetComponent<EffectChain>().chainID = chainNumber;
		currentEffectChain = effectChain;
	}

	public void CloseEffectChain()
	{
		if (currentEffectChain)
		{
			chainNumber++;
		}
		currentEffectChain = null;
	}

	public bool CheckEffectAndRecord(string effectID)
	{
		var effectCanBeProcessed = false;
		if (!currentEffectChain)
		{
			MakeANewEffectChain();
		}

		var currentEffectChainScript = currentEffectChain.GetComponent<EffectChain>();
		if (currentEffectChainScript.processedEffectIDs.Contains(effectID))
		{
			CloseEffectChain();
			effectCanBeProcessed = false;
		}
		else
		{
			currentEffectChainScript.processedEffectIDs.Add(effectID);
			effectCanBeProcessed = true;
		}

		return effectCanBeProcessed;
	}
}