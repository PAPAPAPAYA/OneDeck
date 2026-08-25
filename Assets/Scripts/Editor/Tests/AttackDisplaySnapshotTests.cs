using DefaultNamespace;
using NUnit.Framework;

/// <summary>
/// EditMode tests for the attack display snapshot / incremental commit (phase-3 review #2):
/// the attack print must stay frozen at the pre-logic value during the logic phase and
/// step through deltas as AttackChange requests play, never jumping mid-logic.
/// </summary>
public class AttackDisplaySnapshotTests : HeadlessCombatTestFixture
{
	[Test]
	public void GetAttackForDisplay_ReturnsLiveAttackWithoutSnapshot()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;

		Assert.AreEqual(3, cs.GetAttackForDisplay(), "No snapshot -> live GetAttack()");
	}

	[Test]
	public void SnapshotDisplayState_FreezesAttackAtPreChangeValue()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;

		cs.SnapshotDisplayState();
		cs.ModifyAttack(2); // logic-phase mutation must not show on the face

		Assert.AreEqual(3, cs.GetAttackForDisplay(), "Frozen at the snapshot value");
		Assert.AreEqual(5, cs.GetAttack(), "Settlement still reads live GetAttack()");
	}

	[Test]
	public void CommitAttackDisplayDelta_StepsThroughFrozenBaseline()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;

		// Baseline = pre-chain attack (current 5 minus pending deltas 2).
		cs.ModifyAttack(2);
		cs.SetDisplayBaseline(new System.Collections.Generic.List<EnumStorage.StatusEffect>(), 3);

		cs.CommitAttackDisplayDelta(1); // first gain lands
		Assert.AreEqual(4, cs.GetAttackForDisplay(), "Baseline + first delta");

		cs.CommitAttackDisplayDelta(1); // reaction gain lands
		Assert.AreEqual(5, cs.GetAttackForDisplay(), "Baseline + both deltas");
	}

	[Test]
	public void CommitDisplayState_RestoresLiveAttack()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;

		cs.SnapshotDisplayState();
		cs.ModifyAttack(2);

		cs.CommitDisplayState();

		Assert.AreEqual(5, cs.GetAttackForDisplay(), "After commit the face falls back to live GetAttack()");
	}

	[Test]
	public void SetDisplayBaselineWithoutAttackBaseline_PreservesExistingAttackSnapshot()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;

		// Consume/transfer path: SnapshotDisplayState freezes attack, then the status
		// baseline runs without an attack baseline — the freeze must survive.
		cs.SnapshotDisplayState();
		cs.ModifyAttack(-1);

		cs.SetDisplayBaseline(new System.Collections.Generic.List<EnumStorage.StatusEffect>());

		Assert.AreEqual(3, cs.GetAttackForDisplay(), "Attack snapshot survives a status-only baseline");
	}

	[Test]
	public void SetDisplayBaselineWithAttackBaseline_OverridesSnapshot()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.ModifyAttack(2); // now 5

		// AttackChange path: the baseline computation freezes the pre-chain value.
		cs.SetDisplayBaseline(new System.Collections.Generic.List<EnumStorage.StatusEffect>(), 5);

		Assert.AreEqual(5, cs.GetAttackForDisplay(), "Attack baseline wins over any prior snapshot");
	}

	[Test]
	public void CommitAttackDisplayDelta_WorksWithoutSnapshot()
	{
		var card = CreateCard(true, "Card");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 4;

		// No snapshot: commit falls back to live value + delta (idempotent enough for
		// cards whose display never froze, e.g. direct mutations outside a chain).
		cs.CommitAttackDisplayDelta(-1);

		Assert.AreEqual(3, cs.GetAttackForDisplay(), "Live GetAttack() plus delta when no snapshot exists");
	}
}
