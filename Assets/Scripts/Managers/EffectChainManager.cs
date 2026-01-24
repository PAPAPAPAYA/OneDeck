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

	//todo only need current chain or opened chains, change opened chains to only one opened chain and use that
	public GameObject effectChainPrefab;
	public IntSO sessionNumberRef;
	public int chainNumber;
	public GameObject currentWipChain;
	// tracks last effect inst
	public GameObject lastEffectInst;
	public List<GameObject> openedChains;
	public List<GameObject> closedChains;
	public int chainDepth = 0;
	
	// check if [1] new reveal (each new reveal will close opened chains), or [2] same card different effect (new effects will close chains)
	public bool ShouldMakeANewChain(GameObject myCard, GameObject myEffectInst)
	{
		var shouldIMakeANewChain = false;
		
		if (openedChains.Count == 0) // if no opened chains
		{
			shouldIMakeANewChain = true;
		}
		else
		{
			foreach (var chain in openedChains)
			{
				var openedChainScript = chain.GetComponent<EffectChain>();
				if (openedChainScript.cardObject.Equals(myCard) && !openedChainScript.effectObject.Equals(myEffectInst)) // same card, different effect
				{
					shouldIMakeANewChain = true;
				}
			}
		}
		if (shouldIMakeANewChain)
		{
			CloseOpenedChains();
		}
		return shouldIMakeANewChain;
	}

	public void MakeANewChain(GameObject myCard, GameObject myEffectInst)
	{
		chainDepth = 0;
		chainNumber++;
		var newEffectChain = Instantiate(effectChainPrefab, transform);
		var newChainScript = newEffectChain.GetComponent<EffectChain>();
		newChainScript.sessionID = sessionNumberRef.value;
		newChainScript.chainID = chainNumber;
		newChainScript.cardObject = myCard;
		newChainScript.effectObject = myEffectInst;
		newChainScript.open = true;
		currentWipChain = newEffectChain;
		openedChains.Add(newEffectChain);
	}

	public bool EffectCanBeInvoked(string effectID)
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
		chainDepth++;
		return true;
	}

	public void CloseOpenedChains()
	{
		foreach (var chain in openedChains)
		{
			chain.GetComponent<EffectChain>().open = false;
		}

		UtilityFuncManagerScript.CopyList(openedChains, closedChains, false);
		openedChains.Clear();
	}
}