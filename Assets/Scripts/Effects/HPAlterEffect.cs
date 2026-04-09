using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// [IMPORTANT WARNING] All damage methods (DecreaseTheirHp, DecreaseMyHp, etc.) automatically add baseDmg.value!
/// If you pass specific damage values through parameters, set baseDmg to 0 or leave empty in Inspector, otherwise it will cause double damage.
/// For example: baseDmg.value=2 and calling DecreaseTheirHp(2) will cause 4 damage.
/// </summary>
public class HPAlterEffect : EffectScript
{
	#region Fields

	[Tooltip("Base damage value - All damage methods automatically add this! Use negative numbers for damage < base damage")]
	public IntSO baseDmg;
	[HideInInspector]
	public int dmgAmountAlter = 0;
	[HideInInspector]
	public int healAmountAlter = 0;
	
	[Tooltip("Mark if damage is caused by status effect (status effect damage doesn't trigger attack animation)")]
	public bool isStatusEffectDamage = false;
	
	[Tooltip("Extra damage value - used for DecreaseMyHp and DecreaseTheirHp")]
	public int extraDmg = 0;
	
	[Header("Status Effect Count Configuration")]
	[Tooltip("Status effect type to count")]
	public EnumStorage.StatusEffect statusEffectToCheck;
	
	#endregion
	
	#region Private Helpers
	
	/// <summary>
	/// Calculate extra damage (including Power status effect and base damage)
	/// </summary>
	private void DmgCalculator()
	{
		// calculate additional dmg due to [Power]
		var parentCardScript = GetComponentInParent<CardScript>();
		foreach (var myTag in parentCardScript.myStatusEffects)
		{
			if (myTag == EnumStorage.StatusEffect.Power)
			{
				dmgAmountAlter++;
			}
		}

		// add base dmg
		dmgAmountAlter += baseDmg.value;
	}

	/// <summary>
	/// Process shield and HP reduction
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	/// <param name="status">目标玩家状态</param>
	private void ProcessShieldNHp(int dmgAmount, PlayerStatusSO status)
	{
		status.shield -= dmgAmount;
		if (status.shield < 0)
		{
			var hpDecreaseAmount = status.shield;
			status.hp += hpDecreaseAmount;
			status.shield = 0;
		}
	}
	
	/// <summary>
	/// Process damage (extract common logic, including shield and HP processing)
	/// </summary>
	/// <param name="totalDmg">总伤害数值</param>
	/// <param name="targetStatus">目标玩家状态</param>
	private void ProcessDamage(int totalDmg, PlayerStatusSO targetStatus)
	{
		ProcessShieldNHp(totalDmg, targetStatus);
		targetStatus.hp = Mathf.Clamp(targetStatus.hp, 0, targetStatus.hpMax);
	}

	#endregion
	
	#region Damage Effects
	
	/// <summary>
	/// Decrease own HP (consider extra damage)
	/// Use extraDmg field as extra damage value
	/// </summary>
	public void DecreaseMyHp()
	{
		DmgCalculator();
		int totalDmg = extraDmg + dmgAmountAlter;
		
		// Status effect damage doesn't trigger attack animation, execute directly
		if (isStatusEffectDamage)
		{
			ProcessDamage(totalDmg, myCardScript.myStatusRef);
			CheckDmgTargets_DealingDmgToSelf(totalDmg);
			dmgAmountAlter = 0;
			return;
		}
		
		// Request attack animation (attack self)
		// Determine attack target position: player card self-damage rushes to player position, enemy card self-damage rushes to enemy position
		bool isAttackingEnemy = myCardScript.myStatusRef != combatManager.ownerPlayerStatusRef;
		
		if (AttackAnimationManager.me != null)
		{
			AttackAnimationManager.me.RequestAttackAnimation(myCard, isAttackingEnemy, 
				onHit: () =>
				{
					ProcessDamage(totalDmg, myCardScript.myStatusRef);
					CheckDmgTargets_DealingDmgToSelf(totalDmg);
				},
				onComplete: null);
		}
		else
		{
			// If no animation manager, execute directly
			ProcessDamage(totalDmg, myCardScript.myStatusRef);
			CheckDmgTargets_DealingDmgToSelf(totalDmg);
		}
		
		dmgAmountAlter = 0;
	}

