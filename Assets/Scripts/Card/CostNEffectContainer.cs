using System;
using DefaultNamespace.SOScripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

// this script is used to package, or in other words, to associate effects with their corresponding costs
// all cost functions need to implement here as they all aim to change variable [costCanBePayed]
// this way, we can assign effects and their costs via UnityEvent, even as UnityEvents can't return values in a straight forward way
// so this script is responsible for checking effect cost
public class CostNEffectContainer : MonoBehaviour
{
	#region GET MY CARD SCRIPT

	private CardScript _myCardScript;

	private void OnEnable()
	{
		if (GetComponentInParent<CardScript>())
		{
			_myCardScript = GetComponentInParent<CardScript>();
		}
	}

	#endregion

	[Tooltip("the string SO that combat info displayer use to display effect result")]
	public StringSO effectResultString;

	[Header("Cost and Effect Events")]
	public UnityEvent checkCostEvent;
	[Tooltip("assign effect component's function")]
	public UnityEvent effectEvent;

	private int _costNotMetFlag = 0;

	public void InvokeEffectEvent()
	{
		// check cost
		_costNotMetFlag = 0;
		checkCostEvent?.Invoke();

		// invoke effect
		var effectString = "("+_myCardScript.cardID+") " + _myCardScript.gameObject.name + ": " + gameObject.name; // this string will be used to record and compare to prevent looping
		if (_costNotMetFlag > 0) return; // if cost can not be met, return
		if (EffectChainManager.Me.lastEffectObject == gameObject) return; // prevent effect invoking self
		EffectChainManager.Me.CheckShouldIStartANewChain(_myCardScript.gameObject, gameObject); // check to see if a new chain is warranted, if yes, current container parent will be cleared
		EffectChainManager.Me.MakeANewEffectRecorder(_myCardScript.gameObject, gameObject);
		
		if (EffectChainManager.Me.EffectCanBeInvoked(effectString)) 
		{
			EffectChainManager.Me.lastEffectObject = gameObject;
			effectEvent?.Invoke(); // invoke effects
		}
	}

	#region check cost funcs

	public void CheckCost_Rested(int restRequired)
	{
		if (EnumStorage.DoesListContainAmountOfTag(_myCardScript.myStatusEffects, restRequired, EnumStorage.StatusEffect.Rest)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		// todo implement method to return "your" or "their" card
		effectResultString.value += "// Not enough [Rest] to activate [" + _myCardScript.gameObject.name + "]\n";
	}
	
	public void CheckCost_Infected()
	{
		if (_myCardScript.myStatusEffects.Contains(EnumStorage.StatusEffect.Infected))
		{
			
		}
		else
		{
			_costNotMetFlag++;
		}
	}
	
	public void CheckCost_Mana(int manaRequired)
	{
		if (EnumStorage.DoesListContainAmountOfTag(_myCardScript.myStatusEffects, manaRequired, EnumStorage.StatusEffect.Mana)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		effectResultString.value += "// Not enough [Mana] to activate [" + _myCardScript.gameObject.name + "]\n";
	}

	public void CheckCost_InGrave()
	{
		if (CombatManager.Me.graveZone.Contains(transform.parent.gameObject))
		{
		}
		else
		{
			_costNotMetFlag++;
		}
	}

	#endregion
}