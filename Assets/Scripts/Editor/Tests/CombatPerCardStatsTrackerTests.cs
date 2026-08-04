using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for CombatPerCardStatsTracker: aggregation, creator-side attribution,
/// neutral guard, power stack semantics, sorting, deck composition counts, and session reset.
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
	public void RecordDamage_AggregatesCopiesOfSameCardType()
	{
		var copyA = CreateCard(true, "FireImp A", "fire_imp").GetComponent<CardScript>();
		var copyB = CreateCard(true, "FireImp B", "fire_imp").GetComponent<CardScript>();

		_tracker.RecordDamage(copyA, 3f, CardFaction.Enemy);
		_tracker.RecordDamage(copyB, 4f, CardFaction.Enemy);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count, "Two copies of the same cardTypeID from the same creator side should aggregate into one row");
		Assert.AreEqual(7f, rows[0].GetValue(CombatStatType.DamageDealtToOpponent));
	}

	[Test]
	public void SameCardType_OnBothFactions_ProducesTwoRows()
	{
		var playerCopy = CreateCard(true, "FireImp (Player)", "fire_imp").GetComponent<CardScript>();
		var enemyCopy = CreateCard(false, "FireImp (Enemy)", "fire_imp").GetComponent<CardScript>();

		_tracker.RecordDamage(playerCopy, 5f, CardFaction.Enemy);
		_tracker.RecordDamage(enemyCopy, 2f, CardFaction.Player);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(2, rows.Count, "Same cardTypeID created by both sides must produce two separate rows");

		var playerRow = rows.Find(r => r.faction == CardFaction.Player);
		var enemyRow = rows.Find(r => r.faction == CardFaction.Enemy);
		Assert.IsNotNull(playerRow);
		Assert.IsNotNull(enemyRow);
		Assert.AreEqual(5f, playerRow.GetValue(CombatStatType.DamageDealtToOpponent));
		Assert.AreEqual(2f, enemyRow.GetValue(CombatStatType.DamageDealtToOpponent));
	}

	[Test]
	public void PlayerGeneratedEnemyCard_DamagingEnemy_CountsAsPlayerDamageToOpponent()
	{
		// A player effect creates an enemy-owned card (e.g. a curse); its damage to the enemy
		// is the PLAYER's damage-to-opponent, not the enemy's self-damage.
		var generatedCard = CreateCard(false, "Curse", "curse").GetComponent<CardScript>();
		var creator = CreateCard(true, "CurseGiver", "curse_giver").GetComponent<CardScript>();
		_tracker.RegisterGeneratedCard(generatedCard, creator);

		_tracker.RecordDamage(generatedCard, 5f, CardFaction.Enemy);

		var rows = _tracker.GetSessionRows();
		var curseRow = rows.Find(r => r.cardTypeID == "curse");
		Assert.IsNotNull(curseRow);
		Assert.AreEqual(CardFaction.Player, curseRow.faction, "Row must be attributed to the creator side, not the owner side");
		Assert.AreEqual(5f, curseRow.GetValue(CombatStatType.DamageDealtToOpponent));
		Assert.AreEqual(0f, curseRow.GetValue(CombatStatType.DamageDealtToSelf));
	}

	[Test]
	public void DamageToCreatorsOwnSide_CountsAsSelfDamage()
	{
		var card = CreateCard(true, "SelfHarmer", "self_harmer").GetComponent<CardScript>();

		_tracker.RecordDamage(card, 3f, CardFaction.Player);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count);
		Assert.AreEqual(3f, rows[0].GetValue(CombatStatType.DamageDealtToSelf));
		Assert.AreEqual(0f, rows[0].GetValue(CombatStatType.DamageDealtToOpponent));
	}

	[Test]
	public void NeutralCard_IsExcluded()
	{
		var startCard = CreateStartCard().GetComponent<CardScript>();

		_tracker.RecordTrigger(startCard);
		_tracker.RecordDamage(startCard, 10f, CardFaction.Enemy);
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

		_tracker.RecordDamage(card, 0f, CardFaction.Enemy);
		_tracker.RecordDamage(card, -2f, CardFaction.Player);

		Assert.AreEqual(0, _tracker.GetSessionRows().Count, "Zero/negative damage must not create a row");
	}

	[Test]
	public void GetSessionRows_SortsByDamageDescThenFactionPlayerFirst()
	{
		var lowPlayer = CreateCard(true, "Low", "low").GetComponent<CardScript>();
		var highEnemy = CreateCard(false, "High", "high").GetComponent<CardScript>();
		var midEnemy = CreateCard(false, "Mid", "mid").GetComponent<CardScript>();
		var midPlayer = CreateCard(true, "Mid", "mid").GetComponent<CardScript>();

		_tracker.RecordDamage(lowPlayer, 1f, CardFaction.Enemy);
		_tracker.RecordDamage(highEnemy, 9f, CardFaction.Player);
		_tracker.RecordDamage(midEnemy, 5f, CardFaction.Player);
		_tracker.RecordDamage(midPlayer, 5f, CardFaction.Enemy);

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
	public void BeginSession_ClearsDeckCountsAndCreatorSides()
	{
		var copyA = CreateCard(true, "FireImp A", "fire_imp");
		var copyB = CreateCard(true, "FireImp B", "fire_imp");
		_tracker.RegisterDeckComposition(new List<GameObject> { copyA, copyB });

		var generated = CreateCard(false, "Curse", "curse").GetComponent<CardScript>();
		var creator = CreateCard(true, "Giver", "giver").GetComponent<CardScript>();
		_tracker.RegisterGeneratedCard(generated, creator);

		_tracker.BeginSession();

		// Copy count forgotten: new rows default to instanceCount 1
		_tracker.RecordTrigger(copyA.GetComponent<CardScript>());
		var row = _tracker.GetSessionRows().Find(r => r.cardTypeID == "fire_imp");
		Assert.AreEqual(1, row.instanceCount, "BeginSession must wipe the deck composition snapshot");

		// Creator side forgotten: the enemy-owned card falls back to its owner faction
		_tracker.RecordDamage(generated, 5f, CardFaction.Enemy);
		var curseRow = _tracker.GetSessionRows().Find(r => r.cardTypeID == "curse");
		Assert.AreEqual(CardFaction.Enemy, curseRow.faction);
		Assert.AreEqual(5f, curseRow.GetValue(CombatStatType.DamageDealtToSelf));
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

	[Test]
	public void RegisterDeckComposition_CountsCopiesPerTypeAndFaction()
	{
		var copyA = CreateCard(true, "FireImp A", "fire_imp");
		var copyB = CreateCard(true, "FireImp B", "fire_imp");
		var enemyCopy = CreateCard(false, "FireImp (Enemy)", "fire_imp");
		var startCard = CreateStartCard(); // neutral: must be skipped by the snapshot

		_tracker.RegisterDeckComposition(new List<GameObject> { copyA, copyB, enemyCopy, startCard });

		_tracker.RecordTrigger(copyA.GetComponent<CardScript>());
		_tracker.RecordTrigger(enemyCopy.GetComponent<CardScript>());

		var playerRow = _tracker.GetSessionRows().Find(r => r.faction == CardFaction.Player);
		var enemyRow = _tracker.GetSessionRows().Find(r => r.faction == CardFaction.Enemy);
		Assert.AreEqual(2, playerRow.instanceCount, "Two same-type player cards in the initial deck must count 2");
		Assert.AreEqual(1, enemyRow.instanceCount);
	}

	[Test]
	public void RegisterDeckComposition_CreatorSideLockedToDeckOwner()
	{
		// An initial-deck enemy card keeps the Enemy creator side even when it damages the enemy.
		var enemyCard = CreateCard(false, "Backfire", "backfire");
		_tracker.RegisterDeckComposition(new List<GameObject> { enemyCard });

		_tracker.RecordDamage(enemyCard.GetComponent<CardScript>(), 4f, CardFaction.Enemy);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count);
		Assert.AreEqual(CardFaction.Enemy, rows[0].faction);
		Assert.AreEqual(4f, rows[0].GetValue(CombatStatType.DamageDealtToSelf));
	}

	[Test]
	public void RegisterDeckComposition_PreCreatesZeroRowsForEveryDeckCard()
	{
		var copyA = CreateCard(true, "FireImp A", "fire_imp");
		var copyB = CreateCard(true, "FireImp B", "fire_imp");
		var enemyCard = CreateCard(false, "Spider", "spider");
		var startCard = CreateStartCard(); // neutral: no row even with pre-creation

		_tracker.RegisterDeckComposition(new List<GameObject> { copyA, copyB, enemyCard, startCard });

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(2, rows.Count, "Every non-neutral deck card type shows even without any recorded stat");
		var playerRow = rows.Find(r => r.cardTypeID == "fire_imp");
		var enemyRow = rows.Find(r => r.cardTypeID == "spider");
		Assert.AreEqual(2, playerRow.instanceCount, "Copy count snapshot still applies to pre-created rows");
		Assert.AreEqual(0f, playerRow.GetValue(CombatStatType.TriggerCount));
		Assert.AreEqual(0f, enemyRow.GetValue(CombatStatType.DamageDealtToOpponent));
	}

	[Test]
	public void RegisterGeneratedCard_CountsGenerationOnCreator_AndPreCreatesZeroRow()
	{
		var creator = CreateCard(true, "Giver", "giver").GetComponent<CardScript>();
		var generated = CreateCard(false, "Curse", "curse").GetComponent<CardScript>();

		_tracker.RegisterGeneratedCard(generated, creator);
		_tracker.RegisterGeneratedCard(CreateCard(false, "Curse2", "curse").GetComponent<CardScript>(), creator);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(2, rows.Count);
		var creatorRow = rows.Find(r => r.cardTypeID == "giver");
		var generatedRow = rows.Find(r => r.cardTypeID == "curse");
		Assert.AreEqual(2f, creatorRow.GetValue(CombatStatType.CardsGenerated), "Each generation counts once on the creator");
		Assert.AreEqual(CardFaction.Player, generatedRow.faction, "Generated card row is attributed to the creator side");
		Assert.AreEqual(0f, generatedRow.GetValue(CombatStatType.TriggerCount), "Generated card shows even with zero stats");
	}

	[Test]
	public void RecordBury_SplitsSourceSideByOwnerRelativeToSource_AndCountsVictim()
	{
		var source = CreateCard(true, "Undertaker", "undertaker").GetComponent<CardScript>();
		var friendlyVictim = CreateCard(true, "Ally", "ally").GetComponent<CardScript>();
		var enemyVictim = CreateCard(false, "Foe", "foe").GetComponent<CardScript>();

		_tracker.RecordBury(source, friendlyVictim);
		_tracker.RecordBury(source, enemyVictim);

		var rows = _tracker.GetSessionRows();
		var sourceRow = rows.Find(r => r.cardTypeID == "undertaker");
		Assert.AreEqual(1f, sourceRow.GetValue(CombatStatType.FriendlyBuried));
		Assert.AreEqual(1f, sourceRow.GetValue(CombatStatType.EnemyBuried));
		Assert.AreEqual(1f, rows.Find(r => r.cardTypeID == "ally").GetValue(CombatStatType.TimesBuried));
		Assert.AreEqual(1f, rows.Find(r => r.cardTypeID == "foe").GetValue(CombatStatType.TimesBuried));
	}

	[Test]
	public void RecordBury_EnemySourceBuryingEnemyCard_CountsAsFriendlyForSource()
	{
		var enemySource = CreateCard(false, "EnemyUndertaker", "enemy_undertaker").GetComponent<CardScript>();
		var enemyVictim = CreateCard(false, "EnemyAlly", "enemy_ally").GetComponent<CardScript>();

		_tracker.RecordBury(enemySource, enemyVictim);

		var sourceRow = _tracker.GetSessionRows().Find(r => r.cardTypeID == "enemy_undertaker");
		Assert.AreEqual(1f, sourceRow.GetValue(CombatStatType.FriendlyBuried), "Friendly/enemy is relative to the burying card's owner");
		Assert.AreEqual(0f, sourceRow.GetValue(CombatStatType.EnemyBuried));
	}

	[Test]
	public void RecordStage_CountsFriendlySourceSideOnly_ButAlwaysCountsVictim()
	{
		var source = CreateCard(true, "Promoter", "promoter").GetComponent<CardScript>();
		var friendly = CreateCard(true, "Ally", "ally").GetComponent<CardScript>();
		var enemy = CreateCard(false, "Foe", "foe").GetComponent<CardScript>();

		_tracker.RecordStage(source, friendly);
		_tracker.RecordStage(source, enemy);

		var rows = _tracker.GetSessionRows();
		var sourceRow = rows.Find(r => r.cardTypeID == "promoter");
		Assert.AreEqual(1f, sourceRow.GetValue(CombatStatType.FriendlyStaged), "Only friendly stagings count source-side (no enemy-staged column by design)");
		Assert.AreEqual(1f, rows.Find(r => r.cardTypeID == "ally").GetValue(CombatStatType.TimesStaged));
		Assert.AreEqual(1f, rows.Find(r => r.cardTypeID == "foe").GetValue(CombatStatType.TimesStaged), "Victim side counts even when the staged card is enemy-owned");
	}

	[Test]
	public void RecordBuryAndStage_SkipNeutralParticipants()
	{
		var startCard = CreateStartCard().GetComponent<CardScript>();
		var normal = CreateCard(true, "Ally", "ally").GetComponent<CardScript>();

		_tracker.RecordBury(startCard, normal);   // neutral source: no source-side count
		_tracker.RecordBury(normal, startCard);   // neutral victim: neither side counts
		_tracker.RecordStage(startCard, normal);
		_tracker.RecordStage(normal, startCard);

		var rows = _tracker.GetSessionRows();
		Assert.AreEqual(1, rows.Count, "Only the normal card may appear");
		Assert.AreEqual("ally", rows[0].cardTypeID);
		Assert.AreEqual(1f, rows[0].GetValue(CombatStatType.TimesBuried), "Victim side still counts when the source is neutral");
		Assert.AreEqual(1f, rows[0].GetValue(CombatStatType.TimesStaged));
		Assert.AreEqual(0f, rows[0].GetValue(CombatStatType.FriendlyBuried), "Burying the neutral start card counts on neither side");
		Assert.AreEqual(0f, rows[0].GetValue(CombatStatType.EnemyBuried));
		Assert.AreEqual(0f, rows[0].GetValue(CombatStatType.FriendlyStaged));
	}
}

