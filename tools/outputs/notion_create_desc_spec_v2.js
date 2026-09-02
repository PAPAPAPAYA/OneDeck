// Create "4.0 card desc 书写规范 v2" page under OD (sibling of the v1 spec page).
// Usage: node notion_create_desc_spec_v2.js
const fs = require("fs");
const path = require("path");

const CRED = "C:/Users/damen/.kimi-code/credentials/mcp";
const TOK_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-tokens.json");
const CLIENT_FILE = path.join(CRED, "notion-c4e3b68fbe50d678f30d0e3b-client.json");
const TOKEN_URL = "https://mcp.notion.com/token";
const MCP_URL = "https://mcp.notion.com/mcp";
const OD_PAGE_ID = "333827b8-c3c1-80fa-9bae-eb547a15270d";

function readJson(p) { return JSON.parse(fs.readFileSync(p, "utf-8").replace(/^\uFEFF/, "")); }

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
	return { sid, payload };
}

const CONTENT = `# 目的
区分两种指代：**卡指代**（特定一张卡，cardTypeID 键控）与 **tag 指代**（所有带该 tag 的卡）。v1 的句子结构不变，本页新增指代记法。与 repo \`docs/CardDesc_TagReference_Convention_v2.md\` 同步。
# 铁律
1. **卡指代 → 裸写卡名**：信徒 = 次元裂缝 token（typeID=RIFT）；诅咒 = 诅咒 token（typeID=JU_ON）。例：\`复活1敌方诅咒\`（typeIDFilter=JU_ON）、\`生成1信徒\`（生成 token 本体）
2. **tag 指代 → 强制携带短语**：\`N张tag为【X】的[主人]卡\`。【】从此只允许出现在 tag 短语内部——卡名、强调、状态一律不用括号
3. **条款头触发词豁免**：\`遗言：\` \`苏醒：\` \`回响：\` \`被动：\` \`强化反应：\` 是本卡自指语法，保持裸写，不套 tag 短语
4. **状态/数值条件不用括号**：被强化（attackGrowth>0）、攻击力最高、非生物等直接写条件文字
# 句式表
| 场景 | 模板 | 示例 |
| --- | --- | --- |
| 选区目标 | 动词 N 张tag为【X】的[主人]卡 | 复活1张tag为【信徒】的友方卡 |
| 全体 | 动词 所有tag为【X】的[主人]卡 | 埋葬所有tag为【诅咒】的敌方卡 |
| 触发 | tag为【X】的[主人]卡[动作]时： | tag为【诅咒】的敌方卡揭晓时：复活1友方 |
| 计数 | 每有1张tag为【X】的[主人]卡，… | 每有1张tag为【信徒】的友方卡，强化1敌方诅咒 |
| 存在 | 若无tag为【X】的[主人]卡，… | 若无tag为【诅咒】的敌方卡，生成1敌方诅咒 |
# Unity 渲染映射
prefab cardDesc 写 \`tag为【<tag:Believer>】的友方卡\`；\`ComputeDynamicCardDesc\` 把 \`<tag:X>\` 替换为 TagTooltipDatabase 显示名（Believer→信徒、DeathRattle→遗言），卡面渲染 \`tag为【信徒】的友方卡\`，与 DB 逐字一致。
# 引擎键控速查
- **tag 键控（用 tag 短语）**：\`ReviveMyCardsWithTag\` / \`BuryMyCardsWithTag\` 等 *WithTag 方法——现役 3 张：RIFT_SHEPHERD、EULOGIST、GRAVE_PUPPETEER（兜底分支）
- **typeID 键控（裸卡名）**：\`EnhanceCurse*\`（FindEnemyCardWithTypeID(JU_ON)，无则生成）、\`onEnemyCurseCardRevealed\`（cardTypeID==JU_ON 判定）、\`RiftOverrideAwareReviveEffect\`（仅绑 RIFT token + typeIDFilter=JU_ON）
# 2026-09-02 迁移记录
- 正向改写 3 张：RIFT_SHEPHERD / EULOGIST / GRAVE_PUPPETEER
- 反向去括号 20 处：生成系 [信徒]×7、typeID 键控 敌方[诅咒]×9 文件、ELITE_REVIVER【被强化】（状态词）
- WEAKENING_FIELD：JU_ON 转非生物(Status)后，生物过滤天然排除诅咒，desc 删「除了诅咒」特例
- 清理 6 张死配置 tagsToCheck:[None]；Notion 4.0 DB 同步 6 页 + side note 审计行`;

async function run(access) {
	const init = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "zcode-spec-v2", version: "1.0" } }, null, access);
	const sid = init.sid;
	await rpc("notifications/initialized", {}, sid, access);
	const call = await rpc("tools/call", {
		name: "notion-create-pages",
		arguments: {
			parent: { page_id: OD_PAGE_ID },
			pages: [{ properties: { title: "📐 4.0 card desc 书写规范 v2：卡指代与 tag 指代" }, icon: "📐", content: CONTENT }],
		},
	}, sid, access);
	const text = (call.payload.result && call.payload.result.content || []).map(c => c.text || "").join("\n");
	console.log(text.slice(0, 800));
}

(async () => {
	try {
		let access = readJson(TOK_FILE).access_token;
		try { await run(access); }
		catch (e) {
			if (/auth|401|unauthorized|invalid_token/i.test(e.message)) {
				console.log("auth error, refreshing...");
				access = await refreshToken();
				await run(access);
			} else throw e;
		}
	} catch (e) { console.log("FAILED:", e.message); process.exit(1); }
})();
