using DefaultNamespace;
using UnityEngine;

namespace TagSystem
{
	// 1. get tag owner card script
	// 2. dynamically change card script name
	// 3. mimic tag owner card script's status refs
	
	// require components
	[RequireComponent(typeof(CardScript))]
	[RequireComponent(typeof(GameEventListener))]
	[RequireComponent(typeof(CostNEffectContainer))]
	public class ResolverScript : MonoBehaviour
	{
		private CardScript _tagOwnerCardScript; // card script of the tag owner
		private CardScript _myCardScript; // since effect scripts read from its card script to display info

		private void OnEnable()
		{
			_tagOwnerCardScript = transform.parent.GetComponent<CardScript>();
			_myCardScript = GetComponent<CardScript>();
			_myCardScript.cardName = _tagOwnerCardScript.cardName + _myCardScript.cardName;
			_myCardScript.myStatusRef = _tagOwnerCardScript.myStatusRef;
			_myCardScript.theirStatusRef = _tagOwnerCardScript.theirStatusRef;
		}
	}
}