/// <summary>
/// EditMode smoke tests for the runtime-built Result stats panel UI (two creator-side halves).
/// </summary>
public class ResultStatsPanelTests : HeadlessCombatTestFixture
{
	[Test]
	public void Build_SplitsRowsIntoPlayerAndEnemyHalves_AndClearDestroysPanel()
	{
		var canvasGo = CreateGameObject("TestCanvas");
		var canvas = canvasGo.AddComponent<Canvas>();

		var panelGo = CreateGameObject("TestResultStatsPanel");
		var panel = panelGo.AddComponent<ResultStatsPanel>();

		var rows = new List<PerCardStatRecord>
		{
			MakeRow("fire_imp", "Fire Imp", CardFaction.Player, 7f),
			MakeRow("fire_imp", "Fire Imp", CardFaction.Enemy, 2f)
		};

		panel.Build(canvas, rows);

		var playerContent = canvas.transform.Find("ResultStatsPanelRoot/Body/Halves/Half_Player/ScrollView/Viewport/Content");
		var enemyContent = canvas.transform.Find("ResultStatsPanelRoot/Body/Halves/Half_Enemy/ScrollView/Viewport/Content");
		Assert.IsNotNull(playerContent, "Player half content should exist");
		Assert.IsNotNull(enemyContent, "Enemy half content should exist");
		Assert.AreEqual(1, playerContent.childCount, "Player half holds the player-created row");
		Assert.AreEqual(1, enemyContent.childCount, "Enemy half holds the enemy-created row");

		// Header: Card + one cell per registry column
		var header = canvas.transform.Find("ResultStatsPanelRoot/Body/Halves/Half_Player/Header");
		Assert.IsNotNull(header);
		Assert.AreEqual(1 + CombatStatRegistry.GetColumnsSorted().Count, header.childCount);

		panel.Clear();
		Assert.IsNull(canvas.transform.Find("ResultStatsPanelRoot"), "Clear must destroy the panel root");
	}

