using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ChangeCardTarget : EffectScript
	{
		public void ChangeCardTargetPlayerStatus()
		{
			if (myParentCardScript != null)
			{
				myParentCardScript.myStatusRef = 
					myParentCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
						combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
				myParentCardScript.theirStatusRef = 
					myParentCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef ? 
						combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
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