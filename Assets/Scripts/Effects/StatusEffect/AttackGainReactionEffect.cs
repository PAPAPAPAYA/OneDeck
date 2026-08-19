using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	/// <summary>
	/// Attack-gain reaction (attack-attribute redesign; supersedes PowerReactionEffect and
	/// StatusEffectAmplifierEffect). Reacts to the attack-gain event family:
	/// - GiveAttackToCardThatGainedAttack: give attack to the card that just gained attack
	///   (WEAPON_SPIRIT "每当友方获得攻击力:额外 +1").
	/// - AmplifyAttackGain: re-gain attack on self when this card gained attack
	///   (POWER_CRAVER "获得攻击力时:获得 2 倍攻击力").
	/// </summary>
	public class AttackGainReactionEffect : AttackGiverEffect
	{
		[Header("Attack Reaction")]
		[Tooltip("Amount of attack to give to the card that just gained attack")]
		public int attackAmount = 1;
		[Tooltip("If true, will not react when this card itself gains attack")]
		public bool excludeSelf = true;
		[Tooltip("Multiplier for attack gain on self (AmplifyAttackGain). e.g. 2 means this card gains 2x the attack it just gained")]
		public int attackMultiplier = 2;

		/// <summary>
		/// Give attack to the card that just gained attack (listener on onAnyCardGainedAttack).
		/// </summary>
		public void GiveAttackToCardThatGainedAttack()
		{
			var targetCard = combatManager.lastCardGainedAttack;
			if (targetCard == null) return;
			if (excludeSelf && targetCard == myCardScript) return;

			ApplyAttackCore(targetCard, attackAmount, statusEffectParticlePrefab, particleYOffset);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { targetCard }, attackAmount);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Re-gain attack on self when this card gained attack (listener on onMeGainedAttack).
		/// Gains (attackMultiplier - 1) x the amount just gained.
		/// </summary>
		public void AmplifyAttackGain()
		{
			if (combatManager.lastCardGainedAttack != myCardScript) return;
			if (attackMultiplier <= 1) return;

			int lastAmount = combatManager.lastAttackGainedAmount;
			int extraAmount = lastAmount * (attackMultiplier - 1);
			if (extraAmount > 0)
			{
				GiveSelfAttack(extraAmount);
				CombatInfoDisplayer.me?.RefreshDeckInfo();
			}
		}
	}
}
