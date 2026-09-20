'use strict';

/**
 * Infinity gate tests (plans/plan-infinity-detection-2026-09-17.md §20.4).
 *
 * P3's acceptance criterion is "a flagged deck is no longer served", and that lives entirely in
 * server.js — so it is tested here, over HTTP, against a throwaway SQLite db. DATA_DIR must be
 * set BEFORE requiring server.js: the module resolves DB_PATH at load time.
 *
 * cardTypeIDs are opaque strings to the server (no catalog validation), so the tests fabricate
 * them freely; what matters is content equality, which is what the fingerprint keys on.
 * Rows/players are shared across tests on purpose: registration is rate-limited to 10/hour per
 * IP in production, and the per-deck uniqueness in `content(tag)` is what keeps tests isolated.
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const TMP = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-api-test-'));
process.env.DATA_DIR = TMP;

const { app, db } = require('../server.js');

const INFINITE_VERDICT = 'EnemyDeck';
const GAME_VERSION = '4.0';
const MAX_SESSION = 6;

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

	for (const name of ['offender1', 'offender2', 'offender3', 'victim1', 'victim2', 'victim3'])
	{
		const response = await post('/api/players/register', { username: 'odtest_' + name });
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
	try { json = await response.json(); } catch { /* non-json (html/redirect) */ }
	return { status: response.status, body: json };
}

async function get(pathname)
{
	const response = await fetch(base + pathname);
	let json = null;
	try { json = await response.json(); } catch { /* non-json */ }
	return { status: response.status, body: json };
}

async function uploadDeck(playerId, cardTypeIDs, options)
{
	const opts = options || {};
	const response = await post('/api/decks', {
		playerId,
		gameVersion: opts.gameVersion || GAME_VERSION,
		sessionNum: opts.sessionNum === undefined ? 1 : opts.sessionNum,
		hpMax: 20, winAmount: 0, heartLeft: 3,
		cardTypeIDs,
	});
	assert.strictEqual(response.status, 201, 'upload -> ' + JSON.stringify(response.body));
	return response.body;
}

async function fetchOpponents(playerId, options)
{
	const opts = options || {};
	const query = 'playerId=' + playerId
		+ '&gameVersion=' + (opts.gameVersion || GAME_VERSION)
		+ '&maxSession=' + (opts.maxSession === undefined ? MAX_SESSION : opts.maxSession)
		+ '&perSession=5';
	const response = await get('/api/decks/opponents?' + query);
	assert.strictEqual(response.status, 200, 'opponents -> ' + response.status);
	return response.body;
}

/** Card ids unique to a test, so one test's flagged fingerprint cannot leak into another. */
function content(tag, extra)
{
	const ids = ['CARD_' + tag + '_A', 'CARD_' + tag + '_B'];
	return extra ? ids.concat(extra) : ids;
}

// The server picks at most `perSession` rows at random, but a session holding fewer candidates
// than that returns all of them — so a handful of calls is enough to see a deck that is served.
const SERVE_PROBES = 4;

async function everServed(playerId, deckId)
{
	for (let i = 0; i < SERVE_PROBES; i++)
	{
		const body = await fetchOpponents(playerId);
		if (body.decks.some((d) => d.deckId === deckId)) return true;
	}
	return false;
}

async function neverServed(playerId, deckId)
{
	for (let i = 0; i < SERVE_PROBES; i++)
	{
		const body = await fetchOpponents(playerId);
		if (body.decks.some((d) => d.deckId === deckId)) return false;
	}
	return true;
}

function flagOf(deckId)
{
	return db.prepare('SELECT flag, flag_reason, fingerprint FROM decks WHERE deck_id = ?').get(deckId);
}

async function report(playerId, deckId, options)
{
	const opts = options || {};
	return post('/api/loop-reports', {
		playerId,
		gameVersion: opts.gameVersion || GAME_VERSION,
		opponentDeckId: deckId,
		verdict: opts.verdict === undefined ? INFINITE_VERDICT : opts.verdict,
		seed: opts.seed === undefined ? 4242 : opts.seed,
		signals: opts.signals || 'arrangement-cycle abc repeats=9',
		payload: opts.payload || '{"mySide":["CARD_X"]}',
	});
}

