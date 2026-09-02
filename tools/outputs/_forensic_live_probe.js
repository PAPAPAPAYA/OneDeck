// Live probe: current 4.0 DB state for the 复辟 (revive enemy curse) family.
// 1) SQL row list (what the default view exposes)
// 2) page-level fetch of known 复辟-family page ids (view-hidden rows still resolve if not hard-deleted)
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const DS_URL = "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836";
const OUT = path.join(__dirname, "_forensic_live_40db.json");

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const readJson = (p) => JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, ""));

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

let rpcId = 0;
async function rpc(method, params, sessionId, access) {
	const headers = { "Content-Type": "application/json", Accept: "application/json, text/event-stream", Authorization: "Bearer " + access };
	if (sessionId) headers["mcp-session-id"] = sessionId;
	const resp = await fetch(MCP_URL, { method: "POST", headers, body: JSON.stringify({ jsonrpc: "2.0", id: ++rpcId, method, params }) });
	const sid = resp.headers.get("mcp-session-id");
	const text = await resp.text();
	const ct = resp.headers.get("content-type") || "";
	let payload;
	if (ct.includes("text/event-stream")) {
		const lines = text.split("\n").filter((l) => l.startsWith("data: ")).map((l) => l.slice(6));
		payload = JSON.parse(lines[lines.length - 1]);
	} else payload = JSON.parse(text);
	return { sid, payload, status: resp.status };
}

async function callTool(access, sid, name, args, attempt = 1) {
	const call = await rpc("tools/call", { name, arguments: args }, sid, access);
	if (call.payload.error) {
		const msg = JSON.stringify(call.payload.error);
		if (call.status === 401 || /auth|unauthorized/i.test(msg)) throw new Error("AUTH:" + msg);
		if (call.status === 429 || /rate/i.test(msg)) {
			if (attempt <= 3) { await sleep(3000 * attempt); return callTool(access, sid, name, args, attempt + 1); }
		}
		throw new Error("TOOL:" + msg.slice(0, 300));
	}
	const result = call.payload.result || {};
	if (result.isError) {
		const text = (result.content || []).map((c) => c.text || "").join(" ");
		// notion-fetch of a trashed/missing page often surfaces as isError with "Could not find" text
		const err = new Error("TOOL:" + text.slice(0, 300));
		err.isErrorResult = true;
		throw err;
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
		try { return JSON.parse(seg.slice(0, lastBrace + 1)); } catch (e) { /* next */ }
	}
	return null;
}

// Shortlist: every 复辟-family page id seen in ANY snapshot + the 08-28 已删 family.
const aug27 = JSON.parse(fs.readFileSync(__dirname + "/notion_40db_rows.json", "utf-8")).results;
const familyIds = {};
for (const r of aug27) {
	const blob = JSON.stringify([r.tag, r["card desc"], r.CARD_TYPE_ID]);
	if (/复辟|复活敌方|敌方诅咒|REVIV|CURSE/i.test(blob)) {
		familyIds[r.id.replace(/-/g, "")] = { ctid: r.CARD_TYPE_ID, url: r.url, seen: "08-27" };
	}
}
// extra ids from the 08-28 script not matched above (KINGSLAYER-victim etc.)
for (const id of ["3c8827b8c3c18088b3e1fe19e7297bc7", "3c8827b8c3c180a6b644e7eb58178a6e", "3c7827b8c3c181879aafffd6b12de80b", "3c7827b8c3c181d1a443eee0ec57c0dd", "3c7827b8c3c181ee909ef789a0f746e7", "3c7827b8c3c181f8b215efdf602bf450"]) {
	if (!familyIds[id]) familyIds[id] = { ctid: "(08-28 script only)", url: id, seen: "script" };
}

(async () => {
	let access = await refreshToken();
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-fubi-probe", version: "1.0" } }, null, access);
	if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	// Step 1: SQL full row list
	const sqlRows = [];
	let offset = 0;
	while (true) {
		const sql = 'SELECT url, "CARD_TYPE_ID", "状态", "rarity", "card desc" FROM "' + DS_URL + '" LIMIT 100 OFFSET ' + offset;
		const sqlText = await callTool(access, sid, "notion-query-data-sources", { data: { data_source_urls: [DS_URL], query: sql } });
		const parsed = JSON.parse(sqlText);
		sqlRows.push(...(parsed.results || []));
		if (!parsed.has_more || !parsed.results || parsed.results.length === 0) break;
		offset += parsed.results.length;
	}
	console.log("SQL visible rows:", sqlRows.length);
	const stDist = {};
	for (const r of sqlRows) { const k = r["状态"] || "(null=启用)"; stDist[k] = (stDist[k] || 0) + 1; }
	console.log("status distribution:", JSON.stringify(stDist));
	console.log("--- SQL 复辟-family rows:");
	for (const r of sqlRows) {
		if (/复辟|复活敌方|敌方诅咒/.test((r["card desc"] || ""))) console.log(r.CARD_TYPE_ID + " | " + (r.rarity || "") + " | 状态=" + (r["状态"] || "null") + " | " + String(r["card desc"] || "").slice(0, 50));
	}

	// Step 2: page-level probes for family ids
	console.log("\n--- page-level probes (survives = row still exists even if hidden from view)");
	const probeOut = {};
	let authRetried = false;
	for (const [id, info] of Object.entries(familyIds)) {
		try {
			const t = await callTool(access, sid, "notion-fetch", { id: info.url });
			const props = parseProperties(t) || {};
			const title = (props.name && (props.name.title || props.name)) || "";
			console.log("EXISTS  " + info.ctid + " | 状态=" + JSON.stringify(props["状态"] || null) + " | tag=" + JSON.stringify(props.tag || null) + " | desc=" + String(props["card desc"] ? JSON.stringify(props["card desc"]).slice(0, 60) : "?"));
			probeOut[id] = { ctid: info.ctid, exists: true, status: props["状态"] || null, tag: props.tag || null };
		} catch (e) {
			if (String(e.message).startsWith("AUTH:") && !authRetried) { authRetried = true; access = await refreshToken(); continue; }
			console.log("GONE    " + info.ctid + " | " + String(e.message).slice(0, 120));
			probeOut[id] = { ctid: info.ctid, exists: false, err: String(e.message).slice(0, 200) };
		}
		await sleep(250);
	}
	fs.writeFileSync(OUT, JSON.stringify({ sqlCount: sqlRows.length, sqlRows, probeOut }, null, 2), "utf-8");
	console.log("\nsaved -> " + OUT);
})().catch((e) => { console.log("FAILED:", e.message); process.exit(1); });
