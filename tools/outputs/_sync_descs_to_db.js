// Sync prefab cardDesc (number-format pass 2026-09-08) into Notion "4.0 card database".
// Coverage: WHERE CARD_TYPE_ID IN (all prefab typeIDs) to bypass the 100-row SQL cap.
// Writes: rows where DB desc differs but matches after normalization (bold tags stripped,
// whitespace collapsed, fullwidth->halfwidth punctuation, <tag:X> resolved to display names).
// Content diffs are ALSO written (Unity prefab is canonical per 2026-09-07 dbsync verdict),
// but listed separately. Run with --dry first.
// Usage: node _sync_descs_to_db.js [--dry]
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const DS_URL = "collection://3c7827b8-c3c1-8002-8b45-000bc02fa836";

const TAG_NAMES = { Believer: "信徒", DeathRattle: "遗言", Revive: "复活", Curse: "诅咒", Awaken: "苏醒", Sleep: "蛰伏", Linger: "回响", ManaX: "法力" };

function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }

function resolveTags(s) {
	return (s || "").replace(/<tag:(\w+)>/g, (m, t) => (TAG_NAMES[t] !== undefined ? TAG_NAMES[t] : m));
}
function dbValue(s) { return resolveTags(s); }
function norm(s) {
	return (s || "")
		.replace(/<b>|<\/b>/g, "")
		.replace(/\s+/g, "")
		.replace(/；/g, ";").replace(/：/g, ":").replace(/，/g, ",")
		.replace(/（/g, "(").replace(/）/g, ")")
		.replace(/【/g, "[").replace(/】/g, "]");
}

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
	if (method !== "notifications/initialized" && payload.error) throw new Error("RPC error: " + JSON.stringify(payload.error).slice(0, 400));
	return { sid, payload };
}

async function callTool(name, args, sid, access) {
	const r = await rpc("tools/call", { name, arguments: args }, sid, access);
	return ((r.payload.result && r.payload.result.content) || []).map(c => c.text || "").join("\n");
}

function unescapeJson(s) { try { return JSON.parse('"' + s + '"'); } catch (e) { return s; } }

