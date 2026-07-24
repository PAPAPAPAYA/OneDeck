using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for CombatPerCardStatsTracker: aggregation, faction split,
/// neutral guard, power stack semantics, sorting, and session reset.
/// </summary>
public class CombatPerCardStatsTrackerTests : HeadlessCombatTestFixture
{
	private CombatPerCardStatsTracker _tracker;

	[SetUp]
	public override void SetUp()
	{
		base.SetUp();
		var obj = CreateGameObject("TestCombatPerCardStatsTracker");
		_tracker = obj.AddComponent<CombatPerCardStatsTracker>();
		CombatPerCardStatsTracker.Me = _tracker; // Awake does not fire in Edit Mode
	}

	[TearDown]
	public override void TearDown()
	{
		CombatPerCardStatsTracker.Me = null;
		base.TearDown();
	}

	[Test]
	public void RecordDamageToOpponent_AggregatesCopiesOfSameCardType()
	{
		var copyA = CreateCard(true, "FireImp A", "fire_imp").GetComponent<CardScript>();
		var copyB = CreateCard(true, "FireImp B", "fire_imp").GetComponent<CardScript>();

		_tracker.RecordDamageToOpponent(copyA, 3f);
		_tracker.RecordDamageToOpponent(copyB, 4f);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count, "Two copies of the same cardTypeID on the same faction should aggregate into one row");
		Assert.AreEqual(7f, rows[0].GetValue(CombatStatType.DamageDealtToOpponent));
	}

	[Test]
	public void SameCardType_OnBothFactions_ProducesTwoRows()
	{
		var playerCopy = CreateCard(true, "FireImp (Player)", "fire_imp").GetComponent<CardScript>();
		var enemyCopy = CreateCard(false, "FireImp (Enemy)", "fire_imp").GetComponent<CardScript>();

		_tracker.RecordDamageToOpponent(playerCopy, 5f);
		_tracker.RecordDamageToOpponent(enemyCopy, 2f);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(2, rows.Count, "Same cardTypeID on both factions must produce two separate rows");

		var playerRow = rows.Find(r => r.faction == CardFaction.Player);
		var enemyRow = rows.Find(r => r.faction == CardFaction.Enemy);
		Assert.IsNotNull(playerRow);
		Assert.IsNotNull(enemyRow);
		Assert.AreEqual(5f, playerRow.GetValue(CombatStatType.DamageDealtToOpponent));
		Assert.AreEqual(2f, enemyRow.GetValue(CombatStatType.DamageDealtToOpponent));
	}

	[Test]
	public void NeutralCard_IsExcluded()
	{
		var startCard = CreateStartCard().GetComponent<CardScript>();

		_tracker.RecordTrigger(startCard);
		_tracker.RecordDamageToOpponent(startCard, 10f);
		_tracker.RecordPowerGiven(startCard, 2);

		Assert.AreEqual(0, _tracker.GetSessionRows().Count, "Neutral/start cards must never produce rows");
	}

	[Test]
	public void RecordTrigger_CountsEveryInvocation()
	{
		var card = CreateCard(true, "Ticker", "ticker").GetComponent<CardScript>();

		_tracker.RecordTrigger(card);
		_tracker.RecordTrigger(card);
		_tracker.RecordTrigger(card);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count);
		Assert.AreEqual(3f, rows[0].GetValue(CombatStatType.TriggerCount));
	}

	[Test]
	public void PowerGivenAndReceived_RecordStacksNotGrantCount()
	{
		var giver = CreateCard(true, "Buffer", "buffer").GetComponent<CardScript>();
		var receiver = CreateCard(true, "Buffee", "buffee").GetComponent<CardScript>();

		_tracker.RecordPowerGiven(giver, 3); // one grant of 3 stacks must count 3, not 1
		_tracker.RecordPowerReceived(receiver, 3);

		var rows = _tracker.GetSessionRows();
		var giverRow = rows.Find(r => r.cardTypeID == "buffer");
		var receiverRow = rows.Find(r => r.cardTypeID == "buffee");
		Assert.AreEqual(3f, giverRow.GetValue(CombatStatType.PowerGiven));
		Assert.AreEqual(3f, receiverRow.GetValue(CombatStatType.PowerReceived));
	}

	[Test]
	public void RecordDamage_IgnoresNonPositiveAmounts()
	{
		var card = CreateCard(true, "ZeroHit", "zero_hit").GetComponent<CardScript>();

		_tracker.RecordDamageToOpponent(card, 0f);
		_tracker.RecordDamageToSelf(card, -2f);

		Assert.AreEqual(0, _tracker.GetSessionRows().Count, "Zero/negative damage must not create a row");
	}

	[Test]
	public void GetSessionRows_SortsByDamageDescThenFactionPlayerFirst()
	{
		var lowPlayer = CreateCard(true, "Low", "low").GetComponent<CardScript>();
		var highEnemy = CreateCard(false, "High", "high").GetComponent<CardScript>();
		var midEnemy = CreateCard(false, "Mid", "mid").GetComponent<CardScript>();
		var midPlayer = CreateCard(true, "Mid", "mid").GetComponent<CardScript>();

		_tracker.RecordDamageToOpponent(lowPlayer, 1f);
		_tracker.RecordDamageToOpponent(highEnemy, 9f);
		_tracker.RecordDamageToOpponent(midEnemy, 5f);
		_tracker.RecordDamageToOpponent(midPlayer, 5f);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(4, rows.Count);
		Assert.AreEqual("high", rows[0].cardTypeID, "Highest damage first");
		Assert.AreEqual(CardFaction.Player, rows[1].faction, "On damage tie, Player row comes before Enemy row");
		Assert.AreEqual(CardFaction.Enemy, rows[2].faction);
		Assert.AreEqual("low", rows[3].cardTypeID);
	}

	[Test]
	public void BeginSession_ClearsAllRecords()
	{
		var card = CreateCard(true, "Ticker", "ticker").GetComponent<CardScript>();
		_tracker.RecordTrigger(card);
		Assert.AreEqual(1, _tracker.GetSessionRows().Count);

		_tracker.BeginSession();

		Assert.AreEqual(0, _tracker.GetSessionRows().Count, "BeginSession must wipe the previous combat's records");
	}

	[Test]
	public void EmptyCardTypeID_FallsBackToGameObjectName()
	{
		var card = CreateCard(true, "NamelessCard").GetComponent<CardScript>(); // cardTypeID stays empty

		_tracker.RecordTrigger(card);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count);
		Assert.AreEqual("NamelessCard", rows[0].cardTypeID);
	}
}

