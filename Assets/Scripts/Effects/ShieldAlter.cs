using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ShieldAlter : EffectScript
	{
		[HideInInspector]
		public int shieldUpAmountAlter;

		public void UpMyShield(int amount)
		{
			myCardScript.myStatusRef.shield += amount + shieldUpAmountAlter;
			CheckShieldUpTarget_UppingSelfShield(amount);
		}
		private void CheckShieldUpTarget_UppingSelfShield(int shieldAmount)
		{
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player gave shield to player
			{
				effectResultString.value += "// [" + myCard.name + "] gave [" + (shieldAmount + shieldUpAmountAlter) + "] shield to You\n";
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
			else // enemy gave shield to enemy
			{
				effectResultString.value += "// [" + myCard.name + "] gave [" + (shieldAmount + shieldUpAmountAlter) + "] shield to Enemy\n";
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
		}
	}
}