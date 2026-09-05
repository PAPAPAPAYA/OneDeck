'use strict';

/**
 * OneDeck async-PvP backend.
 *
 * Endpoints:
 *   POST /api/players/register   - create player (username -> playerId)
 *   POST /api/decks              - upload a ghost deck snapshot
 *   GET  /api/decks/opponents    - batch-fetch opponent decks by session range
 *   POST /api/matches/report     - report a battle result (idempotent by reportId)
 *   POST /api/stats/snapshot     - upload lifetime cumulative stats (idempotent upsert)
 *   POST /api/runs               - upload one full run record (idempotent by runId)
 *   POST /api/cards/catalog      - upload card metadata for a game version (upsert)
 *   GET  /api/health             - liveness probe
 *   GET  /admin                  - HTML dashboard (?token=...)
 *   GET  /admin/run/:id          - per-run detail (?token=...)
 *
 * Stack: Express + better-sqlite3, single file by design (same deployment model as pkidle).
 * Auth model: playerId is the credential (async ghost PvP tolerates this).
 * Admin token: env ADMIN_TOKEN, or auto-generated to DATA_DIR/admin_token.txt on first boot.
 */

const path = require('path');
const fs = require('fs');
const crypto = require('crypto');
const express = require('express');
const Database = require('better-sqlite3');

// ---------------------------------------------------------------------------
// Config
// ---------------------------------------------------------------------------

const PORT = parseInt(process.env.PORT || '3000', 10);
const HOST = process.env.HOST || '127.0.0.1';
const DATA_DIR = process.env.DATA_DIR || path.join(__dirname, '..', 'data');
const DB_PATH = path.join(DATA_DIR, 'onedeck.db');

fs.mkdirSync(DATA_DIR, { recursive: true });

function loadAdminToken()
{
	if (process.env.ADMIN_TOKEN) return process.env.ADMIN_TOKEN;
	const tokenPath = path.join(DATA_DIR, 'admin_token.txt');
	try
	{
		const existing = fs.readFileSync(tokenPath, 'utf8').trim();
		if (existing) return existing;
	}
	catch { /* first boot */ }
	const generated = crypto.randomBytes(24).toString('hex');
	fs.writeFileSync(tokenPath, generated, { mode: 0o600 });
	console.log('[onedeck-api] generated admin token, stored at ' + tokenPath);
	return generated;
}

const ADMIN_TOKEN = loadAdminToken();

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------

const db = new Database(DB_PATH);
db.pragma('journal_mode = WAL');

db.exec(`
CREATE TABLE IF NOT EXISTS players (
	player_id TEXT PRIMARY KEY,
	username TEXT NOT NULL,
	username_norm TEXT NOT NULL UNIQUE,
	created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS decks (
	deck_id INTEGER PRIMARY KEY AUTOINCREMENT,
	player_id TEXT NOT NULL,
	username TEXT NOT NULL,
	game_version TEXT NOT NULL,
	session_num INTEGER NOT NULL,
	hp_max INTEGER NOT NULL DEFAULT 0,
	win_amount INTEGER NOT NULL DEFAULT 0,
	heart_left INTEGER NOT NULL DEFAULT 0,
	card_type_ids TEXT NOT NULL,
	defense_wins INTEGER NOT NULL DEFAULT 0,
	defense_losses INTEGER NOT NULL DEFAULT 0,
	created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_decks_match ON decks(game_version, session_num);

CREATE TABLE IF NOT EXISTS match_reports (
	report_id TEXT PRIMARY KEY,
	player_id TEXT NOT NULL,
	opponent_deck_id INTEGER NOT NULL,
	won INTEGER NOT NULL,
	session_num INTEGER NOT NULL,
	game_version TEXT NOT NULL,
	created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS stats_snapshots (
	player_id TEXT NOT NULL,
	kind TEXT NOT NULL,
	game_version TEXT NOT NULL,
	card_type_id TEXT NOT NULL,
	session_num INTEGER NOT NULL,
	appear INTEGER NOT NULL DEFAULT 0,
	bought INTEGER NOT NULL DEFAULT 0,
	util_appear INTEGER NOT NULL DEFAULT 0,
	util_bought INTEGER NOT NULL DEFAULT 0,
	combats INTEGER NOT NULL DEFAULT 0,
	wins INTEGER NOT NULL DEFAULT 0,
	losses INTEGER NOT NULL DEFAULT 0,
	updated_at TEXT NOT NULL,
	PRIMARY KEY (player_id, kind, game_version, card_type_id, session_num)
);

CREATE TABLE IF NOT EXISTS stats_meta (
	player_id TEXT NOT NULL,
	game_version TEXT NOT NULL,
	total_shop_visits INTEGER NOT NULL DEFAULT 0,
	total_rerolls INTEGER NOT NULL DEFAULT 0,
	enemy_source_server INTEGER DEFAULT 0,
	enemy_source_local INTEGER DEFAULT 0,
	enemy_source_pool INTEGER DEFAULT 0,
	updated_at TEXT NOT NULL,
	PRIMARY KEY (player_id, game_version)
);

CREATE TABLE IF NOT EXISTS snapshot_batches (
	batch_id INTEGER PRIMARY KEY AUTOINCREMENT,
	player_id TEXT NOT NULL,
	game_version TEXT NOT NULL,
	uploaded_at TEXT NOT NULL,
	payload TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS runs (
	run_id TEXT PRIMARY KEY,
	player_id TEXT NOT NULL,
	game_version TEXT NOT NULL,
	started_at TEXT,
	ended_at TEXT,
	result TEXT NOT NULL,
	final_session INTEGER NOT NULL DEFAULT 0,
	hearts_left INTEGER NOT NULL DEFAULT 0,
	final_deck TEXT NOT NULL DEFAULT '[]',
	seen_pool_pct REAL NOT NULL DEFAULT 0,
	uploaded_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS run_shop_visits (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	run_id TEXT NOT NULL,
	session_num INTEGER NOT NULL,
	offered TEXT NOT NULL DEFAULT '[]',
	utility_offered TEXT NOT NULL DEFAULT '[]',
	bought TEXT NOT NULL DEFAULT '[]',
	reroll_count INTEGER NOT NULL DEFAULT 0,
	seen_pool_pct REAL NOT NULL DEFAULT 0,
	gold_enter INTEGER NOT NULL DEFAULT 0,
	gold_after_payday INTEGER NOT NULL DEFAULT 0,
	gold_exit INTEGER NOT NULL DEFAULT 0,
	ts TEXT
);
CREATE INDEX IF NOT EXISTS idx_visits_run ON run_shop_visits(run_id);

CREATE TABLE IF NOT EXISTS run_combats (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	run_id TEXT NOT NULL,
	session_num INTEGER NOT NULL,
	won INTEGER NOT NULL,
	hearts_left INTEGER NOT NULL DEFAULT 0,
	rounds INTEGER NOT NULL DEFAULT 0,
	opponent_deck_id INTEGER,
	per_card TEXT NOT NULL DEFAULT '[]',
	ts TEXT
);
CREATE INDEX IF NOT EXISTS idx_combats_run ON run_combats(run_id);

CREATE TABLE IF NOT EXISTS card_catalog (
	game_version TEXT NOT NULL,
	card_type_id TEXT NOT NULL,
	name TEXT NOT NULL DEFAULT '',
	tags TEXT NOT NULL DEFAULT '[]',
	rarity TEXT NOT NULL DEFAULT '',
	cost INTEGER NOT NULL DEFAULT 0,
	updated_at TEXT NOT NULL,
	PRIMARY KEY (game_version, card_type_id)
);
`);

