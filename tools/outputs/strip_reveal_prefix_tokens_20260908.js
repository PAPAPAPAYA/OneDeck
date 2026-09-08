// Token-card follow-up to strip_reveal_prefix_20260908.js: same treatment for the
// JU_ON (curse) / RIFT (believer) token prefabs under 3.0's _DONT INCLUDE/Token/.
// Same guards as the 4.0 batch: exactly one occurrence, anchored right after cardDesc: ".
// The retired 3.0 pool prefabs also contain the prefix but are explicitly out of scope.
// DB rows for both tokens mirror the prefab text verbatim (halfwidth punctuation) and are
// updated separately via Notion MCP.
const fs = require('fs');
const path = require('path');

const base = path.join(__dirname, '..', '..', 'Assets', 'Prefabs', 'Cards');
const files = [
	'3.0 no cost (current)/_DONT INCLUDE/Token/JU_ON.prefab',
	'3.0 no cost (current)/_DONT INCLUDE/Token/RIFT.prefab',
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
