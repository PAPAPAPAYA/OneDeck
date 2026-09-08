// Strip the leading on-reveal prefix "揭晓时:" from cardDesc of 4.0 card prefabs.
// Unity YAML stores CJK as uppercase \uXXXX escapes, so the raw prefix is "\u63ED\u6653\u65F6:".
// Batch approved 2026-09-08: 12 prefabs (incl. -1_Test/BURY). The 3 "敌方诅咒揭晓时"
// passive-condition cards (CURSE_THIRST_BEAST_4.0 / RELIC_CURSE_GRAVE / RELIC_CURSE_REVIVAL)
// are intentionally untouched — deleting there would change semantics.
// Runtime effects are unaffected: cardDesc is display text only.
const fs = require('fs');
const path = require('path');

const base = path.join(__dirname, '..', '..', 'Assets', 'Prefabs', 'Cards', '4.0');
const files = [
	'-1_Test/BURY.prefab',
	'1_Uncommon/CURSE_THIRST_SHAMAN_4.0.prefab',
	'1_Uncommon/EULOGIST.prefab',
	'1_Uncommon/GRAVE_TOGETHER_4.0.prefab',
	'1_Uncommon/RIFT_PRIEST.prefab',
	'1_Uncommon/RIFT_STRIKER.prefab',
	'1_Uncommon/SNOWBALL.prefab',
	'2_Rare/DETERIORATION_4.0.prefab',
	'2_Rare/GRAVE_MILLER.prefab',
	'2_Rare/QUAD_STRIKER.prefab',
	'2_Rare/SLIME_4.0.prefab',
	'2_Rare/UNFINISHED_ROBOT_4.0.prefab',
];

const PREFIX = '\\u63ED\\u6653\\u65F6:'; // 揭晓时: as stored in the YAML bytes
let failed = false;

for (const f of files) {
	const p = path.join(base, f);
	const t = fs.readFileSync(p, 'utf8');
	const occurrences = t.split(PREFIX).length - 1;
	const anchored = t.includes('cardDesc: "' + PREFIX);
	if (occurrences !== 1 || !anchored) {
		console.log('SKIP (occurrences=' + occurrences + ', anchoredAtCardDesc=' + anchored + '): ' + f);
		failed = true;
		continue;
	}
	fs.writeFileSync(p, t.replace(PREFIX, ''), 'utf8');
	console.log('OK: ' + f);
}

process.exit(failed ? 1 : 0);
