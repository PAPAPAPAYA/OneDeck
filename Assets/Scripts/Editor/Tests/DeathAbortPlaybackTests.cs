using System.Collections;
using System.Collections.Generic;
using DefaultNamespace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Regression tests for VISUAL-FIX(2026-09-14): death-abort animation playback.
// The playback checkpoints cut on DISPLAYED HP (the CombatInfoDisplayer queue-frozen
// value), so these tests drive SnapshotHpDisplay/CommitHpDisplay to simulate the
// logic-time freeze and the lethal hit's landing (Attack onHit -> CommitHpDisplay,
// played synchronously by NullCombatVisuals in headless mode).
//
// Driving pattern: PlayRecorderCoroutine is driven DIRECTLY from the UnityTest
// (the proven-reliable EditMode pattern used by RecorderAnimationPlayerTests).
// Driving the PlayRecordersCoroutine wrapper nests MonoBehaviour StartCoroutine
// calls, which the EditMode runner does not deterministically await (see the
// ignored nested-coroutine test in RecorderAnimationPlayerTests). The wrapper's
// root-loop checkpoint is instead covered by stepping the wrapper enumerator ONE
// MoveNext: the abort fires before the first yield, so the enumerator completes
// synchronously and every assertion is deterministic.
public class DeathAbortPlaybackTests : HeadlessCombatTestFixture
{
	private int _commitCount;
	private int _locksCleared;

	private AnimationRequest MakeEnemyAttackCommit(GameObject attackerCard, int hpLoss)
	{
		var target = EnemyStatus;
		return new AnimationRequest
		{
			type = AnimationRequestType.Attack,
			attackerCard = attackerCard,
			isAttackingEnemy = true,
			onHit = () => CombatInfoDisplayer.me?.CommitHpDisplay(target, hpLoss)
		};
	}

	private RecorderAnimationPlayer CreatePlayer()
	{
		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;
		return player;
	}

	private void SubscribeCounters()
	{
		_commitCount = 0;
		_locksCleared = 0;
		InfoDisplayer.onHpDisplayCommitted += (isOwner, hpLoss, displayed) => _commitCount++;
		InfoDisplayer.onHpDisplayLocksCleared += () => _locksCleared++;
	}

	/// <summary>
	/// Step the PlayRecordersCoroutine wrapper once: the root-loop death-abort
	/// checkpoint fires before the first yield, so a cut batch completes on this
	/// single MoveNext (no coroutine scheduling involved).
	/// </summary>
	private static bool RunBatchToFirstYieldOrCompletion(RecorderAnimationPlayer player, List<GameObject> roots)
	{
		IEnumerator batch = player.PlayRecordersCoroutine(roots);
		return !batch.MoveNext();
	}

