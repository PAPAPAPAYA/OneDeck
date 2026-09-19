'use strict';

/**
 * Tests for tools/outputs/post_loop_reports.js (plan §23) — the one step that writes to
 * production, so the tests are about restraint as much as about posting: a dry run must send
 * nothing, --post without a reporter must refuse, and only actionable entries may be posted.
 *
 * Dependency-free on purpose: a bare node:http stub stands in for the server, so this file can be
 * run straight from the repo without the server package installed:
 *   node --test tools/outputs/post_loop_reports.test.js
 */

const test = require('node:test');
const assert = require('node:assert');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');

const POSTER = path.join(__dirname, 'post_loop_reports.js');

function fixtureReport()
{
	return {
		generatedUtc: '2026-09-19T10:00:00.000Z',
		unityVersion: '6000.3.9f1',
		dummySize: 3,
		dummyHp: 100000000,
		seeds: [4242, 7],
		candidates: 4,
		scanned: 3,
		infinite: 2,
		clean: 1,
		unresolved: 0,
		skippedFlagged: 0,
		deduped: 0,
		warnings: [],
		decks: [
			{
				deckId: 11, label: 'deck 11 (a, 4.0, session 0)', source: 'server', gameVersion: '4.0',
				status: 'infinite', cards: ['RELIC_CURSE_REVIVAL', 'CURSE_GARDENER'],
				reveals: 19, rounds: 1, repeats: 9, oneMinimal: true, truncated: false, multiSeedStable: true,
				reportJson: JSON.stringify({
					responsibility: 'EnemyDeck',
					mySide: ['RELIC_CURSE_REVIVAL', 'CURSE_GARDENER'],
					oneMinimal: true, truncated: false, multiSeedStable: true,
					reproSeeds: [4242, 7], tripSignal: 'arrangement-cycle abcd repeats=9 reveals=19',
				}),
			},
			{
				deckId: 0, label: 'lethal infinite test', source: 'recorded', gameVersion: '0.2.0',
				status: 'infinite', cards: ['RELIC_CURSE_REVIVAL', 'CURSE_GARDENER'],
				reveals: 19, rounds: 1, repeats: 9, oneMinimal: true, truncated: false, multiSeedStable: true,
				reportJson: '{"responsibility":"EnemyDeck","mySide":["RELIC_CURSE_REVIVAL","CURSE_GARDENER"]}',
			},
			{
				deckId: 13, label: 'deck 13 (c, 4.0, session 2)', source: 'server', gameVersion: '4.0',
				status: 'infinite', cards: ['CURSE_REVIVER', 'CURSE_SUMMONER'],
				reveals: 162, rounds: 7, repeats: 38, oneMinimal: false, truncated: false, multiSeedStable: false,
				reportJson: '{"responsibility":"EnemyDeck","mySide":["CURSE_REVIVER"],"oneMinimal":false,"multiSeedStable":false}',
			},
			{
				deckId: 12, label: 'deck 12 (b, 4.0, session 0)', source: 'server', gameVersion: '4.0',
				status: 'clean', cards: ['CURSE_GARDENER'],
				reveals: 40, rounds: 3, repeats: 0, oneMinimal: false, truncated: false, multiSeedStable: false,
				reportJson: '',
			},
		],
	};
}

function writeFixture()
{
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-poster-test-'));
	const file = path.join(dir, 'infinity_scan_fixture.json');
	fs.writeFileSync(file, JSON.stringify(fixtureReport()), 'utf8');
	return { dir, file };
}

/**
 * Always spawn ASYNCHRONOUSLY. A synchronous spawn blocks this process's event loop, so the stub
 * server below could never answer and the poster would wait forever — that deadlock cost a run.
 */
function runPoster(args)
{
	return new Promise((resolve) =>
	{
		const child = spawn(process.execPath, [POSTER, ...args]);
		let stdout = '';
		let stderr = '';
		child.stdout.on('data', (chunk) => { stdout += chunk; });
		child.stderr.on('data', (chunk) => { stderr += chunk; });
		child.on('close', (status) => resolve({ status, stdout, stderr }));
	});
}

/** Stub server that records every request body, then answers like the real endpoint. */
function startStub()
{
	const received = [];
	const server = http.createServer((req, res) =>
	{
		let body = '';
		req.on('data', (chunk) => { body += chunk; });
		req.on('end', () =>
		{
			received.push({ url: req.url, body: body ? JSON.parse(body) : null });
			res.writeHead(201, { 'Content-Type': 'application/json' });
			res.end(JSON.stringify({ ok: true, flagged: true, comboKey: 'deadbeefdeadbeef', comboStatus: 'active' }));
		});
	});
	return new Promise((resolve) =>
	{
		server.listen(0, '127.0.0.1', () =>
		{
			resolve({ server, received, port: server.address().port });
		});
	});
}

