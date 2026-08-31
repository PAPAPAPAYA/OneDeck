using UnityEngine;

/// <summary>
/// RELIC_RIFT_OVERRIDE engine (4.0 step-5): "被动：友方[信徒]效果变为：复活1敌方[诅咒]；放逐自身".
/// The believer token's reveal effect (original: ExileSelf + ReviveMyCards(1)) is intercepted
/// here on the REVIVE half only: while the side's rift-override flag is armed (set by the
/// passive card each shuffle) and the token belongs to that side, the token instead revives
/// 1 ENEMY curse (JU_ON by the combats curse type). Enemy-side believers always keep the
/// default behavior. The ExileSelf half of the token effect stays untouched.
/// </summary>
public class RiftOverrideAwareReviveEffect : ReviveEffect
{
	/// <summary>
	/// Replacement binding for the believer token's revive container: override-aware.
	/// </summary>
	public void FriendRiftRevealOrOverride()
	{
		if (IsSideOverrideActive())
		{
			var storage = GameEventStorage.me;
			if (storage != null && storage.curseCardTypeID != null && !string.IsNullOrEmpty(storage.curseCardTypeID.value))
			{
				typeIDFilter = storage.curseCardTypeID.value;
			}
			ReviveTheirCards(1);
			return;
		}
		ReviveMyCards(1);
	}

	private bool IsSideOverrideActive()
	{
		var tracker = ValueTrackerManager.me;
		if (tracker == null || myCardScript == null || myCardScript.myStatusRef == null || combatManager == null) return false;
		var flag = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef
			? tracker.riftOverrideOwnerThisRoundRef
			: tracker.riftOverrideEnemyThisRoundRef;
		return flag != null && flag.value > 0;
	}
}
