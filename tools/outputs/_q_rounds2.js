// Read-only probe: rounds + schema coverage in production.
const db = require('better-sqlite3')('/var/www/onedeck/data/onedeck.db', { readonly: true });
const j = (v) => JSON.stringify(v);

console.log('== runs ==');
console.log(j(db.prepare('SELECT run_id, result, final_session, hearts_left, uploaded_at FROM runs ORDER BY uploaded_at DESC').all()));

console.log('== series / hp_max coverage ==');
console.log(j(db.prepare("SELECT COUNT(*) AS rows, SUM(CASE WHEN series IS NOT NULL AND series != '[]' THEN 1 ELSE 0 END) AS with_series FROM run_combats").get()));
console.log(j(db.prepare('SELECT COUNT(*) AS rows, SUM(CASE WHEN hp_max > 0 THEN 1 ELSE 0 END) AS with_hpmax, SUM(CASE WHEN gold_exit != 0 THEN 1 ELSE 0 END) AS with_gold FROM run_shop_visits').get()));

console.log('== per_card sample ==');
const c = db.prepare('SELECT per_card FROM run_combats ORDER BY ts DESC LIMIT 1').get();
console.log(c ? String(c.per_card).slice(0, 400) : 'none');

console.log('== deck snapshots (ghost) ==');
console.log(j(db.prepare('SELECT deck_id, session_num, hp_max, win_amount, heart_left, created_at FROM decks ORDER BY deck_id DESC LIMIT 8').all()));

console.log('== stats_snapshots ==');
console.log(j(db.prepare("SELECT kind, COUNT(*) AS rows, MIN(session_num) AS mn, MAX(session_num) AS mx FROM stats_snapshots GROUP BY kind").all()));
db.close();
