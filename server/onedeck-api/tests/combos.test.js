'use strict';

/**
 * Combo library tests (plans/plan-infinity-detection-2026-09-17.md §21, phase P4).
 *
 * P4's acceptance criterion is "a deck CONTAINING the specimen combo is filtered at match time".
 * P3's content flag only withholds the exact same card list, so the interesting cases here are
 * containment (a padded deck), the ingest gate (only a proven minimum may enter the library) and
 * the review flow (admin retire / reactivate).
 *
 * Same harness style as loopReports.test.js: throwaway DATA_DIR set before requiring server.js,
 * a shared player pool (registration is rate-limited in production), and card ids unique per test
 * so one test's combo cannot leak into another.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-combos-test-'));
process.env.DATA_DIR = TMP;

const { app, db } = require('../server.js');

const GAME_VERSION = '4.0';
const MAX_SESSION = 6;
const SERVE_PROBES = 4;

let base = '';
let server = null;
let adminToken = '';
const players = {};

test.before(async () =>
{
	server = app.listen(0, '127.0.0.1');
	await new Promise((resolve) => server.once('listening', resolve));
	base = 'http://127.0.0.1:' + server.address().port;
	adminToken = fs.readFileSync(path.join(TMP, 'admin_token.txt'), 'utf8').trim();

	for (const name of ['owner1', 'owner2', 'victim1', 'victim2'])
	{
		const response = await post('/api/players/register', { username: 'odcombo_' + name });
		assert.strictEqual(response.status, 201, 'register ' + name + ' -> ' + JSON.stringify(response.body));
		players[name] = response.body.playerId;
	}
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
	try { json = await response.json(); } catch { /* non-json */ }
	return { status: response.status, body: json };
}

async function postForm(pathname, fields)
{
	const body = Object.keys(fields)
		.map((key) => encodeURIComponent(key) + '=' + encodeURIComponent(fields[key]))
		.join('&');
	const response = await fetch(base + pathname, {
		method: 'POST',
		headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
		body,
		redirect: 'manual',
	});
	return response.status;
}

async function get(pathname)
{
	const response = await fetch(base + pathname);
	let json = null;
	try { json = await response.json(); } catch { /* non-json */ }
	return { status: response.status, body: json };
}

async function uploadDeck(playerId, cardTypeIDs, sessionNum)
{
	const response = await post('/api/decks', {
		playerId,
		gameVersion: GAME_VERSION,
		sessionNum: sessionNum === undefined ? 1 : sessionNum,
		hpMax: 20, winAmount: 0, heartLeft: 3,
		cardTypeIDs,
	});
	assert.strictEqual(response.status, 201, 'upload -> ' + JSON.stringify(response.body));
	return response.body.deckId;
}

async function fetchOpponents(playerId)
{
	const query = 'playerId=' + playerId + '&gameVersion=' + GAME_VERSION
		+ '&maxSession=' + MAX_SESSION + '&perSession=5';
	const response = await get('/api/decks/opponents?' + query);
	assert.strictEqual(response.status, 200, 'opponents -> ' + response.status);
	return response.body;
}

/** Card ids unique to a test tag, so combos never bleed across tests. */
function cards(tag, letters)
{
	return letters.map((letter) => 'CARD_' + tag + '_' + letter);
}

async function everServed(playerId, deckId)
{
	for (let i = 0; i < SERVE_PROBES; i++)
	{
		if ((await fetchOpponents(playerId)).decks.some((d) => d.deckId === deckId)) return true;
	}
	return false;
}

async function neverServed(playerId, deckId)
{
	for (let i = 0; i < SERVE_PROBES; i++)
	{
		if ((await fetchOpponents(playerId)).decks.some((d) => d.deckId === deckId)) return false;
	}
	return true;
}

/**
 * A LoopReport payload as LoopReportUploader sends it: the minimized set plus the proof flags.
 * The server ingests a combo only when the set is unbounded (criterion v2, plan §26: a periodic
 * run the round boundary did not reset), 1-minimal, untruncated and multi-seed stable.
 */
function provenPayload(mySide, overrides)
{
	return JSON.stringify(Object.assign({
		responsibility: 'EnemyDeck',
		mySide: mySide,
		oneMinimal: true,
		truncated: false,
		multiSeedStable: true,
		unbounded: true,
		cycles: 100,
		period: 2,
		roundStarved: true,
		reproSeeds: [4242, 7],
	}, overrides || {}));
}

async function report(playerId, deckId, payload, verdict, seed)
{
	return post('/api/loop-reports', {
		playerId,
		gameVersion: GAME_VERSION,
		opponentDeckId: deckId,
		verdict: verdict === undefined ? 'EnemyDeck' : verdict,
		seed: seed === undefined ? 4242 : seed,
		signals: 'arrangement-cycle abc repeats=9',
		payload: payload,
	});
}

function comboRow(cardsList)
{
	const key = db.prepare('SELECT combo_key FROM combos WHERE cards = ?').get(JSON.stringify(cardsList));
	return key ? db.prepare('SELECT * FROM combos WHERE combo_key = ?').get(key.combo_key) : null;
}

