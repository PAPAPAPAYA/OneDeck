// Probe: compare SQL id vs search card-DB id, and read current desc for sample rows.
const fs = require("fs");
const path = require("path");
const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const DS_URL = "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836";
function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }
async function refreshToken() {
	const tok = readJson(TOK_FILE); const client = readJson(CLIENT_FILE);
	const body = new URLSearchParams({ grant_type: "refresh_token", refresh_token: tok.refresh_token, client_id: client.client_id });
	const resp = await fetch(TOKEN_URL, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body });
	if (!resp.ok) throw new Error("refresh failed HTTP " + resp.status);
	const fresh = await resp.json();
	tok.access_token = fresh.access_token; if (fresh.refresh_token) tok.refresh_token = fresh.refresh_token;
	fs.writeFileSync(TOK_FILE, JSON.stringify(tok, null, 2), "utf-8");
	return fresh.access_token;
}
async function rpc(method, params, sessionId, access) {
	const headers = { "Content-Type": "application/json", "Accept": "application/json, text/event-stream", "Authorization": "Bearer " + access };
	if (sessionId) headers["mcp-session-id"] = sessionId;
	const resp = await fetch(MCP_URL, { method: "POST", headers, body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }) });
	const sid = resp.headers.get("mcp-session-id"); const text = await resp.text(); const ct = resp.headers.get("content-type") || "";
	let payload;
	if (ct.includes("text/event-stream")) { const dl = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6)); payload = JSON.parse(dl[dl.length - 1]); }
	else payload = JSON.parse(text);
	if (method !== "notifications/initialized" && payload.error) throw new Error(JSON.stringify(payload.error).slice(0, 300));
	return { sid, payload };
}
async function callTool(name, args, sid, access) {
	const r = await rpc("tools/call", { name, arguments: args }, sid, access);
	return ((r.payload.result && r.payload.result.content) || []).map(c => c.text || "").join("\n");
}
function extractDesc(fText) {
	const marker = "card desc";
	let i = fText.indexOf(marker);
	if (i < 0) return "(no marker)";
	let seg = fText.slice(i + marker.length, i + marker.length + 400);
	seg = seg.split('","')[0];
	seg = seg.replace(/\\"/g, '"').replace(/^["\\:]+/, "").replace(/["\\]+$/, "");
	return seg;
}
(async () => {
	const access = await refreshToken();
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "probe-ids", version: "1.0" } }, null, access);
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);
	const tids = ["SPIKE_SKELETON_4.0", "RIFT_SHEPHERD", "SLIME_4.0", "GRAVE_FIST", "BATTLE_HORN", "RELIC_HIVE", "FUNERAL_WILL"];
	const inList = tids.map(t => "'" + t + "'").join(",");
	const qText = await callTool("notion-query-data-sources", { data: { mode: "sql", data_source_urls: [DS_URL], query: 'SELECT "CARD_TYPE_ID", "card desc", "id" FROM "' + DS_URL + '" WHERE "CARD_TYPE_ID" IN (' + inList + ')' } }, sid, access);
	const sqlRows = {};
	for (const r of (JSON.parse(qText).results || [])) sqlRows[r.CARD_TYPE_ID] = r;
	for (const tid of tids) {
		const sText = await callTool("notion-search", { query: tid }, sid, access);
		const hits = (JSON.parse(sText).results || []).filter(r => r.title === tid);
		const cardDb = hits.find(r => String(r.path || "").includes("4.0 card database"));
		const sqlId = sqlRows[tid] ? sqlRows[tid].id : "SQL-MISS";
		const searchId = cardDb ? cardDb.id : "SEARCH-MISS";
		let desc = "(n/a)";
		if (cardDb) {
			const fText = await callTool("notion-fetch", { id: cardDb.id }, sid, access);
			desc = extractDesc(fText);
		}
		console.log(tid);
		console.log("  sqlId=" + sqlId + " searchId=" + searchId + " same=" + (sqlId === searchId));
		console.log("  desc_now=" + desc);
	}
})().catch(e => { console.log("FAILED:", e.message); process.exit(1); });
