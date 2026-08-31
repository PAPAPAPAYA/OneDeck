using UnityEngine;

/// <summary>
/// GRAVE_ROBBER engine (4.0 step-5): "复活1攻击力最高敌方；攻击力变为该卡攻击力".
/// Ruling 2026-08-30: SNAPSHOT — the attack value is copied once at revive resolution and
/// held on the carrier (CardScript.attackSnapshotValue), so later changes to the resurrected
/// enemy never ripple back into this card. Reuses the referenced ReviveEffect configured with
/// ReviveSortBy.MaxAttack; the revived card is read from CombatManager.lastCardRevived.
/// </summary>
public class GraveRobberEffect : EffectScript
{
	[Tooltip("ReviveEffect configured for enemy-side max-attack revive (same child GO)")]
	public ReviveEffect reviveEngine;

	public void ReviveStrongestEnemyAndSnapshot()
	{
		if (reviveEngine == null || myCardScript == null) return;
		reviveEngine.ReviveTheirCards(1);

		var revived = combatManager != null ? combatManager.lastCardRevived : null;
		if (revived == null) return;
		if (revived.myStatusRef == myCardScript.myStatusRef) return; // defensive: only enemy sources
		myCardScript.attackSnapshotValue = revived.GetAttack();
		CombatInfoDisplayer.me?.RefreshDeckInfo();
	}
}
