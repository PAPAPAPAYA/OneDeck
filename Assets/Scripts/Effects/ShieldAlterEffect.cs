using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ShieldAlterEffect : EffectScript
	{
		[HideInInspector]
		public int shieldUpAmountAlter;

		[Header("Based on IntSO")]
		[Tooltip("IntSO used when this card belongs to the owner/player")]
		public IntSO ownerIntSO;
		[Tooltip("IntSO used when this card belongs to the enemy")]
		public IntSO enemyIntSO;

		public void UpMyShield(int amount)
		{
			myCardScript.myStatusRef.shield += amount + shieldUpAmountAlter;
			CheckShieldUpTarget_UppingSelfShield(amount);
		}

		/// <summary>
		/// Based on ownerIntSO/enemyIntSO, increase own shield.
		/// Uses ownerIntSO when this card belongs to the owner, otherwise enemyIntSO.
		/// </summary>
		public virtual void UpMyShield_BasedOnIntSO()
		{
			IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
			if (intSO == null) return;
			if (intSO.value <= 0) return;

			UpMyShield(intSO.value);
		}
		private void CheckShieldUpTarget_UppingSelfShield(int shieldAmount)
		{
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // player gave shield to player
			{
				AppendLog("// [" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]为" + GameColorPalette.Me.friendly.OpenTag + "你</color>提供了[" + GameColorPalette.Me.shield.OpenTag + (shieldAmount + shieldUpAmountAlter) + "</color>]层护盾");
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
			else // enemy gave shield to enemy
			{
				AppendLog("// [" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]为" + GameColorPalette.Me.enemy.OpenTag + "敌人</color>提供了[" + GameColorPalette.Me.shield.OpenTag + (shieldAmount + shieldUpAmountAlter) + "</color>]层护盾");
				GameEventStorage.me.onTheirPlayerShieldUpped?.RaiseOwner(); // timepoint
				GameEventStorage.me.onMyPlayerShieldUpped?.RaiseOpponent(); // timepoint
			}
		}
	}
}
