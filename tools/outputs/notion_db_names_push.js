// Batch-write 中文名 into the 4.0 card database via the official Notion MCP
// (same OAuth path as notion_mcp_push.js). Reads:
//   tools/outputs/worldview_v2_names.json  { CARD_TYPE_ID: 中文名 }
//   tools/outputs/worldview_v2_urls.json   { CARD_TYPE_ID: page_id }
// Usage: node notion_db_names_push.js
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

const NAMES = JSON.parse(fs.readFileSync(path.join(__dirname, "worldview_v2_names.json"), "utf-8"));
const URLS = JSON.parse(fs.readFileSync(path.join(__dirname, "worldview_v2_urls.json"), "utf-8"));

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

async function updateOne(access, sid, pageId, name, attempt = 1) {
  const call = await rpc(
    "tools/call",
    {
      name: "notion-update-page",
      arguments: {
        page_id: pageId,
        command: "update_properties",
        properties: { "中文名": name },
      },
    },
    sid,
    access
  );
  if (call.payload.error) {
    const msg = JSON.stringify(call.payload.error);
    if (call.status === 401 || /auth|unauthorized/i.test(msg)) throw new Error("AUTH:" + msg);
    if (call.status === 429 || /rate/i.test(msg)) {
      if (attempt <= 3) {
        await sleep(3000 * attempt);
        return updateOne(access, sid, pageId, name, attempt + 1);
      }
    }
    return { ok: false, err: msg.slice(0, 200) };
  }
  const result = call.payload.result || {};
  if (result.isError) {
    const text = (result.content || []).map((c) => c.text || "").join(" ");
    return { ok: false, err: text.slice(0, 200) };
  }
  return { ok: true };
}

(async () => {
  let access = readJson(TOK_FILE).access_token;
  const init = await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "zcode-names-push", version: "1.0" },
  }, null, access);
  if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  const ids = Object.keys(NAMES);
  const missing = ids.filter((id) => !URLS[id]);
  if (missing.length) {
    console.log("NO PAGE URL FOR:", missing.join(", "));
  }
  const todo = ids.filter((id) => URLS[id]);
  console.log("updating", todo.length, "pages...");

  let ok = 0;
  const failures = [];
  let authRetried = false;
  for (let i = 0; i < todo.length; i++) {
    const id = todo[i];
    try {
      const r = await updateOne(access, sid, URLS[id], NAMES[id]);
      if (r.ok) ok++;
      else failures.push(id + " -> " + r.err);
    } catch (e) {
      if (String(e.message).startsWith("AUTH:") && !authRetried) {
        authRetried = true;
        console.log("auth error, refreshing token...");
        access = await refreshToken();
        i--; // retry same item
        continue;
      }
      failures.push(id + " -> " + e.message.slice(0, 200));
    }
    if ((i + 1) % 10 === 0) console.log("...", i + 1, "/", todo.length);
    await sleep(200);
  }

  console.log("DONE ok=" + ok + " fail=" + failures.length);
  if (failures.length) {
    console.log("FAILURES:");
    for (const f of failures) console.log("  " + f);
    process.exit(1);
  }
})().catch((e) => {
  console.log("FAILED:", e.message);
  process.exit(1);
});
