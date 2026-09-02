// One-off forensics: trace 复辟 (revive enemy curse) cards across snapshots. Temporary file.
const fs = require("fs");
const flat = s => String(s).replace(/-/g, "");

const aug27 = JSON.parse(fs.readFileSync(__dirname + "/notion_40db_rows.json", "utf-8")).results;
const byId = {};
for (const r of aug27) byId[flat(r.id)] = r;

console.log("### A. 08-28 status script (20 ids) -> card mapping via 08-27 snapshot");
const src = fs.readFileSync(__dirname + "/notion_update_40db_status.js", "utf-8");
const re = /id:\s*"([0-9a-f]+)"[\s\S]*?note:\s*"((?:[^"\\]|\\.)*)"/g;
let m;
while ((m = re.exec(src))) {
	const r = byId[m[1]];
	const hit = (m[2].match(/【2026-08-28[^\]]*\】([^"]*)/) || [])[1] || "";
	console.log((r ? r.CARD_TYPE_ID : "??UNMATCHED??") + " | rar=" + (r ? r.rarity : "") + " | " + String(r ? r["card desc"] : "").slice(0, 45) + " | " + hit.trim());
}

console.log();
console.log("### B. 08-30 snapshot: 复辟 family");
const s30 = JSON.parse(fs.readFileSync(__dirname + "/notion_40db_rows_2026-08-30.json", "utf-8")).rows;
for (const r of s30) {
	const tag = (r.tag || []).join(",");
	if (/复辟|复活敌方|敌方诅咒/.test(tag + " " + (r.desc || "")))
		console.log(r.ctid + " | " + r.rarity + " | status=" + r.status + " | unity=" + r.unity + " | " + tag + " | " + String(r.desc || "").slice(0, 50));
}

console.log();
console.log("### C. 09-01 notion_4_0_rows.json: 复辟 family");
const s91 = JSON.parse(fs.readFileSync(__dirname + "/notion_4_0_rows.json", "utf-8"));
for (const r of s91) {
	if (/复辟|复活敌方|敌方诅咒/.test((r.tag || "") + " " + (r.desc || "")))
		console.log(r.cardTypeID + " | " + r.rarity + " | status=" + r.status + " | " + (r.zhName || "") + " | " + String(r.desc || "").slice(0, 50));
}

console.log();
console.log("### D. 09-01 notion_db_tags.json: status distribution");
const tags = JSON.parse(fs.readFileSync(__dirname + "/notion_db_tags.json", "utf-8"));
const stCount = {};
for (const [k, v] of Object.entries(tags)) {
	const key = v.status || "(null=启用)";
	stCount[key] = (stCount[key] || 0) + 1;
}
console.log(JSON.stringify(stCount));
console.log("--- 复辟/诅咒 family rows:");
for (const [k, v] of Object.entries(tags)) {
	const t = (v.tags || []).join(",");
	if (/复辟|复活敌方|敌方诅咒|诅咒/.test(t)) console.log(k + " | " + v.rarity + " | status=" + v.status + " | tags=" + t);
}
