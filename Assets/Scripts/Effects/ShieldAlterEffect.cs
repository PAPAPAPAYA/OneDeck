using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ShieldAlterEffect : EffectScript
	{
		[HideInInspector]
		public int shieldUpAmountAlter;

		public void UpMyShield(int amount)
		{
			myCardScript.myStatusRef.shield += amount + shieldUpAmountAlter;
			CheckShieldUpTarget_UppingSelfShield(amount);
		}

		public void UpMyShield_BasedOnIntSO(IntSO intSO)
		{
			if (intSO == null) return;
			UpMyShield(intSO.value);
		}
		private void CheckShieldUpTarget_UppingSelfShield(int shieldAmount)
		{
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player gave shield to player
			{
				effectResultString.value += "// [<color=#87CEEB>" + myCard.name + "</color>]为<color=#87CEEB>你</color>提供了[<color=grey>" + (shieldAmount + shieldUpAmountAlter) + "</color>]层护盾\n";
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
			else // enemy gave shield to enemy
			{
				effectResultString.value += "// [<color=orange>" + myCard.name + "</color>]为<color=orange>敌人</color>提供了[<color=grey>" + (shieldAmount + shieldUpAmountAlter) + "</color>]层护盾\n";
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
		}
	}
}
