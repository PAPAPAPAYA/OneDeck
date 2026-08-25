using DefaultNamespace;
using NUnit.Framework;
using UnityEngine.Events;

/// <summary>
/// EditMode tests for the 萦绕自动机 (Linger automaton) pattern: a [萦绕] card with a
/// GameEventListener on beforeRoundStart fires once per round start — the hook used by
/// 军号 / 威慑光环 / 暮鼓 / 永恒引擎 style per-round-start automaton cards.
/// </summary>
public class LingerAutomatonTests : HeadlessCombatTestFixture
{
	[Test]
	public void LingerCard_ReactingToBeforeRoundStart_FiresOncePerRoundStart()
	{
		var card = CreateCard(true, "LingerAutomaton");
		card.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.Linger);

		var listener = card.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.beforeRoundStart;
		listener.response = new UnityEvent();
		int fires = 0;
		listener.response.AddListener(() => fires++);
		GameEventStorage.beforeRoundStart.RegisterListener(listener);

		GameEventStorage.beforeRoundStart.Raise();
		GameEventStorage.beforeRoundStart.Raise();

		Assert.AreEqual(2, fires, "Per-round-start automaton fires once per round start");
	}

	[Test]
	public void LingerAutomaton_OnlyReceivesFactionOwnRoundStartEvents_WhenFiltered()
	{
		// Round-start timepoint uses plain Raise(); faction filtering, when needed, is the
		// listener's own concern (e.g. 威慑光环 随机敌方[攻击者] 降攻 checks faction targets).
		var enemyCard = CreateCard(false, "EnemyLingerAutomaton");
		enemyCard.GetComponent<CardScript>().myTags.Add(EnumStorage.Tag.Linger);

		var listener = enemyCard.AddComponent<GameEventListener>();
		listener.@event = GameEventStorage.beforeRoundStart;
		listener.response = new UnityEvent();
		int fires = 0;
		listener.response.AddListener(() => fires++);
		GameEventStorage.beforeRoundStart.RegisterListener(listener);

		GameEventStorage.beforeRoundStart.Raise();

		Assert.AreEqual(1, fires, "Enemy-side automaton also hears the global round-start timepoint");
	}
}
