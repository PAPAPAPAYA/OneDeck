using UnityEngine;
using System.Collections.Generic;

namespace DefaultNamespace
{
	public class EffectRecorder : MonoBehaviour
	{
		public int sessionID;
		public int chainID;
		public string processedEffectID;
		
		public GameObject cardObject;
		public GameObject effectObject;
		public List<AnimationRequest> animationRequests = new List<AnimationRequest>();
		public bool animationPlayed = false;
	}
}