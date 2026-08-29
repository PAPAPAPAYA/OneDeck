// Apply 状态 (备用/已删) + side note to rows of the 4.0 card database via Notion MCP.
// Usage: node apply_status_updates.js [updates.json]
//   updates.json: [{"page_id": "...", "status": "已删", "note": "<FULL new side note>"}]
// Page id = last UUID segment of the row url (https://app.notion.com/<id>).
// Reads the Notion MCP OAuth token from the kimi-code cache and refreshes it if needed.
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }

async function refreshToken() {
  const tok = readJson(TOK_FILE), client = readJson(CLIENT_FILE);
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
    const dataLines = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6));
    payload = JSON.parse(dataLines[dataLines.length - 1]);
  } else payload = JSON.parse(text);
  return { sid, payload, status: resp.status };
}

async function run(access, updates) {
  let init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-card-pool-audit", version: "1.0" } }, null, access);
  if (init.payload.error || init.status >= 400) throw new Error("init failed: " + JSON.stringify(init.payload.error || init.status));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  for (const u of updates) {
    const call = await rpc("tools/call", {
      name: "notion-update-page",
      arguments: {
        page_id: u.page_id,
        command: "update_properties",
        properties: { "状态": u.status, "side note": u.note },
      },
    }, sid, access);
    const text = (call.payload.result && call.payload.result.content || []).map(c => c.text || "").join("\n");
    if (call.payload.error) console.log("FAIL " + u.page_id + ": " + JSON.stringify(call.payload.error).slice(0, 200));
    else console.log("OK   " + u.page_id + " " + u.status + " | " + text.slice(0, 100).replace(/\n/g, " "));
  }
}

(async () => {
  const updatesFile = process.argv[2] || path.join(__dirname, "updates.json");
  const updates = JSON.parse(fs.readFileSync(updatesFile, "utf-8"));
  try {
    let access = readJson(TOK_FILE).access_token;
    try { await run(access, updates); }
    catch (e) {
      if (/auth|401|unauthorized|invalid_token/i.test(e.message)) {
        console.log("auth error, refreshing token and retrying...");
        access = await refreshToken();
        await run(access, updates);
      } else throw e;
    }
  } catch (e) { console.log("FAILED:", e.message); process.exit(1); }
})();
