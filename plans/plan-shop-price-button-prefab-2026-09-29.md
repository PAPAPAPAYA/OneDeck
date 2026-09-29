# Plan: 买/卖价格按钮 Prefab 化 — 纯样式框架 (2026-09-29)

- **Date**: 2026-09-29
- **Status**: IMPLEMENTED 2026-09-29（本会话实施：PriceButton.prefab 变体烘焙 + ShopCardView prefab 分支 + 场景接线；零视觉探针 23/23 PASS（fallback vs prefab 分支逐项相等，含 re-parent 与 `_faceColor` 捕获）；EditMode 664/663/1 Ignore = 基线零漂移（新程序集上；前两次 run 因 CS0136 编译错静默跑旧 DLL，离线 csproj build 抓出后修复重跑）；§4.3 Play 目测矩阵待用户。EditMode 坑：打包 prefab 实例内部子物体 SetParent 被静默忽略（探针须先 Unpack；Play 不受影响））
- **Request (user, 2026-09-29)**: 售出 / 购买按钮（卡牌上的价格按钮）做与重掷按钮同样的 prefab 化处理。
- **Reads**: `plans/plan-shop-reroll-button-prefab-2026-09-29.md`（同款机制 + 共享 `_faceColor` 捕获修复）, `plans/plan-shop-price-text-style-centralization-2026-09-29.md`（价签文字样式集中化——本计划管不到的第三件事）, `plans/plan-shop-hud-prefab-widgets-2026-09-23.md`, `docs/UIUX_Guidelines.md` §2/§3.1

## 1. Goal / non-goals

Goal:
1. 新建 `Assets/Prefabs/ShopHud/PriceButton.prefab`（`Tpl_WorldButton` 变体，**删 Label 子物体**）：三层颜色（PaletteTint）、z 层级、按压手感、sprite 全部 prefab 数据；全池买/卖按钮共用一颗样式源。
2. `ShopCardView.EnsurePriceButton` 加 prefab 分支：实例化框架 → re-parent 价签 print → wire label；每帧 `UpdatePriceDisplay` 的双态逻辑一行不动。
3. 零视觉变化 + 零行为变化。

Non-goals:
- **价签文字的字体/字号/加粗不进本计划**——print 序列化在共享壳模板（PhysicalCardParent / MinionPhysicalCardParent 共 2 颗）上，集中化 = 编辑模板，是独立工作项（姊妹计划）。注：初版「print 逐张序列化、130+ 张无共享基底」的判断 2026-09-29 晚已反转（4.0 池 116 张卡 prefab 无一含 TMP），见姊妹计划 Correction。
- **面片尺寸不进 prefab**——face 天生动态（按 print textBounds + padding 重算），`ResizePriceButtonFace` 保留为尺寸权威。
- 卡牌 enlarge/restore（`HandleClickToRestore` / `EnlargeCard`）、`PriceStrikeLine` 划线、买/卖 action、disabled + 卡面变暗逻辑——全保留代码。
- 重掷按钮（姊妹计划）、`PhysButton` 状态机、palette、fallback 链。

## 2. Current state (verified facts, 2026-09-29)

### 2.1 与重掷按钮的三处本质差异

1. **label 外置**：`EnsurePriceButton`（ShopCardView.cs:162-212）是「围着现有 print 搭框架」——print（`CardPhysObjScript.cardPricePrint`，CardPhysObjScript.cs:35）re-parent 进 Visual（:198-199，localPosition (0,0,printZ=−0.02)），`button.label = print`。按钮 prefab 里没有 Label 子物体可烘。
2. **面片尺寸动态**：`ResizePriceButtonFace`（:215-225）每次价格文本变化按 `print.textBounds × localScale(0.2) + PRICE_FACE_PAD_X 0.22 / Y 0.14` 重算 face/collider/shadow（`ConfigureWorldFaceSize`）。首刷必写（`_lastPriceText` 初始 null → `SetPriceText` 必走到 :137），prefab 烘的面尺寸只是首帧前占位。→ 重掷那套「尺寸权威 = prefab + SyncColliderToFace」在此**不适用**（物理差异，非取舍）。
3. **双态全代码**：buy/sell action 翻转（:88）、hoverText 买入/售出（:89）、disabled + `SetFaceDimmed`（:86-87）、折扣价 `<s>` markup + `PriceStrikeLine`（:91-108）——每帧 `UpdatePriceDisplay` 驱动，本来就是逻辑。

### 2.2 运行时装配事实（prefab 烘焙 = 等价替换这些字面量）

| 项 | 现值（ShopCardView.cs） | prefab 烘焙 |
|---|---|---|
| 层级 | Root(BoxCollider2D)/Visual/Shadow/Face + 外挂 print（:172-201） | 同层级，print 槽位由代码 re-parent |
| Shadow z | printZ + 0.08 = **0.06**（:183） | 烘进 Shadow localPosition |
| Face z | printZ + 0.04 = **0.02**（:191） | 烘进 Face localPosition |
| sprite | `_cardPhysObj.cardFace.sprite`（shadow+face 同源，:185/:193，卡面 sprite 资产） | 烘 chromeSprite（`ShopUXManager.chromeSprite` 本来就是同一卡面 sprite 资产）；见 §3.2 决策 |
| 颜色 | `GameColorPalette.CardShadowColor` / `.ButtonFaceColor`（:187/:195） | PaletteTint 槽位继承模板：Shadow=CardShadow(5) / Face=ButtonFace(13)——与现值一一对应 |
| 手感 | restShadow = hoverLift = 4px×(3.2/118) ≈ **0.1085**，denyShift = 6px ≈ **0.1628**（:205-207） | **模板值是 0.05/0.05/0.075，变体必须 override 三项**（恰为 PhysButton 字段默认值 0.1085/0.1085/0.1628） |
| label | `print`（外置） | 代码 `button.label = print` |

