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
	public GameObject wipChain;

	#region WIP

	public void CloseWIPChain()
	{
		wipChain = null;
		chainNumber++;
	}
	
	public void MakeANewWipChain(GameObject cardObj, GameObject effectObj)
	{
		var effectChain = Instantiate(effectChainPrefab, transform);
		var chainScript = effectChain.GetComponent<EffectChain>();
		chainScript.sessionID = sessionNumberRef.value;
		chainScript.chainID = chainNumber;
		chainScript.cardObject = cardObj;
		chainScript.effectObject = effectObj;
		wipChain = effectChain;
	}
	
	public bool CheckWipEffectAndRecord(string effectID, GameObject cardObj, GameObject effectObj)
	{
		if (!wipChain)
		{
			MakeANewWipChain(cardObj, effectObj);
		}
		var effectCanBeProcessed = false;
		var wipChainScript = wipChain.GetComponent<EffectChain>();
		if (wipChainScript.processedEffectIDs.Contains(effectID) && wipChainScript.cardObject == cardObj && wipChainScript.effectObject == effectObj)
		{
			effectCanBeProcessed = false;
		}
		else
		{
			effectCanBeProcessed = true;
			wipChainScript.processedEffectIDs.Add(effectID);
		}
		return effectCanBeProcessed;
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
			print("check effect failed: clear effect chain");
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