// --------------------------------------------------------------------------- tests

test('confirmed report stops serving every deck with the same content', async () =>
{
	const twins = content('serve');
	const unrelated = content('serve_other');

	const first = await uploadDeck(players.offender1, twins, { sessionNum: 1 });
	const second = await uploadDeck(players.offender1, twins, { sessionNum: 1 });
	const other = await uploadDeck(players.offender1, unrelated, { sessionNum: 1 });

	// Sanity: both are served before the report.
	assert.ok(await everServed(players.victim1, first.deckId), 'first twin must be served before the report');
	assert.ok(await everServed(players.victim1, other.deckId), 'unrelated deck must be served before the report');

	const reported = await report(players.victim1, first.deckId);
	assert.strictEqual(reported.status, 201, JSON.stringify(reported.body));
	assert.strictEqual(reported.body.flagged, true, 'a confirmed EnemyDeck verdict flags');

	// Content-scoped: the twin upload (a DIFFERENT deck_id, never reported) is flagged too.
	assert.strictEqual(flagOf(first.deckId).flag, 1);
	assert.strictEqual(flagOf(second.deckId).flag, 1, 'same content => flagged, even though only the twin was reported');
	assert.strictEqual(flagOf(other.deckId).flag, 0, 'different content stays clean');

	assert.strictEqual(await neverServed(players.victim1, first.deckId), true, 'reported deck must never come back');
	assert.strictEqual(await neverServed(players.victim1, second.deckId), true, 'twin deck must never come back');
	assert.ok(await everServed(players.victim1, other.deckId), 'unrelated deck is still served');
});

test('re-uploading flagged content is flagged at insert', async () =>
{
	const ids = content('upload');
	const original = await uploadDeck(players.offender1, ids, { sessionNum: 2 });
	assert.strictEqual((await report(players.victim1, original.deckId)).body.flagged, true);

	// The evasion path this whole design exists to close: a brand-new deck row of proven content.
	const reupload = await uploadDeck(players.offender1, ids, { sessionNum: 2 });
	assert.strictEqual(reupload.flagged, true, 'upload response must report the auto-flag');
	assert.strictEqual(flagOf(reupload.deckId).flag, 1, 'new row lands flagged');
	assert.strictEqual(await neverServed(players.victim1, reupload.deckId), true, 're-upload is never served');
});

test('the content fingerprint ignores card order: a shuffled re-upload is flagged at insert', async () =>
{
	const ids = content('order', ['CARD_order_C']);
	const original = await uploadDeck(players.offender1, ids, { sessionNum: 2 });
	assert.strictEqual((await report(players.victim1, original.deckId)).body.flagged, true);

	// Same multiset, different order: the fingerprint sorts before hashing (server.js), so the
	// shuffled row must hit the auto-flag path exactly like a byte-identical re-upload.
	const reordered = await uploadDeck(players.offender1, ids.slice().reverse(), { sessionNum: 2 });
	assert.strictEqual(flagOf(reordered.deckId).fingerprint, flagOf(original.deckId).fingerprint,
		'card order must not change the content fingerprint');
	assert.strictEqual(reordered.flagged, true, 'upload response must report the auto-flag');
	assert.strictEqual(flagOf(reordered.deckId).flag, 1, 'the shuffled row lands flagged');
	assert.strictEqual(await neverServed(players.victim1, reordered.deckId), true, 'shuffled re-upload is never served');
});

test('flagging is content-scoped: supersets and subsets stay served', async () =>
{
	const flagged = await uploadDeck(players.offender2, content('scope'), { sessionNum: 1 });
	const superset = await uploadDeck(players.offender2, content('scope', ['CARD_scope_EXTRA']), { sessionNum: 1 });
	assert.strictEqual((await report(players.victim2, flagged.deckId)).body.flagged, true);

	assert.strictEqual(flagOf(flagged.deckId).flag, 1);
	assert.strictEqual(flagOf(superset.deckId).flag, 0, 'a superset is a different multiset (subset matching is P4)');
	assert.ok(await everServed(players.victim2, superset.deckId), 'superset deck is still served');
});

