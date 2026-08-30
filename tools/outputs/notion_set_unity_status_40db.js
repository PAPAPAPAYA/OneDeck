// Sync "Unity 配置状态" column of 4.0 card database (2026-08-28 roadmap step 0).
// Marks the 23 prefab-configured cards as 已配置; verifies rarity of the two
// rarity-moved cards (SPIKE_SKELETON -> normal, GRAVE_TOGETHER -> uncommon).
// Usage: node notion_set_unity_status_40db.js
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

const DS_URL = "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836";
// 42 enabled cards configured as prefabs under Assets/Prefabs/Cards/4.0/
const CONFIGURED = [
	"BLACKSMITH", "GRAVE_DREDGER", "GRAVE_FIST", "RIFT_INSECT", "SOLDIER_SKELETON",
	"SPIKE_SKELETON", "TWIN_STRIKER", "WAR_TRAINER",
	"AVENGER", "EULOGIST", "GRAVE_PUNCH", "GRAVE_TOGETHER", "HEXER", "RIFT_HATCHERY",
	"RIFT_PRIEST", "RIFT_STRIKER", "SACRIFICIAL_SPIRIT", "SNOWBALL",
	"DETERIORATION", "GRAVE_MILLER", "QUAD_STRIKER", "SLIME", "UNFINISHED_ROBOT",
	// 2026-08-29 A-group batch (revive/awaken + passive engine)
	"NECROMANCER", "SOUL_TRADER", "REVIVE_SUMMONER", "RIFT_SHEPHERD", "SPIRIT_CALLER",
	"BEAST_REVIVER", "FLURRY_REVIVER", "MASS_REVIVER", "DUO_REVIVER", "FUNERAL_WILL",
	"UNDYING_WARRIOR", "CURSE_THIRST_BEAST", "RIFT_MEDIUM", "DOOM_HERALD",
	"RELIC_HIVE", "RELIC_CHAIN_BURIAL", "RELIC_ATTACK_HEX", "RELIC_CURSE_GRAVE", "RELIC_CURSE_REVIVAL",
	// 2026-08-30 step-4 batches 1-3 (attack-times / round-end / resolver)
	"BATTLE_HORN", "COMBO_STARTER", "COMBO_GRANTER", "EXILE_BERSERKER", "RELIC_CURSE_HASTE",
	"FINAL_ESCORT", "RELIC_TALLY", "GRAVE_GIANT", "CURSE_EATER", "MIMIC_BLADE",
	// 2026-08-30: 11 cards renamed X_4.0 (dupe-avoidance vs 3.0) — DB rows carry the _4.0 ID
	"AVENGER_4.0", "BLACKSMITH_4.0", "CURSE_THIRST_BEAST_4.0", "GRAVE_PUNCH_4.0", "GRAVE_TOGETHER_4.0",
	"RIFT_INSECT_4.0", "SLIME_4.0", "SOLDIER_SKELETON_4.0", "SPIKE_SKELETON_4.0", "UNFINISHED_ROBOT_4.0", "DETERIORATION_4.0",
];
// rarity fixes from 4.0_Rarity_Iteration_StS2_2026-08-28.md §5.6
const RARITY_FIX = { SPIKE_SKELETON: "normal", GRAVE_TOGETHER: "uncommon" };

function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }

async function refreshToken() {
	const tok = readJson(TOK_FILE), client = readJson(CLIENT_FILE);
	const body = new URLSearchParams({ grant_type: "refresh_token", refresh_token: tok.refresh_token, client_id: client.client_id });
	const resp = await fetch(TOKEN_URL, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body });
	if (!resp.ok) throw new Error("token refresh failed: HTTP " + resp.status);
	const fresh = await resp.json();
	tok.access_token = fresh.access_token;
	if (fresh.refresh_token) tok.refresh_token = fresh.refresh_token;
	fs.writeFileSync(TOK_FILE, JSON.stringify(tok, null, 2), "utf-8");
	return fresh.access_token;
}

