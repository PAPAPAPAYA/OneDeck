// Read-only: dump the production card_catalog as "CAT<TAB>id<TAB>name<TAB>rarity<TAB>cost<TAB>tags".
const db = require('better-sqlite3')('/var/www/onedeck/data/onedeck.db', { readonly: true });
const rows = db.prepare("SELECT card_type_id, name, rarity, cost, tags, updated_at FROM card_catalog WHERE game_version = '0.2.0' ORDER BY card_type_id").all();
for (const r of rows)
{
	console.log('CAT\t' + r.card_type_id + '\t' + String(r.name).replace(/[\t\r\n]/g, ' ') + '\t' + r.rarity + '\t' + r.cost + '\t' + r.tags + '\t' + r.updated_at);
}
console.log('CATCOUNT\t' + rows.length);
db.close();
