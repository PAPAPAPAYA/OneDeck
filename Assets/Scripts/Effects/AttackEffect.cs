using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Attack settlement for the attack-attribute redesign.
/// Attack() resolves the card's current attack (GetAttack(), including permanent growth,
/// this-round modifiers and dynamic resolvers) and deals it per segment (GetAttackTimes()).
/// Each segment reuses the HPAlterEffect damage pipeline (immediate HP loss + Attack animation
/// + damage events + per-card stats), preserving the xN multi-hit rules.
/// </summary>
public class AttackEffect : HPAlterEffect
{
	/// <summary>
	/// Attack damage comes from the card's attack attribute, not baseDmg + Power layers.
	/// </summary>
	protected override int ComputeTotalDamage()
	{
		return (myCardScript != null ? myCardScript.GetAttack() : 0) + extraDmg;
	}

	/// <summary>
	/// Attack: one segment per attack-time on the card (attack xN).
	/// No-ops when the card has no attack to resolve (0 or negative).
	/// </summary>
	public void Attack()
	{
		if (myCardScript == null) return;
		AttackTimes(myCardScript.GetAttackTimes());
	}

	/// <summary>
	/// Attack with an explicit segment count (e.g. a woken card attacking once with its own attack).
	/// No-ops when the card has no attack to resolve (0 or negative).
	/// </summary>
	public void AttackTimes(int times)
	{
		if (myCardScript == null || times <= 0 || myCardScript.GetAttack() <= 0) return;
		for (int i = 0; i < times; i++)
		{
			// RELIC_BLOOD_PACT (ruling 2026-08-31): while armed, friendly attacks on the ENEMY
			// player deal NO damage and instead enhance the enemy curse by the same amount —
			// self-damage (AttackSelfTimes -> DecreaseMyHp) is untouched. Attack events still
			// raise below (the attack action happened, only its resolution changed).
			if (BloodPactConvertsDamage())
			{
				EnhanceCurseForBloodPact();
				continue;
			}
			DecreaseTheirHp();
		}
		// Attack-action timepoint: raised once per attack action, not per segment.
		RaiseAttackEvents(false);
	}

	/// <summary>
	/// Curse engine used by the blood-pact conversion (configured on the same child GO).
	/// </summary>
	[Tooltip("CurseEffect that receives the blood-pact enhancement (same child GO)")]
	public DefaultNamespace.Effects.CurseEffect curseEngine;

	private bool BloodPactConvertsDamage()
	{
		if (myCardScript == null || combatManager == null || myCardScript.myStatusRef == null) return false;
		var tracker = ValueTrackerManager.me;
		if (tracker == null) return false;
		var flag = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef
			? tracker.bloodPactOwnerThisRoundRef
			: tracker.bloodPactEnemyThisRoundRef;
		return flag != null && flag.value > 0;
	}

	private void EnhanceCurseForBloodPact()
	{
		if (curseEngine == null) return;
		int amount = myCardScript != null ? myCardScript.GetAttack() : 0;
		curseEngine.EnhanceCurse(amount);
	}

	/// <summary>
	/// Self-attack: the card's attack resolves against its own player (attack self-damage).
	/// Same segment rules as Attack().
	/// </summary>
	public void AttackSelf()
	{
		if (myCardScript == null) return;
		AttackSelfTimes(myCardScript.GetAttackTimes());
	}

	/// <summary>
	/// Self-attack with an explicit segment count (e.g. a woken card attacking itself once).
	/// No-ops when the card has no attack to resolve (0 or negative).
	/// </summary>
	public void AttackSelfTimes(int times)
	{
		if (myCardScript == null || times <= 0 || myCardScript.GetAttack() <= 0) return;
		for (int i = 0; i < times; i++)
		{
			DecreaseMyHp();
		}
		// Self-attacks count as attack actions for onAnyCardAttacked, but never for
		// onAnyFriendlyCardAttacked (a friendly [attacker] attacking does not include
		// self-damage, e.g. JU_ON burning itself).
		RaiseAttackEvents(true);
	}

	/// <summary>
	/// Attack with one segment per opponent-buried-count tracker value
	/// (BONE_COMBINATION "攻击 ×本回合被埋葬的敌方数量"). Each hit deals the card's attack.
	/// </summary>
	public void AttackTimesBasedOnOpponentBuriedCount()
	{
		int times = 0;
		if (ValueTrackerManager.me != null)
		{
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
			{
				if (ValueTrackerManager.me.enemyCardsBuriedCountRef != null)
				{
					times = ValueTrackerManager.me.enemyCardsBuriedCountRef.value;
				}
			}
			else
			{
				if (ValueTrackerManager.me.ownerCardsBuriedCountRef != null)
				{
					times = ValueTrackerManager.me.ownerCardsBuriedCountRef.value;
				}
			}
		}
		AttackTimes(times);
	}

	/// <summary>
	/// Attack with one segment per ownerIntSO/enemyIntSO value
	/// (BODY_CANON "墓地每有 1 张友方卡:攻击").
	/// </summary>
	public virtual void AttackTimesBasedOnIntSO()
	{
		IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
		if (intSO == null) return;
		AttackTimes(intSO.value);
	}

	/// <summary>
	/// Attack-action timepoint (once per action, not per segment).
	/// onAnyCardAttacked covers every attack action (self-attacks included);
	/// onAnyFriendlyCardAttacked covers non-self attack actions only.
	/// Both are delivered to the attacking card's faction: a friendly attacker raises
	/// RaiseOwner() (friendly-side listeners hear "a friendly card attacked"), an enemy
	/// attacker raises RaiseOpponent() — so an enemy card's self-damage never reaches
	/// friendly listeners (e.g. 战旗 "友方[攻击者]攻击时").
	/// </summary>
	private void RaiseAttackEvents(bool isSelf)
	{
		if (myCardScript == null || combatManager == null) return;
		combatManager.lastCardAttacked = myCardScript;

		var storage = GameEventStorage.me;
		if (storage == null) return;

		if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
		{
			storage.onAnyCardAttacked?.RaiseOwner();
			if (!isSelf)
			{
				storage.onAnyFriendlyCardAttacked?.RaiseOwner();
			}
		}
		else
		{
			storage.onAnyCardAttacked?.RaiseOpponent();
			if (!isSelf)
			{
				storage.onAnyFriendlyCardAttacked?.RaiseOpponent();
			}
		}
	}
}
