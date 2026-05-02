using System;
using System.Collections.Generic;
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
	[Tooltip("Target Card Type ID for cost checking (e.g., 'fly')")]
	public StringSO targetCardTypeID;

	[Header("Cost and Effect Events")]
	public UnityEvent checkCostEvent;
	[Tooltip("Execute after cost check but before effect (e.g., Delay Cost)")]
	public UnityEvent preEffectEvent;
	[Tooltip("assign effect component's function")]
	public UnityEvent effectEvent;

	private int _costNotMetFlag = 0;
	private readonly List<string> _costFailMessages = new();

	/// <summary>
	/// Used externally (e.g., MinionCostEffect in preEffectEvent) to set cost check failure
	/// </summary>
	public void SetCostNotMet(string failMessage)
	{
		_costNotMetFlag++;
		_costFailMessages.Add(failMessage);
	}

	public void InvokeEffectEvent()
	{
		// check cost
		_costNotMetFlag = 0;
		_costFailMessages.Clear();
		checkCostEvent?.Invoke();

		if (_costNotMetFlag > 0)
		{
			// Only display fail messages if card is in reveal zone — centralized UI decision
			if (CombatManager.Me.revealZone == transform.parent.gameObject)
			{
				foreach (var msg in _costFailMessages)
					CombatLog.me?.Append(msg);
			}
			_costFailMessages.Clear();
			return;
		}

		// execute pre-effect (e.g., Delay Cost)
		preEffectEvent?.Invoke();

		if (_costNotMetFlag > 0)
		{
			// Centralized UI decision for pre-effect cost failures too
			if (CombatManager.Me.revealZone == transform.parent.gameObject)
			{
				foreach (var msg in _costFailMessages)
					CombatLog.me?.Append(msg);
			}
			_costFailMessages.Clear();
			return;
		}

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
		var cardOwnerInfo = CombatInfoDisplayer.me.ReturnCardOwnerInfo(_myCardScript.myStatusRef);
		_costFailMessages.Add(
			"// [复活]不足，无法复活 " +
			cardOwnerInfo +
			" [" + _myCardScript.gameObject.name + "]\n");
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
		var cardOwnerInfo = CombatInfoDisplayer.me.ReturnCardOwnerInfo(_myCardScript.myStatusRef);
		_costFailMessages.Add(
			"// [休息]状态已消耗，" +
			cardOwnerInfo +
			" [" + _myCardScript.gameObject.name + "]跳过本回合\n");
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
		_costFailMessages.Add("// [法力]不足，无法激活[" + _myCardScript.gameObject.name + "]\n");
	}

	public void CheckCost_Power(int powerRequired)
	{
		if (EnumStorage.DoesListContainAmountOfStatusEffect(_myCardScript.myStatusEffects, powerRequired, EnumStorage.StatusEffect.Mana)) return; // if check succeeded, do nothing
		// if check failed, process
		_costNotMetFlag++;
		_costFailMessages.Add("// [力量]不足，无法激活[" + _myCardScript.gameObject.name + "]\n");
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
			// Skip neutral cards and Start Card
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
		_costFailMessages.Add("// 牌库中敌方卡牌不足，无法激活[" + _myCardScript.gameObject.name + "](需要" + enemyCardCount + "张)\n");
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
		_costFailMessages.Add("// [反击]不足，无法激活[" + _myCardScript.gameObject.name + "](需要" + counterRequired + "层)\n");
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
			_costFailMessages.Add("// [" + _myCardScript.gameObject.name + "]不在牌库中\n");
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
			_costFailMessages.Add("// 牌库中没有起始牌，[" + _myCardScript.gameObject.name + "]无法激活\n");
			return;
		}
		
		// Cost is met if this card's index is smaller than Start Card's index (closer to bottom)
		if (thisCardIndex < startCardIndex) return;
		
		// Cost not met
		_costNotMetFlag++;
		_costFailMessages.Add("// [" + _myCardScript.gameObject.name + "]不在起始牌之前\n");
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
			_costFailMessages.Add("// [" + _myCardScript.gameObject.name + "]诅咒卡牌类型ID未设置或为空\n");
			return;
		}

		foreach (var card in CombatManager.Me.combinedDeckZone)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript == null) continue;
			
			// Skip neutral cards and Start Card
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
		_costFailMessages.Add("// 牌库中没有[" + cursedCardTypeID?.value + "]敌方卡牌拥有超过" + powerCount + "层[力量]\n");
	}

	/// <summary>
	/// Check if there are at least [requiredCount] friendly cards in combined deck zone matching targetCardTypeID.
	/// Cost is met if the combined deck contains at least the specified number of own cards with matching cardTypeID.
	/// </summary>
	/// <param name="requiredCount">Required number of matching friendly cards in combined deck</param>
	public void CheckCost_HasOwnCardOfType(int requiredCount)
	{
		// Check if targetCardTypeID is set
		if (targetCardTypeID == null || string.IsNullOrEmpty(targetCardTypeID.value))
		{
			_costNotMetFlag++;
			_costFailMessages.Add("// [" + _myCardScript.gameObject.name + "]目标卡牌类型ID未设置或为空\n");
			return;
		}

		int matchingCardFound = 0;
		foreach (var card in CombatManager.Me.combinedDeckZone)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			// Skip neutral cards and Start Card
			if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
			// Must belong to self (same owner)
			if (cardScript.myStatusRef != _myCardScript.myStatusRef) continue;
			// Must match target card type ID
			if (cardScript.cardTypeID != targetCardTypeID.value) continue;

			matchingCardFound++;
			if (matchingCardFound >= requiredCount) break;
		}

		if (matchingCardFound >= requiredCount) return; // cost met

		// cost not met
		_costNotMetFlag++;
		_costFailMessages.Add("// 牌库中[" + targetCardTypeID.value + "]友方卡牌不足，无法激活[" + _myCardScript.gameObject.name + "](需要" + requiredCount + "张)\n");
	}

	#endregion
}