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

		/// <summary>
		/// Snapshot of whether this recorder's source card was the revealed card at the moment
		/// the recorder was created. Effects like StartCardShuffleEffect clear revealZone during
		/// logic execution, so we must not rely on the live value during animation playback.
		/// </summary>
		public bool sourceWasInRevealZone = false;

		/// <summary>
		/// True when this recorder was created because the effect's cost could not be paid.
		/// Such recorders only play the cost-fail shake and should not show the success emphasize.
		/// </summary>
		public bool isCostFailRecorder = false;
	}
}