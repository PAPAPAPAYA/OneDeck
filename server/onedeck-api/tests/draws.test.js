'use strict';

/**
 * Draw accounting tests (plans/plan-forced-draw-reporting-2026-11-26.md §5).
 *
 * Natural + forced draws end-to-end on the server: run_combats draw columns (kind, cap
 * reason, HP, reveals, signed seed), match_reports draw_kind + the per-kind defense draw
 * counters (D2 ruling: a draw is no longer a defense win/loss), the stats_snapshots draw
 * counters (COALESCE keeps stored values on a legacy snapshot), precedence/coercion rules,
 * the admin draws display, and the raised 900kb body limit.
 *
 * Same pattern as loopReports.test.js: DATA_DIR must be set BEFORE requiring server.js;
 * throwaway SQLite db; rows/players shared across tests on purpose (registration is
 * rate-limited in production).
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-api-draws-test-'));
process.env.DATA_DIR = TMP;

const { app, db } = require('../server.js');

const GAME_VERSION = 'drawtest';

let base = '';
let server = null;
let adminToken = '';
let player = '';

test.before(async () =>
{
	server = app.listen(0, '127.0.0.1');
	await new Promise((resolve) => server.once('listening', resolve));
	base = 'http://127.0.0.1:' + server.address().port;
	adminToken = fs.readFileSync(path.join(TMP, 'admin_token.txt'), 'utf8').trim();

	const response = await post('/api/players/register', { username: 'odtest_drawp' });
	assert.strictEqual(response.status, 201, 'register -> ' + JSON.stringify(response.body));
	player = response.body.playerId;
});

test.after(() =>
{
	if (server) server.close();
	db.close();
	fs.rmSync(TMP, { recursive: true, force: true });
});

// --------------------------------------------------------------------------- helpers

async function post(pathname, body)
{
	const response = await fetch(base + pathname, {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body: JSON.stringify(body),
	});
	let json = null;
	try { json = await response.json(); } catch { /* non-json (html) */ }
	return { status: response.status, body: json };
}

async function get(pathname)
{
	const response = await fetch(base + pathname);
	const clone = response.clone();
	let json = null;
	try { json = await response.json(); } catch { /* non-json (html) */ }
	return { status: response.status, body: json, text: json === null ? await clone.text() : null };
}

function combat(overrides)
{
	return Object.assign({
		sessionNum: 1,
		won: false,
		heartsLeft: 3,
		rounds: 5,
		opponentDeckId: 0,
		perCard: [],
		series: [],
		ts: '2026-11-26T12:00:00+08:00',
	}, overrides);
}

async function uploadRun(runId, combats, extra)
{
	return post('/api/runs', Object.assign({
		playerId: player,
		runId,
		gameVersion: GAME_VERSION,
		result: 'victory',
		finalDeck: ['CARD_A'],
		shopVisits: [],
		combats,
	}, extra || {}));
}

function runCombatRow(runId)
{
	return db.prepare('SELECT * FROM run_combats WHERE run_id = ?').get(runId);
}

function deckDefense(deckId)
{
	return db.prepare('SELECT defense_wins AS dw, defense_losses AS dl,'
		+ ' defense_draws_natural AS ddn, defense_draws_forced AS ddf FROM decks WHERE deck_id = ?').get(deckId);
}

// --------------------------------------------------------------------------- tests

test('a forced combat stores kind, cap reason, HP, reveals and a NEGATIVE seed', async () =>
{
	const response = await uploadRun('run-forced', [combat({
		won: true,
		drawKind: 'forced',
		forcedReason: 'rounds',
		ownerHp: 7,
		enemyHp: 9,
		reveals: 1500,
		combatSeed: -559038737,
	})]);
	assert.strictEqual(response.status, 201, JSON.stringify(response.body));

	const row = runCombatRow('run-forced');
	assert.strictEqual(row.draw_kind, 'forced');
	assert.strictEqual(row.forced_reason, 'rounds');
	assert.strictEqual(row.owner_hp, 7);
	assert.strictEqual(row.enemy_hp, 9);
	assert.strictEqual(row.reveals, 1500);
	assert.strictEqual(row.combat_seed, -559038737, 'the full int32 range must survive (no 0..2^31-1 clamp)');
	// drawKind outranks won server-side: the row stores as a draw.
	assert.strictEqual(row.won, 0);
});

