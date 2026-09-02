// Final probe: per-card fresh props + page_last_edited_at for the 复辟 family.
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

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

async function callTool(access, sid, name, args) {
	const call = await rpc("tools/call", { name, arguments: args }, sid, access);
	if (call.payload.error) throw new Error("TOOL:" + JSON.stringify(call.payload.error).slice(0, 200));
	const result = call.payload.result || {};
	if (result.isError) throw new Error("TOOL:" + (result.content || []).map((c) => c.text || "").join(" ").slice(0, 200));
	return (result.content || []).map((c) => c.text || "").join("\n");
}

function parsePage(text) {
	// text is itself a JSON blob: {"metadata":...,"text":"...<properties>\n{...}\n</properties>...","page_last_edited_at":...}
	let outer = null;
	try { outer = JSON.parse(text); } catch (e) { return null; }
	const body = outer && outer.text || "";
	const m = body.match(/<properties>\s*([\s\S]*?)\s*<\/properties>/);
	let props = null;
	if (m) { try { props = JSON.parse(m[1]); } catch (e) { props = { _parseErr: String(e).slice(0, 80) }; } }
	return { props, lastEdited: outer.page_last_edited_at || null, title: outer.title || null };
}

const live = JSON.parse(fs.readFileSync(__dirname + "/_forensic_live_40db.json", "utf-8"));
const byCtid = {};
for (const r of live.sqlRows) byCtid[r.CARD_TYPE_ID] = r.url;
const aug27 = JSON.parse(fs.readFileSync(__dirname + "/notion_40db_rows.json", "utf-8")).results;
for (const r of aug27) if (!byCtid[r.CARD_TYPE_ID]) byCtid[r.CARD_TYPE_ID] = r.url;

const FAMILY = [
	"CURSE_REVIVER", "CURSE_GARDENER", "RELIC_RIFT_OVERRIDE", "DOOM_HERALD", "GRAVE_HEXER",
	"RELIC_CURSE_REVIVAL", "CURSE_THIRST_BEAST_4.0", "CURSE_ECHO", "DUAL_REVIVER",
	"ELITE_REVIVER", "FLURRY_REVIVER", "REVIVE_SUMMONER", "TREASURE_REVIVER", "MASS_REVIVER",
	"BEAST_REVIVER", "GRAND_REVIVER", "DUO_REVIVER", "RELIC_SOUL_TAX", "RELIC_TAINT",
];

(async () => {
	let access = await refreshToken();
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-fubi-final", version: "1.0" } }, null, access);
	if (init.payload.error) throw new Error("initialize failed");
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);

	const out = {};
	for (const ctid of FAMILY) {
		const url = byCtid[ctid];
		if (!url) { console.log(ctid + " | NO URL"); continue; }
		try {
			const t = await callTool(access, sid, "notion-fetch", { id: url });
			const page = parsePage(t) || {};
			const p = page.props || {};
			out[ctid] = {
				status: p["状态"] ?? null,
				rarity: p["rarity"] ?? null,
				tag: p["tag"] ?? null,
				desc: p["card desc"] ?? null,
				lastEdited: page.lastEdited,
			};
			console.log(ctid + " | 状态=" + (p["状态"] ?? "null") + " | " + (p["rarity"] ?? "?") + " | tag=" + JSON.stringify(p["tag"] ?? null) + " | " + String(p["card desc"] ?? "").slice(0, 40) + " | lastEdit=" + page.lastEdited);
		} catch (e) {
			console.log(ctid + " | FETCH-ERR " + String(e.message).slice(0, 120));
			out[ctid] = { err: String(e.message).slice(0, 200) };
		}
		await sleep(300);
	}
	fs.writeFileSync(__dirname + "/_forensic_fubi_final.json", JSON.stringify(out, null, 2), "utf-8");
	console.log("saved.");
})().catch((e) => { console.log("FAILED:", e.message); process.exit(1); });
