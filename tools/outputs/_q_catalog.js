// Read-only probe: card_catalog contents in production (per game version).
const db = require('better-sqlite3')('/var/www/onedeck/data/onedeck.db', { readonly: true });
const j = (v) => JSON.stringify(v);

console.log('== card_catalog by version ==');
console.log(j(db.prepare('SELECT game_version, COUNT(*) AS rows, MIN(updated_at) AS first_at, MAX(updated_at) AS last_at FROM card_catalog GROUP BY game_version ORDER BY rows DESC').all()));

const best = db.prepare('SELECT game_version FROM card_catalog GROUP BY game_version ORDER BY COUNT(*) DESC LIMIT 1').get();
if (best)
{
	console.log('== dominant version: ' + best.game_version + ' ==');
	console.log(j(db.prepare('SELECT card_type_id, name, tags, rarity, cost, updated_at FROM card_catalog WHERE game_version = ? ORDER BY card_type_id LIMIT 40').all(best.game_version)));
}

console.log('== decks / runs game versions ==');
console.log(j(db.prepare('SELECT game_version, COUNT(*) AS n FROM decks GROUP BY game_version').all()));
console.log(j(db.prepare('SELECT game_version, COUNT(*) AS n FROM runs GROUP BY game_version').all()));
db.close();
