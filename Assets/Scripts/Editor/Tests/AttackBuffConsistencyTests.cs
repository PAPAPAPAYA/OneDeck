using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EditMode tests for the attack-buff full-chain consistency fix (2026-11-26,
/// plan-attack-buff-consistency-2026-11-26): masked cards (IgnoresAttackBuffs) never
/// absorb attack growth anywhere — write gates on ModifyAttack / ModifyAttackThisRound,
/// ApplyAttackCore immune no-op (no stats / events / AttackChange capture + immune log
/// line), grant & drain pool pre-exclusion, and the GRAVE_ROBBER snapshot ledger clear.
/// Display-layer behavior of the same mask set is covered by AttackDisplayMaskedCardTests.
/// </summary>
public class AttackBuffConsistencyTests : HeadlessCombatTestFixture
{
	/// <summary>
	/// Exposes the protected ApplyAttackCore for direct assertions (probe pattern).
	/// </summary>
	private class ApplyAttackProbe : EffectScript
	{
		public bool LastResult;

		public bool Apply(CardScript target, int amount, bool suppressLog = false)
		{
			LastResult = ApplyAttackCore(target, amount, null, 0f, suppressLog: suppressLog);
			return LastResult;
		}
	}

	private CardScript MaskedResolverCard(bool isOwner, string name, int resolverValue)
	{
		var card = CreateCard(isOwner, name);
		var cs = card.GetComponent<CardScript>();
		cs.cardType = EnumStorage.CardType.Creature;
		cs.printedAttack = 3;
		cs.SetAttackResolver(() => resolverValue);
		return cs;
	}

	private CardScript PlainCreatureCard(bool isOwner, string name, int attack)
	{
		var card = CreateCard(isOwner, name);
		var cs = card.GetComponent<CardScript>();
		cs.cardType = EnumStorage.CardType.Creature;
		cs.printedAttack = attack;
		return cs;
	}

	// ---------- write gates ----------

	[Test]
	public void ModifyAttack_GatedOnMaskedCards_LeavesLedgerZero()
	{
		var cs = MaskedResolverCard(true, "Masked", 5);

		cs.ModifyAttack(2);
		cs.ModifyAttack(-1);
		cs.ModifyAttackThisRound(2);

		Assert.AreEqual(0, cs.attackGrowth, "Growth ledger stays 0 on masked cards");
		Assert.AreEqual(0, cs.attackModThisRound, "This-round ledger stays 0 on masked cards");
		Assert.AreEqual(5, cs.GetAttack(), "Settlement unchanged");
	}

	[Test]
	public void ModifyAttack_PlainCard_StillAccumulates()
	{
		var cs = PlainCreatureCard(true, "Plain", 3);

		cs.ModifyAttack(2);
		cs.ModifyAttackThisRound(-1);

		Assert.AreEqual(2, cs.attackGrowth, "Plain cards keep the ungated growth path");
		Assert.AreEqual(-1, cs.attackModThisRound, "Plain cards keep the ungated this-round path");
		Assert.AreEqual(4, cs.GetAttack(), "Plain settlement absorbs the buff");
	}

	// ---------- ApplyAttackCore gate ----------

	[Test]
	public void ApplyAttackCore_OnMaskedTarget_IsLoggedImmuneNoOp()
	{
		var source = CreateCard(true, "Giver");
		var probe = CreateEffect<ApplyAttackProbe>(source);
		var target = MaskedResolverCard(true, "Masked", 5);

		EffectChainManager.MakeANewEffectRecorder(source, probe.gameObject);
		var recorder = EffectChainManager.Me.currentEffectRecorder.GetComponent<EffectRecorder>();
		CombatLog log = global::CombatLog.me;
		log.Clear();

		bool applied = probe.Apply(target, 2);

		Assert.IsFalse(applied, "Masked grant reports not-applied");
		Assert.AreEqual(0, target.attackGrowth, "No growth written");
		Assert.IsNull(CombatManager.lastCardGainedAttack, "No attack-gain event state written");
		Assert.AreEqual(0, recorder.animationRequests.Count, "No AttackChange request captured");
		string text = log.GetRenderedText();
		Assert.IsTrue(text.Contains("无效"), "Log carries the immune line, got: " + text);
		Assert.IsFalse(text.Contains("给予"), "Log must not claim a grant, got: " + text);
	}

	[Test]
	public void ApplyAttackCore_OnMaskedTarget_ImmuneLineIgnoresSuppressLog()
	{
		var source = CreateCard(true, "Giver");
		var probe = CreateEffect<ApplyAttackProbe>(source);
		var target = MaskedResolverCard(true, "Masked", 5);

		CombatLog log = global::CombatLog.me;
		log.Clear();

		probe.Apply(target, 2, suppressLog: true);

		Assert.IsTrue(log.GetRenderedText().Contains("无效"), "Immune line is not bound by suppressLog");
	}

	[Test]
	public void ApplyAttackCore_OnPlainTarget_StillAppliesCapturesAndLogs()
	{
		var source = CreateCard(true, "Giver");
		var probe = CreateEffect<ApplyAttackProbe>(source);
		var target = PlainCreatureCard(true, "Plain", 3);

		EffectChainManager.MakeANewEffectRecorder(source, probe.gameObject);
		var recorder = EffectChainManager.Me.currentEffectRecorder.GetComponent<EffectRecorder>();
		CombatLog log = global::CombatLog.me;
		log.Clear();

		bool applied = probe.Apply(target, 2);

		Assert.IsTrue(applied, "Plain grant applies");
		Assert.AreEqual(2, target.attackGrowth, "Growth lands on plain cards");
		Assert.AreEqual(target, CombatManager.lastCardGainedAttack, "Event state points at the target");
		Assert.AreEqual(1, recorder.animationRequests.Count, "AttackChange request captured");
		Assert.IsTrue(log.GetRenderedText().Contains("给予"), "Grant line logged");
	}

	// ---------- grant pool pre-exclusion ----------

	[Test]
	public void GiveAttackToXFriendly_PreExcludesMaskedCards()
	{
		var source = CreateCard(true, "Giver");
		var effect = CreateEffect<AttackGiverEffect>(source);
		effect.xFriendlyCount = 2;
		effect.yFriendlyLayerCount = 2;

		var masked = MaskedResolverCard(true, "Masked", 5);
		var plain = PlainCreatureCard(true, "Plain", 2);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { masked.gameObject, plain.gameObject, CreateStartCard() });

		effect.GiveAttackToXFriendly();

		Assert.AreEqual(0, masked.attackGrowth, "Masked card stays out of the grant pool");
		Assert.AreEqual(5, masked.GetAttack(), "Masked settlement unchanged");
		Assert.AreEqual(2, plain.attackGrowth, "Plain card receives the grant");
	}

	// ---------- drain pool pre-exclusion ----------

	[Test]
	public void ConsumeRandomEnemyCardsAttack_PreExcludesMaskedCards()
	{
		var source = CreateCard(true, "Consumer");
		var effect = CreateEffect<ConsumeStatusEffect>(source);

		var masked = MaskedResolverCard(false, "MaskedEnemy", 5);
		var plain = PlainCreatureCard(false, "PlainEnemy", 2);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { plain.gameObject, masked.gameObject, CreateStartCard() });

		effect.ConsumeRandomEnemyCardsAttack(2);

		Assert.AreEqual(1, plain.GetAttack(), "Plain enemy drained by 1");
		Assert.AreEqual(5, masked.GetAttack(), "Masked enemy untouched");
		Assert.AreEqual(0, masked.attackGrowth, "Masked ledger still 0");
	}

	// ---------- GRAVE_ROBBER snapshot ledger clear ----------

	[Test]
	public void GraveRobberSnapshot_ClearsPreSnapshotLedger()
	{
		var carrier = PlainCreatureCard(true, "GraveRobber", 1);
		carrier.attackGrowth = 3;
		carrier.attackModThisRound = 2;
		var robber = CreateEffect<GraveRobberEffect>(carrier.gameObject);
		var reviver = CreateEffect<ReviveEffect>(carrier.gameObject);
		reviver.useTargetSelector = false;
		reviver.sortBy = ReviveEffect.ReviveSortBy.MaxAttack;
		robber.reviveEngine = reviver;

		var enemyGrave = PlainCreatureCard(false, "EnemyGrave", 7);
		CombatManager.combinedDeckZone.AddRange(new List<GameObject> { enemyGrave.gameObject, CreateStartCard() });

		robber.ReviveStrongestEnemyAndSnapshot();

		Assert.AreEqual(7, carrier.attackSnapshotValue, "Snapshot holds the revived attack");
		Assert.AreEqual(0, carrier.attackGrowth, "Pre-snapshot growth cleared");
		Assert.AreEqual(0, carrier.attackModThisRound, "This-round ledger cleared");
		Assert.AreEqual(7, carrier.GetAttack(), "Settlement reads the held value");
	}
}
