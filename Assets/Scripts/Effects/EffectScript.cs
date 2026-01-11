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
		myCardScript = GetComponent<CardScript>() ? GetComponent<CardScript>() : myCard.GetComponent<CardScript>();
		if (GetComponent<ResolverScript>()) // if it can get resolver script, then this effect is used in a tag resolver
		{
			myParentCardScript = myCard.GetComponent<CardScript>();
		}
	}
}