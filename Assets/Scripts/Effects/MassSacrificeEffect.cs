using UnityEngine;

/// <summary>
/// MASS_SACRIFICE engine (4.0 step-5): "埋葬所有友方；每埋葬1友方，生成1信徒".
/// Ruling 2026-08-31: the generated believers are NOT delay-revived — they land in the
/// graveyard side (default AddCard placement), so the card is a pure grave-filler /
/// 信徒 mass-producer with its cost being the burial itself. Reads
/// BuryEffect.lastSuccessfulBuryCount after BuryAllMyCards and spawns exactly that many
/// RIFT tokens via the referenced AddTempCard (its normal reveal path is on the token).
/// </summary>
public class MassSacrificeEffect : EffectScript
{
	[Tooltip("BuryEffect that performs the all-friendly burial (same child GO)")]
	public BuryEffect buryEngine;
	[Tooltip("AddTempCard used to spawn the believers (same child GO)")]
	public DefaultNamespace.Effects.AddTempCard addEngine;
	[Tooltip("Believer (RIFT) token prefab")]
	public GameObject riftToken;

	public void SacrificeAllThenSpawnBelievers()
	{
		if (buryEngine == null || addEngine == null || riftToken == null) return;
		buryEngine.BuryAllMyCards();
		int buried = buryEngine.lastSuccessfulBuryCount;
		if (buried <= 0) return;
		addEngine.cardCount = buried;
		addEngine.AddCardToMe(riftToken);
	}
}