	/// <summary>
	/// Self-damage caused by status effect (doesn't trigger attack animation)
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	public void DecreaseMyHpFromStatusEffect(int dmgAmount)
	{
		int totalDmg = dmgAmount + baseDmg.value;
		ProcessDamage(totalDmg, myCardScript.myStatusRef);
		CheckDmgTargets_DealingDmgToSelf(totalDmg);
	}

	#endregion
	
	#region Heal Effects
	
	/// <summary>
	/// Increase own HP (consider extra heal amount)
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	public void IncreaseMyHp(int healAmount)
	{
		myCardScript.myStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckHealTargets_HealingSelf(healAmount + healAmountAlter);
		healAmountAlter = 0;
	}
	
	#endregion
	
	#region Damage Effects (Continued)

	/// <summary>
	/// Decrease opponent HP based on lost HP (/2)
	/// </summary>
	/// <param name="baseDmgAmount">基础伤害数值</param>
	public void DecreaseTheirHp_BasedOnLostHp(int baseDmgAmount)
	{
		var extraDmgAmount = (myCardScript.myStatusRef.hpMax - myCardScript.myStatusRef.hp)/2;
		extraDmg = baseDmgAmount + extraDmgAmount;
		DecreaseTheirHp();
		extraDmg = 0;
	}

	/// <summary>
	/// Decrease opponent HP based on count of cards with Infected status effect owned by card owner in combinedDeckZone and revealZone
	/// </summary>
	/// <param name="baseDmgAmount">基础伤害数值</param>
	public void DecreaseTheirHp_BasedOnInfectedCardsOwned(int baseDmgAmount)
	{
		var infectedCardCount = 0;
		
		// Merge two zones to temporary list
		List<GameObject> allCards = new();
		UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, allCards, false);
		if (combatManager.revealZone != null)
		{
			allCards.Add(combatManager.revealZone);
		}
		
