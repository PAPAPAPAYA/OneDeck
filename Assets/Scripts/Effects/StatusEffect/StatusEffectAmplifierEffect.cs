using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class StatusEffectAmplifierEffect : StatusEffectGiverEffect
	{
		[Header("Status Effect Amplifier")]
		[Tooltip("Multiplier for status effect gain. e.g. 3 means this card gains 3x the counted status effect")]
		public int statusEffectMultiplier = 3;

		/// <summary>
		/// Call this from a GameEventListener on onMeGotStatusEffect
		/// </summary>
		public void AmplifyStatusEffectGain()
		{
			if (combatManager.lastCardGotStatusEffect != myCardScript) return;
			if (statusEffectToCount == EnumStorage.StatusEffect.None) return;
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			if (statusEffectMultiplier <= 1) return;

			var lastEffect = ValueTrackerManager.me?.lastAppliedStatusEffectRef;
			var lastAmount = ValueTrackerManager.me?.lastAppliedStatusEffectAmountRef;
			if (lastEffect == null || lastAmount == null) return;
			if (lastEffect.value != statusEffectToCount) return;

			int extraAmount = lastAmount.value * (statusEffectMultiplier - 1);
			if (extraAmount > 0)
			{
				// VISUAL-FIX(2026-06-21): AmplifyStatusEffectGain has no projectile animation
				//   Cause:    AmplifyStatusEffectGain called ApplyStatusEffectCore directly,
				//             which captures StatusEffectChange but not StatusEffectProjectile
				//   Affects:  StatusEffectAmplifierEffect, StatusEffectGiverEffect, RecorderAnimationPlayer
				//   Regress:  Reveal a card whose onMeGotStatusEffect triggers AmplifyStatusEffectGain
				//             Check: card pops up, projectile flies in, then slots back in
				GiveSelfStatusEffect(extraAmount);
				CombatInfoDisplayer.me?.RefreshDeckInfo();
			}
		}
	}
}
