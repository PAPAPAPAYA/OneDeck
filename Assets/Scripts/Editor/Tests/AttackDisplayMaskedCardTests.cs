using DefaultNamespace;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// EditMode tests for the masked-card display guard (2026-11-26 display/settlement
/// consistency fix): cards whose GetAttack() resolves through a branch that ignores
/// attackGrowth / attackModThisRound (dynamic resolver, snapshot hold, curse override)
/// must never show a buffed value on the face — neither via AttackChange commits nor
/// via the back-derived playback baseline. Plain-card stepping behavior is covered by
/// AttackDisplaySnapshotTests; this suite locks the masked side.
/// </summary>
public class AttackDisplayMaskedCardTests : HeadlessCombatTestFixture
{
	[Test]
	public void ResolverCard_CommitAttackDisplayDelta_IsNoOp()
	{
		var card = CreateCard(true, "MaskedCard");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.SetAttackResolver(() => 5);
		// Direct field write: ModifyAttack is write-gated on masked cards now, so the growth
		// residue must be planted by hand for this display-layer test.
		cs.attackGrowth = 2;

		Assert.AreEqual(5, cs.GetAttack(), "Settlement ignores the buff on resolver cards");
		cs.CommitAttackDisplayDelta(2);
		Assert.AreEqual(5, cs.GetAttackForDisplay(), "Face must not step to the phantom buff");
	}

	[Test]
	public void ResolverCard_BackDerivedBaseline_IsIgnored()
	{
		var card = CreateCard(true, "MaskedCard");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.SetAttackResolver(() => 5);
		cs.attackGrowth = 2; // direct write (ModifyAttack is gated on masked cards)

		// RecorderAnimationPlayer back-derives the playback baseline as
		// GetAttack() - pendingDelta; the face must bypass it for masked cards.
		cs.SetDisplayBaseline(new List<EnumStorage.StatusEffect>(), cs.GetAttack() - 2, null);
		Assert.AreEqual(5, cs.GetAttackForDisplay(), "Face reads live settlement, not the back-derived baseline");
	}

	[Test]
	public void ResolverCard_NegativeDelta_IsNoOp()
	{
		// Attack drains (ConsumeOwnAttack-style negative commits) must not drop the face
		// either: a masked card never lost the attack in settlement.
		var card = CreateCard(true, "MaskedCard");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.SetAttackResolver(() => 5);

		cs.CommitAttackDisplayDelta(-1);
		Assert.AreEqual(5, cs.GetAttackForDisplay(), "Negative commit must not drop the face");
	}

	[Test]
	public void SnapshotCard_CommitAndBaseline_ShowHeldValue()
	{
		var card = CreateCard(true, "GraveRobberStyle");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 4;
		cs.attackSnapshotValue = 6; // GRAVE_ROBBER-style hold: settlement returns 6 permanently
		cs.attackGrowth = 1;        // residue write (ModifyAttack is gated on masked cards)

		Assert.AreEqual(6, cs.GetAttack(), "Settlement reads the held value");
		cs.CommitAttackDisplayDelta(1);
		Assert.AreEqual(6, cs.GetAttackForDisplay(), "Commit must not move the face");
		cs.SetDisplayBaseline(new List<EnumStorage.StatusEffect>(), cs.GetAttack() - 1, null);
		Assert.AreEqual(6, cs.GetAttackForDisplay(), "Back-derived baseline must not move the face");
	}

	[Test]
	public void CurseOverrideCard_CommitAndBaseline_ShowOverrideValue()
	{
		// RELIC_GRAVE_CURSE override: enemy-side curse card + owner-side flag armed.
		GameEventStorage.curseCardTypeID.value = "TEST_CURSE";
		ValueTrackerManager.curseAttackOverrideOwnerThisRoundRef.value = 1;

		// Owner grave population below the start card = 1.
		CombatManager.combinedDeckZone.Add(CreateCard(true, "FriendlyGrave"));
		CombatManager.combinedDeckZone.Add(CreateStartCard());

		var curse = CreateCard(false, "Curse", "TEST_CURSE");
		var cs = curse.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.attackGrowth = 2; // residue write (ModifyAttack is gated while the override is armed)

		Assert.AreEqual(1, cs.GetAttack(), "Override replaces attack with the owner grave count");
		cs.CommitAttackDisplayDelta(2);
		Assert.AreEqual(cs.GetAttack(), cs.GetAttackForDisplay(), "Commit must not move the face");
		cs.SetDisplayBaseline(new List<EnumStorage.StatusEffect>(), cs.GetAttack() - 2, null);
		Assert.AreEqual(cs.GetAttack(), cs.GetAttackForDisplay(), "Back-derived baseline must not move the face");
	}

	[UnityTest]
	public IEnumerator PlayRecordersCoroutine_ResolverCard_FaceStaysAtSettlementDuringPlayback()
	{
		var target = CreateCard(true, "MaskedTarget");
		var cs = target.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.SetAttackResolver(() => 5);
		cs.attackGrowth = 2; // phantom buff (direct write; ModifyAttack is gated on masked cards)

		var recorderGo = CreateGameObject("Root");
		var recorder = recorderGo.AddComponent<EffectRecorder>();
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.AttackChange,
			targetCard = target,
			statusEffectAmount = 1
		});

		var playerGo = CreateGameObject("Player");
		var player = playerGo.AddComponent<RecorderAnimationPlayer>();
		RecorderAnimationPlayer.me = player;

		// First MoveNext runs ComputeAndApplyDisplayBaselines synchronously: without the
		// read-port guard the face would sit at GetAttack() - pendingDelta (4) here.
		var e = player.PlayRecordersCoroutine(new List<GameObject> { recorderGo });
		Assert.IsTrue(e.MoveNext(), "Batch should produce its first playback step");

		Assert.AreEqual(5, cs.GetAttack(), "Settlement stays unbuffed on resolver cards");
		Assert.AreEqual(cs.GetAttack(), cs.GetAttackForDisplay(), "Face equals settlement from the first playback step");

		// Drain the batch one editor frame per outer step so the nested playback coroutines
		// advance and the finally block commits display state.
		while (e.MoveNext())
		{
			yield return null;
		}
		Assert.AreEqual(cs.GetAttack(), cs.GetAttackForDisplay(), "Face stays at settlement after playback");
	}

	[Test]
	public void ResolverCard_AttackTimesDisplay_StillSteps()
	{
		// The guard is value-only: attack-segment grants still animate through the existing
		// times machinery (settlement absorbs times buffs for resolver cards too).
		var card = CreateCard(true, "MaskedCard");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.SetAttackResolver(() => 5);

		Assert.AreEqual(1, cs.GetAttackTimesForDisplay(), "Live times without a baseline");
		cs.SetDisplayBaseline(new List<EnumStorage.StatusEffect>(), null, 1);
		cs.CommitAttackTimesDisplayDelta(1);
		Assert.AreEqual(2, cs.GetAttackTimesForDisplay(), "Times stepping is NOT masked");
		cs.CommitDisplayState();
		Assert.AreEqual(cs.GetAttackTimes(), cs.GetAttackTimesForDisplay(), "After commit the badge falls back to live times");
	}
}
