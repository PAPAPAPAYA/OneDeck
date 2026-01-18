using UnityEngine;
using System.Collections.Generic;

namespace DefaultNamespace
{
	public class EffectChain : MonoBehaviour
	{
		public int sessionID;
		public int chainID;
		public List<string> processedEffectIDs = new List<string>();
		
		// WIP
		public GameObject cardObject;
		public GameObject effectObject;
	}
}