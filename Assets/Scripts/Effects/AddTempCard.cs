using DefaultNamespace.Managers;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		public void AddCardToMe(GameObject cardToAdd)
		{
			CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, true);
			combatManager.Shuffle();
		}
		public void AddCardToEnemy(GameObject cardToAdd)
		{
			CombatFuncs.me.AddCardInTheMiddleOfCombat(cardToAdd, false);
			combatManager.Shuffle();
		}
	}
}