test('a natural draw stores kind, both HP 0, won = 0', async () =>
{
	const response = await uploadRun('run-natural', [combat({
		drawKind: 'natural',
		ownerHp: 0,
		enemyHp: 0,
	})]);
	assert.strictEqual(response.status, 201, JSON.stringify(response.body));

	const row = runCombatRow('run-natural');
	assert.strictEqual(row.draw_kind, 'natural');
	assert.strictEqual(row.forced_reason, '');
	assert.strictEqual(row.owner_hp, 0);
	assert.strictEqual(row.enemy_hp, 0);
	assert.strictEqual(row.won, 0);
});

test('a legacy payload without the new fields keeps the old shape', async () =>
{
	const response = await uploadRun('run-legacy', [combat({ won: true })]);
	assert.strictEqual(response.status, 201, JSON.stringify(response.body));

	const row = runCombatRow('run-legacy');
	assert.strictEqual(row.draw_kind, '');
	assert.strictEqual(row.forced_reason, '');
	assert.strictEqual(row.owner_hp, null);
	assert.strictEqual(row.enemy_hp, null);
	assert.strictEqual(row.reveals, 0);
	assert.strictEqual(row.combat_seed, 0);
	assert.strictEqual(row.won, 1);
});

test('match reports: per-kind defense draw counters, dedup replay changes nothing', async () =>
{
	// Ghost deck owned by a second player (the reporter must not fight their own deck).
	const other = await post('/api/players/register', { username: 'odtest_drawg' });
	const ghostId = other.body.playerId;
	const deck = await post('/api/decks', {
		playerId: ghostId, gameVersion: GAME_VERSION, sessionNum: 1,
		hpMax: 20, winAmount: 0, heartLeft: 3, cardTypeIDs: ['CARD_GHOST'],
	});
	assert.strictEqual(deck.status, 201, JSON.stringify(deck.body));
	const deckId = deck.body.deckId;

	const before = deckDefense(deckId);
	assert.deepStrictEqual(before, { dw: 0, dl: 0, ddn: 0, ddf: 0 });

	const natural = await post('/api/matches/report', {
		playerId: player, reportId: 'report-natural-0001', opponentDeckId: deckId,
		won: false, drawKind: 'natural', ownerHp: 0, enemyHp: 0, sessionNum: 1, gameVersion: GAME_VERSION,
	});
	assert.strictEqual(natural.status, 201, JSON.stringify(natural.body));

	const forced = await post('/api/matches/report', {
		playerId: player, reportId: 'report-forced-000001', opponentDeckId: deckId,
		won: false, drawKind: 'forced', ownerHp: 5, enemyHp: 5, sessionNum: 2, gameVersion: GAME_VERSION,
	});
	assert.strictEqual(forced.status, 201, JSON.stringify(forced.body));

	let after = deckDefense(deckId);
	assert.strictEqual(after.ddn, 1, 'natural draw lands in defense_draws_natural');
	assert.strictEqual(after.ddf, 1, 'forced draw lands in defense_draws_forced');
	assert.strictEqual(after.dw, before.dw, 'a draw must not change defense_wins (D2 ruling)');
	assert.strictEqual(after.dl, before.dl, 'a draw must not change defense_losses (D2 ruling)');

	const naturalRow = db.prepare("SELECT * FROM match_reports WHERE report_id = 'report-natural-0001'").get();
	assert.strictEqual(naturalRow.draw_kind, 'natural');
	assert.strictEqual(naturalRow.owner_hp, 0);
	assert.strictEqual(naturalRow.enemy_hp, 0);
	assert.strictEqual(naturalRow.won, 0);
	const forcedRow = db.prepare("SELECT * FROM match_reports WHERE report_id = 'report-forced-000001'").get();
	assert.strictEqual(forcedRow.draw_kind, 'forced');
	assert.strictEqual(forcedRow.owner_hp, 5);

	// Replay the same reportId: deduped, and no counter moves.
	const replay = await post('/api/matches/report', {
		playerId: player, reportId: 'report-natural-0001', opponentDeckId: deckId,
		won: false, drawKind: 'natural', ownerHp: 0, enemyHp: 0, sessionNum: 1, gameVersion: GAME_VERSION,
	});
	assert.strictEqual(replay.status, 200);
	assert.strictEqual(replay.body.deduped, true);
	assert.deepStrictEqual(deckDefense(deckId), after, 'the dedup replay must not touch any counter');
});

