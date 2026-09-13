// One-off deploy smoke for UTC+8 uploaded_at (2026-09-13). Registers a throwaway
// test_ player, uploads a minimal 1-combat run, asserts runs.uploaded_at carries
// the +08:00 suffix, then deletes every row it created. Never touches real data.
'use strict';
const http = require('http');
const Database = require('better-sqlite3');

const PORT = process.env.SMOKE_PORT || 3000;
const DB = '/var/www/onedeck/data/onedeck.db';

function post(path, body)
{
	return new Promise((resolve, reject) =>
	{
		const data = JSON.stringify(body);
		const req = http.request(
			{ host: '127.0.0.1', port: PORT, path, method: 'POST',
				headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } },
			(res) =>
			{
				let b = '';
				res.on('data', (c) => { b += c; });
				res.on('end', () => resolve({ status: res.statusCode, body: b }));
			});
		req.on('error', reject);
		req.write(data);
		req.end();
	});
}

(async () =>
{
	// Pre-clean any leftovers from earlier smoke attempts (fixed prefixes only).
	const pre = new Database(DB);
	const staleRuns = pre.prepare("SELECT run_id FROM runs WHERE run_id LIKE 'smokecst8%'").all().map((r) => r.run_id);
	for (const id of staleRuns)
	{
		pre.prepare('DELETE FROM run_combats WHERE run_id = ?').run(id);
		pre.prepare('DELETE FROM run_shop_visits WHERE run_id = ?').run(id);
		pre.prepare('DELETE FROM runs WHERE run_id = ?').run(id);
	}
	pre.prepare("DELETE FROM players WHERE username LIKE 'test_cst8_%'").run();
	pre.close();
	if (staleRuns.length > 0) console.log('pre-cleaned stale smoke runs: ' + staleRuns.join(','));

	const runId = 'smokecst8' + Date.now().toString(36) + 'x';
	const uname = 'test_cst8_' + Math.floor(Math.random() * 1e9).toString(36);
	const reg = await post('/api/players/register', { username: uname });
	if (reg.status !== 201) { console.log('FAIL register ' + reg.status + ' ' + reg.body); process.exit(1); }
	const playerId = JSON.parse(reg.body).playerId;

	const payload = {
		playerId, runId, gameVersion: 'deploy-smoke', result: 'defeat',
		finalSession: 1, heartsLeft: 1, finalDeck: ['WOLF'], seenPoolPct: 0.1,
		startedAt: '2026-09-13T19:00:00.000+08:00', endedAt: '2026-09-13T19:10:00.000+08:00',
		shopVisits: [],
		combats: [{ sessionNum: 1, won: false, heartsLeft: 1, rounds: 5, opponentDeckId: 0,
			perCard: [{ cardTypeID: 'WOLF', triggers: 1, damageToOpponent: 3, damageToSelf: 0 }],
			series: [], ts: '2026-09-13T19:05:00.000+08:00' }]
	};
	// POST /api/runs returns 201 on insert (200 would also be acceptable).
	const up = await post('/api/runs', payload);
	if (up.status !== 201 && up.status !== 200) { console.log('FAIL runs ' + up.status + ' ' + up.body); process.exit(1); }

	const db = new Database(DB, { readonly: true });
	const row = db.prepare('SELECT uploaded_at, started_at, ended_at FROM runs WHERE run_id = ?').get(runId);
	console.log('run row: ' + JSON.stringify(row));
	const c1 = !!row, c2 = /\+08:00$/.test(row && row.uploaded_at),
		c3 = row && row.started_at === payload.startedAt, c4 = row && row.ended_at === payload.endedAt;
	console.log('cond row=' + c1 + ' regex=' + c2 + ' startedEq=' + c3 + ' endedEq=' + c4);
	let pass = c1 && c2 && c3 && c4;

	const combatTs = db.prepare('SELECT ts FROM run_combats WHERE run_id = ?').get(runId);
	console.log('combat ts: ' + JSON.stringify(combatTs) + ' eq=' + (combatTs && combatTs.ts === payload.combats[0].ts));
	pass = pass && combatTs && combatTs.ts === payload.combats[0].ts;
	db.close();

	const del = new Database(DB);
	del.prepare('DELETE FROM run_combats WHERE run_id = ?').run(runId);
	del.prepare('DELETE FROM run_shop_visits WHERE run_id = ?').run(runId);
	del.prepare('DELETE FROM runs WHERE run_id = ?').run(runId);
	del.prepare('DELETE FROM players WHERE player_id = ?').run(playerId);
	const left = del.prepare("SELECT COUNT(*) AS c FROM runs WHERE run_id = ?").get(runId).c
		+ del.prepare('SELECT COUNT(*) AS c FROM players WHERE player_id = ?').get(playerId).c;
	del.close();

	console.log(pass && left === 0 ? 'PASS uploaded_at=+08:00, client fields intact, rows cleaned'
		: 'FAIL pass=' + pass + ' leftoverRows=' + left);
	process.exit(pass && left === 0 ? 0 : 1);
})().catch((e) => { console.log('FAIL ' + e.message); process.exit(1); });