	[Test]
	public void IsDeathVisuallyLanded_FollowsDisplayedHpNotLogicHp()
	{
		// Logic HP is already zeroed before playback starts (the predicate must NOT
		// fire on it); the display is still frozen at 4 behind two pending locks.
		EnemyStatus.hp = 0;
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 4);
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 4);
		Assert.IsFalse(CombatManager.IsDeathVisuallyLanded,
			"Logic HP 0 with displayed HP still 4 must NOT count as visually landed");

		// The lethal hit's animation lands (its onHit commit) -> displayed HP hits 0.
		InfoDisplayer.CommitHpDisplay(EnemyStatus, 4);
		Assert.IsTrue(CombatManager.IsDeathVisuallyLanded,
			"Displayed HP 0 after the lethal commit must count as visually landed");
	}

	[UnityTest]
	public IEnumerator Playback_CutsRemainingRequestsAfterLethalCommitLands()
	{
		var attacker = CreateCard(true, "Attacker");

		// Logic phase already resolved: two 2-dmg hits killed the 4-hp enemy (live HP 0),
		// display frozen at 4 behind two pending locks (HPAlterEffect logic-time behavior).
		EnemyStatus.hp = 0;
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 4);
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 4);

		// Hits 1+2 are the lethal segment; hit 3 and recorder B are queued animations
		// whose onHit would be a ghost commit if they ever played.
		var recorderA = CreateGameObject("RecorderA").AddComponent<EffectRecorder>();
		recorderA.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));
		recorderA.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));
		recorderA.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));

		var recorderB = CreateGameObject("RecorderB").AddComponent<EffectRecorder>();
		recorderB.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));

		SubscribeCounters();
		var player = CreatePlayer();

		int attacksBefore = NullVisuals.playAttackAnimCalls;

		// Lethal segment (hits 1+2) plays to landing; hit 3 is cut at the request-loop
		// checkpoint (the first firing, which also clears the pending display locks).
		yield return player.PlayRecorderCoroutine(recorderA);

		Assert.AreEqual(attacksBefore + 2, NullVisuals.playAttackAnimCalls,
			"Only the lethal segment may play; the rest of the queue is cut after it lands");
		Assert.IsTrue(recorderA.animationPlayed,
			"Recorder A entered playback before the cut (flag is set at recorder entry)");
		Assert.AreEqual(2, _commitCount,
			"Exactly the two played hits may commit - no ghost commits for cut animations");
		Assert.AreEqual(1, _locksCleared,
			"Death abort must clear pending display locks exactly once per firing");
		Assert.AreEqual(0, InfoDisplayer.GetDisplayedEnemyHp(),
			"Displayed HP is resynced to the live value (0) after the abort");

		// Recorder B driven after the abort: its start checkpoint cuts it before entry
		// (the already-handled flag suppresses a second lock clear, not the cut itself).
		yield return player.PlayRecorderCoroutine(recorderB);

		Assert.IsFalse(recorderB.animationPlayed,
			"Recorder B is cut at its start checkpoint; production marks it in PlayRecorderAnimationsAndWait's finally");
		Assert.AreEqual(attacksBefore + 2, NullVisuals.playAttackAnimCalls,
			"Cut recorder B must not play its attack");
		Assert.AreEqual(2, _commitCount,
			"Cut recorder B must not add commits (no ghost commits)");
		Assert.AreEqual(1, _locksCleared,
			"The already-handled flag must suppress a second lock clear within the same batch");
	}

	[UnityTest]
	public IEnumerator Playback_NonLethalBatchPlaysEveryRequest()
	{
		var attacker = CreateCard(true, "Attacker");

		// Two hits land on the frozen display (10 -> 8 -> 6); displayed HP stays above
		// zero, so no checkpoint may cut anything and no lock clear may fire.
		EnemyStatus.hp = 6;
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 10);
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 10);

		var recorderA = CreateGameObject("RecorderA").AddComponent<EffectRecorder>();
		recorderA.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));
		var recorderB = CreateGameObject("RecorderB").AddComponent<EffectRecorder>();
		recorderB.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));

		SubscribeCounters();
		var player = CreatePlayer();

		int attacksBefore = NullVisuals.playAttackAnimCalls;

		yield return player.PlayRecorderCoroutine(recorderA);
		yield return player.PlayRecorderCoroutine(recorderB);

		Assert.AreEqual(attacksBefore + 2, NullVisuals.playAttackAnimCalls,
			"Non-lethal batch must play every queued animation");
		Assert.IsTrue(recorderA.animationPlayed, "Recorder A plays through");
		Assert.IsTrue(recorderB.animationPlayed, "Recorder B plays through");
		Assert.AreEqual(2, _commitCount, "Both played hits commit");
		Assert.AreEqual(0, _locksCleared, "No display-lock clear may fire without a death abort");
		Assert.AreEqual(6, InfoDisplayer.GetDisplayedEnemyHp(),
			"Displayed HP stays queue-committed above zero");
	}

	[UnityTest]
	public IEnumerator Playback_HeadlessFallsBackToLogicHp()
	{
		// The fixture always injects CombatInfoDisplayer.me; null it to exercise the
		// no-display-layer branch of IsDeathVisuallyLanded (logic HP predicate).
		var savedDisplayer = CombatInfoDisplayer.me;
		CombatInfoDisplayer.me = null;
		try
		{
			var attacker = CreateCard(true, "Attacker");
			var player = CreatePlayer();

			// Dead enemy (logic HP 0): the root-loop checkpoint cuts the batch before
			// the first yield, so the wrapper completes on the very first MoveNext.
			EnemyStatus.hp = 0;
			var deadRecorder = CreateGameObject("DeadRecorder").AddComponent<EffectRecorder>();
			deadRecorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.Attack,
				attackerCard = attacker,
				isAttackingEnemy = true
			});

			int attacksBefore = NullVisuals.playAttackAnimCalls;
			bool completedWithoutYield = RunBatchToFirstYieldOrCompletion(
				player, new List<GameObject> { deadRecorder.gameObject });

			Assert.IsTrue(completedWithoutYield,
				"Logic-HP death (no display layer) must abort at the root checkpoint, before the first yield");
			Assert.IsFalse(deadRecorder.animationPlayed,
				"The dead batch's recorder never enters playback");
			Assert.AreEqual(attacksBefore, NullVisuals.playAttackAnimCalls,
				"Logic-HP death (no display layer) must abort before any request plays");

			// Alive enemy: the same setup plays through normally.
			EnemyStatus.hp = 5;
			var aliveRecorder = CreateGameObject("AliveRecorder").AddComponent<EffectRecorder>();
			aliveRecorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.Attack,
				attackerCard = attacker,
				isAttackingEnemy = true
			});

			yield return player.PlayRecorderCoroutine(aliveRecorder);

			Assert.AreEqual(attacksBefore + 1, NullVisuals.playAttackAnimCalls,
				"Alive enemy (no display layer) must play through normally");
			Assert.IsTrue(aliveRecorder.animationPlayed, "Alive batch runs to completion");
		}
		finally
		{
			CombatInfoDisplayer.me = savedDisplayer;
		}
	}

	[UnityTest]
	public IEnumerator Playback_DeathAbortRearmsForEachBatch()
	{
		var attacker = CreateCard(true, "Attacker");

		// Batch 1: the only snapshot's commit IS the lethal hit; the second request is
		// cut at the request-loop checkpoint.
		EnemyStatus.hp = 0;
		InfoDisplayer.SnapshotHpDisplay(EnemyStatus, 2);

		var recorderA = CreateGameObject("RecorderA").AddComponent<EffectRecorder>();
		recorderA.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));
		recorderA.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));

		SubscribeCounters();
		var player = CreatePlayer();

		int attacksBefore = NullVisuals.playAttackAnimCalls;

		yield return player.PlayRecorderCoroutine(recorderA);

		Assert.AreEqual(attacksBefore + 1, NullVisuals.playAttackAnimCalls,
			"Batch 1: cut right after the lethal hit lands");
		Assert.AreEqual(1, _locksCleared, "Batch 1: abort cleared the locks once");

		// Batch 2 (same player, death state unchanged): the per-batch flag resets on
		// wrapper entry, so the root-loop checkpoint re-arms and cuts before the first yield.
		var recorderB = CreateGameObject("RecorderB").AddComponent<EffectRecorder>();
		recorderB.animationRequests.Add(MakeEnemyAttackCommit(attacker, 2));

		bool completedWithoutYield = RunBatchToFirstYieldOrCompletion(
			player, new List<GameObject> { recorderB.gameObject });

		Assert.IsTrue(completedWithoutYield,
			"Batch 2: root checkpoint cuts the batch before the first yield");
		Assert.IsFalse(recorderB.animationPlayed, "Batch 2 recorder is cut at the root checkpoint");
		Assert.AreEqual(attacksBefore + 1, NullVisuals.playAttackAnimCalls,
			"Batch 2: nothing plays, the batch aborts immediately");
		Assert.AreEqual(2, _locksCleared, "Abort re-arms per batch: the lock clear fires again");
		Assert.AreEqual(1, _commitCount, "Batch 2 must not add commits (no ghost commits)");
	}
}
