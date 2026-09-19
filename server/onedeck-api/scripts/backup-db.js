'use strict';

/**
 * WAL-safe online backup of the live database, for use right before a deploy.
 *
 * Why not `cp`: the server runs with journal_mode = WAL, so recent commits can still live in
 * onedeck.db-wal — a plain file copy can silently produce an older database. better-sqlite3's
 * backup() uses SQLite's online backup API and always yields a consistent snapshot.
 *
 * Usage (on the box, from the folder holding server.js):
 *   node scripts/backup-db.js                 # -> ../data/onedeck.db.bak-<utc stamp>.db
 *   node scripts/backup-db.js data/onedeck.db # explicit source
 * Prints the destination path and verifies the copy opens (row count of `decks`).
 */

const fs = require('fs');
const path = require('path');
const Database = require('better-sqlite3');

// Same default as server.js: DATA_DIR is __dirname/../data on the server layout.
const source = process.argv[2]
	|| path.join(process.env.DATA_DIR || path.join(__dirname, '..', '..', 'data'), 'onedeck.db');

if (!fs.existsSync(source))
{
	console.error('[backup-db] source does not exist: ' + source);
	process.exit(1);
}

const stamp = new Date().toISOString().replace(/[:.]/g, '-').replace('Z', '');
const dest = source + '.bak-' + stamp + '.db';

const db = new Database(source);
db.backup(dest)
	.then((info) =>
	{
		// Prove the snapshot is readable before declaring success.
		const copy = new Database(dest, { readonly: true });
		const decks = copy.prepare('SELECT COUNT(*) AS c FROM decks').get().c;
		copy.close();
		db.close();
		console.log('[backup-db] ' + dest + ' (' + info.totalPages + ' pages, ' + decks + ' decks)');
	})
	.catch((error) =>
	{
		db.close();
		console.error('[backup-db] FAILED: ' + error.message);
		process.exit(1);
	});
