using System;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using UnityEngine;

// mana: positive tag that can be stacked
public class ManaAlterEffect : TagGiverEffect
{
	public void ConsumeMana(int amount)
	{
		if (!EnumStorage.DoesListContainAmountOfTag(myCardScript.myTags, amount, EnumStorage.Tag.Mana)) return;
		for (var i = myCardScript.myTags.Count - 1; i >= 0; i--)
		{
			if (myCardScript.myTags[i] == EnumStorage.Tag.Mana)
			{
				myCardScript.myTags.RemoveAt(i);
			}
		}
	}
}