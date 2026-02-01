using Unity.VisualScripting;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ConsumeStausEffect : EffectScript
	{
		public EnumStorage.StatusEffect statusEffectToConsume;
		public void ConsumeStatusEffect(int amount)
		{
			// first check if amount is met
			if (!EnumStorage.DoesListContainAmountOfStatusEffect(myCardScript.myStatusEffects, amount, statusEffectToConsume)) return;
			// then remove status effect
			var amountRemoved = 0;
			for (var i = myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
			{
				if (myCardScript.myStatusEffects[i] == statusEffectToConsume && amountRemoved < amount)
				{
					myCardScript.myStatusEffects.RemoveAt(i);
					amountRemoved++;
				}
			}
			// lastly, refresh info display
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		// caution: only used by status effect resolver to destroy self after resolving
		public void DestroySelf()
		{
			Destroy(gameObject);
		}
	}
}