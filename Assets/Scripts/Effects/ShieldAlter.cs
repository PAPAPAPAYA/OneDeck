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
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>] gave [<color=grey>" + (shieldAmount + shieldUpAmountAlter) + "</color>] shield to <color=#87CEEB>You</color>\n";
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
			else // enemy gave shield to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>] gave [<color=grey>" + (shieldAmount + shieldUpAmountAlter) + "</color>] shield to <color=orange>Enemy</color>\n";
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
		}
	}
}