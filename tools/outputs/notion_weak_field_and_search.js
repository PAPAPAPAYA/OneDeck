// WEAKENING_FIELD desc update (JU_ON re-typed Status; creature filter subsumes the
// curse exclusion) + tool discovery + search for the desc-convention spec page.
// Usage: node notion_weak_field_and_search.js
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
		const dl = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6));
		payload = JSON.parse(dl[dl.length - 1]);
	} else { payload = JSON.parse(text); }
	return { sid, payload };
}

async function callTool(access, sid, name, args) {
	const call = await rpc("tools/call", { name, arguments: args }, sid, access);
	if (call.payload.error) throw new Error(name + " failed: " + JSON.stringify(call.payload.error).slice(0, 300));
	const result = call.payload.result || {};
	const text = (result.content || []).map(c => c.text || "").join("\n");
	if (result.isError) throw new Error(name + " isError: " + text.slice(0, 300));
	return text;
}

async function run(access) {
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-weakfield", version: "1.0" } }, null, access);
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	// 1. WEAKENING_FIELD row
	await callTool(access, sid, "notion-update-page", {
		page_id: "3c8827b8-c3c1-8039-9fa1-f850ef2f09b2",
		command: "update_properties",
		properties: {
			"card desc": "所有生物本回合攻击力-1",
			"side note": "大部分诅咒卡攻击力为0，尝试利用这点\n【2026-09-02】JU_ON 转非生物(Status)后，生物过滤天然排除诅咒，desc 删「除了诅咒」特例（引擎已无 typeID 排除）",
		},
	});
	console.log("UPDATED WEAKENING_FIELD");

	// 2. Discover tools (look for content-append / create-page capabilities)
	const tools = await rpc("tools/list", {}, sid, access);
	const names = (tools.payload.result.tools || []).map(t => t.name);
	console.log("TOOLS: " + names.join(", "));

	// 3. Search for the convention/trigger-definitions page
	for (const q of ["描述规范", "轴触发", "词表"]) {
		try {
			const t = await callTool(access, sid, "notion-search", { query: q });
			console.log("SEARCH[" + q + "] " + t.slice(0, 700).replace(/\n+/g, " | "));
		} catch (e) { console.log("SEARCH[" + q + "] failed: " + e.message); }
	}
}

(async () => {
	try {
		let access = readJson(TOK_FILE).access_token;
		try { await run(access); }
		catch (e) {
			if (/auth|401|unauthorized|invalid_token/i.test(e.message)) {
				console.log("auth error, refreshing...");
				access = await refreshToken();
				await run(access);
			} else throw e;
		}
	} catch (e) { console.log("FAILED:", e.message); process.exit(1); }
})();
