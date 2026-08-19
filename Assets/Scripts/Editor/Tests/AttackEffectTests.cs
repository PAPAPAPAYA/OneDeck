using DefaultNamespace;
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

		var recorder = EffectChainManager.currentEffectRecorder.GetComponent<EffectRecorder>();
		Assert.AreEqual(1, recorder.animationRequests.Count, "1 segment -> 1 request");
		Assert.AreEqual(AnimationRequestType.Attack, recorder.animationRequests[0].type, "Should be Attack type");
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
	public void Attack_RaisesOnAnyCardAttackedOncePerAction()
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

		Assert.AreEqual(1, attackCount, "One attack action, even with 2 segments");
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
}
