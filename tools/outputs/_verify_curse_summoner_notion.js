// One-off Notion verification for CURSE_SUMMONER (plan Step 5).
// Fetches the card page via notion-fetch and checks the two expected values.
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

const PAGE_ID = "3cf827b8-c3c1-8060-a783-e1b0120b8eaf";
const EXPECT_NAME = "替人叫魂的喇叭";
const EXPECT_STATUS = "已配置";

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

(async () => {
	try {
		let access = readJson(TOK_FILE).access_token;
		let init = await rpc("initialize", {
			protocolVersion: "2025-06-18",
			capabilities: {},
			clientInfo: { name: "zcode-curse-summoner-verify", version: "1.0" },
		}, null, access);
		if (init.payload.error && /AUTH|401|unauthorized|invalid_token/i.test(JSON.stringify(init.payload.error))) {
			access = await refreshToken();
			init = await rpc("initialize", {
				protocolVersion: "2025-06-18",
				capabilities: {},
				clientInfo: { name: "zcode-curse-summoner-verify", version: "1.0" },
			}, null, access);
		}
		if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error).slice(0, 200));
		const sid = init.sid;
		await rpc("notifications/initialized", {}, sid, access);

		// discover notion-fetch input schema
		const list = await rpc("tools/list", {}, sid, access);
		const tools = (list.payload.result && list.payload.result.tools) || [];
		const fetchTool = tools.find((t) => t.name === "notion-fetch");
		if (!fetchTool) throw new Error("notion-fetch tool not found; tools=" + tools.map((t) => t.name).join(","));

		let args = { page_id: PAGE_ID };
		const schemaProps = fetchTool.inputSchema && fetchTool.inputSchema.properties ? fetchTool.inputSchema.properties : {};
		if (schemaProps.page_id === undefined) {
			if (schemaProps.id !== undefined) args = { id: PAGE_ID, type: "page" };
			else args = { url: "https://www.notion.so/" + PAGE_ID.replace(/-/g, "") };
		}

		const call = await rpc("tools/call", { name: "notion-fetch", arguments: args }, sid, access);
		if (call.payload.error) throw new Error("fetch failed: " + JSON.stringify(call.payload.error).slice(0, 200));
		const result = call.payload.result || {};
		const text = (result.content || []).map((c) => c.text || "").join("\n");

		const nameHit = text.includes(EXPECT_NAME);
		const statusHit = text.includes(EXPECT_STATUS);
		const statusMiss = text.includes("可直接配置");

		console.log("page fetched, chars=" + text.length);
		console.log("中文名(" + EXPECT_NAME + "): " + (nameHit ? "PASS" : "MISS"));
		console.log("Unity配置状态含 已配置: " + (statusHit ? "PASS" : "MISS"));
		console.log("Unity配置状态仍含 可直接配置: " + (statusMiss ? "YES(bad)" : "no(good)"));
		if (!nameHit || !statusHit) {
			const lines = text.split("\n").filter((l) => /中文名|配置状态/.test(l));
			console.log("---- relevant lines ----");
			console.log(lines.join("\n").slice(0, 1500));
			process.exit(1);
		}
		console.log("VERIFY OK");
	} catch (e) {
		console.log("FAILED:", e.message);
		process.exit(1);
	}
})();
