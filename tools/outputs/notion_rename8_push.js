// One-off: rename 8 creature cards' 中文名 in the 4.0 card database (2026-10-11 §1-priority ruling).
// Same OAuth/MCP path as notion_db_names_push.js. Verifies by re-query after update.
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const DB = "3c7827b8-c3c1-8002-8b45-000bc02fa836";

const RENAMES = {
	"GRAVE_FIST": "血祭司",
	"GRAVE_PUNCH_4.0": "剜心祭司",
	"GRAVE_TOGETHER_4.0": "殉葬人",
	"MILLBLADE": "千刀手",
	"DECIMATION": "监斩官",
	"REANIMATOR": "百鬼将",
	"WOKEN_BLADE": "起尸鬼",
	"BEAST_REVIVER": "马戏团的熊",
};

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

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

function parseResult(call, label) {
	if (call.payload.error) throw new Error(label + " rpc error: " + JSON.stringify(call.payload.error).slice(0, 200));
	const result = call.payload.result || {};
	if (result.isError) throw new Error(label + " tool error: " + (result.content || []).map((c) => c.text || "").join(" ").slice(0, 200));
	const text = (result.content || []).map((c) => c.text || "").join("\n");
	try {
		return JSON.parse(text);
	} catch (e) {
		throw new Error(label + " non-JSON: " + text.replace(/\s+/g, " ").slice(0, 160));
	}
}

async function queryRow(access, sid, tid) {
	const call = await rpc("tools/call", {
		name: "notion-query-data-sources",
		arguments: {
			data: {
				data_source_urls: ["collection://" + DB],
				query: 'SELECT CARD_TYPE_ID, "中文名", url FROM "collection://' + DB + '" WHERE CARD_TYPE_ID = \'' + tid + "'",
				mode: "sql",
			},
		},
	}, sid, access);
	const page = parseResult(call, "query " + tid);
	return (page.results || [])[0] || null;
}

(async () => {
	let access = readJson(TOK_FILE).access_token;
	const init = await rpc("initialize", {
		protocolVersion: "2025-06-18",
		capabilities: {},
		clientInfo: { name: "zcode-rename8", version: "1.0" },
	}, null, access);
	if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	const report = [];
	let ok = 0;
	for (const [tid, newName] of Object.entries(RENAMES)) {
		try {
			const row = await queryRow(access, sid, tid);
			if (!row) { report.push(tid + " -> NO ROW"); continue; }
			const before = Array.isArray(row["中文名"]) ? row["中文名"].join("") : (row["中文名"] || "");
			const pageId = (row.url || "").split("/").pop().replace(/-/g, "");
			const upd = await rpc("tools/call", {
				name: "notion-update-page",
				arguments: {
					page_id: pageId,
					command: "update_properties",
					properties: { "中文名": newName },
				},
			}, sid, access);
			parseResult(upd, "update " + tid);
			await sleep(1500);
			const after = await queryRow(access, sid, tid);
			const got = after && (Array.isArray(after["中文名"]) ? after["中文名"].join("") : after["中文名"]);
			const pass = got === newName;
			if (pass) ok++;
			report.push(tid + " " + before + " -> " + got + (pass ? " [ok]" : " [MISMATCH]"));
		} catch (e) {
			report.push(tid + " -> FAILED: " + e.message.slice(0, 160));
		}
		await sleep(300);
	}
	console.log(report.join("\n"));
	console.log("DONE ok=" + ok + "/" + Object.keys(RENAMES).length);
	if (ok !== Object.keys(RENAMES).length) process.exit(1);
})().catch(async (e) => {
	const msg = e.message || "";
	if (/auth|401|unauthorized|invalid_token/i.test(msg)) {
		console.log("auth error — rerun after refresh");
	}
	console.log("FAILED:", msg);
	process.exit(1);
});