### 2.3 依附事实

- `EnsurePriceButton` 惰性构建、每次 shop phase 显示才激活（:64-76 showPrice 门控）；买（shopItemIndex ≥ 0）/卖（玩家卡组卡）/升级行 utility 卡全部走同一条路径 → 一颗 prefab 覆盖全部实例。
- 根放置：`printT.parent` 下、localPosition = print 原位、localScale = 1（:173-176）——按钮是卡层级子物体，继承卡 TargetScale，卡本地单位烘焙成立。
- `PhysButton._faceColor` 捕获缺口与重掷同款（PhysButton.cs:62/:162/:316-320）：prefab 分支实例化后必须补 `SetWorldFace(face)`，否则 disabled 恢复 face 变纯白。修复收进本计划 + 姊妹计划各补一句（公共 API，零 PhysButton 改动）。
- 变体删继承子物体走 `m_RemovedGameObjects`（Unity 变体标准能力，ExitButton 文件可见该字段）。

## 3. 方案

### 3.1 烘焙 PriceButton.prefab

1. `Tpl_WorldButton` 变体，命名 `PriceButton`，**删除 Label 子物体**（print 外置，代码 re-parent）。
2. Override：手感三项 restShadow/hoverLift = 0.1085、denyShift = 0.1628（§2.2）；Shadow localPosition z = 0.06、Face z = 0.02（Face x/y 0，print 位 −0.02 由代码放）。
3. Face m_Size 烘一个合理占位（如 1×0.4），首刷即被 `ResizePriceButtonFace` 覆写；无需 SyncColliderToFace（运行时 `ConfigureWorldFaceSize` 本来就每帧口径重写）。
4. PaletteTint 槽位继承模板（CardShadow / ButtonFace）；改色 = 改槽位或 ColorSO 资产。
5. sprite：**决策——烘 chromeSprite，但代码里 copy 卡面 sprite 的两行先保留**（零风险：个别卡若用不同 face sprite 不破；确认全池同资产后可删 copy）。PaletteTint 管颜色，sprite copy 只管形状来源，两者不冲突。

### 3.2 代码改动清单

| 文件 | 改动 |
|---|---|
| `ShopUXManager.cs` | 新增 `public GameObject priceButtonPrefab;`（与 rerollButtonPrefab 同排） |
| `ShopCardView.cs` `EnsurePriceButton` | prefab 分支：`Instantiate(priceButtonPrefab)` → 根 localPosition = print 原位 / rotation identity / scale 1（同 :174-176）→ print re-parent 进 Visual（:198-199 原样）→ `button.label = print` → `SetWorldFace(face 子物体)`（补捕获）→ （sprite copy 行视 §3.1-5 决策保留）；**未接线回退**现运行时构建（保测试 / 旧场景） |
| `ShopCardView.cs` 其余 | `UpdatePriceDisplay` / `SetPriceText` / `ResizePriceButtonFace` / enlarge / buy/sell / strike line **零改动** |

## 4. Verification gates

1. **零视觉基线**：买（有/无折扣）、卖、utility 升级行三类实例改前后逐位对比（face/shadow 尺寸、颜色、z）；disabled 变暗 + 恢复颜色正确（`_faceColor` 回归点）。
2. **EditMode 套件** 664/663/1 基线（stale-assembly 纪律：refresh_unity + DLL mtime 核验；run_tests 前先 SaveOpenScenes）。
3. **Play 目测矩阵**（用户）：买入 affordable→unaffordable 变暗/恢复；hover 抬升 + 买入/售出 hoverText 换字；折扣卡划线显示/隐藏（hover 时藏）；点击买入后货架 refit；卖出后卡位回填；滚动输入门。
4. 行尾纪律：改动 `.cs` CRLF + Tab，`ls-files --eol` 验证。

## 5. 坑与注意

- print re-parent 顺序：必须先实例化框架再 re-parent（print 的 localPosition/localScale 由代码定，:199 保持 printZ=−0.02）。
- `PriceStrikeLine` 是 print 的子物体，随 re-parent 自动跟进，无需处理。
- 回退路径保留 → `EnsurePriceButton` 的旧构建代码不删，只加分支。
- 卡牌 enlarge 时按钮随卡整体缩放（卡层级子物体），prefab 化不改变此行为——勿在 prefab 根上烘非 1 scale。

## 6. Open items

1. 全池 cardFace sprite 同资产核验后，删代码里的 sprite copy（1 行 + 1 行）。
2. 价签文字样式集中化 → 姊妹计划 `plans/plan-shop-price-text-style-centralization-2026-09-29.md`，路径待用户拍板（批量烘 prefab vs 运行时权威）。
