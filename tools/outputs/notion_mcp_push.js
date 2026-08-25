// Push a markdown doc to Notion under the OD page via the official Notion MCP
// (streamable HTTP at mcp.notion.com). No token needed: reads the OAuth token
// from the kimi-code fastmcp cache (~/.kimi-code/credentials/mcp) and refreshes
// it automatically (Node fetch passes Cloudflare; Python urllib gets 1010).
// Usage: node notion_mcp_push.js  (edit DOC / TITLE / PARENT below)
const fs = require("fs");
const path = require("path");

const DOC = "D:/Unity Projects/OneDeck/docs/ThreeGames_Finisher_Research.md";
const TITLE = "三款主流卡牌游戏的终端设计调研（游戏王 / 炉石传说 / 万智牌）";
const PARENT_PAGE = "333827b8-c3c1-80fa-9bae-eb547a15270d"; // OD page

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

async function push(access) {
  const init = await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "zcode-push", version: "1.0" },
  }, null, access);
  if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  const md = fs.readFileSync(DOC, "utf-8").replace(/^\uFEFF/, "").replace(/\r\n/g, "\n").trim();
  const call = await rpc("tools/call", {
    name: "notion-create-pages",
    arguments: {
      parent: { type: "page_id", page_id: PARENT_PAGE },
      pages: [{ properties: { title: TITLE }, content: md }],
    },
  }, sid, access);

  if (call.payload.error) throw new Error("create failed: " + JSON.stringify(call.payload.error).slice(0, 1000));
  const text = (call.payload.result && call.payload.result.content || [])
    .map(c => c.text || "").join("\n");
  console.log("RESULT:", (text || "").slice(0, 1500));
}

(async () => {
  try {
    let access = readJson(TOK_FILE).access_token;
    try {
      await push(access);
    } catch (e) {
      if (/auth|401|unauthorized/i.test(e.message)) {
        console.log("auth error, refreshing token and retrying...");
        access = await refreshToken();
        await push(access);
      } else {
        throw e;
      }
    }
  } catch (e) {
    console.log("FAILED:", e.message);
    process.exit(1);
  }
})();
