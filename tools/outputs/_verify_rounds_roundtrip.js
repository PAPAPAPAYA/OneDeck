// Loopback check: does the CURRENT server.js persist run_combats.rounds (and the
// S0 per-card damage split) from a client-shaped payload? Runs a throwaway instance
// on a temp DATA_DIR - production is untouched.
const { spawn } = require('child_process');
const fs = require('fs');
const os = require('os');
const path = require('path');
const SERVER_DIR = path.join(__dirname, '..', '..', 'server', 'onedeck-api');
const Database = require(path.join(SERVER_DIR, 'node_modules', 'better-sqlite3'));

const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'onedeck-rounds-'));
const PORT = 3210;
const BASE = 'http://127.0.0.1:' + PORT;

const child = spawn(process.execPath, [path.join(SERVER_DIR, 'server.js')], {
	env: Object.assign({}, process.env, { DATA_DIR: tmp, PORT: String(PORT) }),
	stdio: ['ignore', 'pipe', 'pipe'],
});
child.stdout.on('data', (d) => process.stdout.write('[srv] ' + d));
child.stderr.on('data', (d) => process.stderr.write('[srv-err] ' + d));

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () =>
{
	let ok = true;
	try
	{
		// wait for listen
		for (let i = 0; i < 40; i++)
		{
			try { const r = await fetch(BASE + '/api/health'); if (r.ok) break; }
			catch { await sleep(250); }
		}

		const reg = await fetch(BASE + '/api/players/register', {
			method: 'POST', headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ username: 'pr' + String(Date.now()).slice(-6) }),
		}).then((r) => r.json());
		console.log('register ->', JSON.stringify(reg));

		// Payload shaped like the Unity client: RunUploadRequest / RunCombatEntry.
		// Combat 0 uses the current per-card split, combat 1 the legacy single damageDealt.
		const payload = {
			playerId: reg.playerId,
			runId: 'probe' + Date.now().toString(16) + 'rounds',
			gameVersion: 'test-rounds',
			result: 'defeat',
			finalSession: 2,
			heartsLeft: 0,
			finalDeck: ['A', 'B'],
			seenPoolPct: 0.5,
			startedAt: '2026-09-10T00:00:00.0000000Z',
			endedAt: '2026-09-10T00:10:00.0000000Z',
			shopVisits: [
				{ sessionNum: 0, offered: ['A'], utilityOffered: [], bought: ['A'], rerollCount: 1,
					seenPoolPct: 0.2, hpMax: 20, goldEnter: 0, goldAfterPayday: 5, goldExit: 3,
					ts: '2026-09-10T00:01:00.0000000Z' },
			],
			combats: [
				{ sessionNum: 0, won: true, heartsLeft: 3, rounds: 7, opponentDeckId: 0,
					ts: '2026-09-10T00:02:00.0000000Z',
					perCard: [{ cardTypeID: 'HEXER', triggers: 4, damageToOpponent: 9, damageToSelf: 2 }],
					series: [{ revealIndex: 1, roundNum: 1, ownerHP: 20, enemyHP: 20, ownerShield: 0,
						enemyShield: 0, ownerDeckSize: 8, enemyDeckSize: 8, side: 0, cardTypeID: 'HEXER' }] },
				{ sessionNum: 1, won: false, heartsLeft: 0, rounds: 3, opponentDeckId: 0,
					ts: '2026-09-10T00:05:00.0000000Z',
					perCard: [{ cardTypeID: 'JU_ON', triggers: 2, damageDealt: 5 }],
					series: null },
			],
		};

		const res = await fetch(BASE + '/api/runs', {
			method: 'POST', headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(payload),
		});
		console.log('POST /api/runs ->', res.status, JSON.stringify(await res.json()));

		child.kill('SIGTERM');
		await sleep(600);

		const db = new Database(path.join(tmp, 'onedeck.db'), { readonly: true });
		const rows = db.prepare('SELECT session_num, won, hearts_left, rounds, opponent_deck_id, per_card, series FROM run_combats ORDER BY session_num').all();
		const visits = db.prepare('SELECT session_num, hp_max, gold_exit FROM run_shop_visits').all();
		db.close();

		console.log('run_combats ->', JSON.stringify(rows, null, 1));
		console.log('run_shop_visits ->', JSON.stringify(visits));

		const expect = [{ s: 0, r: 7 }, { s: 1, r: 3 }];
		for (const e of expect)
		{
			const row = rows.find((r) => r.session_num === e.s);
			if (!row || row.rounds !== e.r) { ok = false; console.log('FAIL: session ' + e.s + ' rounds != ' + e.r); }
		}
		if (rows.some((r) => typeof r.series !== 'string')) { ok = false; console.log('FAIL: series missing'); }
		const series0 = JSON.parse(rows.find((r) => r.session_num === 0).series);
		if (!series0.length || series0[0].roundNum !== 1) { ok = false; console.log('FAIL: series payload not stored'); }
		if (visits.length !== 1 || visits[0].hp_max !== 20) { ok = false; console.log('FAIL: hp_max not stored'); }
		console.log(ok ? 'RESULT: PASS - rounds / series / hp_max persisted' : 'RESULT: FAIL');
	}
	catch (e)
	{
		ok = false;
		console.log('ERROR: ' + (e && e.stack ? e.stack : e));
	}
	finally
	{
		try { child.kill('SIGKILL'); } catch { /* already gone */ }
		process.exit(ok ? 0 : 1);
	}
})();
