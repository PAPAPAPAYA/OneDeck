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
    public int chainNumber;
    public GameObject currentEffectChain;

    private void MakeANewEffectChain()
    {
        var effectChain = Instantiate(effectChainPrefab, transform);
        effectChain.GetComponent<EffectChain>().chainID = chainNumber;
        currentEffectChain = effectChain;
    }

    private void CloseEffectChain()
    {
        currentEffectChain = null;
        chainNumber++;
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