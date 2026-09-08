const fs = require("fs");
const path = require("path");
const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }
async function refreshToken() {
	const tok = readJson(TOK_FILE);
	const client = readJson(CLIENT_FILE);
	const body = new URLSearchParams({ grant_type: "refresh_token", refresh_token: tok.refresh_token, client_id: client.client_id });
	const resp = await fetch(TOKEN_URL, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body });
	if (!resp.ok) throw new Error("refresh failed HTTP " + resp.status);
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
		const dl = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6));
		payload = JSON.parse(dl[dl.length - 1]);
	} else { payload = JSON.parse(text); }
	return { sid, status: resp.status, payload };
}
(async () => {
	try {
		const access = await refreshToken();
		console.log("token ok len=" + access.length);
		const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "probe", version: "1.0" } }, null, access);
		console.log("init status=" + init.status + " hasError=" + !!init.payload.error + " sid=" + (init.sid || "none"));
		if (init.payload.error) { console.log(JSON.stringify(init.payload.error).slice(0,400)); return; }
		const sid = init.sid;
		const r2 = await rpc("notifications/initialized", {}, sid, access);
		console.log("initialized status=" + r2.status + " hasError=" + !!r2.payload.error);
		const tl = await rpc("tools/list", {}, sid, access);
		if (tl.payload.error) { console.log("tools/list error: " + JSON.stringify(tl.payload.error).slice(0,400)); return; }
		const tools = (tl.payload.result.tools || []).map(t => t.name);
		console.log("TOOLS: " + tools.join(", "));
	} catch (e) { console.log("FAILED:", e.message); }
})();