test('stats snapshots: draws stored, and a legacy snapshot keeps the stored draws', async () =>
{
	const withDraws = await post('/api/stats/snapshot', {
		playerId: player, gameVersion: GAME_VERSION,
		shop: [],
		winrate: [{ cardTypeID: 'CARD_A', sessionNum: 1, combats: 4, wins: 2, losses: 1, drawsNatural: 1, drawsForced: 0 }],
	});
	assert.strictEqual(withDraws.status, 200, JSON.stringify(withDraws.body));
	let row = db.prepare("SELECT * FROM stats_snapshots WHERE player_id = ? AND kind = 'winrate' AND card_type_id = 'CARD_A'").get(player);
	assert.strictEqual(row.draws_natural, 1);
	assert.strictEqual(row.draws_forced, 0);

	// The zeroing trap: a legacy snapshot (fields absent) must keep the stored values.
	const legacy = await post('/api/stats/snapshot', {
		playerId: player, gameVersion: GAME_VERSION,
		shop: [{ cardTypeID: 'CARD_SHOP', sessionNum: 1, appear: 3, bought: 1, utilAppear: 0, utilBought: 0 }],
		winrate: [{ cardTypeID: 'CARD_A', sessionNum: 1, combats: 6, wins: 3, losses: 2 }],
	});
	assert.strictEqual(legacy.status, 200, JSON.stringify(legacy.body));
	row = db.prepare("SELECT * FROM stats_snapshots WHERE player_id = ? AND kind = 'winrate' AND card_type_id = 'CARD_A'").get(player);
	assert.strictEqual(row.draws_natural, 1, 'a legacy snapshot must not zero draws_natural (COALESCE)');
	assert.strictEqual(row.draws_forced, 0);
	assert.strictEqual(row.combats, 6, 'non-draw columns still replace');

	const shopRow = db.prepare("SELECT * FROM stats_snapshots WHERE player_id = ? AND kind = 'shop' AND card_type_id = 'CARD_SHOP'").get(player);
	assert.strictEqual(shopRow.draws_natural, null, 'shop rows bind null (never touch draw columns)');
});

test('precedence and coercion: won+drawKind is a draw; unknown kinds coerce to "" with 2xx', async () =>
{
	const both = await uploadRun('run-both', [combat({ won: true, drawKind: 'forced', forcedReason: 'totalReveals' })]);
	assert.strictEqual(both.status, 201);
	const bothRow = runCombatRow('run-both');
	assert.strictEqual(bothRow.draw_kind, 'forced', 'drawKind outranks won');
	assert.strictEqual(bothRow.won, 0);
	assert.strictEqual(bothRow.forced_reason, 'totalReveals');

	const unknown = await uploadRun('run-unknown', [combat({ won: true, drawKind: 'maybe', ownerHp: 3 })]);
	assert.strictEqual(unknown.status, 201, 'an unknown draw kind must never 4xx: the outbox would drop the whole run');
	const unknownRow = runCombatRow('run-unknown');
	assert.strictEqual(unknownRow.draw_kind, '');
	assert.strictEqual(unknownRow.won, 1, 'coerced to "" -> an ordinary combat, won kept');

	const forcedReasonOnWin = await uploadRun('run-reason-no-draw', [combat({ won: true, forcedReason: 'rounds' })]);
	assert.strictEqual(forcedReasonOnWin.status, 201);
	assert.strictEqual(runCombatRow('run-reason-no-draw').forced_reason, '',
		'forced_reason without a draw kind is dropped');
});