async function rpc(method, params, sessionId, access) {
	const headers = { "Content-Type": "application/json", "Accept": "application/json, text/event-stream", "Authorization": "Bearer " + access };
	if (sessionId) headers["mcp-session-id"] = sessionId;
	const resp = await fetch(MCP_URL, { method: "POST", headers, body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }) });
	const sid = resp.headers.get("mcp-session-id");
	const text = await resp.text();
	const ct = resp.headers.get("content-type") || "";
	let payload;
	if (ct.includes("text/event-stream")) {
		const dataLines = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6));
		payload = JSON.parse(dataLines[dataLines.length - 1]);
	} else payload = JSON.parse(text);
	return { sid, payload, status: resp.status };
}

async function toolCall(sid, access, name, args) {
	const call = await rpc("tools/call", { name, arguments: args }, sid, access);
	if (call.payload.error) throw new Error(name + " error: " + JSON.stringify(call.payload.error).slice(0, 300));
	const text = (call.payload.result && call.payload.result.content || []).map(c => c.text || "").join("\n");
	return text;
}

async function main(access) {
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-40db-unity-status", version: "1.0" } }, null, access);
	if (init.payload.error || init.status >= 400) throw new Error("init failed: " + JSON.stringify(init.payload.error || init.status));
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	// 1. Pull rows via SQL; fall back to per-card search when quota-limited.
	let rows = null;
	try {
		const sql = 'SELECT "userDefined:ID" AS id, "CARD_TYPE_ID" AS ctid, "rarity" AS rarity, "状态" AS status, "Unity 配置状态" AS unity, url FROM ' + JSON.stringify(DS_URL);
		const raw = await toolCall(sid, access, "notion-query-data-sources", {
			data: { data_source_urls: [DS_URL], query: sql },
		});
		const parsed = JSON.parse(raw);
		rows = (parsed.results || []).map(r => ({
			id: (r.url || "").split("-").pop(),
			pageId: r.url, ctid: r.ctid, rarity: r.rarity, status: r.status, unity: r.unity,
		}));
		console.log("rows via SQL: " + rows.length);
	} catch (e) {
		console.log("SQL query unavailable (" + e.message.slice(0, 120) + "), falling back to search-per-card");
	}
	if (!rows) {
		rows = [];
		for (const name of CONFIGURED) {
			const raw = await toolCall(sid, access, "notion-search", {
				query: name, query_type: "internal", data_source_url: DS_URL, page_size: 3, max_highlight_length: 0,
			});
			const parsed = JSON.parse(raw);
			const hit = (parsed.results || []).find(r => r.title === name && r.type === "page");
			if (!hit) { console.log("MISS " + name); continue; }
			rows.push({ id: hit.id, pageId: hit.url || hit.id, ctid: name, rarity: null, status: null, unity: null });
		}
		console.log("rows via search: " + rows.length);
	}

	// 2. Set Unity 配置状态=已配置 on the 23 configured cards; fix rarity where stale.
	let ok = 0, fail = 0;
	for (const name of CONFIGURED) {
		const row = rows.find(r => r.ctid === name);
		if (!row) { console.log("SKIP(no row) " + name); fail++; continue; }
		const props = { "Unity 配置状态": "已配置" };
		const wantRarity = RARITY_FIX[name];
		if (wantRarity && row.rarity && row.rarity !== wantRarity) props["rarity"] = wantRarity;
		const needUpdate = !row.unity || row.unity !== "已配置" || props["rarity"];
		if (!needUpdate) { console.log("SKIP(clean) " + name); ok++; continue; }
		try {
			await toolCall(sid, access, "notion-update-page", {
				page_id: row.pageId, command: "update_properties", properties: props,
			});
			console.log("OK   " + name + " -> 已配置" + (props["rarity"] ? " + rarity=" + props["rarity"] : ""));
			ok++;
		} catch (e) {
			console.log("FAIL " + name + ": " + e.message.slice(0, 200));
			fail++;
		}
	}
	console.log("done: " + ok + " ok, " + fail + " failed/skipped");
}

(async () => {
	try {
		let access = readJson(TOK_FILE).access_token;
		try { await main(access); }
		catch (e) {
			if (/auth|401|unauthorized|invalid_token/i.test(e.message)) {
				console.log("auth error, refreshing token and retrying...");
				access = await refreshToken();
				await main(access);
			} else throw e;
		}
	} catch (e) { console.log("FAILED:", e.message); process.exit(1); }
})();
