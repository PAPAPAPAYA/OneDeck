using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ChangeCardTarget : EffectScript
	{
		public void ChangeCardTargetPlayerStatus()
		{
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card is originally player's
			{
				myCardScript.myStatusRef = combatManager.enemyPlayerStatusRef;
				myCardScript.theirStatusRef = combatManager.ownerPlayerStatusRef;
				myCardScript.transform.parent = combatManager.enemyDeckParent.transform;
			}
			else // if this card is originally enemy's
			{
				myCardScript.myStatusRef = combatManager.ownerPlayerStatusRef;
				myCardScript.theirStatusRef = combatManager.enemyPlayerStatusRef;
				myCardScript.transform.parent = combatManager.playerDeckParent.transform;
			}
			
		}
	}
}