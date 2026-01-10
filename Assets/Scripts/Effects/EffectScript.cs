using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class EffectScript : MonoBehaviour
{
	public StringSO effectResultString;
	
	protected CombatManager combatManager;
	protected GameObject myCard;
	protected CardScript myCardScript;
	protected void OnEnable()
	{
		combatManager = CombatManager.Me;
		myCard = transform.parent.gameObject;
		myCardScript = GetComponent<CardScript>() ? GetComponent<CardScript>() : myCard.GetComponent<CardScript>();
	}
}