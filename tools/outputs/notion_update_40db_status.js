// Mark 4.0 card database rows 备用/已删 (2026-08-28 audit) + append reason to side note.
// Usage: node notion_update_40db_status.js
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";

// id: page id, status: 备用|已删, note: FULL new side note (old + appended line)
const UPDATES = [
  { id: "3c7827b8c3c1811bb20edf8fe4db7fea", status: "备用", note: "尝试延后\n【2026-08-28 备用】延后机制本期不加，机制落地后再启用" },
  { id: "3c7827b8c3c18198b4b6ccbd6548cf53", status: "备用", note: "尝试回响\n【2026-08-28 备用】回响机制本期不加，机制落地后再启用" },
  { id: "3c8827b8c3c18052860ac523f2f5492a", status: "备用", note: "【2026-08-28 备用】交换机制本期不加，机制落地后再启用" },
  { id: "3c7827b8c3c1812eaa7dc0280803fe44", status: "已删", note: "链接信徒和埋葬，具体的效果需要是信徒触发揭晓效果前，埋葬卡组顶（而非揭晓区中的信徒），不然就是信徒复活后直接埋葬掉了\n【2026-08-28 已删】磨牌引擎三胞胎删并（保留 43/114）" },
  { id: "3c7827b8c3c18172ab66fe64d95d2e06", status: "已删", note: "同样是链接信徒和诅咒，是比较简单的变种\n【2026-08-28 已删】诅咒引擎同型删（保留 76/38）" },
  { id: "3c7827b8c3c1818098ccc11a493bb085", status: "已删", note: "【2026-08-28 已删】大磨牌三张删并（保留 17/77）" },
  { id: "3c7827b8c3c18180ad90ed7b9956b076", status: "已删", note: "链接埋葬和信徒，也是非埋葬的构筑一个反制埋葬的手段。对于埋葬构筑而言，功能类似于给遗言卡一个被揭晓的路径，因为遗言卡在埋葬构筑中一般会被直接埋葬而不会被揭晓，信徒能复活被埋葬了的卡）\n【2026-08-28 已删】信徒生成引擎同型删（保留 15/41）" },
  { id: "3c7827b8c3c181888238e58d449d5462", status: "已删", note: "添加了一个需要满足的条件才能让效果常时生效从而控制强度\n【2026-08-28 已删】遗言引擎密度删" },
  { id: "3c7827b8c3c1818d8a2af07fea52b26e", status: "已删", note: "给埋葬构筑链接、加一个强化乘区，因为当埋葬次数多时，这张卡会复活很多次，相当于多次攻击，但是可能的问题是难以被强化到，需要关注\n【2026-08-28 已删】复活自身反应三张删并" },
  { id: "3c7827b8c3c181a7b583e02efaf009bc", status: "已删", note: "【2026-08-28 已删】放逐转换四连删并（保留 108/109）" },
  { id: "3c7827b8c3c181ceb48ed0e844a5ca79", status: "已删", note: "链接埋葬和其他构筑，加上了非生物的限定控制强度，但是需要看这个限定是哪种会比较好，比如可能换成生物\n【2026-08-28 已删】授遗言引擎密度删" },
  { id: "3c7827b8c3c181fcb2a9ef4ef53ef0d6", status: "已删", note: "【2026-08-28 已删】与 63 RIFT_PRIEST 孪生（生成2信徒）删一" },
  { id: "3c7827b8c3c18126800ef545e68fcbdb", status: "已删", note: "【2026-08-28 已删】与 92 SNOWBALL 孪生（被强化成长）删一" },
  { id: "3c7827b8c3c181839d4ade4253fc7c79", status: "已删", note: "【2026-08-28 已删】复辟合并删（复活敌方诅咒×4 冗余）" },
  { id: "3c7827b8c3c181879aafffd6b12de80b", status: "已删", note: "【2026-08-28 已删】复辟四重奏删并（复活攻击力最高敌方×5 冗余）" },
  { id: "3c7827b8c3c181d1a443eee0ec57c0dd", status: "已删", note: "【2026-08-28 已删】谓词复活族删（与 86/99 重叠）" },
  { id: "3c7827b8c3c181ee909ef789a0f746e7", status: "已删", note: "【2026-08-28 已删】复辟四重奏删并（复活攻击力最高敌方×5 冗余）" },
  { id: "3c7827b8c3c181f8b215efdf602bf450", status: "已删", note: "一方面是强代价的强化卡，另一方面可以被诅咒构筑利用，因为诅咒构筑会不断强化敌方的诅咒\n【2026-08-28 已删】复辟四重奏删并（复活攻击力最高敌方×5 冗余）" },
  { id: "3c8827b8c3c18088b3e1fe19e7297bc7", status: "已删", note: "【2026-08-28 已删】被 73 KINGSLAYER 严格上位" },
  { id: "3c8827b8c3c180a6b644e7eb58178a6e", status: "已删", note: "【2026-08-28 已删】放逐转换四连删并（保留 108/109）" },
];

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

async function run(access) {
  let init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-40db-status", version: "1.0" } }, null, access);
  if (init.payload.error || init.status >= 400) throw new Error("init failed: " + JSON.stringify(init.payload.error || init.status));
  const sid = init.sid;
  await rpc("notifications/initialized", {}, sid, access);

  for (const u of UPDATES) {
    const call = await rpc("tools/call", {
      name: "notion-update-page",
      arguments: {
        page_id: u.id,
        command: "update_properties",
        properties: { "状态": u.status, "side note": u.note },
      },
    }, sid, access);
    const text = (call.payload.result && call.payload.result.content || []).map(c => c.text || "").join("\n");
    if (call.payload.error) console.log("FAIL " + u.id + ": " + JSON.stringify(call.payload.error).slice(0, 200));
    else console.log("OK   " + u.id + " " + u.status + " | " + text.slice(0, 100).replace(/\n/g, " "));
  }
}

(async () => {
  try {
    let access = readJson(TOK_FILE).access_token;
    try { await run(access); }
    catch (e) {
      if (/auth|401|unauthorized|invalid_token/i.test(e.message)) {
        console.log("auth error, refreshing token and retrying...");
        access = await refreshToken();
        await run(access);
      } else throw e;
    }
  } catch (e) { console.log("FAILED:", e.message); process.exit(1); }
})();