// S0 (2026-09-04): migrate dbs created before the new columns (e.g. the live ECS db).
// Fresh dbs already get the columns from CREATE TABLE above; this is a no-op there.
function ensureColumn(table, columnDdl)
{
	const cols = db.prepare('PRAGMA table_info(' + table + ')').all().map((c) => c.name);
	const name = columnDdl.split(/\s+/)[0];
	if (!cols.includes(name)) db.exec('ALTER TABLE ' + table + ' ADD COLUMN ' + columnDdl);
}
ensureColumn('run_combats', 'rounds INTEGER NOT NULL DEFAULT 0');
ensureColumn('run_combats', 'opponent_deck_id INTEGER');
ensureColumn('run_combats', "series TEXT NOT NULL DEFAULT '[]'");
// Nullable-with-default: the upsert INSERTs NULL when a legacy client omits enemySource,
// so these must accept NULL (COALESCE in DO UPDATE keeps the stored value then).
ensureColumn('stats_meta', 'enemy_source_server INTEGER DEFAULT 0');
ensureColumn('stats_meta', 'enemy_source_local INTEGER DEFAULT 0');
ensureColumn('stats_meta', 'enemy_source_pool INTEGER DEFAULT 0');

const stmts = {
	playerById: db.prepare('SELECT * FROM players WHERE player_id = ?'),
	playerByName: db.prepare('SELECT * FROM players WHERE username_norm = ?'),
	insertPlayer: db.prepare('INSERT INTO players (player_id, username, username_norm, created_at) VALUES (?, ?, ?, ?)'),

	insertDeck: db.prepare(`INSERT INTO decks
		(player_id, username, game_version, session_num, hp_max, win_amount, heart_left, card_type_ids, created_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`),
	deckById: db.prepare('SELECT * FROM decks WHERE deck_id = ?'),
	randomDecks: db.prepare(`SELECT * FROM decks
		WHERE game_version = ? AND session_num = ? AND player_id != ?
		ORDER BY RANDOM() LIMIT ?`),
	deckDefenseWin: db.prepare('UPDATE decks SET defense_wins = defense_wins + 1 WHERE deck_id = ?'),
	deckDefenseLoss: db.prepare('UPDATE decks SET defense_losses = defense_losses + 1 WHERE deck_id = ?'),

	insertReport: db.prepare(`INSERT OR IGNORE INTO match_reports
		(report_id, player_id, opponent_deck_id, won, session_num, game_version, created_at)
		VALUES (?, ?, ?, ?, ?, ?, ?)`),

	upsertSnapshot: db.prepare(`INSERT INTO stats_snapshots
		(player_id, kind, game_version, card_type_id, session_num,
		 appear, bought, util_appear, util_bought, combats, wins, losses, updated_at)
		VALUES (@playerId, @kind, @gameVersion, @cardTypeId, @sessionNum,
		 @appear, @bought, @utilAppear, @utilBought, @combats, @wins, @losses, @updatedAt)
		ON CONFLICT (player_id, kind, game_version, card_type_id, session_num)
		DO UPDATE SET appear = excluded.appear, bought = excluded.bought,
			util_appear = excluded.util_appear, util_bought = excluded.util_bought,
			combats = excluded.combats, wins = excluded.wins, losses = excluded.losses,
			updated_at = excluded.updated_at`),
	upsertMeta: db.prepare(`INSERT INTO stats_meta (player_id, game_version, total_shop_visits, total_rerolls,
		enemy_source_server, enemy_source_local, enemy_source_pool, updated_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?)
		ON CONFLICT (player_id, game_version)
		DO UPDATE SET total_shop_visits = excluded.total_shop_visits,
			total_rerolls = excluded.total_rerolls,
			enemy_source_server = COALESCE(excluded.enemy_source_server, stats_meta.enemy_source_server),
			enemy_source_local = COALESCE(excluded.enemy_source_local, stats_meta.enemy_source_local),
			enemy_source_pool = COALESCE(excluded.enemy_source_pool, stats_meta.enemy_source_pool),
			updated_at = excluded.updated_at`),
	insertBatch: db.prepare('INSERT INTO snapshot_batches (player_id, game_version, uploaded_at, payload) VALUES (?, ?, ?, ?)'),

	insertRun: db.prepare(`INSERT OR IGNORE INTO runs
		(run_id, player_id, game_version, started_at, ended_at, result,
		 final_session, hearts_left, final_deck, seen_pool_pct, uploaded_at)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`),
	insertVisit: db.prepare(`INSERT INTO run_shop_visits
		(run_id, session_num, offered, utility_offered, bought, reroll_count,
		 seen_pool_pct, gold_enter, gold_after_payday, gold_exit, ts)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`),
	insertCombat: db.prepare(`INSERT INTO run_combats
		(run_id, session_num, won, hearts_left, rounds, opponent_deck_id, per_card, series, ts)
		VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`),

	upsertCatalog: db.prepare(`INSERT INTO card_catalog
		(game_version, card_type_id, name, tags, rarity, cost, updated_at)
		VALUES (?, ?, ?, ?, ?, ?, ?)
		ON CONFLICT (game_version, card_type_id)
		DO UPDATE SET name = excluded.name, tags = excluded.tags,
			rarity = excluded.rarity, cost = excluded.cost, updated_at = excluded.updated_at`),
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function nowIso()
{
	return new Date().toISOString();
}

function isStr(v, min, max)
{
	return typeof v === 'string' && v.length >= min && v.length <= max;
}

function toInt(v, min, max, dflt)
{
	const n = typeof v === 'number' ? v : parseInt(v, 10);
	if (!Number.isFinite(n) || n < min || n > max) return dflt;
	return Math.trunc(n);
}

function toFrac(v)
{
	const n = typeof v === 'number' ? v : parseFloat(v);
	if (!Number.isFinite(n)) return 0;
	return Math.min(1, Math.max(0, n));
}

function strArray(v, maxItems, maxLen)
{
	if (!Array.isArray(v) || v.length > maxItems) return null;
	const out = [];
	for (const item of v)
	{
		if (typeof item !== 'string' || item.length < 1 || item.length > maxLen) return null;
		out.push(item);
	}
	return out;
}

function badRequest(res, code)
{
	return res.status(400).json({ error: code });
}

function getPlayer(playerId)
{
	if (!isStr(playerId, 8, 64)) return null;
	return stmts.playerById.get(playerId) || null;
}

function requirePlayer(req, res)
{
	const playerId = req.method === 'GET' ? req.query.playerId : (req.body && req.body.playerId);
	const player = getPlayer(playerId);
	if (!player)
	{
		res.status(401).json({ error: 'unknown_player' });
		return null;
	}
	return player;
}

// Tiny in-memory per-IP rate limiter (no extra dependency).
function makeRateLimiter(max, windowMs)
{
	const hits = new Map();
	setInterval(() =>
	{
		const now = Date.now();
		for (const [k, v] of hits) if (v.resetAt <= now) hits.delete(k);
	}, windowMs).unref();
	return (req, res, next) =>
	{
		const key = req.ip || 'unknown';
		const now = Date.now();
		let rec = hits.get(key);
		if (!rec || rec.resetAt <= now)
		{
			rec = { count: 0, resetAt: now + windowMs };
			hits.set(key, rec);
		}
		rec.count++;
		if (rec.count > max) return res.status(429).json({ error: 'rate_limited' });
		next();
	};
}

// ---------------------------------------------------------------------------
// App & middleware
// ---------------------------------------------------------------------------

const app = express();
app.disable('x-powered-by');
app.set('trust proxy', true); // real client IP arrives via nginx X-Forwarded-For
app.use(express.json({ limit: '256kb' }));
app.use((err, req, res, next) =>
{
	if (err && err.type === 'entity.parse.failed') return badRequest(res, 'invalid_json');
	if (err && err.type === 'entity.too.large') return res.status(413).json({ error: 'payload_too_large' });
	next(err);
});
app.use('/api', makeRateLimiter(300, 60 * 1000));
const registerLimiter = makeRateLimiter(10, 60 * 60 * 1000);

// ---------------------------------------------------------------------------
// API: players
// ---------------------------------------------------------------------------

app.post('/api/players/register', registerLimiter, (req, res) =>
{
	const username = typeof (req.body && req.body.username) === 'string' ? req.body.username.trim() : '';
	const charCount = [...username].length;
	if (charCount < 2 || charCount > 16 || /[\x00-\x1f\x7f]/.test(username))
	{
		return badRequest(res, 'invalid_username');
	}
	const norm = username.toLowerCase();
	if (stmts.playerByName.get(norm)) return res.status(409).json({ error: 'username_taken' });
	const playerId = crypto.randomUUID();
	stmts.insertPlayer.run(playerId, username, norm, nowIso());
	return res.status(201).json({ playerId, username });
});

// ---------------------------------------------------------------------------
// API: ghost decks
// ---------------------------------------------------------------------------

app.post('/api/decks', (req, res) =>
{
	const player = requirePlayer(req, res);
	if (!player) return;
	const ids = strArray(req.body.cardTypeIDs, 100, 64);
	if (!ids || ids.length === 0) return badRequest(res, 'invalid_card_list');
	if (!isStr(req.body.gameVersion, 1, 32)) return badRequest(res, 'invalid_game_version');
	const sessionNum = toInt(req.body.sessionNum, 0, 99, -1);
	if (sessionNum < 0) return badRequest(res, 'invalid_session');
	const hpMax = toInt(req.body.hpMax, 0, 9999, 0);
	const winAmount = toInt(req.body.winAmount, 0, 999, 0);
	const heartLeft = toInt(req.body.heartLeft, 0, 99, 0);
	const info = stmts.insertDeck.run(
		player.player_id, player.username, req.body.gameVersion, sessionNum,
		hpMax, winAmount, heartLeft, JSON.stringify(ids), nowIso());
	return res.status(201).json({ deckId: Number(info.lastInsertRowid) });
});

app.get('/api/decks/opponents', (req, res) =>
{
	const player = requirePlayer(req, res);
	if (!player) return;
	if (!isStr(req.query.gameVersion, 1, 32)) return badRequest(res, 'invalid_game_version');
	const maxSession = toInt(req.query.maxSession, 0, 30, 5);
	const perSession = toInt(req.query.perSession, 1, 5, 2);
	const decks = [];
	for (let s = 0; s <= maxSession; s++)
	{
		const rows = stmts.randomDecks.all(req.query.gameVersion, s, player.player_id, perSession);
		for (const r of rows)
		{
			decks.push({
				deckId: r.deck_id,
				sessionNum: r.session_num,
				username: r.username,
				cardTypeIDs: JSON.parse(r.card_type_ids),
				hpMax: r.hp_max,
				winAmount: r.win_amount,
				heartLeft: r.heart_left,
				defenseWins: r.defense_wins,
				defenseLosses: r.defense_losses,
			});
		}
	}
	return res.json({ decks });
});

// ---------------------------------------------------------------------------
// API: match reports
// ---------------------------------------------------------------------------

app.post('/api/matches/report', (req, res) =>
{
	const player = requirePlayer(req, res);
	if (!player) return;
	if (!isStr(req.body.reportId, 8, 64)) return badRequest(res, 'invalid_report_id');
	const deckId = toInt(req.body.opponentDeckId, 1, Number.MAX_SAFE_INTEGER, -1);
	if (deckId < 0) return badRequest(res, 'invalid_deck_id');
	const deck = stmts.deckById.get(deckId);
	if (!deck) return res.status(404).json({ error: 'deck_not_found' });
	if (deck.player_id === player.player_id) return badRequest(res, 'own_deck');
	const won = req.body.won === true ? 1 : 0;
	const sessionNum = toInt(req.body.sessionNum, 0, 99, deck.session_num);
	const gameVersion = isStr(req.body.gameVersion, 1, 32) ? req.body.gameVersion : deck.game_version;
	const info = stmts.insertReport.run(
		req.body.reportId, player.player_id, deckId, won, sessionNum, gameVersion, nowIso());
	if (info.changes === 0) return res.json({ ok: true, deduped: true });
	if (won) stmts.deckDefenseLoss.run(deckId);
	else stmts.deckDefenseWin.run(deckId);
	return res.status(201).json({ ok: true });
});

// ---------------------------------------------------------------------------
// API: cumulative stats snapshots (idempotent upsert)
// ---------------------------------------------------------------------------

app.post('/api/stats/snapshot', (req, res) =>
{
	const player = requirePlayer(req, res);
	if (!player) return;
	if (!isStr(req.body.gameVersion, 1, 32)) return badRequest(res, 'invalid_game_version');
	const gameVersion = req.body.gameVersion;
	const shop = Array.isArray(req.body.shop) ? req.body.shop.slice(0, 5000) : [];
	const winrate = Array.isArray(req.body.winrate) ? req.body.winrate.slice(0, 5000) : [];

	let rows = 0;
	const tx = db.transaction(() =>
	{
		for (const r of shop)
		{
			if (!r || !isStr(r.cardTypeID, 1, 64)) continue;
			stmts.upsertSnapshot.run({
				playerId: player.player_id, kind: 'shop', gameVersion,
				cardTypeId: r.cardTypeID, sessionNum: toInt(r.sessionNum, 0, 99, 0),
				appear: toInt(r.appear, 0, 1e9, 0), bought: toInt(r.bought, 0, 1e9, 0),
				utilAppear: toInt(r.utilAppear, 0, 1e9, 0), utilBought: toInt(r.utilBought, 0, 1e9, 0),
				combats: 0, wins: 0, losses: 0, updatedAt: nowIso(),
			});
			rows++;
		}
		for (const r of winrate)
		{
			if (!r || !isStr(r.cardTypeID, 1, 64)) continue;
			stmts.upsertSnapshot.run({
				playerId: player.player_id, kind: 'winrate', gameVersion,
				cardTypeId: r.cardTypeID, sessionNum: toInt(r.sessionNum, 0, 99, 0),
				appear: 0, bought: 0, utilAppear: 0, utilBought: 0,
				combats: toInt(r.combats, 0, 1e9, 0), wins: toInt(r.wins, 0, 1e9, 0),
				losses: toInt(r.losses, 0, 1e9, 0), updatedAt: nowIso(),
			});
			rows++;
		}
		if (req.body.meta && typeof req.body.meta === 'object')
		{
			// enemySource absent (older clients) -> null -> COALESCE keeps existing counters.
			const src = (typeof req.body.meta.enemySource === 'object' && req.body.meta.enemySource) || null;
			stmts.upsertMeta.run(player.player_id, gameVersion,
				toInt(req.body.meta.totalShopVisits, 0, 1e9, 0),
				toInt(req.body.meta.totalRerolls, 0, 1e9, 0),
				src ? toInt(src.server, 0, 1e9, 0) : null,
				src ? toInt(src.local, 0, 1e9, 0) : null,
				src ? toInt(src.pool, 0, 1e9, 0) : null, nowIso());
		}
		const raw = JSON.stringify(req.body);
		stmts.insertBatch.run(player.player_id, gameVersion, nowIso(), raw.slice(0, 200 * 1024));
	});
	tx();
	return res.json({ ok: true, rows });
});

// ---------------------------------------------------------------------------
// API: per-run records (idempotent by runId)
// ---------------------------------------------------------------------------

const RUN_RESULTS = new Set(['victory', 'defeat', 'abandoned']);

app.post('/api/runs', (req, res) =>
{
	const player = requirePlayer(req, res);
	if (!player) return;
	if (!isStr(req.body.runId, 8, 64)) return badRequest(res, 'invalid_run_id');
	if (!isStr(req.body.gameVersion, 1, 32)) return badRequest(res, 'invalid_game_version');
	if (!RUN_RESULTS.has(req.body.result)) return badRequest(res, 'invalid_result');
	const finalDeck = strArray(req.body.finalDeck || [], 200, 64);
	if (finalDeck === null) return badRequest(res, 'invalid_final_deck');
	const shopVisits = Array.isArray(req.body.shopVisits) ? req.body.shopVisits.slice(0, 100) : [];
	const combats = Array.isArray(req.body.combats) ? req.body.combats.slice(0, 100) : [];

	const visits = [];
	for (const v of shopVisits)
	{
		if (!v || typeof v !== 'object') return badRequest(res, 'invalid_shop_visit');
		const offered = strArray(v.offered || [], 50, 64);
		const utilityOffered = strArray(v.utilityOffered || [], 50, 64);
		const bought = strArray(v.bought || [], 50, 64);
		if (offered === null || utilityOffered === null || bought === null) return badRequest(res, 'invalid_shop_visit');
		visits.push({
			sessionNum: toInt(v.sessionNum, 0, 99, 0),
			offered, utilityOffered, bought,
			rerollCount: toInt(v.rerollCount, 0, 99, 0),
			seenPoolPct: toFrac(v.seenPoolPct),
			goldEnter: toInt(v.goldEnter, 0, 999999, 0),
			goldAfterPayday: toInt(v.goldAfterPayday, 0, 999999, 0),
			goldExit: toInt(v.goldExit, 0, 999999, 0),
			ts: isStr(v.ts, 1, 40) ? v.ts : null,
		});
	}

	const combatsClean = [];
	for (const c of combats)
	{
		if (!c || typeof c !== 'object') return badRequest(res, 'invalid_combat');
		const perCard = Array.isArray(c.perCard) ? c.perCard.slice(0, 200) : [];
		const perCardClean = [];
		for (const p of perCard)
		{
			if (!p || !isStr(p.cardTypeID, 1, 64)) return badRequest(res, 'invalid_combat');
			// S0 splits damage by side; legacy payloads with only damageDealt map onto damageToOpponent.
			let dmgOpp;
			if (p.damageToOpponent === undefined && p.damageToSelf === undefined && p.damageDealt !== undefined)
			{
				dmgOpp = toInt(p.damageDealt, 0, 1e9, 0);
			}
			else
			{
				dmgOpp = toInt(p.damageToOpponent, 0, 1e9, 0);
			}
			perCardClean.push({
				cardTypeID: p.cardTypeID,
				triggers: toInt(p.triggers, 0, 1e6, 0),
				damageToOpponent: dmgOpp,
				damageToSelf: toInt(p.damageToSelf, 0, 1e9, 0),
			});
		}
		const opponentDeckId = toInt(c.opponentDeckId, 1, Number.MAX_SAFE_INTEGER, 0);
		// Per-reveal combat series (HP/shield/deck curves + reveal sequence). Legacy
		// clients omit the field entirely; cap samples at 500 per combat.
		const series = Array.isArray(c.series) ? c.series.slice(0, 500) : [];
		const seriesClean = [];
		for (const s of series)
		{
			if (!s || typeof s !== 'object') return badRequest(res, 'invalid_combat');
			seriesClean.push({
				revealIndex: toInt(s.revealIndex, 0, 1e6, 0),
				roundNum: toInt(s.roundNum, 0, 999, 0),
				ownerHP: toInt(s.ownerHP, 0, 1e9, 0),
				enemyHP: toInt(s.enemyHP, 0, 1e9, 0),
				ownerShield: toInt(s.ownerShield, 0, 1e9, 0),
				enemyShield: toInt(s.enemyShield, 0, 1e9, 0),
				ownerDeckSize: toInt(s.ownerDeckSize, 0, 999, 0),
				enemyDeckSize: toInt(s.enemyDeckSize, 0, 999, 0),
				side: toInt(s.side, 0, 2, 0),
				cardTypeID: isStr(s.cardTypeID, 0, 64) ? s.cardTypeID : '',
			});
		}
		combatsClean.push({
			sessionNum: toInt(c.sessionNum, 0, 99, 0),
			won: c.won === true ? 1 : 0,
			heartsLeft: toInt(c.heartsLeft, 0, 99, 0),
			rounds: toInt(c.rounds, 0, 999, 0),
			opponentDeckId: opponentDeckId > 0 ? opponentDeckId : null,
			perCard: perCardClean,
			series: seriesClean,
			ts: isStr(c.ts, 1, 40) ? c.ts : null,
		});
	}

	if (combatsClean.length === 0)
	{
		// Zero-combat runs (quit before the first fight) are never stored. Respond ok
		// (not 4xx) so the client outbox drops the item instead of retrying forever.
		console.log('[onedeck-api] run skipped (no combats): ' + req.body.runId);
		return res.json({ ok: true, skipped: true });
	}

	const tx = db.transaction(() =>
	{
		const info = stmts.insertRun.run(
			req.body.runId, player.player_id, req.body.gameVersion,
			isStr(req.body.startedAt, 1, 40) ? req.body.startedAt : null,
			isStr(req.body.endedAt, 1, 40) ? req.body.endedAt : null,
			req.body.result,
			toInt(req.body.finalSession, 0, 99, 0),
			toInt(req.body.heartsLeft, 0, 99, 0),
			JSON.stringify(finalDeck), toFrac(req.body.seenPoolPct), nowIso());
		if (info.changes === 0) return false;
		for (const v of visits)
		{
			stmts.insertVisit.run(req.body.runId, v.sessionNum,
				JSON.stringify(v.offered), JSON.stringify(v.utilityOffered), JSON.stringify(v.bought),
				v.rerollCount, v.seenPoolPct, v.goldEnter, v.goldAfterPayday, v.goldExit, v.ts);
		}
		for (const c of combatsClean)
		{
			stmts.insertCombat.run(req.body.runId, c.sessionNum, c.won, c.heartsLeft, c.rounds,
				c.opponentDeckId, JSON.stringify(c.perCard), JSON.stringify(c.series), c.ts);
		}
		return true;
	});
	const inserted = tx();
	if (!inserted) return res.json({ ok: true, deduped: true });
	return res.status(201).json({ ok: true });
});

// ---------------------------------------------------------------------------
// API: card catalog (metadata for server-side analysis)
// ---------------------------------------------------------------------------

app.post('/api/cards/catalog', (req, res) =>
{
	const player = requirePlayer(req, res);
	if (!player) return;
	if (!isStr(req.body.gameVersion, 1, 32)) return badRequest(res, 'invalid_game_version');
	const cards = Array.isArray(req.body.cards) ? req.body.cards.slice(0, 1000) : [];
	if (cards.length === 0) return badRequest(res, 'empty_catalog');
	let rows = 0;
	const tx = db.transaction(() =>
	{
		for (const c of cards)
		{
			if (!c || !isStr(c.cardTypeID, 1, 64)) continue;
			const tags = strArray(c.tags || [], 10, 32) || [];
			stmts.upsertCatalog.run(
				req.body.gameVersion, c.cardTypeID,
				isStr(c.name, 0, 64) ? c.name : '',
				JSON.stringify(tags),
				isStr(c.rarity, 0, 32) ? c.rarity : '',
				toInt(c.cost, 0, 999, 0), nowIso());
			rows++;
		}
	});
	tx();
	return res.json({ ok: true, rows });
});

// ---------------------------------------------------------------------------
// API: health
// ---------------------------------------------------------------------------

app.get('/api/health', (req, res) =>
{
	res.json({ ok: true, uptime: Math.round(process.uptime()), time: nowIso() });
});

// ---------------------------------------------------------------------------
// Admin dashboard (token-gated, server-rendered HTML)
// ---------------------------------------------------------------------------

function requireAdmin(req, res, next)
{
	if (req.query.token !== ADMIN_TOKEN) return res.status(403).send('Forbidden');
	next();
}

function esc(s)
{
	return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;')
		.replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

const ADMIN_CSS = 'body{background:#0f1115;color:#e6e8ec;font:14px/1.6 sans-serif;margin:24px;}'
	+ 'h1{font-size:22px}h2{font-size:17px;color:#7cc4ff;margin-top:28px;border-bottom:1px solid #2a2f3a;padding-bottom:6px}'
	+ 'table{border-collapse:collapse;margin:8px 0}th,td{border:1px solid #2a2f3a;padding:5px 10px;text-align:left}'
	+ 'th{background:#161a22}td.num{text-align:right}.muted{color:#9aa3b2}'
	+ '.cards{display:flex;gap:12px;flex-wrap:wrap}.card{background:#161a22;border:1px solid #2a2f3a;border-radius:8px;padding:10px 16px}'
	+ '.card b{font-size:20px;display:block}a{color:#7cc4ff}';

function adminPage(title, token, bodyHtml)
{
	return '<!DOCTYPE html><html><head><meta charset="UTF-8"><title>' + esc(title) + '</title><style>'
		+ ADMIN_CSS + '</style></head><body><h1>' + esc(title) + '</h1>' + bodyHtml
		+ '<p class="muted">OneDeck API admin &middot; <a href="/admin?token=' + esc(token) + '">dashboard</a></p></body></html>';
}

// Resolve display names from the catalog version with the most rows.
function loadCatalogMap()
{
	const best = db.prepare('SELECT game_version, COUNT(*) AS c FROM card_catalog GROUP BY game_version ORDER BY c DESC LIMIT 1').get();
	const map = new Map();
	if (!best) return map;
	const rows = db.prepare('SELECT card_type_id, name, tags FROM card_catalog WHERE game_version = ?').all(best.game_version);
	for (const r of rows)
	{
		let tags = [];
		try { tags = JSON.parse(r.tags); } catch { /* keep empty */ }
		map.set(r.card_type_id, { name: r.name || r.card_type_id, tags });
	}
	return map;
}

function cardName(catalog, id)
{
	const c = catalog.get(id);
	return c ? c.name : id;
}

// Legacy per-card rows merged damage into damageDealt; S0+ rows carry the split fields.
function dmgOpp(p)
{
	return typeof p.damageToOpponent === 'number' ? p.damageToOpponent : (p.damageDealt || 0);
}

// Owner HP (red) vs enemy HP (orange, shield-inclusive) sparkline across the reveal series.
function hpCurveSvg(series)
{
	if (!Array.isArray(series) || series.length < 2) return '<span class="muted">-</span>';
	const w = 160, h = 40, pad = 2;
	let maxVal = 1;
	for (const s of series)
	{
		maxVal = Math.max(maxVal, s.ownerHP + (s.ownerShield || 0), s.enemyHP + (s.enemyShield || 0));
	}
	const firstIdx = series[0].revealIndex || 0;
	const span = Math.max(1, (series[series.length - 1].revealIndex || firstIdx) - firstIdx);
	const point = (s, hp, shield) =>
	{
		const x = pad + (w - 2 * pad) * (((s.revealIndex || firstIdx) - firstIdx) / span);
		const y = h - pad - (h - 2 * pad) * ((hp + shield) / maxVal);
		return x.toFixed(1) + ',' + y.toFixed(1);
	};
	const ownerLine = series.map((s) => point(s, s.ownerHP || 0, s.ownerShield || 0)).join(' ');
	const enemyLine = series.map((s) => point(s, s.enemyHP || 0, s.enemyShield || 0)).join(' ');
	return '<svg width="' + w + '" height="' + h + '" style="vertical-align:middle">'
		+ '<polyline fill="none" stroke="#e74c3c" stroke-width="1.5" points="' + ownerLine + '"/>'
		+ '<polyline fill="none" stroke="#f39c12" stroke-width="1.5" points="' + enemyLine + '"/>'
		+ '</svg>';
}

app.get('/admin', requireAdmin, (req, res) =>
{
	const token = req.query.token;
	const catalog = loadCatalogMap();
	const count = (sql) => db.prepare(sql).get().c;

	// Overview cards
	const overview = '<div class="cards">'
		+ '<div class="card"><b>' + count('SELECT COUNT(*) AS c FROM players') + '</b>players</div>'
		+ '<div class="card"><b>' + count('SELECT COUNT(*) AS c FROM decks') + '</b>ghost decks</div>'
		+ '<div class="card"><b>' + count('SELECT COUNT(*) AS c FROM match_reports') + '</b>match reports</div>'
		+ '<div class="card"><b>' + count('SELECT COUNT(*) AS c FROM runs') + '</b>runs</div>'
		+ '<div class="card"><b>' + count('SELECT COUNT(DISTINCT game_version) AS c FROM decks') + '</b>versions</div>'
		+ '</div>';

	// Shop stats aggregated over latest snapshots of all players
	let shopHtml = '<h2>Shop stats (seen / bought, per session)</h2>';
	const shopRows = db.prepare(`SELECT game_version, card_type_id, session_num,
		SUM(appear) AS appear, SUM(bought) AS bought,
		SUM(util_appear) AS util_appear, SUM(util_bought) AS util_bought
		FROM stats_snapshots WHERE kind = 'shop'
		GROUP BY game_version, card_type_id, session_num
		ORDER BY game_version, session_num, appear DESC`).all();
	if (shopRows.length === 0)
	{
		shopHtml += '<p class="muted">no data yet</p>';
	}
	else
	{
		shopHtml += '<table><tr><th>version</th><th>session</th><th>card</th><th>seen</th><th>bought</th><th>buy rate</th><th>util seen</th><th>util bought</th></tr>';
		for (const r of shopRows)
		{
			const rate = r.appear > 0 ? (100 * r.bought / r.appear).toFixed(1) + '%' : '-';
			shopHtml += '<tr><td>' + esc(r.game_version) + '</td><td class="num">' + r.session_num + '</td><td>'
				+ esc(cardName(catalog, r.card_type_id)) + '</td><td class="num">' + r.appear + '</td><td class="num">'
				+ r.bought + '</td><td class="num">' + rate + '</td><td class="num">' + r.util_appear
				+ '</td><td class="num">' + r.util_bought + '</td></tr>';
		}
		shopHtml += '</table>';
	}

	// Win rates aggregated over latest snapshots of all players
	let winHtml = '<h2>Card win rates (per session)</h2>';
	const winRows = db.prepare(`SELECT game_version, card_type_id, session_num,
		SUM(combats) AS combats, SUM(wins) AS wins, SUM(losses) AS losses
		FROM stats_snapshots WHERE kind = 'winrate'
		GROUP BY game_version, card_type_id, session_num
		ORDER BY game_version, session_num, combats DESC`).all();
	if (winRows.length === 0)
	{
		winHtml += '<p class="muted">no data yet</p>';
	}
	else
	{
		winHtml += '<table><tr><th>version</th><th>session</th><th>card</th><th>combats</th><th>wins</th><th>losses</th><th>win rate</th></tr>';
		for (const r of winRows)
		{
			const rate = r.combats > 0 ? (100 * r.wins / r.combats).toFixed(1) + '%' : '-';
			winHtml += '<tr><td>' + esc(r.game_version) + '</td><td class="num">' + r.session_num + '</td><td>'
				+ esc(cardName(catalog, r.card_type_id)) + '</td><td class="num">' + r.combats + '</td><td class="num">'
				+ r.wins + '</td><td class="num">' + r.losses + '</td><td class="num">' + rate + '</td></tr>';
		}
		winHtml += '</table>';
	}

	// Ghost decks: defense record per session
	let deckHtml = '<h2>Ghost decks</h2>';
	const deckRows = db.prepare(`SELECT game_version, session_num, COUNT(*) AS n,
		SUM(defense_wins) AS dw, SUM(defense_losses) AS dl
		FROM decks GROUP BY game_version, session_num ORDER BY game_version, session_num`).all();
	if (deckRows.length === 0)
	{
		deckHtml += '<p class="muted">no decks yet</p>';
	}
	else
	{
		deckHtml += '<table><tr><th>version</th><th>session</th><th>decks</th><th>defense wins</th><th>defense losses</th><th>defense win rate</th></tr>';
		for (const r of deckRows)
		{
			const total = r.dw + r.dl;
			const rate = total > 0 ? (100 * r.dw / total).toFixed(1) + '%' : '-';
			deckHtml += '<tr><td>' + esc(r.game_version) + '</td><td class="num">' + r.session_num + '</td><td class="num">'
				+ r.n + '</td><td class="num">' + r.dw + '</td><td class="num">' + r.dl + '</td><td class="num">' + rate + '</td></tr>';
		}
		deckHtml += '</table>';
	}

	// Enemy deck source coverage (S0 telemetry): server ghosts vs local-json fallback vs default pool.
	let srcHtml = '<h2>Enemy deck sources (lifetime per-player counters)</h2>';
	const src = db.prepare(`SELECT COALESCE(SUM(enemy_source_server), 0) AS srv,
		COALESCE(SUM(enemy_source_local), 0) AS loc, COALESCE(SUM(enemy_source_pool), 0) AS pool
		FROM stats_meta`).get();
	if (src.srv + src.loc + src.pool === 0)
	{
		srcHtml += '<p class="muted">no data yet</p>';
	}
	else
	{
		srcHtml += '<table><tr><th>server ghosts</th><th>local json fallback</th><th>default pool</th></tr>'
			+ '<tr><td class="num">' + src.srv + '</td><td class="num">' + src.loc + '</td><td class="num">'
			+ src.pool + '</td></tr></table>';
	}

	// Recent runs with archetype derived from final deck tags
	let runHtml = '<h2>Recent runs</h2>';
	const runRows = db.prepare(`SELECT r.*, p.username FROM runs r LEFT JOIN players p ON p.player_id = r.player_id
		ORDER BY r.uploaded_at DESC LIMIT 100`).all();
	const archetypes = new Map(); // tag -> {runs, victories, sumSession}
	if (runRows.length === 0)
	{
		runHtml += '<p class="muted">no runs yet</p>';
	}
	else
	{
		runHtml += '<table><tr><th>run</th><th>player</th><th>version</th><th>result</th><th>final session</th><th>hearts</th><th>deck</th><th>pool seen</th><th>archetype</th><th>uploaded</th></tr>';
		for (const r of runRows)
		{
			let deck = [];
			try { deck = JSON.parse(r.final_deck); } catch { /* keep empty */ }
			const tagCounts = new Map();
			for (const id of deck)
			{
				const c = catalog.get(id);
				if (!c) continue;
				for (const t of c.tags) tagCounts.set(t, (tagCounts.get(t) || 0) + 1);
			}
			let dominant = '-';
			let bestN = 0;
			for (const [t, n] of tagCounts) if (n > bestN) { dominant = t; bestN = n; }
			const agg = archetypes.get(dominant) || { runs: 0, victories: 0, sumSession: 0 };
			agg.runs++;
			if (r.result === 'victory') agg.victories++;
			agg.sumSession += r.final_session;
			archetypes.set(dominant, agg);
			runHtml += '<tr><td><a href="/admin/run/' + encodeURIComponent(r.run_id) + '?token=' + esc(token) + '">'
				+ esc(r.run_id.slice(0, 8)) + '</a></td><td>' + esc(r.username || '?') + '</td><td>' + esc(r.game_version)
				+ '</td><td>' + esc(r.result) + '</td><td class="num">' + r.final_session + '</td><td class="num">'
				+ r.hearts_left + '</td><td class="num">' + deck.length + '</td><td class="num">'
				+ (100 * r.seen_pool_pct).toFixed(0) + '%</td><td>' + esc(dominant) + '</td><td class="muted">'
				+ esc(r.uploaded_at) + '</td></tr>';
		}
		runHtml += '</table>';
	}

	// Archetype summary
	let archHtml = '<h2>Archetypes (dominant tag of final deck)</h2>';
	if (archetypes.size === 0)
	{
		archHtml += '<p class="muted">no runs yet (or card catalog not uploaded)</p>';
	}
	else
	{
		archHtml += '<table><tr><th>archetype</th><th>runs</th><th>victories</th><th>victory rate</th><th>avg final session</th></tr>';
		const sorted = [...archetypes.entries()].sort((a, b) => b[1].runs - a[1].runs);
		for (const [tag, a] of sorted)
		{
			archHtml += '<tr><td>' + esc(tag) + '</td><td class="num">' + a.runs + '</td><td class="num">' + a.victories
				+ '</td><td class="num">' + (100 * a.victories / a.runs).toFixed(1) + '%</td><td class="num">'
				+ (a.sumSession / a.runs).toFixed(1) + '</td></tr>';
		}
		archHtml += '</table>';
	}

	res.send(adminPage('OneDeck Admin', token, overview + archHtml + shopHtml + winHtml + deckHtml + srcHtml + runHtml));
});

app.get('/admin/run/:id', requireAdmin, (req, res) =>
{
	const token = req.query.token;
	const catalog = loadCatalogMap();
	const run = db.prepare('SELECT r.*, p.username FROM runs r LEFT JOIN players p ON p.player_id = r.player_id WHERE r.run_id = ?').get(req.params.id);
	if (!run) return res.status(404).send(adminPage('Run not found', token, '<p>unknown run id</p>'));

	let deck = [];
	try { deck = JSON.parse(run.final_deck); } catch { /* keep empty */ }
	let html = '<p class="muted">' + esc(run.run_id) + '</p>'
		+ '<div class="cards">'
		+ '<div class="card"><b>' + esc(run.username || '?') + '</b>player</div>'
		+ '<div class="card"><b>' + esc(run.result) + '</b>result</div>'
		+ '<div class="card"><b>' + run.final_session + '</b>final session</div>'
		+ '<div class="card"><b>' + run.hearts_left + '</b>hearts left</div>'
		+ '<div class="card"><b>' + (100 * run.seen_pool_pct).toFixed(0) + '%</b>pool seen</div>'
		+ '</div>'
		+ '<h2>Final deck (' + deck.length + ')</h2><p>'
		+ esc(deck.map((id) => cardName(catalog, id)).join(', ')) + '</p>';

	html += '<h2>Shop visits</h2>';
	const visits = db.prepare('SELECT * FROM run_shop_visits WHERE run_id = ? ORDER BY session_num, id').all(run.run_id);
	if (visits.length === 0)
	{
		html += '<p class="muted">none</p>';
	}
	else
	{
		html += '<table><tr><th>session</th><th>offered</th><th>bought</th><th>rerolls</th><th>pool seen</th><th>gold in</th><th>after payday</th><th>gold out</th></tr>';
		for (const v of visits)
		{
			let offered = [];
			let bought = [];
			try { offered = JSON.parse(v.offered); } catch { /* keep empty */ }
			try { bought = JSON.parse(v.bought); } catch { /* keep empty */ }
			html += '<tr><td class="num">' + v.session_num + '</td><td>'
				+ esc(offered.map((id) => cardName(catalog, id)).join(', ')) + '</td><td>'
				+ esc(bought.map((id) => cardName(catalog, id)).join(', ') || '-') + '</td><td class="num">'
				+ v.reroll_count + '</td><td class="num">' + (100 * v.seen_pool_pct).toFixed(0) + '%</td><td class="num">'
				+ v.gold_enter + '</td><td class="num">' + v.gold_after_payday + '</td><td class="num">'
				+ v.gold_exit + '</td></tr>';
		}
		html += '</table>';
	}

	html += '<h2>Combats</h2>';
	const combats = db.prepare('SELECT * FROM run_combats WHERE run_id = ? ORDER BY session_num, id').all(run.run_id);
	if (combats.length === 0)
	{
		html += '<p class="muted">none</p>';
	}
	else
	{
		html += '<table><tr><th>session</th><th>result</th><th>hearts left</th><th>rounds</th><th>vs deck</th><th>HP curve</th><th>top damage cards</th></tr>';
		for (const c of combats)
		{
			let perCard = [];
			try { perCard = JSON.parse(c.per_card); } catch { /* keep empty */ }
			perCard.sort((a, b) => dmgOpp(b) - dmgOpp(a));
			const top = perCard.slice(0, 5)
				.map((p) => cardName(catalog, p.cardTypeID) + ' (' + dmgOpp(p) + ')').join(', ');
			let series = [];
			try { series = JSON.parse(c.series || '[]'); } catch { /* keep empty */ }
			html += '<tr><td class="num">' + c.session_num + '</td><td>' + (c.won ? 'won' : 'lost')
				+ '</td><td class="num">' + c.hearts_left + '</td><td class="num">' + (c.rounds || '-')
				+ '</td><td class="num">' + (c.opponent_deck_id || '-')
				+ '</td><td>' + hpCurveSvg(series)
				+ '</td><td>' + esc(top || '-') + '</td></tr>';
		}
		html += '</table>';
	}

	res.send(adminPage('Run ' + run.run_id.slice(0, 8), token, html));
});

// ---------------------------------------------------------------------------
// Fallbacks & startup
// ---------------------------------------------------------------------------

app.use((req, res) => res.status(404).json({ error: 'not_found' }));

const server = app.listen(PORT, HOST, () =>
{
	console.log('[onedeck-api] listening on http://' + HOST + ':' + PORT);
});

function shutdown()
{
	server.close(() =>
	{
		db.close();
		process.exit(0);
	});
	setTimeout(() => process.exit(0), 3000).unref();
}
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