// --------------------------------------------------------------------------- tests

test('a proven minimum is ingested and withholds decks that CONTAIN it', async () =>
{
	const combo = cards('contain', ['A', 'B']);
	// The looping deck itself, a padded deck that embeds the pair, and an unrelated deck.
	const looping = await uploadDeck(players.owner1, cards('contain', ['A', 'B', 'C', 'D']));
	const padded = await uploadDeck(players.owner2, cards('contain', ['A', 'B', 'X', 'Y', 'Z']));
	const unrelated = await uploadDeck(players.owner1, cards('contain', ['A', 'X', 'Y']));

	assert.ok(await everServed(players.victim1, padded), 'the padded deck is served before the report');

	const response = await report(players.victim1, looping, provenPayload(combo));
	assert.strictEqual(response.status, 201, JSON.stringify(response.body));
	assert.strictEqual(response.body.flagged, true);
	assert.notStrictEqual(response.body.comboKey, '', 'a proven minimum must be registered');
	assert.strictEqual(response.body.comboStatus, 'active');

	const row = comboRow(combo);
	assert.ok(row, 'the combo row exists');
	assert.strictEqual(row.status, 'active');
	assert.strictEqual(row.report_count, 1);

	// P3 behavior: the exact deck (and its own future uploads) is withheld.
	assert.strictEqual(await neverServed(players.victim1, looping), true, 'the reported deck is withheld');
	// P4 behavior: so is any deck that merely CONTAINS the pair.
	assert.strictEqual(await neverServed(players.victim1, padded), true,
		'a deck containing the combo must be withheld at match time');
	// And nothing else is affected.
	assert.ok(await everServed(players.victim1, unrelated), 'a deck without the combo is still served');
});

test('only a proven minimum enters the library', async () =>
{
	const combo = cards('gate', ['A', 'B']);
	const looping = await uploadDeck(players.owner1, cards('gate', ['A', 'B', 'C']));
	const padded = await uploadDeck(players.owner2, cards('gate', ['A', 'B', 'X', 'Y']));

	const cases = [
		{ name: 'not 1-minimal', payload: provenPayload(combo, { oneMinimal: false }) },
		{ name: 'truncated', payload: provenPayload(combo, { truncated: true }) },
		{ name: 'not multi-seed stable', payload: provenPayload(combo, { multiSeedStable: false }) },
		{ name: 'not unbounded (criterion v2: a gate-bounded repetition)', payload: provenPayload(combo, { unbounded: false }) },
		{ name: 'unbounded flag absent (pre-v2 client)', payload: provenPayload(combo, { unbounded: undefined }) },
		{ name: 'proof flags missing entirely', payload: JSON.stringify({ mySide: combo }) },
		{ name: 'empty minimized set', payload: provenPayload([]) },
		{ name: 'payload not json', payload: 'not-json-at-all' },
	];

	for (const entry of cases)
	{
		const response = await report(players.victim1, looping, entry.payload, 'EnemyDeck', 1000 + cases.indexOf(entry));
		assert.strictEqual(response.status, 201, entry.name + ': evidence is still stored');
		assert.strictEqual(response.body.comboKey, '', entry.name + ' must not enter the library');
		// The deck-level flag from P3 still applies — only the CONTAINMENT check is withheld.
		assert.strictEqual(response.body.flagged, true, entry.name + ': the reported deck is still flagged');
	}

	assert.strictEqual(await neverServed(players.victim1, looping), true, 'the reported deck is withheld');
	assert.ok(await everServed(players.victim1, padded),
		'without a proven minimum a merely containing deck is still served (that is P4 territory only)');
});

test('containment is multiset: a combo needing two copies is not satisfied by one', async () =>
{
	const pair = cards('multi', ['A', 'A', 'B']);
	const looping = await uploadDeck(players.owner1, cards('multi', ['A', 'A', 'B']));
	const oneCopy = await uploadDeck(players.owner2, cards('multi', ['A', 'B']));
	const twoCopies = await uploadDeck(players.owner2, cards('multi', ['A', 'A', 'B', 'C']));

	assert.strictEqual((await report(players.victim1, looping, provenPayload(pair))).body.comboKey !== '', true);

	assert.ok(await everServed(players.victim1, oneCopy), 'one copy of A does not satisfy [A, A, B]');
	assert.strictEqual(await neverServed(players.victim1, twoCopies), true, 'two copies does');
});

test('evidence-only verdicts never create combos', async () =>
{
	const combo = cards('evidence', ['A', 'B']);
	const deck = await uploadDeck(players.owner2, cards('evidence', ['A', 'B', 'C']));

	for (const verdict of ['', 'None', 'OwnerDeck', 'PairOnly'])
	{
		const response = await report(players.victim2, deck, provenPayload(combo), verdict, 7);
		assert.strictEqual(response.status, 201);
		assert.strictEqual(response.body.comboKey, '', 'verdict "' + verdict + '" must not ingest a combo');
	}
	assert.strictEqual(comboRow(combo), null);
});

