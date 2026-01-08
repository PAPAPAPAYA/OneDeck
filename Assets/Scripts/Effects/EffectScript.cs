using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class EffectScript : MonoBehaviour
{
	public StringSO effectResultString;
	[Header("Tag Related Refs")]
	public GameObject myTagResolver;
	public bool canTagBeStacked = false;
	public EnumStorage.Tag tagToGive;
	protected CombatManager cm;
	protected GameObject myCard;
	protected CardScript myCardScript;
	protected void OnEnable()
	{
		cm = CombatManager.Me;
		myCard = transform.parent.gameObject;
		myCardScript = GetComponent<CardScript>() ? GetComponent<CardScript>() : myCard.GetComponent<CardScript>();
	}
	
	public void GiveTagToRandom(int amount)
	{
		if (myTagResolver == null) return;
		var cardsToInfect = new List<GameObject>();
		UtilityFuncManagerScript.CopyGameObjectList(cm.combinedDeckZone, cardsToInfect, true);
		UtilityFuncManagerScript.CopyGameObjectList(cm.graveZone, cardsToInfect, false);
		cardsToInfect = UtilityFuncManagerScript.ShuffleList(cardsToInfect);
		if (!canTagBeStacked)
		{
			for (var i = cardsToInfect.Count - 1; i >= 0; i--)
			{
				if (cardsToInfect[i].GetComponent<CardScript>().myTags.Contains(tagToGive))
				{
					cardsToInfect.RemoveAt(i);
				}
			}
		}
		if (cardsToInfect.Count <= 0) return;
		amount = Mathf.Clamp(amount, 0, cardsToInfect.Count);
		for (var i = 0; i < amount; i++)
		{
			var targetCardScript = cardsToInfect[i].GetComponent<CardScript>();
			targetCardScript.myTags.Add(tagToGive);
			var targetCardOwnerString = targetCardScript.myStatusRef == cm.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
			effectResultString.value += "[" + myCardScript.cardName + "] infected " + targetCardOwnerString + targetCardScript.cardName + "]\n";
			Instantiate(myTagResolver, targetCardScript.transform);
		}
	}
}