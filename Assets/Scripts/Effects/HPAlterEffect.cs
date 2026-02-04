using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// 【重要警告】所有伤害方法（DecreaseTheirHp, DecreaseMyHp等）都会自动加上 baseDmg.value！
/// 如果你通过参数传入具体伤害值，请将 baseDmg 设为0或在Inspector中留空，否则会造成双倍伤害。
/// 例如：baseDmg.value=2 且调用 DecreaseTheirHp(2) 会造成 4 点伤害。
/// </summary>
public class HPAlterEffect : EffectScript
{
	[Tooltip("基础伤害值 - 所有伤害方法会自动加上这个值！要造成<基础伤害值的伤害时使用负数")]
	public IntSO baseDmg;
	[HideInInspector]
	public int dmgAmountAlter = 0;
	[HideInInspector]
	public int healAmountAlter = 0;
	
	/// <summary>
	/// 计算额外伤害（包括Power状态效果和基础伤害）
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
	/// 处理护盾和生命值减少
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
	/// 减少自身生命值（考虑额外伤害）
	/// </summary>
	/// <param name="extraDmg">额外伤害数值</param>
	public void DecreaseMyHp(int extraDmg)
	{
		DmgCalculator();
		ProcessShieldNHp(extraDmg + dmgAmountAlter, myCardScript.myStatusRef);
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckDmgTargets_DealingDmgToSelf(extraDmg + dmgAmountAlter);
		dmgAmountAlter = 0;
	}

	/// <summary>
	/// 增加自身生命值（考虑额外治疗量）
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	public void IncreaseMyHp(int healAmount)
	{
		myCardScript.myStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckHealTargets_HealingSelf(healAmount + healAmountAlter);
		healAmountAlter = 0;
	}

	/// <summary>
	/// 根据已损失生命值减少对方生命值 (/2)
	/// </summary>
	/// <param name="baseDmgAmount">基础伤害数值</param>
	public void DecreaseTheirHp_BasedOnLostHp(int baseDmgAmount)
	{
		var extraDmgAmount = (myCardScript.myStatusRef.hpMax - myCardScript.myStatusRef.hp)/2;
		DecreaseTheirHp(baseDmgAmount + extraDmgAmount);
	}

	/// <summary>
	/// 根据combinedDeckZone、revealZone和graveZone中拥有Infected状态效果且属于卡片所有者的卡的数量减少对方生命值
	/// </summary>
	/// <param name="baseDmgAmount">基础伤害数值</param>
	public void DecreaseTheirHp_BasedOnInfectedCardsOwned(int baseDmgAmount)
	{
		var infectedCardCount = 0;
		
		// 合并三个区域到临时列表
		List<GameObject> allCards = new();
		UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, allCards, false);
		UtilityFuncManagerScript.CopyGameObjectList(combatManager.graveZone, allCards, false);
		if (combatManager.revealZone != null)
		{
			allCards.Add(combatManager.revealZone);
		}
		
		// 遍历所有卡片统计Infected数量
		foreach (var card in allCards)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript.myStatusRef == myCardScript.myStatusRef && 
			    cardScript.myStatusEffects.Contains(EnumStorage.StatusEffect.Infected))
			{
				infectedCardCount++;
			}
		}
		
		DecreaseTheirHp(baseDmgAmount + infectedCardCount);
	}

	/// <summary>
	/// 减少对方生命值（考虑额外伤害）
	/// </summary>
	/// <param name="extraDmg">额外伤害数值</param>
	public void DecreaseTheirHp(int extraDmg)
	{
		DmgCalculator();
		ProcessShieldNHp(extraDmg + dmgAmountAlter, myCardScript.theirStatusRef);
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckDmgTargets_DealingDmgToOpponent(extraDmg + dmgAmountAlter);
		dmgAmountAlter = 0;
	}

	/// <summary>
	/// 增加对方生命值（考虑额外治疗量）
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	public void IncreaseTheirHp(int healAmount)
	{
		myCardScript.theirStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckHealTargets_HealingOpponent(healAmount + healAmountAlter);
		healAmountAlter = 0;
	}

	/// <summary>
	/// 检查伤害来源和目标，触发对应事件并显示文本信息（对对手造成伤害时）
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	private void CheckDmgTargets_DealingDmgToOpponent(int dmgAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy dealt dmg to player
		{
			effectResultString.value += "// their [" + myCard.name + "] dealt [" + (dmgAmount) + "] damage to You\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOpponent(); // timepoint
			DeckTester.me.deckBDmgOutputs_ToOpp.Add(dmgAmount);
		}
		else // player dealt dmg to enemy
		{
			effectResultString.value += "// your [" + myCard.name + "] dealt [" + (dmgAmount) + "] damage to Enemy\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOwner(); // timepoint
			DeckTester.me.deckADmgOutputs_ToOpp.Add(dmgAmount);
		}
	}

	/// <summary>
	/// 检查伤害来源和目标，触发对应事件并显示文本信息（对自己造成伤害时）
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	private void CheckDmgTargets_DealingDmgToSelf(int dmgAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player dealt dmg to player
		{
			effectResultString.value += "// your [" + myCard.name + "] dealt [" + (dmgAmount) + "] damage to You\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOpponent(); // timepoint
			DeckTester.me.deckADmgOutputs_ToSelf.Add(dmgAmount);
		}
		else // enemy dealt dmg to enemy
		{
			effectResultString.value += "// their [" + myCard.name + "] dealt [" + (dmgAmount) + "] damage to Enemy\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOwner(); // timepoint
			DeckTester.me.deckBDmgOutputs_ToSelf.Add(dmgAmount);
		}
	}

	/// <summary>
	/// 检查治疗来源和目标，触发对应事件并显示文本信息（治疗自己时）
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	private void CheckHealTargets_HealingSelf(int healAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player healed player
		{
			effectResultString.value += "// your [" + myCard.name + "] healed You for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOpponent(); // timepoint
		}
		else // enemy healed enemy
		{
			effectResultString.value += "// their [" + myCard.name + "] healed Enemy for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOwner(); // timepoint
		}
	}

	/// <summary>
	/// 检查治疗来源和目标，触发对应事件并显示文本信息（治疗对手时）
	/// </summary>
	/// <param name="healAmount">治疗数值</param>
	private void CheckHealTargets_HealingOpponent(int healAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy healed player
		{
			effectResultString.value += "// their [" + myCard.name + "] healed You for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOpponent(); // timepoint
		}
		else // player healed enemy
		{
			effectResultString.value += "// your [" + myCard.name + "] healed Enemy for [" + (healAmount + healAmountAlter) + "]\n";
			GameEventStorage.me.onMyPlayerHealed?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerHealed?.RaiseOwner(); // timepoint
		}
	}
}