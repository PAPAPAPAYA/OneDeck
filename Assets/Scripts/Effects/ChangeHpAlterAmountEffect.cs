using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ChangeHpAlterAmountEffect : EffectScript
	{
		// get all hp alter effect component in parent
		// change their dmg amount alter variable
		public void ChangeHpAmountAlter(int changeAmount)
		{
			var parent = transform.parent;
			if (parent == null) return; // if this object doesn't have a parent, then do nothing cause all effects are a child object of a card
			var allHpAlterEffects = parent.GetComponentsInChildren<HPAlterEffect>();
			foreach (var hpAlterEffect in allHpAlterEffects)
			{
				hpAlterEffect.dmgAmountAlter += changeAmount;
			}
		}
	}
}