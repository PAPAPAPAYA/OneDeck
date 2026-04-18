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
				ApplyStatusEffectCore(myCardScript, statusEffectToGive, extraAmount,
					myStatusEffectResolverScript, statusEffectParticlePrefab, particleYOffset,
					canStatusEffectBeStacked ? extraAmount : 1);
				CombatInfoDisplayer.me?.RefreshDeckInfo();
			}
		}
	}
}
