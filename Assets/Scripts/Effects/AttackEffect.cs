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
			DecreaseTheirHp();
		}
		// Attack-action timepoint: raised once per attack action, not per segment.
		GameEventStorage.me?.onAnyCardAttacked?.Raise();
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
		// Attack-action timepoint: raised once per attack action, not per segment.
		GameEventStorage.me?.onAnyCardAttacked?.Raise();
	}
}