test('admin dashboard shows the split counters, the draws section and the run page draw rows', async () =>
{
	// A default-pool draw (opponent_deck_id NULL): the LEFT JOIN must keep it visible.
	const poolDraw = await uploadRun('run-pool-draw', [combat({ drawKind: 'natural', opponentDeckId: 0 })]);
	assert.strictEqual(poolDraw.status, 201);

	const dashboard = await get('/admin?token=' + encodeURIComponent(adminToken));
	assert.strictEqual(dashboard.status, 200);
	const html = dashboard.text;
	assert.ok(html.includes('natural draws'), 'overview counter card for natural draws');
	assert.ok(html.includes('forced draws'), 'overview counter card for forced draws');
	assert.ok(html.includes('Draws · natural + forced'), 'draws section exists');
	assert.ok(html.includes('default pool'), 'a default-pool draw is listed (LEFT JOIN keeps opponent_deck_id NULL rows)');
	assert.ok(html.includes('draws nat'), 'card win-rate table draw columns');
	assert.ok(html.includes('defense draws nat'), 'ghost defense table draw columns');

	const runPage = await get('/admin/run/' + encodeURIComponent('run-natural') + '?token=' + encodeURIComponent(adminToken));
	assert.strictEqual(runPage.status, 200);
	assert.ok(runPage.text.includes('draw (natural)'), 'run page renders the natural draw');

	const forcedPage = await get('/admin/run/' + encodeURIComponent('run-forced') + '?token=' + encodeURIComponent(adminToken));
	assert.strictEqual(forcedPage.status, 200);
	assert.ok(forcedPage.text.includes('draw (forced: rounds)'), 'run page renders the forced draw with its cap reason');

	// Unauthenticated admin access stays blocked.
	const noToken = await get('/admin');
	assert.strictEqual(noToken.status, 403);
});

test('a ~900kb run body is accepted', async () =>
{
	// ~175 bytes per sample -> 10 combats x 500 samples is just under the 900kb express cap
	// (and far over the old 256kb cap that dropped these runs entirely).
	const fatSample = {
		revealIndex: 1, roundNum: 1, ownerHP: 20, enemyHP: 20, ownerShield: 0, enemyShield: 0,
		ownerDeckSize: 30, enemyDeckSize: 30, side: 0, cardTypeID: 'CARD_0123456789abcdefghij',
	};
	const combats = [];
	for (let c = 0; c < 10; c++)
	{
		const series = [];
		for (let i = 0; i < 500; i++) series.push(Object.assign({}, fatSample, { revealIndex: i + 1 }));
		combats.push(combat({ sessionNum: c + 1, won: true, series }));
	}
	const body = JSON.stringify({
		playerId: player, runId: 'run-fat-body', gameVersion: GAME_VERSION, result: 'victory',
		finalDeck: ['CARD_A'], shopVisits: [], combats,
	});
	assert.ok(body.length > 700 * 1024, 'the fixture body should be ~900kb, got ' + body.length);
	assert.ok(body.length < 900 * 1024, 'fixture must stay under the 900kb cap, got ' + body.length);

	const response = await fetch(base + '/api/runs', {
		method: 'POST',
		headers: { 'Content-Type': 'application/json' },
		body,
	});
	assert.strictEqual(response.status, 201, 'a ~900kb body must be accepted (plan §5)');
	const stored = db.prepare('SELECT COUNT(*) AS c FROM run_combats WHERE run_id = ?').get('run-fat-body');
	assert.strictEqual(stored.c, 10);
});
