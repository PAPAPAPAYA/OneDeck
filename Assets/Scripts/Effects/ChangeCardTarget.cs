using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ChangeCardTarget : EffectScript
	{
		public void ChangeCardTargetPlayerStatus()
		{
			if (myParentCardScript != null) // if my parent card script isn't null, then this effect is used in a resolver
			{
				if (myParentCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card is originally player's
				{
					myParentCardScript.myStatusRef = combatManager.enemyPlayerStatusRef;
					myParentCardScript.theirStatusRef = combatManager.ownerPlayerStatusRef;
					myParentCardScript.transform.parent = combatManager.enemyDeckParent.transform;
				}
				else // if this card is originally enemy's
				{
					myParentCardScript.myStatusRef = combatManager.ownerPlayerStatusRef;
					myParentCardScript.theirStatusRef = combatManager.enemyPlayerStatusRef;
					myParentCardScript.transform.parent = combatManager.playerDeckParent.transform;
				}
				// myParentCardScript.myStatusRef = 
				// 	myParentCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
				// 		combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
				// myParentCardScript.theirStatusRef = 
				// 	myParentCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef ? 
				// 		combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
			}
			else
			{
				myCardScript.myStatusRef = 
					myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
						combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
				myCardScript.theirStatusRef = 
					myCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef ? 
						combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
			}
		}
	}
}