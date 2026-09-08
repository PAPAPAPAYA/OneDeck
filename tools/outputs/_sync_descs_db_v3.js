// Sync prefab cardDesc into Notion "4.0 card database" — v3 FINAL.
// Page ids are resolved via notion-search (title === cardTypeID, path contains "4.0 card database")
// because query-data-sources SQL proved to hand out stale ids / silently failing writes.
// Every update response is validated (isError + echo of page_id). Backslash-tolerant verify.
// Usage:
//   node _sync_descs_db_v3.js classify [--dry]   -> sync_targets.json
//   node _sync_descs_db_v3.js update             -> update_results.json  (reads sync_targets.json)
//   node _sync_descs_db_v3.js verify             -> full re-read comparison
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const DS_URL = "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836";
const TAG_NAMES = { Believer: "信徒", DeathRattle: "遗言", Revive: "复活", Curse: "诅咒", Awaken: "苏醒", Sleep: "蛰伏", Linger: "回响", ManaX: "法力" };
const sleep = ms => new Promise(r => setTimeout(r, ms));

function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }
function resolveTags(s) { return (s || "").replace(/<tag:(\w+)>/g, (m, t) => (TAG_NAMES[t] !== undefined ? TAG_NAMES[t] : m)); }
function dbValue(s) { return resolveTags(s); }
function squash(s) { return (s || "").replace(/\\/g, "").replace(/\s+/g, ""); }
function norm(s) {
	return (s || "")
		.replace(/<b>|<\/b>/g, "")
		.replace(/\s+/g, "")
		.replace(/；/g, ";").replace(/：/g, ":").replace(/，/g, ",")
		.replace(/（/g, "(").replace(/）/g, ")")
		.replace(/【/g, "[").replace(/】/g, "]");
}
async function refreshToken() {
	const tok = readJson(TOK_FILE); const client = readJson(CLIENT_FILE);
	const body = new URLSearchParams({ grant_type: "refresh_token", refresh_token: tok.refresh_token, client_id: client.client_id });
	const resp = await fetch(TOKEN_URL, { method: "POST", headers: { "Content-Type": "application/x-www-form-urlencoded" }, body });
	if (!resp.ok) throw new Error("token refresh failed: HTTP " + resp.status);
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
	if (method !== "notifications/initialized" && payload.error) throw new Error("RPC error: " + JSON.stringify(payload.error).slice(0, 300));
	return { sid, payload };
}
async function callTool(name, args, sid, access) {
	const r = await rpc("tools/call", { name, arguments: args }, sid, access);
	const res = r.payload.result || {};
	const text = (res.content || []).map(c => c.text || "").join("\n");
	return { text, isError: res.isError === true };
}
function extractDesc(fText) {
	const m = fText.match(/\\"card desc\\":\\"((?:[^"\\]|\\.)*?)\\"/);
	if (m) return m[1].replace(/\\\\/g, "\\").replace(/\\"/g, '"');
	const u = fText.match(/"card desc":"((?:[^"\\]|\\.)*)"/);
	return u ? JSON.parse('"' + u[1] + '"') : null;
}
async function findCardDbPage(tid, sid, access) {
	const { text, isError } = await callTool("notion-search", { query: tid }, sid, access);
	if (isError) throw new Error("search isError for " + tid);
	const hits = (JSON.parse(text).results || []).filter(r => r.title === tid);
	const hit = hits.find(r => String(r.path || "").includes("4.0 card database")) || hits.find(r => String(r.path || "").includes("card database"));
	return hit || null;
}
async function freshSession() {
	const access = await refreshToken();
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-desc-sync-v3", version: "1.0" } }, null, access);
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);
	return { access, sid };
}

(async () => {
	const mode = process.argv[2] || "classify";
	const DRY = process.argv.includes("--dry");
	const descs = readJson(__dirname + "/unity_descs_40.json").descs;
	const { access, sid } = await freshSession();

	if (mode === "classify") {
		const targets = [];
		const equal = [];
		const noPage = [];
		for (const [tid, wantRaw] of Object.entries(descs)) {
			const want = dbValue(wantRaw);
			try {
				const hit = await findCardDbPage(tid, sid, access);
				if (!hit) { noPage.push(tid); continue; }
				const { text: fText } = await callTool("notion-fetch", { id: hit.id }, sid, access);
				const dbDesc = extractDesc(fText);
				if (dbDesc === null) { noPage.push(tid + "(no desc prop)"); continue; }
				if (dbDesc === want || squash(dbDesc) === squash(want)) { equal.push(tid); continue; }
				targets.push({ tid, pageId: hit.id, dbDesc, want });
			} catch (e) { noPage.push(tid + "(err " + e.message.slice(0, 60) + ")"); }
			await sleep(80);
		}
		console.log("already-equal=" + equal.length + " targets=" + targets.length + " noPage=" + noPage.length);
		if (noPage.length) console.log("noPage list: " + noPage.join(", "));
		console.log("\n--- target diffs ---");
		for (const t of targets) console.log(t.tid + "\n  DB  : " + t.dbDesc + "\n  WANT: " + t.want);
		fs.writeFileSync(__dirname + "/sync_targets.json", JSON.stringify({ generated: new Date().toISOString(), targets }, null, 1), "utf-8");
		console.log("\nwrote sync_targets.json" + (DRY ? " (dry: nothing written to Notion)" : ""));
		return;
	}

	if (mode === "update") {
		const { targets } = readJson(__dirname + "/sync_targets.json");
		const results = [];
		let ok = 0, fail = 0;
		for (const t of targets) {
			try {
				// re-resolve id via search to guarantee freshness
				const hit = await findCardDbPage(t.tid, sid, access);
				if (!hit) { fail++; results.push({ tid: t.tid, ok: false, err: "page not found" }); continue; }
				const { text, isError } = await callTool("notion-update-page", {
					page_id: hit.id,
					command: "update_properties",
					properties: { "card desc": t.want },
				}, sid, access);
				const good = !isError && text.indexOf(hit.id.slice(0, 8)) >= 0;
				if (good) ok++; else { fail++; results.push({ tid: t.tid, ok: false, err: text.slice(0, 200) }); }
			} catch (e) { fail++; results.push({ tid: t.tid, ok: false, err: e.message.slice(0, 200) }); }
			await sleep(200);
		}
		console.log("UPDATE done: ok=" + ok + " fail=" + fail);
		for (const r of results.filter(r => !r.ok)) console.log("FAILED " + r.tid + ": " + r.err);
		fs.writeFileSync(__dirname + "/update_results.json", JSON.stringify({ ok, fail, failures: results.filter(r => !r.ok) }, null, 1), "utf-8");
		return;
	}

	if (mode === "verify") {
		const { targets } = readJson(__dirname + "/sync_targets.json");
		let landed = 0, drift = 0;
		const bad = [];
		for (const t of targets) {
			const { text: fText, isError } = await callTool("notion-fetch", { id: t.pageId }, sid, access);
			const got = isError ? "(fetch error)" : extractDesc(fText);
			if (got !== null && squash(got) === squash(t.want)) landed++;
			else { drift++; bad.push(t.tid + " => " + (got === null ? "(no desc prop)" : got)); }
			await sleep(80);
		}
		console.log("VERIFY: landed=" + landed + " drift=" + drift);
		for (const b of bad) console.log("DRIFT " + b);
		return;
	}
	console.log("unknown mode");
})().catch(e => { console.log("FAILED:", e.message); process.exit(1); });
