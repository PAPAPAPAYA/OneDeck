// Pull fresh per-card tags from the 4.0 card database into
// tools/outputs/notion_db_tags.json.
//
// Why page-fetch per card: the SQL/view layer serves a stale cache for the
// recently-updated "tag" column (hit 2026-09-01 during the tag strictness
// pass), so SQL is only trusted for stable identity columns (CARD_TYPE_ID,
// url, 状态, 中文名, rarity); tags come from page-level notion-fetch which is
// always fresh.
//
// Usage: node notion_db_tags_pull.js
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

const DS_URL = "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836";
const OUT_FILE = path.join(__dirname, "notion_db_tags.json");

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

async function callTool(access, sid, name, args, attempt = 1) {
	const call = await rpc("tools/call", { name, arguments: args }, sid, access);
	if (call.payload.error) {
		const msg = JSON.stringify(call.payload.error);
		if (call.status === 401 || /auth|unauthorized/i.test(msg)) throw new Error("AUTH:" + msg);
		if (call.status === 429 || /rate/i.test(msg)) {
			if (attempt <= 3) {
				await sleep(3000 * attempt);
				return callTool(access, sid, name, args, attempt + 1);
			}
		}
		throw new Error("TOOL:" + msg.slice(0, 300));
	}
	const result = call.payload.result || {};
	if (result.isError) {
		const text = (result.content || []).map((c) => c.text || "").join(" ");
		throw new Error("TOOL:" + text.slice(0, 300));
	}
	return (result.content || []).map((c) => c.text || "").join("\n");
}

function parseProperties(fetchText) {
	const re = /<properties>/g;
	let m;
	while ((m = re.exec(fetchText)) !== null) {
		const open = fetchText.indexOf("{", m.index);
		const close = fetchText.indexOf("</properties>", m.index);
		if (open === -1 || close === -1 || close < open) continue;
		const seg = fetchText.slice(open, close);
		const lastBrace = seg.lastIndexOf("}");
		if (lastBrace === -1) continue;
		try {
			return JSON.parse(seg.slice(0, lastBrace + 1));
		} catch (e) {
			// try the next <properties> occurrence
		}
	}
	return null;
}

(async () => {
	let access = await refreshToken();
	const init = await rpc("initialize", {
		protocolVersion: "2025-06-18",
		capabilities: {},
		clientInfo: { name: "zcode-tags-pull", version: "1.0" },
	}, null, access);
	if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	// Step 1: row list via SQL (identity columns only — the tag column is excluded
	// because the SQL layer serves a stale cache for recently written values).
	const rows = [];
	let offset = 0;
	while (true) {
		const sql = 'SELECT url, "CARD_TYPE_ID", "状态", "中文名", "rarity" FROM "' + DS_URL + '" LIMIT 100 OFFSET ' + offset;
		const sqlText = await callTool(access, sid, "notion-query-data-sources", {
			data: { data_source_urls: [DS_URL], query: sql },
		});
		const parsed = JSON.parse(sqlText);
		rows.push(...(parsed.results || []));
		if (!parsed.has_more || !parsed.results || parsed.results.length === 0) break;
		offset += parsed.results.length;
	}
	console.log("sql rows:", rows.length);

	// Step 2: fresh tag per card via page-level fetch.
	const out = {};
	let authRetried = false;
	for (let i = 0; i < rows.length; i++) {
		const row = rows[i];
		const id = row.CARD_TYPE_ID;
		try {
			const pageText = await callTool(access, sid, "notion-fetch", { id: row.url });
			const props = parseProperties(pageText);
			out[id] = {
				url: row.url,
				status: row["状态"] || null,
				cnName: row["中文名"] || null,
				rarity: row.rarity || null,
				tags: Array.isArray(props && props.tag) ? props.tag : [],
			};
		} catch (e) {
			if (String(e.message).startsWith("AUTH:") && !authRetried) {
				authRetried = true;
				console.log("auth error, refreshing token...");
				access = await refreshToken();
				i--;
				continue;
			}
			out[id] = { url: row.url, status: row["状态"] || null, cnName: row["中文名"] || null, rarity: row.rarity || null, tags: [], error: e.message.slice(0, 200) };
		}
		if ((i + 1) % 20 === 0) console.log("...", i + 1, "/", rows.length);
		await sleep(250);
	}

	fs.writeFileSync(OUT_FILE, JSON.stringify(out, null, "\t"), "utf-8");
	const errs = Object.keys(out).filter((k) => out[k].error);
	console.log("DONE cards=" + Object.keys(out).length + " errors=" + errs.length + " -> " + OUT_FILE);
	if (errs.length) {
		for (const k of errs) console.log("  ERR", k, out[k].error);
		process.exit(1);
	}
})().catch((e) => {
	console.log("FAILED:", e.message);
	process.exit(1);
});
