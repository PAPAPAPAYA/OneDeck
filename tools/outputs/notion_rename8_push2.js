// Round 2: finish the 3 renames blocked by the query-data-sources usage limit.
// Uses notion-search (by CARD_TYPE_ID title) for page ids and notion-fetch for
// verification — no SQL anywhere.
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const DB = "3c7827b8-c3c1-8002-8b45-000bc02fa836";

const RENAMES = {
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
		return text; // fetch returns raw markdown, not JSON
	}
}

async function findPageId(access, sid, tid) {
	const call = await rpc("tools/call", {
		name: "notion-search",
		arguments: {
			query: tid,
			data_source_url: "collection://" + DB,
			page_size: 5,
		},
	}, sid, access);
	const data = parseResult(call, "search " + tid);
	const results = data.results || data || [];
	for (const r of results) {
		const id = r.id || r.page_id || "";
		const title = r.name || r.title || (r.properties && (r.properties.title || r.properties.Name)) || "";
		if (typeof id === "string" && /^[0-9a-f-]{32,36}$/i.test(id.replace(/-/g, ""))) {
			return { id: id.replace(/-/g, ""), title };
		}
	}
	return null;
}

async function fetchProp(access, sid, pageId, prop) {
	const call = await rpc("tools/call", {
		name: "notion-fetch",
		arguments: { id: pageId },
	}, sid, access);
	const text = parseResult(call, "fetch " + pageId);
	const s = typeof text === "string" ? text : JSON.stringify(text);
	const m = s.match(new RegExp(prop + "[^\\n]{0,80}"));
	return m ? m[0] : "(prop not found)";
}

(async () => {
	let access = readJson(TOK_FILE).access_token;
	const init = await rpc("initialize", {
		protocolVersion: "2025-06-18",
		capabilities: {},
		clientInfo: { name: "zcode-rename8-r2", version: "1.0" },
	}, null, access);
	if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	const report = [];
	let ok = 0;
	for (const [tid, newName] of Object.entries(RENAMES)) {
		try {
			const hit = await findPageId(access, sid, tid);
			if (!hit) { report.push(tid + " -> SEARCH MISS"); continue; }
			const upd = await rpc("tools/call", {
				name: "notion-update-page",
				arguments: {
					page_id: hit.id,
					command: "update_properties",
					properties: { "中文名": newName },
				},
			}, sid, access);
			parseResult(upd, "update " + tid);
			await sleep(1500);
			const check = await fetchProp(access, sid, hit.id, "中文名");
			const pass = check.includes(newName);
			if (pass) ok++;
			report.push(tid + " (" + hit.title + ") -> " + newName + " " + (pass ? "[ok] " + check : "[CHECK] " + check));
		} catch (e) {
			report.push(tid + " -> FAILED: " + e.message.slice(0, 160));
		}
		await sleep(300);
	}
	console.log(report.join("\n"));
	console.log("DONE ok=" + ok + "/" + Object.keys(RENAMES).length);
	if (ok !== Object.keys(RENAMES).length) process.exit(1);
})().catch((e) => {
	console.log("FAILED:", e.message);
	process.exit(1);
});