		// Iterate all cards to count Infected
		foreach (var card in allCards)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			// Skip neutral cards, only count own infected cards
			if (!CombatManager.ShouldSkipEffectProcessing(cardScript) &&
			    cardScript.myStatusRef == myCardScript.myStatusRef && 
			    cardScript.myStatusEffects.Contains(EnumStorage.StatusEffect.Infected))
			{
				infectedCardCount++;
			}
		}
		
		extraDmg = baseDmgAmount + infectedCardCount;
		DecreaseTheirHp();
		extraDmg = 0;
	}

	/// <summary>
	/// Decrease opponent HP based on count of friendly cards with specified cardTypeID in combinedDeckZone and revealZone
	/// </summary>
	/// <param name="cardTypeID">要统计的卡牌类型ID</param>
	public void DecreaseTheirHp_BasedOnFriendlyCardTypeCount(string cardTypeID)
	{
		var cardCount = 0;
		
		// Merge two zones to temporary list
		List<GameObject> allCards = new();
		UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, allCards, false);
		if (combatManager.revealZone != null)
		{
			allCards.Add(combatManager.revealZone);
		}
		
		// Iterate all cards to count friendly cards of specified type
		foreach (var card in allCards)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			// Skip neutral cards, only count own cards of specified type
			if (!CombatManager.ShouldSkipEffectProcessing(cardScript) &&
			    cardScript.myStatusRef == myCardScript.myStatusRef && 
			    cardScript.cardTypeID == cardTypeID)
			{
				cardCount++;
			}
		}
		
		extraDmg = cardCount;
		DecreaseTheirHp();
		extraDmg = 0;
	}

	/// <summary>
	/// Decrease own HP based on count of statusEffectToCheck status effects on self
	/// Damage value = status effect count
	/// </summary>
	public void DecreaseMyHp_BasedOnMyStatusEffectCount()
	{
		int statusEffectCount = 0;
		
		// Iterate self card's status effect list, count specified type
		foreach (var myTag in myCardScript.myStatusEffects)
		{
			if (myTag == statusEffectToCheck)
			{
				statusEffectCount++;
			}
		}
		
		extraDmg = statusEffectCount;
		DecreaseMyHp();
		extraDmg = 0;
	}

	/// <summary>
	/// Decrease opponent HP (consider extra damage)
	/// Use extraDmg field as extra damage value
	/// </summary>
	public void DecreaseTheirHp()
	{
		DmgCalculator();
		int totalDmg = extraDmg + dmgAmountAlter;
		
		// Status effect damage doesn't trigger attack animation, execute directly
		if (isStatusEffectDamage)
		{
			ProcessDamage(totalDmg, myCardScript.theirStatusRef);
			CheckDmgTargets_DealingDmgToOpponent(totalDmg);
			dmgAmountAlter = 0;
			return;
		}
		
		// Determine attack target (true=attack enemy, false=attack player self)
		bool isAttackingEnemy = myCardScript.theirStatusRef != combatManager.ownerPlayerStatusRef;
		
		// Request attack animation
		if (AttackAnimationManager.me != null)
		{
			AttackAnimationManager.me.RequestAttackAnimation(myCard, isAttackingEnemy, 
				onHit: () =>
				{
					ProcessDamage(totalDmg, myCardScript.theirStatusRef);
					CheckDmgTargets_DealingDmgToOpponent(totalDmg);
				},
				onComplete: null);
		}
		else
		{
			// If no animation manager, execute directly
			ProcessDamage(totalDmg, myCardScript.theirStatusRef);
			CheckDmgTargets_DealingDmgToOpponent(totalDmg);
		}
		
		dmgAmountAlter = 0;
	}

	/// <summary>
	/// Opponent damage caused by status effect (doesn't trigger attack animation)
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	public void DecreaseTheirHpFromStatusEffect(int dmgAmount)
	{
		int totalDmg = dmgAmount + baseDmg.value;
		ProcessDamage(totalDmg, myCardScript.theirStatusRef);
		CheckDmgTargets_DealingDmgToOpponent(totalDmg);
	}

	#region IntSO Based Effects

	public void DecreaseTheirHp_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		extraDmg = intSO.value;
		DecreaseTheirHp();
		extraDmg = 0;
	}

	public void DecreaseMyHp_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		extraDmg = intSO.value;
		DecreaseMyHp();
		extraDmg = 0;
	}

	public void IncreaseTheirHp_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		IncreaseTheirHp(intSO.value);
	}

	public void IncreaseMyHp_BasedOnIntSO(IntSO intSO)
	{
		if (intSO == null) return;
		IncreaseMyHp(intSO.value);
	}

	/// <summary>
	/// Decrease opponent HP multiple times, each dealing baseDmg + extraDmg damage
	/// </summary>
	/// <param name="timesIntSO">伤害次数</param>
	public void DecreaseTheirHpTimesIntSO(IntSO timesIntSO)
	{
		if (timesIntSO == null) return;
		
		int times = timesIntSO.value;
		for (int i = 0; i < times; i++)
		{
			print("call DecreaseTheirHp()");
			DecreaseTheirHp();
		}
	}

	/// <summary>
	/// Decrease opponent HP multiple times, each dealing baseDmg + extraDmg damage
	/// </summary>
	/// <param name="times">伤害次数</param>
	public void DecreaseTheirHpTimesX(int times)
	{
		for (int i = 0; i < times; i++)
		{
			print("call DecreaseTheirHp()");
			DecreaseTheirHp();
		}
	}

	#endregion
	#endregion

	#region Heal Effects (Continued)
	
	/// <summary>
	/// Increase opponent HP (consider extra heal amount)
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	public void IncreaseTheirHp(int healAmount)
	{
		myCardScript.theirStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckHealTargets_HealingOpponent(healAmount + healAmountAlter);
		healAmountAlter = 0;
	}
	
	#endregion
	
	#region Result Logging
	
	/// <summary>
	/// Check damage source and target, trigger corresponding events and display text (when dealing damage to opponent)
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	private void CheckDmgTargets_DealingDmgToOpponent(int dmgAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy dealt dmg to player
		{
			effectResultString.value += "// <color=orange>their</color> [<color=orange>" + myCard.name + "</color>] dealt [<color=red>" + (dmgAmount) + "</color>] damage to <color=#87CEEB>You</color>\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOpponent(); // timepoint
			DeckTester.me.deckBDmgOutputs_ToOpp.Add(dmgAmount);
		}
		else // player dealt dmg to enemy
		{
			effectResultString.value += "// <color=#87CEEB>your</color> [<color=#87CEEB>" + myCard.name + "</color>] dealt [<color=red>" + (dmgAmount) + "</color>] damage to <color=orange>Enemy</color>\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOwner(); // timepoint
			DeckTester.me.deckADmgOutputs_ToOpp.Add(dmgAmount);
		}
	}

	/// <summary>
	/// Check damage source and target, trigger corresponding events and display text (when dealing damage to self)
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	private void CheckDmgTargets_DealingDmgToSelf(int dmgAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player dealt dmg to player
		{
			effectResultString.value += "// <color=#87CEEB>your</color> [<color=#87CEEB>" + myCard.name + "</color>] dealt [<color=red>" + (dmgAmount) + "</color>] damage to <color=#87CEEB>You</color>\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOpponent(); // timepoint
			DeckTester.me.deckADmgOutputs_ToSelf.Add(dmgAmount);
		}
		else // enemy dealt dmg to enemy
		{
			effectResultString.value += "// <color=orange>their</color> [<color=orange>" + myCard.name + "</color>] dealt [<color=red>" + (dmgAmount) + "</color>] damage to <color=orange>Enemy</color>\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOwner(); // timepoint
			DeckTester.me.deckBDmgOutputs_ToSelf.Add(dmgAmount);
		}
	}

	/// <summary>
	/// Check heal source and target, trigger corresponding events and display text (when healing self)
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	private void CheckHealTargets_HealingSelf(int healAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player healed player
		{
			effectResultString.value += "// <color=#87CEEB>your</color> [<color=#87CEEB>" + myCard.name + "</color>] healed <color=#87CEEB>You</color> for [<color=#90EE90>" + (healAmount + healAmountAlter) + "</color>]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOpponent(); // timepoint
		}
		else // enemy healed enemy
		{
			effectResultString.value += "// <color=orange>their</color> [<color=orange>" + myCard.name + "</color>] healed <color=orange>Enemy</color> for [<color=#90EE90>" + (healAmount + healAmountAlter) + "</color>]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOwner(); // timepoint
		}
	}

	/// <summary>
	/// Check heal source and target, trigger corresponding events and display text (when healing opponent)
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	private void CheckHealTargets_HealingOpponent(int healAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy healed player
		{
			effectResultString.value += "// <color=orange>their</color> [<color=orange>" + myCard.name + "</color>] healed <color=#87CEEB>You</color> for [<color=#90EE90>" + (healAmount + healAmountAlter) + "</color>]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOpponent(); // timepoint
		}
		else // player healed enemy
		{
			effectResultString.value += "// <color=#87CEEB>your</color> [<color=#87CEEB>" + myCard.name + "</color>] healed <color=orange>Enemy</color> for [<color=#90EE90>" + (healAmount + healAmountAlter) + "</color>]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOwner(); // timepoint
		}
	}
	
	#endregion
}