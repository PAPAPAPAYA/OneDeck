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
	protected virtual void OnEnable()
	{
		combatManager = CombatManager.Me;
		myCard = transform.parent.gameObject;
		myCardScript = myCard.GetComponent<CardScript>();
	}
}