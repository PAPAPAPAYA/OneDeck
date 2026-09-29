# Plan: 价签文字样式集中化 (2026-09-29)

- **Date**: 2026-09-29（20:45 事实修正改写；20:50 场景实例删除更新；20:55 样式拍板并落地）
- **Status**: **IMPLEMENTED（样式已烘，2026-09-29）** — Bold + 字号 5 已写入 `PhysicalCardParent.prefab`（20.3 → 5，Bold 原已满足）；§4 验证门**转用户侧**（本会话无 Unity MCP，EditMode 基线须编辑器侧跑；Play 目测本就用户）
- **Correction (2026-09-29, 修正初版错误事实)**: 初版 §2 声称「print 逐张序列化在 130+ 张卡 prefab、无共享基底、改字号需动 130+ 张」——**核验有误，全部反转**：4.0 池 116 张卡 prefab 无一含 TMP 样式数据；价签 print 只序列化在 2 颗共享壳模板上，运行时全池共用。初版路线 A「批量烘 130+ prefab」随之作废，改写为「编辑模板 + 清理场景覆盖」。修正经过：价签按钮 prefab 化评审 → 用户追问「为何 130+ 张卡不能共享基底」→ 逐张核验发现现状已是共享基底。
- **Update (2026-09-29 20:46)**: 用户已删除 GameScene 中唯一的 `PhysicalCardParent` 场景实例——5 条 print override 随之消失。已核验：`m_SourcePrefab`（PhysicalCardParent guid）在 GameScene 命中 0、print fileID（133791163835700354）命中 0；`physicalCardPrefab` 管理器引用不受影响、符合预期。§3 第 3 步完成，§5.3 关闭。
- **Update (2026-09-29 20:49, 拍板 + 落地)**: 用户拍板——**minion 模板不用管**（§5.2 关闭）；**目标样式 = Bold + 字号 5**。层级核验结论：价签 print 是 `PhysicalCardParent` 变体的**新增物体**（基底 `PhysicalCard.prefab` 无 `cardPricePrint`，其 3 个 TMP 是 name/desc 等其它文字），编辑面无歧义。已写入 `PhysicalCardParent.prefab`（:1001-1002，`m_fontSize` / `m_fontSizeBase` 20.3 → 5；`m_fontStyle: 1` Bold 原值保留）。量级提示：print localScale 0.2 不变 → 价签线性尺寸约为原 1/4；`ResizePriceButtonFace` 按 textBounds 自适应，按钮面自动跟随缩小。
- **Request (user, 2026-09-29)**: 买/卖按钮 prefab 化时提出——价签（`cardPricePrint`）的字体 / 字号 / 加粗等文字样式希望能一处配置全局生效。本计划是独立于按钮 prefab 化的第三件事。
- **Reads**: `plans/plan-shop-price-button-prefab-2026-09-29.md`（print 与按钮框架的关系）, `plans/plan-shop-reroll-button-prefab-2026-09-29.md`（§2.2 变体字号死数据坑）, `plans/plan-shop-hud-prefab-widgets-2026-09-23.md`, `docs/ConsistencyTool.md`, memory: `card-desc-multiline-20260908`（PhysicalCardParent 覆盖项批量烘先例——注意其对象是模板不是卡池）, `card-prefab-config-conventions`

## 1. Goal / non-goals

Goal:
1. 价签文字的**样式**（字体资产 / 字号 / 加粗 / 颜色）获得单一配置源——在共享壳模板上序列化，改一处 → 全池（含所有卡池，见 §2.2）买/卖/升级行价签生效。**已完成 = Bold + 字号 5。**
2. 价格**文本内容**（`$5` / `<s>$10</s> $7`）不动——那本来就是 `ShopCardView.SetPriceText` 的代码逻辑。

Non-goals:
- 按钮框架本身（颜色 / 层级 / 手感）→ 姊妹计划 PriceButton.prefab。
- 卡面 desc / 名称等其它卡面文字的样式（print 之外的 TMP）。
- 价格数值逻辑（GetCardPrice / 折扣 / 半价）。
- ~~Minion 模板价签对齐~~ —— 用户拍板不用管（2026-09-29 20:49）。

## 2. Current state (verified facts, 2026-09-29 晚复核)

1. **卡 = 数据片段，壳 = 共享模板**（运行时装配，共享基底已是现状）：
   - 4.0 池 116 张卡 prefab 全部不含 TMP 文字样式：全池 `m_fontSizeBase` 命中 0；抽查 `Assets/Prefabs/Cards/4.0/0_Common/CURSE_REVIVER.prefab` = 311 行、0 个 PrefabInstance 块、无 TMP 组件——纯效果/数据片段（CardScript + GameEventListener/Effect + attack）。
   - 价签 print（`CardPhysObjScript.cardPricePrint`，CardPhysObjScript.cs:35，public TextMeshPro）**只序列化在 UX 壳模板上**：全 `Assets/Prefabs` 范围 `cardPricePrint` 仅 4 处——`PhysicalCardParent`（实挂，fileID 133791163835700354，**变体新增物体**）、`MinionPhysicalCardParent`（实挂，用户拍板弃管）、`StartCardParent` / `EmptyCardSpace`（`fileID: 0` 空引用）。