test('evidence-only reports change no state', async () =>
{
	const deck = await uploadDeck(players.offender2, content('evidence'), { sessionNum: 2 });

	for (const verdict of ['', 'None', 'OwnerDeck', 'PairOnly'])
	{
		const response = await report(players.victim2, deck.deckId, { verdict });
		assert.strictEqual(response.status, 201, 'verdict "' + verdict + '" is stored as evidence');
		assert.strictEqual(response.body.flagged, false, 'verdict "' + verdict + '" must not flag');
	}

	assert.strictEqual(flagOf(deck.deckId).flag, 0, 'no verdict => no flag');
	assert.strictEqual(
		db.prepare('SELECT COUNT(*) AS c FROM loop_reports WHERE deck_id = ?').get(deck.deckId).c, 4);
	assert.ok(await everServed(players.victim2, deck.deckId), 'deck is still served');
});

test('loop reports require a known player: 401 unknown_player, not 404', async () =>
{
	// The §24.1 deploy probe is "POST /api/loop-reports answers 401, not 404" — pin that contract.
	const body = {
		gameVersion: GAME_VERSION,
		opponentDeckId: 1,
		verdict: INFINITE_VERDICT,
		seed: 4242,
		signals: 'arrangement-cycle abc repeats=9',
		payload: '{"mySide":["CARD_X"]}',
	};

	const missing = await post('/api/loop-reports', body);
	assert.strictEqual(missing.status, 401, 'a missing playerId is 401, not 404: ' + JSON.stringify(missing.body));
	assert.strictEqual(missing.body.error, 'unknown_player');

	// Well-formed but never registered (32 hex chars, so it passes the length check) — this is the
	// exact request shape a misconfigured deploy fires at the gate.
	const unknown = await post('/api/loop-reports',
		Object.assign({ playerId: '0123456789abcdef0123456789abcdef' }, body));
	assert.strictEqual(unknown.status, 401, 'an unknown playerId is 401, not 404: ' + JSON.stringify(unknown.body));
	assert.strictEqual(unknown.body.error, 'unknown_player');
});

test('a report against your own deck or a missing deck is rejected', async () =>
{
	const own = await uploadDeck(players.offender3, content('misuse'), { sessionNum: 1 });

	const selfReport = await report(players.offender3, own.deckId);
	assert.strictEqual(selfReport.status, 400);
	assert.strictEqual(selfReport.body.error, 'own_deck');

	const missing = await report(players.offender3, 999999);
	assert.strictEqual(missing.status, 404);
	assert.strictEqual(missing.body.error, 'deck_not_found');

	const badDeckId = await report(players.offender3, 0);
	assert.strictEqual(badDeckId.status, 400);
});

test('reports are idempotent per reporter, and a second victim still adds evidence', async () =>
{
	const deck = await uploadDeck(players.offender3, content('dedupe'), { sessionNum: 2 });

	const first = await report(players.victim1, deck.deckId, { seed: 4242 });
	assert.strictEqual(first.body.deduped, false);
	assert.strictEqual(first.body.flagged, true);

	const retry = await report(players.victim1, deck.deckId, { seed: 4242 });
	assert.strictEqual(retry.status, 200);
	assert.strictEqual(retry.body.deduped, true, 'the same reporter + seed + verdict is a retry');

	// A different seed is a different reproduction, and a different reporter is a different claim.
	const otherSeed = await report(players.victim1, deck.deckId, { seed: 99 });
	assert.strictEqual(otherSeed.body.deduped, false);
	const secondVictim = await report(players.victim2, deck.deckId, { seed: 4242 });
	assert.strictEqual(secondVictim.body.deduped, false);

	assert.strictEqual(
		db.prepare('SELECT COUNT(*) AS c FROM loop_reports WHERE deck_id = ?').get(deck.deckId).c, 3);
	// report_count counts rows that actually landed, so the retry did not bump it.
	assert.strictEqual(
		db.prepare('SELECT report_count FROM flagged_fingerprints WHERE fingerprint = ?')
			.get(flagOf(deck.deckId).fingerprint).report_count, 3);
});

