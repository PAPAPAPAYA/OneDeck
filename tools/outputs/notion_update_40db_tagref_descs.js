// Update 4.0 card database descs per tag-reference convention v2 (2026-09-02):
// card-type references stay bare (信徒/诅咒 = the RIFT/JU_ON tokens); true tag
// selections rewritten as "N张tag为【X】的…卡"; RELIC_RIFT_OVERRIDE strips its tag
// brackets (its implementation is typeID-keyed, not tag-keyed).
// Usage: node notion_update_40db_tagref_descs.js
const fs = require("fs");
const path = require("path");

const UPDATES = [
  {
    id: "3c7827b8-c3c1-81d4-872e-e2a3a22a3ee9", // RELIC_RIFT_OVERRIDE
    desc: "被动：友方信徒效果变为：\"复活1敌方诅咒；放逐自身\"",
    newSide: "【2026-08-31 已实现】仅改友方信徒：RIFT token 的复活容器改绑 RiftOverrideAwareReviveEffect（flag 开后友方信徒揭晓复活敌方诅咒，敌方信徒保持默认；ExileSelf 半条不动）\n【2026-09-02 描述规范v2】desc 去 tag 括号：信徒/诅咒=卡指代（typeID 键控 JU_ON）；【X】记法仅用于 tag 选区",
  },
  {
    id: "3c7827b8-c3c1-8109-9754-f92441794ddd", // RIFT_SHEPHERD
    desc: "攻击；复活1张tag为【信徒】的友方卡",
    newSide: "2026-09-01 复活选区由 RIFT token（typeID=RIFT）改为【信徒】tag（ReviveMyCardsWithTag）；RIFT token 本体无 tag 不在池内（已拍板）；2×牧者互活死循环已拍板接受\n【2026-09-02 描述规范v2】desc 改写为 tag 选区句式",
  },
  {
    id: "3c7827b8-c3c1-81c2-96d5-c6409f561249", // EULOGIST
    desc: "埋葬1张tag为【遗言】的友方卡；攻击",
    newSide: "定向埋葬\n【2026-09-02 描述规范v2】埋葬为 tag 选区（BuryMyCardsWithTag），desc 改写为 tag 选区句式",
  },
  {
    id: "3c7827b8-c3c1-81ec-bf01-d78f6bd989aa", // GRAVE_PUPPETEER
    desc: "让墓地1友方生物攻击；墓地无友方则埋葬1张tag为【遗言】的友方卡",
    newSide: "【2026-08-30 裁定】A 解读：墓地生物原地攻击（不离开墓地、以自身攻击力结算）；目标限定生物、随机1张、攻击后留墓地；空放分支=埋葬1友方遗言卡（再往前继承作者'空放分支'设计意图）\n【2026-09-02 描述规范v2】兜底埋葬为 tag 选区（fallbackBurier.BuryMyCardsWithTag），desc 改写为 tag 选区句式",
  },
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

async function callTool(access, sid, name, args) {
  const call = await rpc("tools/call", { name, arguments: args }, sid, access);
  if (call.payload.error) throw new Error(name + " failed: " + JSON.stringify(call.payload.error).slice(0, 300));
  const result = call.payload.result || {};
  const text = (result.content || []).map(c => c.text || "").join("\n");
  if (result.isError) throw new Error(name + " isError: " + text.slice(0, 300));
  return text;
}

async function run(access) {
  const init = await rpc("initialize", {
    protocolVersion: "2025-06-18",
    capabilities: {},
    clientInfo: { name: "zcode-40db-tagref", version: "1.0" },
  }, null, access);
  if (init.payload.error) throw new Error("initialize failed: " + JSON.stringify(init.payload.error));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  for (const u of UPDATES) {
    await callTool(access, sid, "notion-update-page", {
      page_id: u.id,
      command: "update_properties",
      properties: { "card desc": u.desc, "side note": u.newSide },
    });
    console.log("UPDATED " + u.id);
  }

  // Verify via per-page fetch (DB query views can serve stale cache).
  for (const u of UPDATES) {
    const key = u.desc.slice(0, 12);
    const pageUrl = "https://app.notion.com/" + u.id.replace(/-/g, "");
    const text = await callTool(access, sid, "notion-fetch", { id: pageUrl });
    console.log((text.includes(u.desc) ? "VERIFY_OK  " : "VERIFY_MISS") + " " + u.id + " | " + key + "…");
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
