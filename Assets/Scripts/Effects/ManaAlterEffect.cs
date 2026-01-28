using System;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using UnityEngine;

// mana: positive tag that can be stacked
public class ManaAlterEffect : StatusEffectGiverEffect
{
	public void ConsumeMana(int amount)
	{
		if (!EnumStorage.DoesListContainAmountOfTag(myCardScript.myStatusEffects, amount, EnumStorage.StatusEffect.Mana)) return;
		for (var i = myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
		{
			if (myCardScript.myStatusEffects[i] == EnumStorage.StatusEffect.Mana)
			{
				myCardScript.myStatusEffects.RemoveAt(i);
			}
		}
	}
}