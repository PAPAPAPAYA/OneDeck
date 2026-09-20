using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode prefab assertions for the per-round revive gate rollout
/// (plans/plan-revive-loop-mitigation-2026-09-19.md §8.3 + the 2026-09-20 second wave):
/// the 8 originally gated ReviveEffect components, the two explicitly UNGATED components
/// (CURSE_SUMMONER enemy half, NECROMANCER awaken amplifier), NECROMANCER's Rare move,
/// the second wave (SPIRIT_CALLER/RIFT_REVIVER x2, ELITE/MASS/DUO x1, RELIC_CURSE_REVIVAL x3),
/// and the cardDesc wording (gated revive clause last, 「每回合一次/两次/三次,复活 N …」).
/// Gate field semantics: ReviveEffect.oncePerRound is an int limit, 0 = unlimited.
/// </summary>
public class ReviveOncePerRoundPrefabTests
{
	private const string Common = "Assets/Prefabs/Cards/4.0/0_Common/";
	private const string Uncommon = "Assets/Prefabs/Cards/4.0/1_Uncommon/";
	private const string Rare = "Assets/Prefabs/Cards/4.0/2_Rare/";

	private static GameObject Load(string path)
	{
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
		Assert.IsNotNull(prefab, "prefab missing: " + path);
		return prefab;
	}

	private static ReviveEffect FindReviveOnChild(GameObject prefab, string childName)
	{
		foreach (var effect in prefab.GetComponentsInChildren<ReviveEffect>(true))
		{
			if (effect.gameObject.name == childName) return effect;
		}
		Assert.Fail("ReviveEffect child '" + childName + "' not found on " + prefab.name);
		return null;
	}

	private static void AssertGated(string path, string childName, string expectedDesc, int expectedLimit = 1)
	{
		var prefab = Load(path);
		var effect = FindReviveOnChild(prefab, childName);
		Assert.AreEqual(expectedLimit, effect.oncePerRound,
			prefab.name + " / " + childName + " per-round revive limit");
		Assert.AreEqual(expectedDesc, prefab.GetComponent<CardScript>().cardDesc, prefab.name + " cardDesc");
	}

	[Test]
	public void GraveHexer_Gated()
	{
		AssertGated(Common + "GRAVE_HEXER.prefab", "revive 1 friend",
			"强化 <b>1</b> 敌方诅咒;每回合一次,复活 <b>1</b> 友方");
	}

	[Test]
	public void Kingslayer_Gated()
	{
		AssertGated(Uncommon + "KINGSLAYER.prefab", "revive 1 friend",
			"埋葬 <b>1</b> 攻击力最高敌方;每回合一次,复活 <b>1</b> 友方");
	}

	[Test]
	public void ReviveSummoner_Gated()
	{
		AssertGated(Uncommon + "REVIVE_SUMMONER.prefab", "revive 1",
			"生成 <b>1</b> 信徒;每回合一次,复活 <b>1</b> 友方");
	}

	[Test]
	public void SoulTrader_Gated()
	{
		AssertGated(Uncommon + "SOUL_TRADER.prefab", "revive 2",
			"埋葬 <b>1</b> 友方;每回合一次,复活 <b>2</b> 友方");
	}

	[Test]
	public void CurseSummoner_FriendlyHalfGated_EnemyHalfOpen()
	{
		AssertGated(Uncommon + "CURSE_SUMMONER.prefab", "revive 1 friend",
			"复活 <b>1</b> 敌方诅咒;每回合一次,复活 <b>1</b> 友方");
		var prefab = Load(Uncommon + "CURSE_SUMMONER.prefab");
		Assert.AreEqual(0, FindReviveOnChild(prefab, "revive enemy curse").oncePerRound,
			"复辟 half (revive enemy curse) must stay ungated (§8.3 组四)");
	}

	[Test]
	public void Necromancer_RevealHalfGated_AwakenOpen_Rare()
	{
		AssertGated(Rare + "NECROMANCER.prefab", "revive 1 friendly",
			"苏醒:复活 <b>1</b> 友方;每回合一次,复活 <b>1</b> 友方");
		var prefab = Load(Rare + "NECROMANCER.prefab");
		Assert.AreEqual(0, FindReviveOnChild(prefab, "awaken revive 1").oncePerRound,
			"awaken amplifier must stay ungated (§8.1.3)");
		Assert.AreEqual(EnumStorage.Rarity.Rare, prefab.GetComponent<CardScript>().rarity,
			"NECROMANCER moves Uncommon -> Rare (§8.1.4)");
	}

	[Test]
	public void BeastReviver_Gated()
	{
		AssertGated(Common + "BEAST_REVIVER.prefab", "revive 1 creature",
			"攻击;每回合一次,复活 <b>1</b> 友方实体");
	}

	[Test]
	public void RiftShepherd_Gated()
	{
		AssertGated(Common + "RIFT_SHEPHERD.prefab", "revive 1 believer",
			"攻击;每回合一次,复活 <b>1</b> 张tag为[<tag:Believer>]的友方卡");
	}

	// ------------------------------------------------ 2026-09-20 second wave

	[Test]
	public void SpiritCaller_Gated2()
	{
		AssertGated(Common + "SPIRIT_CALLER.prefab", "revive 1 noncreature",
			"每回合两次,复活 <b>1</b> 友方现象", 2);
	}

	[Test]
	public void EliteReviver_Gated()
	{
		AssertGated(Common + "ELITE_REVIVER.prefab", "revive enhanced",
			"攻击;每回合一次,复活 <b>1</b> 被强化过的友方实体");
	}

	[Test]
	public void RiftReviver_Gated2()
	{
		AssertGated(Uncommon + "RIFT_REVIVER.prefab", "exile 1 rift",
			"放逐 <b>1</b> 友方信徒: 每回合两次,复活 <b>2</b> 友方现象", 2);
	}

	[Test]
	public void RelicCurseRevival_Gated3()
	{
		AssertGated(Uncommon + "RELIC_CURSE_REVIVAL.prefab", "enemy curse revealed revive 1",
			"被动:敌方诅咒揭晓时,每回合三次,复活 <b>1</b> 友方", 3);
	}

	[Test]
	public void MassReviver_Gated()
	{
		AssertGated(Rare + "MASS_REVIVER.prefab", "revive 3 common",
			"每回合一次,复活 <b>3</b> 友方✦卡");
	}

	[Test]
	public void DuoReviver_Gated()
	{
		AssertGated(Rare + "DUO_REVIVER.prefab", "revive 2 uncommon",
			"每回合一次,复活 <b>2</b> 友方✦✦卡");
	}
}