/// <summary>
/// EditMode smoke tests for the runtime-built Result stats panel UI.
/// </summary>
public class ResultStatsPanelTests : HeadlessCombatTestFixture
{
	[Test]
	public void Build_CreatesOneRowPerRecord_AndClearDestroysPanel()
	{
		var canvasGo = CreateGameObject("TestCanvas");
		var canvas = canvasGo.AddComponent<Canvas>();

		var panelGo = CreateGameObject("TestResultStatsPanel");
		var panel = panelGo.AddComponent<ResultStatsPanel>();

		var rows = new List<PerCardStatRecord>
		{
			MakeRow("fire_imp", "Fire Imp", CardFaction.Player, CombatStatType.DamageDealtToOpponent, 7f),
			MakeRow("fire_imp", "Fire Imp", CardFaction.Enemy, CombatStatType.DamageDealtToOpponent, 2f)
		};

		panel.Build(canvas, rows);

		var content = canvas.transform.Find("ResultStatsPanelRoot/Body/ScrollView/Viewport/Content");
		Assert.IsNotNull(content, "Panel content should exist under the canvas");
		Assert.AreEqual(2, content.childCount, "One UI row per record");

		// Header must have one cell per registry column + card + faction
		var header = canvas.transform.Find("ResultStatsPanelRoot/Body/Header");
		Assert.IsNotNull(header);
		Assert.AreEqual(CombatStatRegistry.GetColumnsSorted().Count + 2, header.childCount);

		panel.Clear();
		Assert.IsNull(canvas.transform.Find("ResultStatsPanelRoot"), "Clear must destroy the panel root");
	}

	[Test]
	public void Build_WithNoRows_ShowsEmptyState()
	{
		var canvasGo = CreateGameObject("TestCanvas2");
		var canvas = canvasGo.AddComponent<Canvas>();

		var panelGo = CreateGameObject("TestResultStatsPanel2");
		var panel = panelGo.AddComponent<ResultStatsPanel>();

		panel.Build(canvas, new List<PerCardStatRecord>());

		var content = canvas.transform.Find("ResultStatsPanelRoot/Body/ScrollView/Viewport/Content");
		Assert.IsNotNull(content);
		Assert.AreEqual(1, content.childCount, "Empty state shows a single placeholder row");

		panel.Clear();
	}

	private static PerCardStatRecord MakeRow(string id, string name, CardFaction faction, CombatStatType stat, float value)
	{
		var record = new PerCardStatRecord
		{
			cardTypeID = id,
			displayName = name,
			faction = faction
		};
		record.values[stat] = value;
		return record;
	}
}
