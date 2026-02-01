using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class HPAlterEffect : EffectScript
{
	[Tooltip("base dmg that will be dealt")]
	public IntSO baseDmg;
	[HideInInspector]
	public int dmgAmountAlter = 0;
	[HideInInspector]
	public int healAmountAlter = 0;
	
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

	public void DecreaseMyHp(int extraDmg)
	{
		DmgCalculator();
		ProcessShieldNHp(extraDmg + dmgAmountAlter, myCardScript.myStatusRef);
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckDmgTargets_DealingDmgToSelf(extraDmg);
		dmgAmountAlter = 0;
	}

	public void IncreaseMyHp(int healAmount)
	{
		myCardScript.myStatusRef.hp += healAmount + healAmountAlter;
		myCardScript.myStatusRef.hp = Mathf.Clamp(myCardScript.myStatusRef.hp, 0, myCardScript.myStatusRef.hpMax);
		CheckHealTargets_HealingSelf(healAmount);
		healAmountAlter = 0;
	}

	public void DecreaseTheirHp_BasedOnLostHp(int baseDmgAmount)
	{
		var extraDmgAmount = (myCardScript.myStatusRef.hpMax - myCardScript.myStatusRef.hp)/2;
		DecreaseTheirHp(baseDmgAmount + extraDmgAmount);
	}

	public void DecreaseTheirHp(int extraDmg)
	{
		DmgCalculator();
		ProcessShieldNHp(extraDmg + dmgAmountAlter, myCardScript.theirStatusRef);
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckDmgTargets_DealingDmgToOpponent(extraDmg);
		dmgAmountAlter = 0;
	}

	public void IncreaseTheirHp(int healAmount)
	{
		myCardScript.theirStatusRef.hp -= healAmount + healAmountAlter;
		myCardScript.theirStatusRef.hp = Mathf.Clamp(myCardScript.theirStatusRef.hp, 0, myCardScript.theirStatusRef.hpMax);
		CheckHealTargets_HealingOpponent(healAmount);
		healAmountAlter = 0;
	}

	// check damage from and to to raise corresponding events and show text info
	private void CheckDmgTargets_DealingDmgToOpponent(int dmgAmount)
	{
		if (myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef) // enemy dealt dmg to player
		{
			effectResultString.value += "// their [" + myCard.name + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to You\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOpponent(); // timepoint
			DeckTester.me.deckBDmgOutputs_ToOpp.Add(dmgAmount + dmgAmountAlter);
		}
		else // player dealt dmg to enemy
		{
			effectResultString.value += "// your [" + myCard.name + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to Enemy\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOwner(); // timepoint
			DeckTester.me.deckADmgOutputs_ToOpp.Add(dmgAmount + dmgAmountAlter);
		}
	}

	private void CheckDmgTargets_DealingDmgToSelf(int dmgAmount)
	{
		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player dealt dmg to player
		{
			effectResultString.value += "// your [" + myCard.name + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to You\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOwner(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOpponent(); // timepoint
			DeckTester.me.deckADmgOutputs_ToSelf.Add(dmgAmount + dmgAmountAlter);
		}
		else // enemy dealt dmg to enemy
		{
			effectResultString.value += "// their [" + myCard.name + "] dealt [" + (dmgAmount + dmgAmountAlter) + "] damage to Enemy\n";
			GameEventStorage.me.onMyPlayerTookDmg?.RaiseOpponent(); // timepoint
			GameEventStorage.me.onTheirPlayerTookDmg?.RaiseOwner(); // timepoint
			DeckTester.me.deckBDmgOutputs_ToSelf.Add(dmgAmount + dmgAmountAlter);
		}
	}

	// check heal from and to to raise events and show text info
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