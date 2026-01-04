using System;
using UnityEngine;

namespace TagSystem
{
	[RequireComponent(typeof(HPAlterEffect))]
	public class InfectedResolver : MonoBehaviour
	{
		private HPAlterEffect _myHpAlterScript;
		[Tooltip("be minus to decrease hp")]
		public int dmgAmount = -1;

		private void OnEnable()
		{
			_myHpAlterScript = GetComponent<HPAlterEffect>();
		}

		public void ResolveTag()
		{
			var card = CombatManager.Me.revealZone.GetComponent<CardScript>();
			if (!card.myTags.Contains(EnumStorage.Tag.Infected)) return;
			// apply dmg to card owner
			_myHpAlterScript.AlterHP(dmgAmount, card.myStatusRef);
			// remove tag
			card.myTags.Remove(EnumStorage.Tag.Infected);  
		}
	}
}