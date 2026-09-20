'use strict';

/**
 * Read-only schema and row-count report for the live database — run it before and after a deploy.
 *
 * The server migrates on boot (ensureColumn + a fingerprint backfill for pre-existing rows), so
 * this is how you confirm the migration actually applied without opening sqlite3 or writing
 * anything. Prints MISSING for anything the running code expects but the db does not have yet.
 *
 * Usage (on the box, from the folder holding server.js):
 *   node scripts/inspect-db.js
 *   node scripts/inspect-db.js /var/www/onedeck/data/onedeck.db
 */

const fs = require('fs');
const path = require('path');
const Database = require('better-sqlite3');

// Same default as server.js: DATA_DIR is __dirname/../data on the server layout.
const source = process.argv[2]
	|| path.join(process.env.DATA_DIR || path.join(__dirname, '..', '..', 'data'), 'onedeck.db');

if (!fs.existsSync(source))
{
	console.error('[inspect-db] no database at ' + source);
	process.exit(1);
}

const db = new Database(source, { readonly: true });
console.log('[inspect-db] ' + source);

const tables = db.prepare("SELECT name FROM sqlite_master WHERE type='table'").all().map((r) => r.name);
console.log('tables: ' + tables.sort().join(','));

function check(label, present)
{
	console.log('  ' + (present ? 'ok      ' : 'MISSING ') + label);
}

for (const table of ['combos', 'flagged_fingerprints', 'loop_reports'])
{
	check('table ' + table, tables.includes(table));
}
const deckColumns = tables.includes('decks')
	? db.prepare('PRAGMA table_info(decks)').all().map((c) => c.name)
	: [];
for (const column of ['fingerprint', 'flag', 'flag_reason', 'flagged_at'])
{
	check('decks.' + column, deckColumns.includes(column));
}

// Counts only for what exists — this script is meant to run on a pre-migration db too.
function count(sql)
{
	try { return db.prepare(sql).get().c; }
	catch { return 'n/a'; }
}

console.log('rows:'
	+ ' decks=' + count('SELECT COUNT(*) AS c FROM decks')
	+ ' (fingerprinted=' + (deckColumns.includes('fingerprint')
		? count("SELECT COUNT(*) AS c FROM decks WHERE fingerprint != ''") : 'n/a')
	+ ', flagged=' + (deckColumns.includes('flag')
		? count('SELECT COUNT(*) AS c FROM decks WHERE flag = 1') : 'n/a') + ')'
	+ ' players=' + count('SELECT COUNT(*) AS c FROM players')
	+ ' loop_reports=' + count('SELECT COUNT(*) AS c FROM loop_reports')
	+ ' active_combos=' + count("SELECT COUNT(*) AS c FROM combos WHERE status = 'active'")
	+ ' flagged_fingerprints=' + count('SELECT COUNT(*) AS c FROM flagged_fingerprints'));

db.close();
