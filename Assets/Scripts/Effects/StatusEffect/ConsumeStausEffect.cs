using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ConsumeStausEffect : EffectScript
	{
		public EnumStorage.StatusEffect statusEffectToConsume;
		public void ConsumeStatusEffect(int amount)
		{
			// first check if amount is met
			if (!EnumStorage.DoesListContainAmountOfTag(myCardScript.myStatusEffects, amount, statusEffectToConsume)) return;
			// then remove status effect
			for (var i = myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
			{
				if (myCardScript.myStatusEffects[i] == statusEffectToConsume)
				{
					myCardScript.myStatusEffects.RemoveAt(i);
				}
			}
			// lastly, refresh info display
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}
	}
}