(async () => {
	try {
		const descs = readJson(__dirname + "/unity_descs_40.json").descs;
		const access = await refreshToken();
		const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-desc-sync", version: "1.0" } }, null, access);
		const sid = init.sid;
		await rpc("notifications/initialized", {}, sid, access);

		const tidList = Object.keys(descs).map(t => "'" + t.replace(/'/g, "''") + "'").join(",");
		const qText = await callTool("notion-query-data-sources", {
			data: {
				mode: "sql",
				data_source_urls: [DS_URL],
				query: 'SELECT "CARD_TYPE_ID", "card desc", "id" FROM "' + DS_URL + '" WHERE "CARD_TYPE_ID" IN (' + tidList + ')',
			},
		}, sid, access);
		let parsed;
		try { parsed = JSON.parse(qText); } catch (e) { console.log("SQL RAW (first 800):\n" + qText.slice(0, 800)); return; }
		const rows = parsed.results || parsed;
		if (!Array.isArray(rows) || rows.length === 0) { console.log("SQL returned no rows. RAW:\n" + qText.slice(0, 800)); return; }
		console.log("DB rows fetched: " + rows.length + (parsed.truncated ? " (TRUNCATED)" : ""));
		const keys = Object.keys(rows[0]);
		const idKey = keys.find(k => /id/i.test(k) && k !== "userDefined:ID") || "id";
		const tidKey = keys.find(k => /CARD_TYPE_ID/i.test(k));
		const descKey = keys.find(k => /card desc/i.test(k));
		const uuidOf = v => { const m = String(v).match(/([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i); return m ? m[1] : String(v); };

		const byTid = {};
		for (const r of rows) byTid[String(r[tidKey] || "").trim()] = r;

		const fmtOnly = [];
		const contentDiffs = [];
		const missing = [];
		for (const [tid, want] of Object.entries(descs)) {
			const row = byTid[tid];
			if (!row) { missing.push(tid); continue; }
			const dbDesc = String(row[descKey] || "");
			const wv = dbValue(want);
			if (dbDesc === wv) continue;
			const rec = { tid, pageId: uuidOf(row[idKey]), dbDesc, want: wv };
			if (norm(dbDesc) === norm(want)) fmtOnly.push(rec);
			else contentDiffs.push(rec);
		}

		// Fallback: rows the SQL layer misses (stale index on renamed/new rows) via search + fetch
		for (const tid of missing.slice()) {
			try {
				const sText = await callTool("notion-search", { query: tid }, sid, access);
				const sparsed = JSON.parse(sText);
				const hits = (sparsed.results || []);
				const hit = hits.find(r => r.title === tid && String(r.path || "").includes("4.0 card database"))
					|| hits.find(r => r.title === tid);
				if (!hit) continue;
				const fText = await callTool("notion-fetch", { id: hit.id }, sid, access);
				const m = fText.match(/\\"card desc\\":\\"((?:[^"\\]|\\.)*?)\\"/) || fText.match(/"card desc":"((?:[^"\\]|\\.)*)"/);
				if (!m) continue;
				const dbDesc = unescapeJson(m[1]);
				const wv = dbValue(descs[tid]);
				const rec = { tid, pageId: hit.id, dbDesc, want: wv };
				missing.splice(missing.indexOf(tid), 1);
				if (dbDesc === wv) continue;
				if (norm(dbDesc) === norm(wv)) fmtOnly.push(rec);
				else contentDiffs.push(rec);
			} catch (e) { console.log("FALLBACK FAILED " + tid + ": " + e.message); }
		}
		if (missing.length) console.log("\nFALLBACK still missing: " + missing.join(", "));

		console.log("\n=== FORMAT-ONLY (" + fmtOnly.length + ") ===");
		for (const u of fmtOnly) console.log(u.tid);
		console.log("\n=== CONTENT DIFF, WILL WRITE UNITY-CANONICAL (" + contentDiffs.length + ") ===");
		for (const u of contentDiffs) console.log(u.tid + "\n  DB  : " + u.dbDesc + "\n  UNITY: " + u.want);
		console.log("\n=== NO DB ROW (" + missing.length + "): " + missing.join(", "));

		const updates = fmtOnly.concat(contentDiffs);
		let ok = 0, fail = 0;
		const DRY = process.argv.includes("--dry");
		if (DRY) console.log("\n*** DRY RUN - no writes ***");
		for (const u of updates) {
			if (DRY) { ok++; continue; }
			try {
				await callTool("notion-update-page", {
					page_id: u.pageId,
					command: "update_properties",
					properties: { "card desc": u.want },
				}, sid, access);
				ok++;
			} catch (e) { fail++; console.log("UPDATE FAILED " + u.tid + ": " + e.message); }
		}
		console.log("\nUPDATES: total=" + updates.length + " ok=" + ok + " failed=" + fail);

		const sampleNames = ["REVIVING_STRIKER", "SLIME_4.0", "RIFT_SHEPHERD", "UTILITY_ODDS_1"];
		const samples = sampleNames.map(t => updates.find(u => u.tid === t)).filter(Boolean);
		if (!DRY && samples.length) {
			console.log("\n=== VERIFY SAMPLES ===");
			for (const s of samples) {
				const fText = await callTool("notion-fetch", { id: s.pageId }, sid, access);
				const m = fText.match(/\\"card desc\\":\\"((?:[^"\\]|\\.)*?)\\"/) || fText.match(/"card desc":"((?:[^"\\]|\\.)*)"/);
				const got = m ? unescapeJson(m[1]) : "(property not found)";
				console.log(s.tid + " => " + (got === s.want ? "MATCH" : "MISMATCH: db_now=" + got));
			}
		}
	} catch (e) {
		console.log("FAILED:", e.message);
		process.exit(1);
	}
})();