2. **壳模板 = 全池卡的物理实例来源**：`GameScene.unity` 中 ShopUXManager / CombatUXManager 的 `physicalCardPrefab` 均指向 `PhysicalCardParent`；`ShopUXManager.cs:183` / `:663` 每卡 `Instantiate` 壳 + 嫁接卡数据。3.0 no cost 现行池与 `_DEPRECATED` 池的卡同样走该壳 → **模板编辑对所有池天然生效**（初版「3.0/_DEPRECATED 是否同批处理」的拍板问题自动消解）。
3. **模板是变体链**：`PhysicalCardParent` 是 `PhysicalCard.prefab`（guid 142b009c…）的变体；价签 print 为变体新增物体（基底无 print），样式烘在 `PhysicalCardParent.prefab` 本体——2026-09-29 落地值：字号 **5** + `m_fontStyle: 1`（Bold 合成加粗，穿 CJK fallback 零资产）。
4. **场景实例覆盖坑（已解决 2026-09-29 20:46）**：原 `GameScene.unity` 唯一一颗 `PhysicalCardParent` 场景实例对价签 TMP 挂 5 条 override——09-28 chip「实例 override 盖住变体值」同款。**用户已删除该实例，override 一并清除**（核验：guid 与 print fileID 在场景中均 0 命中）。此类坑的防范仍保留在 §3.4 防漂移约定。
5. 运行时无人改 print 样式：`ShopCardView` 只写 text + ForceMeshUpdate（ShopCardView.cs:133-136），`PhysButton` hover 只换 text——样式完全由模板序列化值决定。

## 3. 方案与执行记录（初版路线 A/B 拍板已因事实反转而失去意义；收窄版 4 步，已完成 3 步）

~~路线 A：批量烘 130+ 卡 prefab~~ —— 作废。无 130+ 份样式可烘（卡 prefab 里没有 print）。
~~路线 B：运行时样式权威~~ —— 无存在必要。模板架构下 prefab 值即唯一真相源，运行时刷样式只会制造两层真相（与 prefab 权威化主线相悖）。

1. ~~**定位样式层**~~ —— **已完成**：价签 print 是 `PhysicalCardParent` 变体新增物体，基底 `PhysicalCard.prefab` 无 print；编辑面 = `PhysicalCardParent.prefab` 本体。
2. ~~**编辑模板**~~ —— **已完成（20:49-20:55）**：`m_fontSize` / `m_fontSizeBase` 20.3 → **5**（Bold 原已满足，保留 `m_fontStyle: 1`）。minion 模板按用户拍板不动。
3. ~~**清理场景覆盖**~~ —— **已完成（20:46）**：用户删除 GameScene 中该实例，5 条 print override 随实例消失，核验通过。
4. **防漂移（约定，长期有效）**：价签样式权威 = 模板序列化值；今后任何场景实例不得再对 print 写字段 override（code review / 目测时遇到即删）。

## 4. Verification gates（用户侧执行：本会话无 Unity MCP，EditMode 基线跑不了；Play 目测本就用户）

0. **执行方说明（2026-09-29 21:06）**：本计划由无 Unity MCP 的会话实施（直接 YAML 编辑 prefab）——prefab-stage 检查（gate 1）、EditMode 基线（gate 3）须由用户在编辑器完成，或交回有 Unity MCP 的会话。Play 目测矩阵（gate 4/5）无论哪条路径都是用户目验。
1. prefab-stage 检查：`PhysicalCardParent` 价签 = 字号 5 + Bold；变体无未预期 override。
2. ~~场景覆盖清理~~ —— 已随实例删除完成（20:46，核验 0 命中）；后续仅防回归。
3. EditMode 套件基线 664/663/1；stale-assembly 纪律（refresh_unity + DLL mtime 核验）+ `run_tests` 前先 `SaveOpenScenes`。
4. Play 目测：买 / 卖 / 升级行价签样式一致（Bold + 小字号，约为原 1/4 线性尺寸）；折扣 `<s>` 划线跟 textBounds 不错位（`PriceStrikeLine` 理论自适应）；价签缩小后按钮可点击区域仍舒适（face 随 textBounds 自适应，但 0.86 单位级面片偏小，目测确认）。
5. 字号变化连带检查：`ResizePriceButtonFace` 按 textBounds 自适应抱合（自动跟随）；卡面其它 TMP（name/desc）不受影响（print 是独立物体）。

## 5. Open items

1. ~~样式目标值~~ —— **已拍板（20:49）**：Bold + 字号 5，已写入。
2. ~~Minion 模板对齐~~ —— **关闭（20:49）**：用户拍板不用管。
3. ~~GameScene 场景实例定性~~ —— **已关闭（20:46）**：用户直接删除实例，override 问题根除。
