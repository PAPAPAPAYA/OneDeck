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
	[Header("Basic Info")]
	[Tooltip("don't assign identical effect name to effects in the same card")]
	public string effectName;
	[TextArea]
	public string effectDescription;

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
		var effectString = "card " + _myCardScript.cardID + ": " + effectName; // this string will be used to record and compare to prevent looping
		if (_costNotMetFlag > 0) return; // if cost can not be met, return
		if (EffectChainManager.Me.lastEffectInst == gameObject) return; // prevent effect invoking self
		EffectChainManager.Me.CheckShouldIStartANewChain(_myCardScript.gameObject, gameObject); // check to see if a new chain is warranted, if yes, current container parent will be cleared
		EffectChainManager.Me.MakeANewEffectRecorder(_myCardScript.gameObject, gameObject);
		
		if (EffectChainManager.Me.EffectCanBeInvoked(effectString)) 
		{
			EffectChainManager.Me.lastEffectInst = gameObject;
			effectEvent?.Invoke(); // invoke effects
		}
	}

	#region check cost funcs

	public void CheckCost_Mana(int manaRequired)
	{
		if (EnumStorage.DoesListContainAmountOfTag(_myCardScript.myTags, manaRequired, EnumStorage.Tag.Mana)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		effectResultString.value += "Not enough mana to activate [" + _myCardScript.cardName + "]";
	}

	public void CheckCost_InGrave()
	{
		if (CombatManager.Me.graveZone.Contains(transform.parent.gameObject))
		{
		}
		else
		{
			_costNotMetFlag++;
			print("not in grave");
		}
	}

	#endregion
}