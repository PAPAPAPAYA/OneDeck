using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// EditMode tests for the delta-commit HP display (CombatInfoDisplayer) introduced
/// by VISUAL-FIX(2026-08-04): each attack hit commits its OWN actual HP loss, so
/// reactive chains that make animation playback order differ from logic order can
/// no longer swap damage numbers between hits (Corpse Explosion 2x2 + Eternal
/// Ghost 1 showed as 2/1/2 under the old absolute-value FIFO).
/// Plan: plans/plan-damage-floater-delta-commit-2026-08-04.md
/// </summary>
public class CombatInfoDisplayerDeltaCommitTests : HeadlessCombatTestFixture
{
	// The reported bug, replayed: parent's reveal hit (-2) lands first; then a
	// reactive bury resolves the ghost's -1 BETWEEN the parent's logic hits, but
	// the ghost's attack animation plays AFTER the parent's second hit. Commits
	// must still attribute 2 to the parent and 1 to the ghost.
	[Test]
	public void OutOfOrderCommits_AttributeCorrectLossPerHit()
	{
		EnemyStatus.hp = 25;
		var committed = new List<string>();
		InfoDisplayer.onHpDisplayCommitted += (isOwner, hpLoss, newDisplayed) =>
			committed.Add((isOwner ? "player" : "enemy") + ":" + hpLoss + "->" + newDisplayed);

		// Hit 1 (parent, on reveal): preHit 25 -> hp 23. Snapshot then immediate commit
		// (its animation played before the second click).
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 25);
		EnemyStatus.hp = 23;
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 2);
		Assert.AreEqual(23, InfoDisplayer.GetDisplayedEnemyHp(), "After hit 1 lands");

		// Chain 2 logic order: ghost reactive hit (23->22) snapshots BEFORE the
		// parent's second hit (22->20), but playback order is parent first.
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 23); // ghost, logic order first
		EnemyStatus.hp = 22;
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 22); // parent hit 2
		EnemyStatus.hp = 20;

		Assert.IsTrue(InfoDisplayer.HasPendingHpDisplay(false), "Display frozen while hits pending");
		Assert.AreEqual(23, InfoDisplayer.GetDisplayedEnemyHp(), "Frozen on preHit of the batch");

		InfoDisplayer.CommitHpDisplay(EnemyStatus, 2); // parent's animation lands first
		Assert.AreEqual(21, InfoDisplayer.GetDisplayedEnemyHp(), "Parent hit commits its own 2, not the ghost's 1");
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 1); // ghost's animation lands second
		Assert.AreEqual(20, InfoDisplayer.GetDisplayedEnemyHp(), "Ghost hit commits its own 1");

		Assert.IsFalse(InfoDisplayer.HasPendingHpDisplay(false), "All hits committed");
		Assert.AreEqual(EnemyStatus.hp, InfoDisplayer.GetDisplayedEnemyHp(), "Displayed settles on live HP");
		CollectionAssert.AreEqual(
			new[] { "enemy:2->23", "enemy:2->21", "enemy:1->20" },
			committed,
			"Per-hit losses stay attached to their own hit regardless of playback order");
	}

	// A hit fully absorbed by shield commits 0 loss: display holds, event still
	// fires (presenter suppresses zero floaters), pending drains normally.
	[Test]
	public void ZeroLossCommit_DisplayHoldsAndDrains()
	{
		EnemyStatus.hp = 30;
		int eventCount = 0;
		InfoDisplayer.onHpDisplayCommitted += (isOwner, hpLoss, newDisplayed) => eventCount++;

		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 30);
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 0);

		Assert.AreEqual(30, InfoDisplayer.GetDisplayedEnemyHp(), "Zero loss does not move the display");
		Assert.AreEqual(1, eventCount, "Commit event fires even for zero loss");
		Assert.IsFalse(InfoDisplayer.HasPendingHpDisplay(false), "Pending drained");
	}

	// Owner side is tracked independently from the enemy side.
	[Test]
	public void Sides_AreIndependent()
	{
		OwnerStatus.hp = 50;
		EnemyStatus.hp = 50;

		InfoDisplayer.SnapshotHpDisplay(OwnerStatus, 50);
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 50);
		EnemyStatus.hp = 46; // logic-phase damage resolves immediately
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 4);

		Assert.AreEqual(46, InfoDisplayer.GetDisplayedEnemyHp(), "Enemy commit applied");
		Assert.AreEqual(50, InfoDisplayer.GetDisplayedOwnerHp(), "Owner side untouched");
		Assert.IsTrue(InfoDisplayer.HasPendingHpDisplay(true), "Owner still frozen");
		Assert.IsFalse(InfoDisplayer.HasPendingHpDisplay(false), "Enemy drained");
	}

	// Cancel path: clearing locks mid-flight unfreezes the display back to live HP
	// and notifies consumers so they resync instead of showing a phantom diff.
	[Test]
	public void ClearHpDisplayLocks_UnfreezesAndNotifies()
	{
		EnemyStatus.hp = 40;
		int clearedCount = 0;
		InfoDisplayer.onHpDisplayLocksCleared += () => clearedCount++;

		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 40);
		EnemyStatus.hp = 35; // damage resolved in logic, animation cancelled before commit
		Assert.AreEqual(40, InfoDisplayer.GetDisplayedEnemyHp(), "Frozen before clear");

		InfoDisplayer.ClearHpDisplayLocks();

		Assert.AreEqual(1, clearedCount, "Clear notification fired");
		Assert.IsFalse(InfoDisplayer.HasPendingHpDisplay(false), "No pending after clear");
		Assert.AreEqual(35, InfoDisplayer.GetDisplayedEnemyHp(), "Falls back to live HP after clear");
	}

	// VISUAL-FIX(2026-08-04) regression: a nested reactive hit (Linger reacting to
	// onTheirPlayerTookDmg, e.g. ETERNAL_GHOST reacting to SMALL_SCALE_DEATH)
	// resolves INSIDE the outer hit's event raise. HPAlterEffect must snapshot
	// BEFORE raising damage events so the outer hit's preHitHp wins the batch
	// freeze — otherwise the display jumps at reveal time and every commit is off
	// until the pending count drains.
	[Test]
	public void NestedReactiveHit_OuterHitWinsTheFreeze()
	{
		EnemyStatus.hp = 25;

		var cardA = CreateCard(true, "Attacker");
		var hpaA = CreateEffect<HPAlterEffect>(cardA);
		hpaA.baseDmg = CreateScriptableObject<IntSO>();
		hpaA.baseDmg.value = 2;

		var cardG = CreateCard(true, "LingerGhost");
		var hpaG = CreateEffect<HPAlterEffect>(cardG);
		hpaG.baseDmg = CreateScriptableObject<IntSO>();
		hpaG.baseDmg.value = 1;

		// Ghost reacts to the enemy taking damage: nested 1-damage hit inside the
		// outer hit's CheckDmgTargets event raise. One-shot flag: the ghost's own hit
		// re-raises the same event (the real game guards this via the chain's
		// same-instance loop guard, which direct calls bypass).
		bool reacted = false;
		RegisterEventCallback(GameEventStorage.onTheirPlayerTookDmg, () =>
		{
			if (reacted) return;
			reacted = true;
			hpaG.DecreaseTheirHp();
		}, OwnerStatus);

		EffectChainManager.MakeANewEffectRecorder(cardA, hpaA.gameObject);
		hpaA.DecreaseTheirHp(); // outer hit 2 (25->23), nested ghost hit 1 (23->22)

		Assert.AreEqual(22, EnemyStatus.hp, "Outer 2 + nested 1 resolved in logic");
		Assert.AreEqual(25, InfoDisplayer.GetDisplayedEnemyHp(),
			"Display must freeze on the OUTER hit's preHit, not the nested hit's mid-burst value");

		// Second outer hit after the reaction (22->20), then simulate playback:
		// each commit must subtract its own loss from a display frozen at 25.
		hpaA.DecreaseTheirHp();
		Assert.AreEqual(20, EnemyStatus.hp, "Second outer hit resolved");
		Assert.AreEqual(25, InfoDisplayer.GetDisplayedEnemyHp(), "Still frozen until the first animation lands");

		InfoDisplayer.CommitHpDisplay(EnemyStatus, 2);
		Assert.AreEqual(23, InfoDisplayer.GetDisplayedEnemyHp());
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 2);
		Assert.AreEqual(21, InfoDisplayer.GetDisplayedEnemyHp());
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 1);
		Assert.AreEqual(20, InfoDisplayer.GetDisplayedEnemyHp(), "Settles on live HP, no fallback jump");

		EffectChainManager.Me.CloseOpenedChain();
	}
}
