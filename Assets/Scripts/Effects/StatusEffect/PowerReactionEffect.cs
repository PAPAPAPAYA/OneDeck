using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class PowerReactionEffect : StatusEffectGiverEffect
	{
		[Header("Power Reaction")]
		[Tooltip("Amount of Power to give to the card that just gained Power")]
		public int powerAmount = 1;
		[Tooltip("If true, will not react when this card itself gains Power")]
		public bool excludeSelf = true;
		public void GivePowerToCardThatGotPower()
		{
			var targetCard = combatManager.lastCardGotPower;
			if (targetCard == null) return;
			if (excludeSelf && targetCard == myCardScript) return;

			// ApplyStatusEffectCore snapshots the display state and captures StatusEffectChange.
			// Because CaptureBatchStatusEffectAnimation adds a StatusEffectProjectile for the same target,
			// RecorderAnimationPlayer.MarkDeferredDisplayCommits() automatically defers CommitDisplayState
			// until the projectile lands, keeping the card text update in sync with the animation.
			ApplyStatusEffectCore(targetCard, EnumStorage.StatusEffect.Power, powerAmount,
				myStatusEffectResolverScript, statusEffectParticlePrefab, particleYOffset,
				canStatusEffectBeStacked ? powerAmount : 1);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { targetCard }, powerAmount);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}
	}
}