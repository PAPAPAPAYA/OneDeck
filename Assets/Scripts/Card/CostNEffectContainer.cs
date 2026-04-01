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
	[Tooltip("在检查cost之后、效果之前执行（如Delay Cost）")]
	public UnityEvent preEffectEvent;
	[Tooltip("assign effect component's function")]
	public UnityEvent effectEvent;

	private int _costNotMetFlag = 0;

	/// <summary>
	/// 用于外部（如 preEffectEvent 中的 MinionCostEffect）设置 cost 检查失败
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

		// 在效果执行前刷新所有追踪数值
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
		// 检查卡是否没有任何 rest status effect
		if (!_myCardScript.myStatusEffects.Contains(EnumStorage.StatusEffect.Rest)) return; // 如果没有 rest，检查通过
		
		// 如果有 rest，移除一个 rest 并阻止效果发动
		// 移除一个 Rest status effect
		for (var i = _myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
		{
			if (_myCardScript.myStatusEffects[i] == EnumStorage.StatusEffect.Rest)
			{
				_myCardScript.myStatusEffects.RemoveAt(i);
				break; // 只移除一个
			}
		}
		
		// 刷新显示
		CombatInfoDisplayer.me.RefreshDeckInfo();
		
		// 阻止效果发动
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

	public void CheckCost_InGrave()
	{
		// [已废弃] 墓地机制已移除，此方法始终返回成功
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

	#endregion
}