test('dry run (the default) sends nothing', async () =>
{
	const { dir, file } = writeFixture();
	try
	{
		const result = await runPoster([file]);
		assert.strictEqual(result.status, 0, result.stderr);
		assert.match(result.stdout, /DRY RUN/);
		assert.match(result.stdout, /postable \(infinite, proven minimum, with a server deck row\): 1/,
			'only the proven entry counts: the local deck has no server row and the single-seed one is unproven');
		assert.match(result.stdout, /deck 11/);
		assert.match(result.stdout, /WITHHELD[\s\S]*deck 13/,
			'an infinite-but-unproven verdict must be reported as withheld, not silently dropped');
	}
	finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('--post without a reporter is refused', async () =>
{
	const { dir, file } = writeFixture();
	try
	{
		const result = await runPoster([file, '--post']);
		assert.strictEqual(result.status, 2);
		assert.match(result.stderr, /--player-id/);
	}
	finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('--post files exactly the actionable entries, with the report payload verbatim', async () =>
{
	const { dir, file } = writeFixture();
	const stub = await startStub();
	try
	{
		const result = await runPoster([file, '--post', '--player-id', 'pid-test',
			'--server', 'http://127.0.0.1:' + stub.port]);
		assert.strictEqual(result.status, 0, result.stderr);

		assert.strictEqual(stub.received.length, 1, 'one request for the one postable deck');
		const request = stub.received[0];
		assert.strictEqual(request.url, '/api/loop-reports');
		assert.strictEqual(request.body.playerId, 'pid-test');
		assert.strictEqual(request.body.opponentDeckId, 11);
		assert.strictEqual(request.body.verdict, 'EnemyDeck');
		assert.strictEqual(request.body.gameVersion, '4.0');
		assert.strictEqual(request.body.seed, 4242, 'the first reproduction seed travels for the audit trail');
		assert.match(request.body.signals, /arrangement-cycle abcd/);
		assert.match(request.body.signals, /batch-scan/, 'the evidence says this came from a scan, not a live victim');

		const payload = JSON.parse(request.body.payload);
		assert.deepStrictEqual(payload.mySide, ['RELIC_CURSE_REVIVAL', 'CURSE_GARDENER']);
		assert.strictEqual(payload.oneMinimal, true);
		assert.match(result.stdout, /FLAGGED/);
		assert.match(result.stdout, /combo=deadbeefdeadbeef/);
	}
	finally
	{
		stub.server.close();
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

test('--include-unproven lifts the bar deliberately', async () =>
{
	const { dir, file } = writeFixture();
	const stub = await startStub();
	try
	{
		const result = await runPoster([file, '--post', '--include-unproven', '--player-id', 'pid-test',
			'--server', 'http://127.0.0.1:' + stub.port]);
		assert.strictEqual(result.status, 0, result.stderr);
		assert.strictEqual(stub.received.length, 2, 'the proven deck and the single-seed deck are both filed');

		const deckIds = stub.received.map((r) => r.body.opponentDeckId).sort((a, b) => a - b);
		assert.deepStrictEqual(deckIds, [11, 13], 'the local recorded deck still has no server row to accuse');
		assert.match(result.stdout, /bar lifted/);
	}
	finally
	{
		stub.server.close();
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

test('a report with nothing actionable posts nothing', async () =>
{
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-poster-test-'));
	const file = path.join(dir, 'empty.json');
	fs.writeFileSync(file, JSON.stringify({ decks: [{ deckId: 5, status: 'clean', reportJson: '' }] }), 'utf8');
	const stub = await startStub();
	try
	{
		const result = await runPoster([file, '--post', '--player-id', 'pid-test',
			'--server', 'http://127.0.0.1:' + stub.port]);
		assert.strictEqual(result.status, 0, result.stderr);
		assert.strictEqual(stub.received.length, 0);
	}
	finally
	{
		stub.server.close();
		fs.rmSync(dir, { recursive: true, force: true });
	}
});

test('a non-report file fails loudly', async () =>
{
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-poster-test-'));
	const file = path.join(dir, 'not-a-report.json');
	fs.writeFileSync(file, JSON.stringify({ hello: 'world' }), 'utf8');
	try
	{
		const result = await runPoster([file]);
		assert.strictEqual(result.status, 1);
		assert.match(result.stderr, /not a scan report/);
	}
	finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
