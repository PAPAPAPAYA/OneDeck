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

			ApplyStatusEffectCore(targetCard, EnumStorage.StatusEffect.Power, powerAmount);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}
	}
}
