using System;
using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class CardManipulationEffect : EffectScript
{
	public void StageSelf() // put self on top of the deck
	{
		if (!combatManager.combinedDeckZone.Contains(myCard)) return;
		combatManager.combinedDeckZone.Remove(myCard);
		combatManager.combinedDeckZone.Add(myCard);
		effectResultString.value += "// ["+myCardScript.cardName+"] is staged to the top of the deck\n";
	}

	public void StageTag(int amount, EnumStorage.Tag tagToCheck) //todo put random cards with tagToCheck on top of the deck
	{
	}

	public void BurySelf() // put self at the bottom of the deck
	{
		if (!combatManager.combinedDeckZone.Contains(transform.parent.gameObject)) return;
		combatManager.combinedDeckZone.Remove(transform.parent.gameObject);
		combatManager.combinedDeckZone.Insert(0, transform.parent.gameObject);
		effectResultString.value += "// ["+myCardScript.cardName+"] is buried to the bottom of the deck\n";
	}
}