	[Test]
	public void Build_WithNoRows_ShowsEmptyStateInBothHalves()
	{
		var canvasGo = CreateGameObject("TestCanvas2");
		var canvas = canvasGo.AddComponent<Canvas>();

		var panelGo = CreateGameObject("TestResultStatsPanel2");
		var panel = panelGo.AddComponent<ResultStatsPanel>();

		panel.Build(canvas, new List<PerCardStatRecord>());

		var playerContent = canvas.transform.Find("ResultStatsPanelRoot/Body/Halves/Half_Player/ScrollView/Viewport/Content");
		var enemyContent = canvas.transform.Find("ResultStatsPanelRoot/Body/Halves/Half_Enemy/ScrollView/Viewport/Content");
		Assert.IsNotNull(playerContent);
		Assert.IsNotNull(enemyContent);
		Assert.AreEqual(1, playerContent.childCount, "Empty player half shows a single placeholder row");
		Assert.AreEqual(1, enemyContent.childCount, "Empty enemy half shows a single placeholder row");

		panel.Clear();
	}

	[Test]
	public void Build_DamageCell_ShowsShareOfHalfTotal_AndCountSuffix()
	{
		var canvasGo = CreateGameObject("TestCanvas3");
		var canvas = canvasGo.AddComponent<Canvas>();

		var panelGo = CreateGameObject("TestResultStatsPanel3");
		var panel = panelGo.AddComponent<ResultStatsPanel>();

		var rows = new List<PerCardStatRecord>
		{
			MakeRow("big", "Big", CardFaction.Player, 12f, 2),
			MakeRow("small", "Small", CardFaction.Player, 4f)
		};

		panel.Build(canvas, rows);

		var playerContent = canvas.transform.Find("ResultStatsPanelRoot/Body/Halves/Half_Player/ScrollView/Viewport/Content");
		Assert.IsNotNull(playerContent);

		// Cell order per row: 0=Card, 1=damage column
		var bigNameCell = playerContent.GetChild(0).GetChild(0).GetComponentInChildren<TMPro.TextMeshProUGUI>();
		Assert.AreEqual("Big (2)", bigNameCell.text, "2+ initial-deck copies show the count suffix");
		var bigDmgCell = playerContent.GetChild(0).GetChild(1).GetComponentInChildren<TMPro.TextMeshProUGUI>();
		Assert.AreEqual("12 (75%)", bigDmgCell.text, "Share is computed against the half total (12 of 16)");

		var smallNameCell = playerContent.GetChild(1).GetChild(0).GetComponentInChildren<TMPro.TextMeshProUGUI>();
		Assert.AreEqual("Small", smallNameCell.text, "Single copies show no count suffix");
		var smallDmgCell = playerContent.GetChild(1).GetChild(1).GetComponentInChildren<TMPro.TextMeshProUGUI>();
		Assert.AreEqual("4 (25%)", smallDmgCell.text);

		panel.Clear();
	}

	private static PerCardStatRecord MakeRow(string id, string name, CardFaction faction, float damageToOpponent, int instanceCount = 1)
	{
		var record = new PerCardStatRecord
		{
			cardTypeID = id,
			displayName = name,
			faction = faction,
			instanceCount = instanceCount
		};
		record.values[CombatStatType.DamageDealtToOpponent] = damageToOpponent;
		return record;
	}
}
