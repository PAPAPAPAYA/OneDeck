using UnityEngine;
using System.Collections.Generic;

namespace DefaultNamespace
{
    public class EffectChain : MonoBehaviour
    {
        public int chainID;
        public List<string> processedEffectIDs = new List<string>();
    }
}