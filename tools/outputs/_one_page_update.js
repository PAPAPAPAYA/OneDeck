// Controlled experiment: update ONE page via search id, print the FULL tool response.
const fs = require("fs");
const path = require("path");
const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
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
(async () => {
	const access = await refreshToken();
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "one-page-test", version: "1.0" } }, null, access);
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);
	const TID = process.argv[2] || "SPIKE_SKELETON_4.0";
	const DESC = process.argv[3];
	if (!DESC) { console.log("usage: node _one_page_update.js TID DESC"); process.exit(1); }
	const sText = await callToolRaw("notion-search", { query: TID }, sid, access);
	const hits = (JSON.parse(sText).results || []).filter(r => r.title === TID);
	const cardDb = hits.find(r => String(r.path || "").includes("4.0 card database"));
	if (!cardDb) { console.log(TID + ": card-db page not found"); process.exit(1); }
	console.log("pageId=" + cardDb.id + " path=" + cardDb.path);
	const upd = await rpc("tools/call", { name: "notion-update-page", arguments: { page_id: cardDb.id, command: "update_properties", properties: { "card desc": DESC } } }, sid, access);
	console.log("isError=" + (upd.payload.result && upd.payload.result.isError));
	const text = ((upd.payload.result && upd.payload.result.content) || []).map(c => c.text || "").join("\n");
	console.log("RESPONSE: " + text.slice(0, 1200));
	async function callToolRaw(name, args, sid2, access2) {
		const r = await rpc("tools/call", { name, arguments: args }, sid2, access2);
		return ((r.payload.result && r.payload.result.content) || []).map(c => c.text || "").join("\n");
	}
})().catch(e => { console.log("FAILED:", e.message); process.exit(1); });
