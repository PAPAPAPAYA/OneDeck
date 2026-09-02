// Verify tag-ref desc updates: fetch a page and print its <properties> section raw.
// Usage: node _verify_tagref_fetch.js <pageUrl>
const fs = require("fs");
const path = require("path");
const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }
async function rpc(access, method, params, sid) {
	const headers = { "Content-Type": "application/json", "Accept": "application/json, text/event-stream", "Authorization": "Bearer " + access };
	if (sid) headers["mcp-session-id"] = sid;
	const resp = await fetch("https://mcp.notion.com/mcp", { method: "POST", headers, body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }) });
	const sid2 = resp.headers.get("mcp-session-id");
	const text = await resp.text();
	const ct = resp.headers.get("content-type") || "";
	let payload;
	if (ct.includes("text/event-stream")) {
		const dl = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6));
		payload = JSON.parse(dl[dl.length - 1]);
	} else { payload = JSON.parse(text); }
	return { sid: sid2, payload };
}
(async () => {
	const tok = readJson(TOK_FILE);
	const client = readJson(CLIENT_FILE);
	let access = tok.access_token;
	const init = await rpc(access, "initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-verify", version: "1.0" } }, null);
	const sid = init.sid;
	await rpc(access, "notifications/initialized", {}, sid);
	const url = process.argv[2];
	const call = await rpc(access, "tools/call", { name: "notion-fetch", arguments: { id: url } }, sid);
	const text = (call.payload.result.content || []).map(c => c.text || "").join("\n");
	const a = text.indexOf("<properties>");
	const b = text.indexOf("</properties>");
	console.log(a >= 0 ? text.slice(a, b > 0 ? b : a + 2500) : text.slice(0, 1500));
})().catch(e => { console.log("FAILED:", e.message); process.exit(1); });
