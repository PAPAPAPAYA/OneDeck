using System.Reflection;
using DefaultNamespace;
using DefaultNamespace.SOScripts;
using NUnit.Framework;

/// <summary>
/// EditMode tests for the attack-attribute engine (AttackEffect + CardScript attack fields).
/// </summary>
public class AttackEffectTests : HeadlessCombatTestFixture
{
	[Test]
	public void Attack_DealsAttackPerSegment()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.extraAttackTimes = 1; // 2 segments total
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(94, EnemyStatus.hp, "2 segments x 3 attack = 6 damage");
	}

	[Test]
	public void Attack_CapturesOneAnimationRequestPerSegment()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();

		// The Attack capture lives in the per-segment recorder (scope-closed by
		// EndAttackSegmentScope), not in the attacker's own invocation recorder.
		Assert.AreEqual(1, EffectChainManager.closedEffectRecorders.Count, "1 segment -> 1 scope-closed segment recorder");
		var segmentRecorder = EffectChainManager.closedEffectRecorders[0].GetComponent<EffectRecorder>();
		Assert.AreEqual(1, segmentRecorder.animationRequests.Count, "1 segment -> 1 request");
		Assert.AreEqual(AnimationRequestType.Attack, segmentRecorder.animationRequests[0].type, "Should be Attack type");
		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void AttackTimes_DealsExplicitSegmentCount()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 4;
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.AttackTimes(3);
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(88, EnemyStatus.hp, "3 segments x 4 attack = 12 damage");
	}

	[Test]
	public void Attack_ZeroAttackDealsNoDamageAndNoAnimation()
	{
		var card = CreateCard(true, "Attacker");
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(0, recorder.animationRequests.Count, "0 attack resolves nothing");
		Assert.AreEqual(100, EnemyStatus.hp, "No damage with 0 attack");
		EffectChainManager.Me.CloseOpenedChain();
	}

	[Test]
	public void Attack_AppliesPermanentGrowth()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		cs.ModifyAttack(1);
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(97, EnemyStatus.hp, "2 base + 1 growth = 3 damage");
	}

	[Test]
	public void Attack_AppliesThisRoundModifierUntilReset()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.ModifyAttackThisRound(-1);
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(98, EnemyStatus.hp, "3 - 1 this round = 2 damage");

		cs.ResetRoundAttackModifiers();
		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(95, EnemyStatus.hp, "After round reset: 3 damage again");
	}

	[Test]
	public void Attack_ResolvesDynamicAttackLive()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 1;
		cs.ModifyAttack(5);
		int dynamicValue = 7;
		cs.SetAttackResolver(() => dynamicValue);
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(93, EnemyStatus.hp, "Resolver wins over base + growth");

		dynamicValue = 10;
		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();
		Assert.AreEqual(83, EnemyStatus.hp, "Resolver re-read at settlement time");
	}

	[Test]
	public void Attack_RaisesOnAnyCardAttackedOncePerSegment()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		cs.extraAttackTimes = 1; // 2 segments
		var atk = CreateEffect<AttackEffect>(card);

		int attackCount = 0;
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => attackCount++);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, attackCount, "Per-segment redesign 2026-09-05: 2 segments -> 2 attack events");
	}

	[Test]
	public void Attack_ZeroAttackDoesNotRaiseAttackEvent()
	{
		var card = CreateCard(true, "Attacker");
		var atk = CreateEffect<AttackEffect>(card);

		int attackCount = 0;
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => attackCount++);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, attackCount, "No attack action when there is nothing to resolve");
	}

	[Test]
	public void AttackSelf_DealsAttackToSelf()
	{
		var card = CreateCard(true, "SelfAttacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		cs.extraAttackTimes = 1; // 2 segments
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.AttackSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(94, OwnerStatus.hp, "2 segments x 3 attack = 6 self damage");
	}

	[Test]
	public void AttackSelf_ZeroAttackDoesNothing()
	{
		var card = CreateCard(true, "SelfAttacker");
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.AttackSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(100, OwnerStatus.hp, "No self damage with 0 attack");
	}

	[Test]
	public void Attack_RaisesOnAnyFriendlyCardAttackedOncePerSegment()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		cs.extraAttackTimes = 1; // 2 segments
		var atk = CreateEffect<AttackEffect>(card);

		int attackCount = 0;
		RegisterEventCallback(GameEventStorage.onAnyFriendlyCardAttacked, () => attackCount++);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, attackCount, "Per-segment redesign 2026-09-05: 2 segments -> 2 non-self attack events");
	}

	[Test]
	public void Attack_FiresSameReactorOncePerSegment()
	{
		// RELIC_HIVE-style regression: a card reacting to the attack event through a REAL
		// CostNEffectContainer invocation must fire once PER SEGMENT — EndAttackSegmentScope
		// scope-closes its recorder so the chain pair guard does not block segment 2+.
		var attacker = CreateCard(true, "Attacker");
		var cs = attacker.GetComponent<CardScript>();
		cs.printedAttack = 2;
		cs.extraAttackTimes = 1; // 2 segments
		var atk = CreateEffect<AttackEffect>(attacker);

		var reactor = CreateCard(true, "Reactor");
		// CreateCostContainer initializes the UnityEvents (null at runtime after AddComponent)
		// and injects _myCardScript — the raw AddComponent path would NRE on effectEvent.
		var container = CreateCostContainer(reactor);
		var listener = reactor.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.onAnyFriendlyCardAttacked;
		listener.response.AddListener(container.InvokeEffectEventVoid);
		GameEventStorage.onAnyFriendlyCardAttacked.RegisterListener(listener);
		int reactionCount = 0;
		container.effectEvent.AddListener(() => reactionCount++);

		EffectChainManager.MakeANewEffectRecorder(attacker, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(2, reactionCount, "2 segments -> the same reactor container fires once per segment");
	}

	[Test]
	public void AttackSelf_RaisesOnAnyCardAttackedButNotFriendlyAttackEvent()
	{
		var card = CreateCard(true, "SelfAttacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		var atk = CreateEffect<AttackEffect>(card);

		int anyCount = 0;
		int friendlyCount = 0;
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => anyCount++);
		RegisterEventCallback(GameEventStorage.onAnyFriendlyCardAttacked, () => friendlyCount++);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.AttackSelf();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, anyCount, "Self-attack counts as an attack action for onAnyCardAttacked (1 segment -> 1 event)");
		Assert.AreEqual(0, friendlyCount, "Self-attack never raises onAnyFriendlyCardAttacked (战旗 must not trigger on self-damage)");
	}

	[Test]
	public void Attack_DeliversFactionFilteredByAttacker()
	{
		var friendlyAttacker = CreateCard(true, "FriendlyAttacker");
		friendlyAttacker.GetComponent<CardScript>().printedAttack = 2;
		var atkFriendly = CreateEffect<AttackEffect>(friendlyAttacker);

		var enemyAttacker = CreateCard(false, "EnemyAttacker");
		enemyAttacker.GetComponent<CardScript>().printedAttack = 2;
		var atkEnemy = CreateEffect<AttackEffect>(enemyAttacker);

		int ownerSideHears = 0;
		int enemySideHears = 0;
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => ownerSideHears++);
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => enemySideHears++, EnemyStatus);

		EffectChainManager.MakeANewEffectRecorder(enemyAttacker, atkEnemy.gameObject);
		atkEnemy.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(0, ownerSideHears, "Enemy attack must not reach owner-side listeners");
		Assert.AreEqual(1, enemySideHears, "Enemy attack reaches enemy-side listeners");

		EffectChainManager.MakeANewEffectRecorder(friendlyAttacker, atkFriendly.gameObject);
		atkFriendly.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, ownerSideHears, "Friendly attack reaches owner-side listeners");
		Assert.AreEqual(1, enemySideHears, "Friendly attack must not reach enemy-side listeners");
	}

	[Test]
	public void Attack_SetsLastCardAttackedContext()
	{
		var card = CreateCard(true, "Attacker");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		var atk = CreateEffect<AttackEffect>(card);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.Attack();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(cs, CombatManager.lastCardAttacked, "lastCardAttacked tracks the attacking card");
	}

	[Test]
	public void AttackTimesBasedOnOpponentBuriedCount_SegmentsAndRaisesAttackEvent()
	{
		var card = CreateCard(true, "BoneCombo");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 2;
		var atk = CreateEffect<AttackEffect>(card);

		ValueTrackerManager.enemyCardsBuriedCountRef.value = 3;

		int attackCount = 0;
		RegisterEventCallback(GameEventStorage.onAnyCardAttacked, () => attackCount++);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.AttackTimesBasedOnOpponentBuriedCount();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(94, EnemyStatus.hp, "3 segments x 2 attack = 6 damage");
		Assert.AreEqual(3, attackCount, "Dynamic segment count still fires the attack event once per segment");
	}

	[Test]
	public void AttackTimesBasedOnIntSO_SegmentsFromIntSO()
	{
		var card = CreateCard(true, "BodyCanon");
		var cs = card.GetComponent<CardScript>();
		cs.printedAttack = 3;
		var atk = CreateEffect<AttackEffect>(card);

		var intSO = CreateScriptableObject<IntSO>();
		intSO.value = 2;
		var ownerField = typeof(HPAlterEffect).GetField("ownerIntSO", BindingFlags.Public | BindingFlags.Instance);
		Assert.IsNotNull(ownerField, "ownerIntSO field must exist on HPAlterEffect");
		ownerField.SetValue(atk, intSO);

		EffectChainManager.MakeANewEffectRecorder(card, atk.gameObject);
		atk.AttackTimesBasedOnIntSO();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(94, EnemyStatus.hp, "2 segments x 3 attack = 6 damage");
	}
}
