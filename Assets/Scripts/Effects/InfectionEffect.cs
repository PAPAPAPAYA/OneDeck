using System.Collections.Generic;
using UnityEngine;

public class InfectionEffect : MonoBehaviour
{
	private CombatManager _cm;
	private GameObject _myCard;
	private CardScript _myCardScript;
	
	private void OnEnable()
	{
		_cm = CombatManager.Me;
		_myCard = transform.parent.gameObject;
		_myCardScript = _myCard.GetComponent<CardScript>();
	}

	public void InfectRandom(int amount)
	{
		var cardsToInfect = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(_cm.combinedDeckZone, cardsToInfect, true);
		UtilityFuncManagerScript.CopyGameObjectList(_cm.graveZone, cardsToInfect, false);
		cardsToInfect = UtilityFuncManagerScript.ShuffleList(cardsToInfect);
		for (var i = cardsToInfect.Count - 1; i >= 0; i--)
		{
			if (cardsToInfect[i].GetComponent<CardScript>().myTags.Contains(EnumStorage.Tag.Infected))
			{
				cardsToInfect.RemoveAt(i);
			}
		}
		if (cardsToInfect.Count <= 0) return;
		amount = Mathf.Clamp(amount, 0, cardsToInfect.Count);
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToInfect[i].GetComponent<CardScript>();
			targetCardScript.myTags.Add(EnumStorage.Tag.Infected);
			var targetCardOwnerString = targetCardScript.myStatusRef == _cm.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
			CombatInfoDisplayer.me.effectResultDisplay.text += "[" + _myCardScript.cardName + "] infected " + targetCardOwnerString + targetCardScript.cardName + "]\n";
		}
	}
}