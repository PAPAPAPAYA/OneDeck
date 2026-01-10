using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{

		public void AddCardToMe(GameObject cardToAdd)
		{
			combatManager.AddCardInTheMiddleOfCombat(cardToAdd, true);
			combatManager.Shuffle();
		}
		public void AddCardToEnemy(GameObject cardToAdd)
		{
			combatManager.AddCardInTheMiddleOfCombat(cardToAdd, false);
			combatManager.Shuffle();
		}
	}
}