test('admin clears the flag, and clearing the content key stops auto-flagging', async () =>
{
	const ids = content('admin');
	const deck = await uploadDeck(players.offender3, ids, { sessionNum: 1 });
	assert.strictEqual((await report(players.victim3, deck.deckId)).body.flagged, true);
	assert.strictEqual(await neverServed(players.victim3, deck.deckId), true);

	const fingerprint = flagOf(deck.deckId).fingerprint;
	const unflag = await fetch(base + '/admin/decks/unflag?token=' + encodeURIComponent(adminToken)
		+ '&fingerprint=' + encodeURIComponent(fingerprint), { method: 'POST', redirect: 'manual' });
	assert.strictEqual(unflag.status, 302, 'admin unflag redirects back to the dashboard');
	assert.strictEqual(flagOf(deck.deckId).flag, 0, 'the row is clean again');
	assert.strictEqual(
		db.prepare('SELECT COUNT(*) AS c FROM flagged_fingerprints WHERE fingerprint = ?').get(fingerprint).c, 0,
		'clearing the content key deregisters it');
	assert.ok(await everServed(players.victim3, deck.deckId), 'deck is served again after the flag is cleared');

	// Deregistered => the next upload of the same content is not auto-flagged.
	const reupload = await uploadDeck(players.offender3, ids, { sessionNum: 3 });
	assert.strictEqual(reupload.flagged, false);

	// An unauthenticated unflag must not work.
	const badToken = await fetch(base + '/admin/decks/unflag?token=nope&deckId=' + deck.deckId,
		{ method: 'POST', redirect: 'manual' });
	assert.strictEqual(badToken.status, 403);
});

test('the client purge list is scoped to the requested version and session range', async () =>
{
	const ids = content('purge');
	const inRange = await uploadDeck(players.offender1, ids, { sessionNum: 3 });
	const outOfRange = await uploadDeck(players.offender1, ids, { sessionNum: 8 });
	const otherVersion = await uploadDeck(players.offender1, ids, { sessionNum: 3, gameVersion: '4.0-alt' });
	assert.strictEqual((await report(players.victim3, inRange.deckId)).body.flagged, true);

	// Fingerprints are version-independent, so both extra rows are flagged too...
	assert.strictEqual(flagOf(outOfRange.deckId).flag, 1);
	assert.strictEqual(flagOf(otherVersion.deckId).flag, 1);

	// ...but the purge list only names what this client's prefetch range could have cached.
	const body = await fetchOpponents(players.victim3, { maxSession: MAX_SESSION });
	assert.ok(body.flaggedDeckIds.includes(inRange.deckId), 'in-range flagged deck is listed');
	assert.ok(!body.flaggedDeckIds.includes(outOfRange.deckId), 'beyond maxSession is not listed');
	assert.ok(!body.flaggedDeckIds.includes(otherVersion.deckId), 'other game version is not listed');

	const narrow = await fetchOpponents(players.victim3, { maxSession: 1 });
	assert.ok(!narrow.flaggedDeckIds.includes(inRange.deckId), 'session 3 is outside a maxSession=1 window');
});

test('admin dashboard renders the infinity sections', async () =>
{
	const response = await fetch(base + '/admin?token=' + encodeURIComponent(adminToken));
	assert.strictEqual(response.status, 200);
	const html = await response.text();
	assert.ok(html.includes('Infinity gate'), 'dashboard shows the infinity gate section');
	assert.ok(html.includes('Loop reports'), 'dashboard lists loop reports');
	assert.ok(html.includes('/admin/decks/unflag?token='), 'dashboard offers the clearing forms');
});
