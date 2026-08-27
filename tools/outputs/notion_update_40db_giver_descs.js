// Update 4.0 card database: 强化N友方 -> 强化N友方生物 (creature-only giver targets).
// Usage: node notion_update_40db_giver_descs.js
const fs = require("fs");
const path = require("path");

const DB_ID = "3c7827b8-c3c1-8002-8b45-000bc02fa836";

const UPDATES = [
  { id: "3c7827b8-c3c1-814d-8ce5-f6169bd5447d", old: "放逐所有信徒，每放逐1，强化1友方", next: "放逐所有信徒，每放逐1，强化1友方生物" },
  { id: "3c7827b8-c3c1-814f-95a0-fbd852ce663a", old: "生成2信徒；强化1友方", next: "生成2信徒；强化1友方生物" },
  { id: "3c7827b8-c3c1-819f-aa2c-e51c16ba49ea", old: "强化2友方", next: "强化2友方生物" },
  { id: "3c7827b8-c3c1-81e3-a1d5-cc6197fec1d6", old: "攻击；强化1友方", next: "攻击；强化1友方生物" },
  { id: "3c7827b8-c3c1-81f8-b215-efdf602bf450", old: "复活攻击力最高敌方；强化3友方", next: "复活攻击力最高敌方；强化3友方生物" },
];

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

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
    "Accept": "application/json, text/event-stream",
    "Authorization": "Bearer " + access,
  };
  if (sessionId) headers["mcp-session-id"] = sessionId;
  const resp = await fetch(MCP_URL, {
    method: "POST",
    headers,
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }),
  });
  const sid = resp.headers.get("mcp-session-id");
  const text = await resp.text();
  const ct = resp.headers.get("content-type") || "";
  let payload;
  if (ct.includes("text/event-stream")) {
    const dataLines = text.split("\n").filter(l => l.startsWith("data: ")).map(l => l.slice(6));
    payload = JSON.parse(dataLines[dataLines.length - 1]);
  } else {
    payload = JSON.parse(text);
  }
  return { sid, payload, status: resp.status };
}

async function run(access) {
  const init = await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "zcode-40db-update", version: "1.0" },
  }, null, access);
  if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  for (const u of UPDATES) {
    const call = await rpc("tools/call", {
      name: "notion-update-page",
      arguments: {
        page_id: u.id,
        command: "update_properties",
        properties: { "card desc": u.next },
      },
    }, sid, access);
    const text = (call.payload.result && call.payload.result.content || [])
      .map(c => c.text || "").join("\n");
    if (call.payload.error) {
      console.log("FAIL " + u.id + ": " + JSON.stringify(call.payload.error).slice(0, 300));
    } else {
      console.log("OK   " + u.id + " -> " + u.next + " | resp: " + (text || "").slice(0, 120).replace(/\n/g, " "));
    }
  }
}

(async () => {
  try {
    let access = readJson(TOK_FILE).access_token;
    try {
      await run(access);
    } catch (e) {
      if (/auth|401|unauthorized|invalid_token/i.test(e.message)) {
        console.log("auth error, refreshing token and retrying...");
        access = await refreshToken();
        await run(access);
      } else {
        throw e;
      }
    }
  } catch (e) {
    console.log("FAILED:", e.message);
    process.exit(1);
  }
})();
