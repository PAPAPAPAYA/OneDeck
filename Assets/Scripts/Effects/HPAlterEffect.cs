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
	
	[Tooltip("标记是否是status effect造成的伤害（status effect伤害不触发攻击动画）")]
	public bool isStatusEffectDamage = false;
	
	[Tooltip("额外伤害值 - 用于DecreaseMyHp和DecreaseTheirHp")]
	public int extraDmg = 0;
	
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
	/// 处理伤害（抽离公共逻辑，包括护盾和生命值处理）
	/// </summary>
	/// <param name="totalDmg">总伤害数值</param>
	/// <param name="targetStatus">目标玩家状态</param>
	private void ProcessDamage(int totalDmg, PlayerStatusSO targetStatus)
	{
		ProcessShieldNHp(totalDmg, targetStatus);
		targetStatus.hp = Mathf.Clamp(targetStatus.hp, 0, targetStatus.hpMax);
	}

	/// <summary>
	/// 减少自身生命值（考虑额外伤害）
	/// 使用 extraDmg 字段作为额外伤害值
	/// </summary>
	public void DecreaseMyHp()
	{
		DmgCalculator();
		int totalDmg = extraDmg + dmgAmountAlter;
		
		// status effect伤害不触发攻击动画，直接执行
		if (isStatusEffectDamage)
		{
			ProcessDamage(totalDmg, myCardScript.myStatusRef);
			CheckDmgTargets_DealingDmgToSelf(totalDmg);
			dmgAmountAlter = 0;
			return;
		}
		
		// 请求攻击动画（攻击自己）
		if (AttackAnimationManager.me != null)
		{
			AttackAnimationManager.me.RequestAttackAnimation(myCard, false, 
				onHit: () =>
				{
					ProcessDamage(totalDmg, myCardScript.myStatusRef);
					CheckDmgTargets_DealingDmgToSelf(totalDmg);
				},
				onComplete: null);
		}
		else
		{
			// 如果没有动画管理器，直接执行
			ProcessDamage(totalDmg, myCardScript.myStatusRef);
			CheckDmgTargets_DealingDmgToSelf(totalDmg);
		}
		
		dmgAmountAlter = 0;
	}

	/// <summary>
	/// 由状态效果造成的自身伤害（不触发攻击动画）
	/// </summary>
	/// <param name="dmgAmount">伤害数值</param>
	public void DecreaseMyHpFromStatusEffect(int dmgAmount)
	{
		int totalDmg = dmgAmount + baseDmg.value;
		ProcessDamage(totalDmg, myCardScript.myStatusRef);
		CheckDmgTargets_DealingDmgToSelf(totalDmg);
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
		extraDmg = baseDmgAmount + extraDmgAmount;
		DecreaseTheirHp();
		extraDmg = 0;
	}

	/// <summary>
	/// 根据combinedDeckZone和revealZone中拥有Infected状态效果且属于卡片所有者的卡的数量减少对方生命值
	/// </summary>
	/// <param name="baseDmgAmount">基础伤害数值</param>
	public void DecreaseTheirHp_BasedOnInfectedCardsOwned(int baseDmgAmount)
	{
		var infectedCardCount = 0;
		
		// 合并两个区域到临时列表
		List<GameObject> allCards = new();
		UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, allCards, false);
		if (combatManager.revealZone != null)
		{
			allCards.Add(combatManager.revealZone);
		}
		
		// 遍历所有卡片统计Infected数量
		foreach (var card in allCards)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			// 跳过中立卡，只统计己方感染卡
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
	/// 根据combinedDeckZone和revealZone中友方指定cardTypeID的卡牌数量减少对方生命值
	/// </summary>
	/// <param name="cardTypeID">要统计的卡牌类型ID</param>
	public void DecreaseTheirHp_BasedOnFriendlyCardTypeCount(string cardTypeID)
	{
		var cardCount = 0;
		
		// 合并两个区域到临时列表
		List<GameObject> allCards = new();
		UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, allCards, false);
		if (combatManager.revealZone != null)
		{
			allCards.Add(combatManager.revealZone);
		}
		
		// 遍历所有卡片统计指定类型的友方卡牌数量
		foreach (var card in allCards)
		{
			if (card == null) continue;
			var cardScript = card.GetComponent<CardScript>();
			// 跳过中立卡，只统计己方指定类型的卡
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
	/// 减少对方生命值（考虑额外伤害）
	/// 使用 extraDmg 字段作为额外伤害值
	/// </summary>
	public void DecreaseTheirHp()
	{
		print("deal dmg");
		DmgCalculator();
		int totalDmg = extraDmg + dmgAmountAlter;
		print("extra dmg: " + extraDmg);
		print("dmg amount alter: " + dmgAmountAlter);
		print("dmg amount: " + totalDmg);
		
		// status effect伤害不触发攻击动画，直接执行
		if (isStatusEffectDamage)
		{
			ProcessDamage(totalDmg, myCardScript.theirStatusRef);
			CheckDmgTargets_DealingDmgToOpponent(totalDmg);
			dmgAmountAlter = 0;
			return;
		}
		
		// 判断攻击目标（true=攻击敌人, false=攻击玩家自己）
		bool isAttackingEnemy = myCardScript.theirStatusRef != combatManager.ownerPlayerStatusRef;
		
		// 请求攻击动画
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
			// 如果没有动画管理器，直接执行
			ProcessDamage(totalDmg, myCardScript.theirStatusRef);
			CheckDmgTargets_DealingDmgToOpponent(totalDmg);
		}
		
		dmgAmountAlter = 0;
	}

	/// <summary>
	/// 由状态效果造成的对方伤害（不触发攻击动画）
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
	/// 多次减少对方生命值，每次造成 baseDmg + extraDmg 伤害
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

	#endregion

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
	/// 检查伤害来源和目标，触发对应事件并显示文本信息（对自己造成伤害时）
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
	/// 检查治疗来源和目标，触发对应事件并显示文本信息（治疗自己时）
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
	/// 检查治疗来源和目标，触发对应事件并显示文本信息（治疗对手时）
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
}