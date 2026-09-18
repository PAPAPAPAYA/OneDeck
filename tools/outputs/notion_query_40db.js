// Query the 4.0 card database via the official Notion MCP (OAuth token from kimi cache).
// Usage: node notion_query_40db.js
const fs = require("fs");
const path = require("path");

const DB_ID = "3c7827b8-c3c1-8002-8b45-000bc02fa836"; // 4.0 card database
const OUT = path.join(__dirname, "notion_40db_rows.json");

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
    clientInfo: { name: "zcode-40db-query", version: "1.0" },
  }, null, access);
  if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  // SQL mode caps each response at 100 rows (has_more flag) — page through
  // with LIMIT/OFFSET until a short batch, then merge into one JSON file.
  const PAGE = 100;
  let all = [];
  for (let offset = 0; ; offset += PAGE) {
    const call = await rpc("tools/call", {
      name: "notion-query-data-sources",
      arguments: {
        data: {
          data_source_urls: ["collection://" + DB_ID],
          query: 'SELECT * FROM "collection://' + DB_ID + '" LIMIT ' + PAGE + " OFFSET " + offset,
          mode: "sql",
        },
      },
    }, sid, access);
    if (call.payload.error) throw new Error("query failed: " + JSON.stringify(call.payload.error));
    const text = (call.payload.result && call.payload.result.content || [])
      .map(c => c.text || "").join("\n");
    const page = JSON.parse(text);
    all = all.concat(page.results || []);
    console.log("page offset=" + offset + " rows=" + (page.results || []).length + " has_more=" + page.has_more);
    if (!page.results || page.results.length < PAGE) break;
  }
  const merged = JSON.stringify({ results: all, has_more: false, total: all.length });
  fs.writeFileSync(OUT, merged, "utf-8");
  console.log("RESULT rows:", all.length);
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