test('the client is told about blocked combos, and a retired combo is not among them', async () =>
{
	const combo = cards('purge', ['A', 'B']);
	const looping = await uploadDeck(players.owner1, cards('purge', ['A', 'B', 'C']), 2);
	assert.strictEqual((await report(players.victim2, looping, provenPayload(combo), 'EnemyDeck', 99)).body.comboKey !== '', true);

	const listed = await fetchOpponents(players.victim2);
	assert.ok(listed.blockedCombos.some((c) => c.cards.join(',') === combo.join(',')),
		'the active combo is returned so the client can purge cached decks');
	assert.ok(listed.flaggedDeckIds.includes(looping), 'the content flag is still reported too');

	const key = comboRow(combo).combo_key;
	assert.strictEqual(await postForm('/admin/combos/status?token=' + encodeURIComponent(adminToken),
		{ comboKey: key, status: 'retired', reason: 'card patched' }), 302);

	const afterRetire = await fetchOpponents(players.victim2);
	assert.ok(!afterRetire.blockedCombos.some((c) => c.key === key), 'a retired combo is not pushed to clients');

	const retired = comboRow(combo);
	assert.strictEqual(retired.status, 'retired');
	assert.strictEqual(retired.retired_reason, 'card patched');
	assert.ok(retired.retired_at, 'the retire is timestamped for the audit trail');
});

test('retiring a combo restores serving, reactivating withholds again', async () =>
{
	const combo = cards('flow', ['A', 'B']);
	const looping = await uploadDeck(players.owner1, cards('flow', ['A', 'B', 'C']));
	const padded = await uploadDeck(players.owner2, cards('flow', ['A', 'B', 'X', 'Y']));
	assert.strictEqual((await report(players.victim1, looping, provenPayload(combo), 'EnemyDeck', 555)).body.comboKey !== '', true);
	assert.strictEqual(await neverServed(players.victim1, padded), true, 'blocked while active');

	const key = comboRow(combo).combo_key;
	const route = '/admin/combos/status?token=' + encodeURIComponent(adminToken);
	assert.strictEqual(await postForm(route, { comboKey: key, status: 'retired', reason: 'verifying' }), 302);
	assert.ok(await everServed(players.victim1, padded), 'a retired combo stops withholding decks');
	// The deck-level content flag is independent of the combo and survives the retire.
	assert.strictEqual(await neverServed(players.victim1, looping), true,
		'the reported deck stays flagged: retiring a combo is not unflagging the deck');

	assert.strictEqual(await postForm(route, { comboKey: key, status: 'active' }), 302);
	assert.strictEqual(await neverServed(players.victim1, padded), true, 'reactivated: withheld again');

	// A reactivated combo has its retire metadata cleared.
	const reactivated = comboRow(combo);
	assert.strictEqual(reactivated.status, 'active');
	assert.strictEqual(reactivated.retired_at, null);
	assert.strictEqual(reactivated.retired_reason, '');

	// Unknown combos and bad statuses are rejected.
	assert.strictEqual(await postForm(route, { comboKey: 'nosuchkey', status: 'retired' }), 400);
	assert.strictEqual(await postForm(route, { comboKey: key, status: 'banana' }), 400);
	const noToken = await fetch(base + '/admin/combos/status?token=nope', {
		method: 'POST',
		headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
		body: 'comboKey=' + encodeURIComponent(key) + '&status=retired',
		redirect: 'manual',
	});
	assert.strictEqual(noToken.status, 403, 'an unauthenticated retire must not work');
});

test('a second report on a retired combo only records the renewed evidence', async () =>
{
	const combo = cards('retired', ['A', 'B']);
	const looping = await uploadDeck(players.owner1, cards('retired', ['A', 'B', 'C']));
	const padded = await uploadDeck(players.owner2, cards('retired', ['A', 'B', 'X']));
	assert.strictEqual((await report(players.victim1, looping, provenPayload(combo), 'EnemyDeck', 11)).body.comboKey !== '', true);

	const key = comboRow(combo).combo_key;
	assert.strictEqual(await postForm('/admin/combos/status?token=' + encodeURIComponent(adminToken),
		{ comboKey: key, status: 'retired', reason: 'patched' }), 302);

	const again = await report(players.victim2, looping, provenPayload(combo), 'EnemyDeck', 22);
	assert.strictEqual(again.body.comboStatus, 'retired', 'a retired combo stays retired on a new report');
	const row = comboRow(combo);
	assert.strictEqual(row.status, 'retired', 'the renewed report must not silently reactivate it');
	assert.strictEqual(row.report_count, 2, 'but the evidence count grows for the admin to see');
	assert.ok(await everServed(players.victim1, padded), 'and it still does not withhold decks');
});

test('the admin dashboard renders the combo library', async () =>
{
	const response = await fetch(base + '/admin?token=' + encodeURIComponent(adminToken));
	assert.strictEqual(response.status, 200);
	const html = await response.text();
	assert.ok(html.includes('Combo library'), 'dashboard shows the combo library section');
	assert.ok(html.includes('/admin/combos/status?token='), 'dashboard offers retire/reactivate forms');
});
