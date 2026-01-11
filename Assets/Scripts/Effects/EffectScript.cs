using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using TagSystem;
using UnityEngine;

public class EffectScript : MonoBehaviour
{
	public StringSO effectResultString;
	
	protected CombatManager combatManager;
	protected GameObject myCard;
	protected CardScript myCardScript;
	protected CardScript myParentCardScript;
	protected virtual void OnEnable()
	{
		combatManager = CombatManager.Me;
		myCard = transform.parent.gameObject;
		
		if (GetComponent<CardScript>()) // if this effect's object has a card script, then this effect is used in a resolver
		{
			myCardScript = GetComponent<CardScript>(); // my card script is then the resolver's card script
		}
		else // if this effect's object doesn't have a card script, then this effect is a normal effect
		{
			myCardScript = myCard.GetComponent<CardScript>(); // my card script is then the parent's card script
		}
		//smyCardScript = GetComponent<CardScript>() ? GetComponent<CardScript>() : myCard.GetComponent<CardScript>();
		if (GetComponent<ResolverScript>()) // if it can get resolver script, then this effect is used in a tag resolver
		{
			myParentCardScript = myCard.GetComponent<CardScript>(); // store the parent card script as my parent card script
		}
	}
}