// One-off Notion writer for the new 4.0 card CURSE_SUMMONER.
// Writes the Chinese name and/or flips "Unity 配置状态" on the card's page
// via the official Notion MCP (same OAuth path as notion_db_names_push.js).
//
// Usage:
//   node notion_curse_summoner_finish.js          -> write 中文名 + set Unity 配置状态=已配置
//   node notion_curse_summoner_finish.js name     -> write 中文名 only
//   node notion_curse_summoner_finish.js status   -> set Unity 配置状态=已配置 only
//   node notion_curse_summoner_finish.js revert   -> set Unity 配置状态=可直接配置 (rollback)
//
// After running, verify via notion-fetch on the page (SQL view may serve stale cache).
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

// 4.0 card database row created 2026-09-02 (userDefined:ID 117).
const PAGE_ID = "3cf827b8-c3c1-8060-a783-e1b0120b8eaf";
const CARD_TYPE_ID = "CURSE_SUMMONER";
const CHINESE_NAME = "替人叫魂的喇叭";

function readJson(p) {
	return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, ""));
}

async function refreshToken() {
	const tok = readJson(TOK_FILE);
	const client = readJson(CLIENT_FILE);
	const body = new URLSearchParams({
		grant_type: "refresh_token",
		refresh_token: tok.refresh_token,
		client_id: client.client_id,
	});
	const resp = await fetch(TOKEN_URL, {
		method: "POST",
		headers: { "Content-Type": "application/x-www-form-urlencoded" },
		body,
	});
	if (!resp.ok) throw new Error("token refresh failed: HTTP " + resp.status);
	const fresh = await resp.json();
	tok.access_token = fresh.access_token;
	if (fresh.refresh_token) tok.refresh_token = fresh.refresh_token;
	tok.expires_in = fresh.expires_in;
	fs.writeFileSync(TOK_FILE, JSON.stringify(tok, null, 2), "utf-8");
	return fresh.access_token;
}

async function rpc(method, params, sessionId, access) {
	const headers = {
		"Content-Type": "application/json",
		Accept: "application/json, text/event-stream",
		Authorization: "Bearer " + access,
	};
	if (sessionId) headers["mcp-session-id"] = sessionId;
	const resp = await fetch(MCP_URL, {
		method: "POST",
		headers,
		body: JSON.stringify({ jsonrpc: "2.0", id: Math.floor(Math.random() * 1e9), method, params }),
	});
	const sid = resp.headers.get("mcp-session-id");
	const text = await resp.text();
	const ct = resp.headers.get("content-type") || "";
	let payload;
	if (ct.includes("text/event-stream")) {
		const dataLines = text.split("\n").filter((l) => l.startsWith("data: ")).map((l) => l.slice(6));
		payload = JSON.parse(dataLines[dataLines.length - 1]);
	} else {
		payload = JSON.parse(text);
	}
	return { sid, payload, status: resp.status };
}

async function updatePage(access, sid, properties) {
	const call = await rpc("tools/call", {
		name: "notion-update-page",
		arguments: {
			page_id: PAGE_ID,
			command: "update_properties",
			properties,
		},
	}, sid, access);
	if (call.payload.error) throw new Error(JSON.stringify(call.payload.error).slice(0, 300));
	const result = call.payload.result || {};
	if (result.isError) {
		const text = (result.content || []).map((c) => c.text || "").join(" ");
		throw new Error(text.slice(0, 300));
	}
}

(async () => {
	const mode = (process.argv[2] || "finish").toLowerCase();
	let properties;
	if (mode === "name") properties = { "中文名": CHINESE_NAME };
	else if (mode === "status") properties = { "Unity 配置状态": "已配置" };
	else if (mode === "revert") properties = { "Unity 配置状态": "可直接配置" };
	else properties = { "中文名": CHINESE_NAME, "Unity 配置状态": "已配置" };

	try {
		let access = readJson(TOK_FILE).access_token;
		try {
			const init = await rpc("initialize", {
				protocolVersion: "2025-06-18",
				capabilities: {},
				clientInfo: { name: "zcode-curse-summoner-finish", version: "1.0" },
			}, null, access);
			if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
			const sid = init.sid;
			await rpc("notifications/initialized", {}, sid, access);
			await updatePage(access, sid, properties);
			console.log("OK  " + CARD_TYPE_ID + " <- " + JSON.stringify(properties));
		} catch (e) {
			if (!/AUTH|auth|401|unauthorized/i.test(e.message)) throw e;
			console.log("auth error, refreshing token and retrying...");
			access = await refreshToken();
			const init = await rpc("initialize", {
				protocolVersion: "2025-06-18",
				capabilities: {},
				clientInfo: { name: "zcode-curse-summoner-finish", version: "1.0" },
			}, null, access);
			if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
			const sid = init.sid;
			await rpc("notifications/initialized", {}, sid, access);
			await updatePage(access, sid, properties);
			console.log("OK  " + CARD_TYPE_ID + " <- " + JSON.stringify(properties));
		}
		console.log("NOTE: verify via notion-fetch on the page (SQL view cache can be stale).");
	} catch (e) {
		console.log("FAILED:", e.message);
		process.exit(1);
	}
})();
