using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class RemoveStatusEffectEffect : EffectScript
	{
		public EnumStorage.StatusEffect statusEffectToRemove;

		public void RemoveStatusEffect()
		{
			myCardScript.myStatusEffects.Remove(statusEffectToRemove);
			effectResultString.value += "// [" + statusEffectToRemove + "] is removed\n";
		}
	}
}