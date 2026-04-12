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

	[Header("Cost Variables")]
	[Tooltip("Cursed Card Type ID, used for specific cost checking")]
	public StringSO cursedCardTypeID;

	[Header("Cost and Effect Events")]
	public UnityEvent checkCostEvent;
	[Tooltip("Execute after cost check but before effect (e.g., Delay Cost)")]
	public UnityEvent preEffectEvent;
	[Tooltip("assign effect component's function")]
	public UnityEvent effectEvent;

	private int _costNotMetFlag = 0;

	/// <summary>
	/// Used externally (e.g., MinionCostEffect in preEffectEvent) to set cost check failure
	/// </summary>
	public void SetCostNotMet(string failMessage)
	{
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
		effectResultString.value += failMessage;
	}

	public void InvokeEffectEvent()
	{
		// check cost
		_costNotMetFlag = 0;
		checkCostEvent?.Invoke();

		if (_costNotMetFlag > 0) return;

		// execute pre-effect (e.g., Delay Cost)
		preEffectEvent?.Invoke();

		if (_costNotMetFlag > 0) return; // if cost can not be met, return

		// Refresh all tracked values before effect execution
		ValueTrackerManager.me?.UpdateAllTrackers();

		// invoke effect
		var effectString = "(" + _myCardScript.cardID + ") " + _myCardScript.gameObject.name + ": " + gameObject.name; // this string will be used to record and compare to prevent looping
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

	public void CheckCost_Revive(int reviveRequired)
	{
		if (EnumStorage.DoesListContainAmountOfStatusEffect(_myCardScript.myStatusEffects, reviveRequired, EnumStorage.StatusEffect.Revive)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		var cardOwnerInfo = CombatInfoDisplayer.me.ReturnCardOwnerInfo(_myCardScript.myStatusRef);
		effectResultString.value +=
			"// Not enough [Revive] to revive " +
			cardOwnerInfo +
			" [" + _myCardScript.gameObject.name + "]\n";
	}

	// will check and consume rest
	public void CheckCost_Rested()
	{
		// Check if the card has no rest status effect
		if (!_myCardScript.myStatusEffects.Contains(EnumStorage.StatusEffect.Rest)) return; // If no rest, check passed
		
		// If has rest, remove one rest and prevent effect activation
		// Remove one Rest status effect
		for (var i = _myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
		{
			if (_myCardScript.myStatusEffects[i] == EnumStorage.StatusEffect.Rest)
			{
				_myCardScript.myStatusEffects.RemoveAt(i);
				break; // Remove only one
			}
		}
		
		// Refresh display
		CombatInfoDisplayer.me.RefreshDeckInfo();
		
		// Prevent effect activation
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		var cardOwnerInfo = CombatInfoDisplayer.me.ReturnCardOwnerInfo(_myCardScript.myStatusRef);
		effectResultString.value +=
			"// [Rest] status consumed, " +
			cardOwnerInfo +
			" [" + _myCardScript.gameObject.name + "] skips this turn\n";
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
		if (EnumStorage.DoesListContainAmountOfStatusEffect(_myCardScript.myStatusEffects, manaRequired, EnumStorage.StatusEffect.Mana)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		effectResultString.value += "// Not enough [Mana] to activate [" + _myCardScript.gameObject.name + "]\n";
	}

	public void CheckCost_Power(int powerRequired)
	{
		if (EnumStorage.DoesListContainAmountOfStatusEffect(_myCardScript.myStatusEffects, powerRequired, EnumStorage.StatusEffect.Mana)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		effectResultString.value += "// Not enough [Power] to activate [" + _myCardScript.gameObject.name + "]\n";
	}

	public void CheckCost_InGrave()
	{
		// [Deprecated] Graveyard mechanic removed, this method always returns success
		return;
	}

	/// <summary>
	/// Check if there are at least [enemyCardCount] cards in combined deck zone that do NOT belong to the session owner (this card's owner).
	/// Cost is met if the combined deck contains at least the specified number of enemy cards.
	/// </summary>
	/// <param name="enemyCardCount">Required number of enemy cards in combined deck</param>
	public void CheckCost_HasEnemyCardInCombinedDeck(int enemyCardCount)
	{
		int enemyCardFound = 0;
		foreach (var card in CombatManager.Me.combinedDeckZone)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			// 跳过中立卡和 Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			if (cardScript.myStatusRef != _myCardScript.myStatusRef)
			{
				enemyCardFound++;
				if (enemyCardFound >= enemyCardCount) break;
			}
		}

		if (enemyCardFound >= enemyCardCount) return; // cost met - enough enemy cards found
		
		// cost not met - not enough enemy cards in combined deck
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		effectResultString.value += "// Not enough enemy cards in deck to activate [" + _myCardScript.gameObject.name + "] (need " + enemyCardCount + ")\n";
	}

	/// <summary>
	/// Check if the card has at least [counterRequired] Counter status effects.
	/// Used for effects that require a specific count to trigger.
	/// </summary>
	/// <param name="counterRequired">Required number of Counter status effects</param>
	public void CheckCost_Counter(int counterRequired)
	{
		if (EnumStorage.DoesListContainAmountOfStatusEffect(_myCardScript.myStatusEffects, counterRequired, EnumStorage.StatusEffect.Counter)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return; // only show fail message if card is in reveal zone
		effectResultString.value += "// Not enough [Counter] to activate [" + _myCardScript.gameObject.name + "] (need " + counterRequired + ")\n";
	}

	/// <summary>
	/// Check if the card's index in combined deck is before (smaller than) the Start Card's index.
	/// Cost is met if this card is positioned before Start Card in the deck.
	/// </summary>
	public void CheckCost_IndexBeforeStartCard()
	{
		var combinedDeck = CombatManager.Me.combinedDeckZone;
		
		// Find this card's index
		int thisCardIndex = combinedDeck.IndexOf(_myCardScript.gameObject);
		if (thisCardIndex == -1)
		{
			_costNotMetFlag++;
			if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
			effectResultString.value += "// [" + _myCardScript.gameObject.name + "] is not in deck\n";
			return;
		}
		
		// Find Start Card's index
		int startCardIndex = -1;
		for (int i = 0; i < combinedDeck.Count; i++)
		{
			var cardScript = combinedDeck[i].GetComponent<CardScript>();
			if (cardScript != null && cardScript.isStartCard)
			{
				startCardIndex = i;
				break;
			}
		}
		
		// If no Start Card found in deck, cost cannot be met
		if (startCardIndex == -1)
		{
			_costNotMetFlag++;
			if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
			effectResultString.value += "// No Start Card in deck, [" + _myCardScript.gameObject.name + "] cannot activate\n";
			return;
		}
		
		// Cost is met if this card's index is smaller than Start Card's index (closer to bottom)
		if (thisCardIndex < startCardIndex) return;
		
		// Cost not met
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
		effectResultString.value += "// [" + _myCardScript.gameObject.name + "] is not before Start Card in deck\n";
	}

	/// <summary>
	/// Check if there is an enemy card matching the cursed card type id with more than X Power status effects in the deck.
	/// Cost is met if there is at least one enemy card matching cursedCardTypeID with more than powerCount Power status effects.
	/// </summary>
	/// <param name="powerCount">Required Power status effect threshold (must be greater than this value)</param>
	public void CheckCost_EnemyCursedCardHasPower(int powerCount)
	{
		// Check if cursedCardTypeID is set
		if (cursedCardTypeID == null || string.IsNullOrEmpty(cursedCardTypeID.value))
		{
			_costNotMetFlag++;
			if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
			effectResultString.value += "// [" + _myCardScript.gameObject.name + "] cursedCardTypeID is not set or null\n";
			return;
		}

		foreach (var card in CombatManager.Me.combinedDeckZone)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// 跳过中立卡和 Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			
			// Check if it's an enemy card
			if (cardScript.myStatusRef == _myCardScript.myStatusRef) continue;
			
			// Check if card type id matches the cursed card type id
			if (cardScript.cardTypeID != cursedCardTypeID?.value) continue;
			
			// Count the number of Power status effects
			int powerAmount = 0;
			foreach (var effect in cardScript.myStatusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Power)
				{
					powerAmount++;
				}
			}
			
			// If Power count exceeds the parameter, cost is met
			if (powerAmount > powerCount)
			{
				return; // cost met
			}
		}
		
		// cost not met - no matching enemy card found with enough Power
		_costNotMetFlag++;
		if (CombatManager.Me.revealZone != transform.parent.gameObject) return;
		effectResultString.value += "// No enemy card [" + cursedCardTypeID?.value + "] with >" + powerCount + " [Power] in deck\n";
	}

	#endregion
}