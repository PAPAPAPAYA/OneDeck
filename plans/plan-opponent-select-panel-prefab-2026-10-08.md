# Plan: 选敌页候选面板 prefab（OpponentSelectPanel）

日期：2026-10-08
状态：**P-S1..S4 已实施（2026-10-09，「颜色用已有的」裁定 + 修改代码授权），EditMode 零漂移，Play 目检待做**。实施记录见 §11。
关联：`plans/plan-opponent-select-page-2026-10-06.md`（主计划；本计划 = §4.3 选敌页内容的第一个交付物细化）、`docs/demo/OpponentSelectDemo.html`（v4 视觉规格）、`plans/plan-hp-name-plate-2026-10-07.md`（名牌规格）、`plans/plan-shop-hud-prefab-widgets-2026-09-23.md`（模板+变体 prefab 体系）

## 0. 拍板记录（2026-10-08 对话）

| # | 决策点 | 拍板 |
|---|--------|------|
| P1 | prefab 内容边界 | **全包含单 prefab**：面板底 + 3 关键卡槽 + 敌方名牌 + 选择按钮全为子节点。morph 飞行 = 子节点按世界坐标 tween 飞出（面板根只淡出不动位），reset 飞回——不需要运行时 reparent |
| P2 | 关键卡做法 | **真实卡面实例**：prefab 内置 3 个卡槽锚点 + 占位卡实例；运行时页面代码替换为 ghost 卡组真实卡 prefab 实例（缩放后≈demo 视觉幅面）。接受卡面为米色而非 demo 红色 |
| P3 | 选择按钮 +$N | **带 +$N 占位**：prefab 文本 authored「选择 +$0」，页面运行时按占位值填充（纯展示，不接 purse；+$N 语义未拍板，见主计划 §9 与 demo NOTE） |

## 1. 目标

把 `docs/demo/OpponentSelectDemo.html` v4 的单个候选面板落成一个可独立编辑、可独立调参的 world-space prefab：`Assets/Prefabs/ShopHud/OpponentSelectPanel.prefab`。页面实例化 N 个、摆位与交互相机接线属于主计划 S2/S3，不在本 prefab 范围（§9）。

## 2. 生态现状（2026-10-08 核验事实）

- ShopHud prefab 家族 = 「模板 `Tpl_*` + PrefabInstance 变体」：`RerollButton.prefab` 等是对 `Tpl_WorldButton`（guid `bad20356...`）的 wrapper 文件；`ShopHudPage/V2` 是模板实例的组合。本计划沿用该模式。
- 面板底 = `chromeSprite`（= `Assets/Sprites/RoundedCorner.png`，`ShopUXManager.chromeSprite`，GameScene 已接）切片 SpriteRenderer + `PaletteTint` `ShopPanelBg` 槽 —— 正是 demo 说的「ShopSectionPanels 同款平铺半透明深色圆角矩形」。
- 敌方名牌颜色已备好：`Assets/Resources/GameColorPalette.asset` 的 `hpPlateFaceEnemy` / `hpPlateInkEnemy` / `hpPlateNameEnemy` 已接线 ColorSO（face ≈ #801424 / ink 与 name ≈ #BFBDB5）；但 `PaletteTint.Slot` 枚举只有 `HpPlateFace`/`HpPlateName` 两个 player 槽 → 需 append 敌方槽位（§6）。
- 名牌渲染 = `HPNumericDisplayHorizontal`（em 制布局 + `editModePreview`）；world 侧模板 = `Tpl_HpNamePlate.prefab`（含 `HudTextBinding` Username(key=7) + 双 `HudCountBinding`）。
- 按钮 = `PhysButton`：hover 弹起只移动 Visual 组（shadow z+0.04 / face z+0.02），根与碰撞体不动 —— demo 的「选择按钮弹起不掀面板」天然成立，无需新机制。
- 几何换算：`PhaseFlightPlanner.PxToWorld`；一页 = `2×ortho`（当前主相机 ortho 6.06 → 12.12 wu），**1 demo px ≈ 0.0163784 wu**。demo 页 420×740px。

## 3. 交付物清单

| 文件 | 性质 | 内容 |
|---|---|---|
| `Assets/Prefabs/ShopHud/OpponentSelectPanel.prefab` | 新 authored prefab | 全包含候选面板（§4 结构） |
| `Assets/Prefabs/ShopHud/SelectButton.prefab` | 新 `Tpl_WorldButton` 变体（PrefabInstance wrapper，同 RerollButton 模式） | 选择按钮：face 1.769×0.622 wu、label「选择 +$0」、敌方红面（§6） |
| `Assets/Scripts/UXPrototype/OpponentSelectPanel.cs` | 新 view 组件（dumb） | serialized 引用集，不解析数据不接缓存（§7） |
| `Assets/Scripts/UXPrototype/PaletteTint.cs` | 唯一既有代码触点 | Slot 枚举 append 3~4 个槽位 + `Resolve()` 行（§6） |

## 4. prefab 结构

```
OpponentSelectPanel                (root：OpponentSelectPanel view 组件)
├── PanelBg                        SpriteRenderer 切片 RoundedCorner，size 5.962×1.916 wu，
│                                 z 0.5（ShopSectionPanels 的 PanelZ 约定），PaletteTint ShopPanelBg
├── KeyCard0 / KeyCard1 / KeyCard2 空锚点（中心定位，见 §5 表）；slot root localScale = keyCardScale
│   └── (占位卡实例 baked，prefab 阶段可见；运行时替换)
├── EnemyPlate                     = Tpl_HpNamePlate.prefab 实例（改名 EnemyPlate）：
│                                     PaletteTint 槽位 per-instance override → 敌方 3 槽（§6）
└── SelectButton                   = SelectButton.prefab 实例（PhysButton；action 由页面代码接，
                                      prefab 只带 Visual/Label/碰撞体与 authored 文本）
```

z 约定（世界空间，远 = 大 z）：PanelBg 0.5 ＞ KeyCard 0.35 ＞ EnemyPlate 0.3 ＞ SelectButton 根 0.3。面板自身无阴影（v4 裁定：阴影只属于内容 —— 卡硬阴影 CardShadow、按钮/名牌自带 shadow 子件）。

## 5. 几何（demo px → wu，k = 0.0163784 @ ortho 6.06；prefab 阶段目检回填实测值）

demo 面板内坐标（面板左上原点，y 向下）→ 本 prefab 面板中心原点 wu（y 向上）：

| 元素 | demo px（面板内） | wu（面板内，中心原点） | 说明 |
|---|---|---|---|
| 面板幅面 | 364×117 | 5.962×1.916 | 圆角由 RoundedCorner sprite 承担 |
| 关键卡中心 ×3 | x = 45/124/203，y = 64 | x = -2.244/-0.950/+0.344，y = -0.090 | 等距 79px；卡顶对名牌顶 |
| 选择按钮 | 左上 (252, 67)，108×38 | 中心 (+2.031, -0.450)，1.769×0.622 | 按钮底 = 卡底、右缘 = 名牌右缘 |
| 名牌 | 顶 y = 12，face 右缘距面板右 12px | top +0.762，face 右缘 x = +2.861 | 右缘对齐：shade 出挑仍越过面板内缘 |
| keyCardScale | demo 0.44（对 150×210px 卡） | **≈0.328**（对真卡 3.3×4.7 wu） | 0.44×(2.457/3.3)；以视觉对齐 demo 为准，prefab 阶段定数 |
| 名牌字号 | em = 34px | em ≈ 0.557 wu | 实例 root scale 按 Tpl 模板基准 em 换算；prefab 阶段定数并回填本行 |

对齐关系（2026-10-08 v4 mockup 裁定，必须在成品上复核）：卡顶 = 名牌顶；卡底 = 按钮底；名牌 face 与按钮同宽同右缘。

## 6. 配色（全部走 palette，遵守单一来源判例）

| 部件 | PaletteTint 槽位 | 现状 |
|---|---|---|
| PanelBg | `ShopPanelBg`（现值即 demo #1A3037 @0.5） | 现成 |
| 名牌 Face / 数字 ink / 名字带 | append `HpPlateFaceEnemy` / `HpPlateInkEnemy` / `HpPlateNameEnemy` → `GameColorPalette` 既有静态（palette 资产已接线） | 需 append 枚举 + Resolve 行（append-only 判例，序列化为 int 不重排） |
| 按钮面 | 新 `EnemyAccent`（#B71C2C）：新 ColorSO `Assets/SORefs/Colors/EnemyAccent.asset` + `GameColorPalette` 字段/静态 + slot | 需拍板后加（open 点 O-A） |
| 按钮 label | `TooltipText`（白；与 demo #ECECE8 差 2-3%，接受；或后续新增槽位） | 现成（O-B 微调点） |
| 关键卡硬阴影 | `CardShadow` | 现成 |
| 名牌阴影带 | `hpPlateShadow`（已接 CardShadow 同资产；渲染 α≈0.5 口径照抄 10-07 铭牌规格） | 现成 |

## 7. view 组件 `OpponentSelectPanel.cs`（dumb refs）

serialized 引用：`panelBg`(SpriteRenderer)、`plateNameBinding`(HudTextBinding)、`plateCountBindings`(HudCountBinding[])、`selectButton`(PhysButton) + `selectLabel`(TMP_Text)、`cardSlots`(Transform[3])、占位卡实例 handles。

职责边界（binder 判例：视觉身份全在 prefab 数据，代码只推字符串/调动作）：本组件**不解析候选、不接 OpponentDeckCache、不写 tween**。主计划 S2 的页面泵对每个实例做 `Fill(username, hp, hpMax, bountyN, cardPrefabs[3])`（隐藏占位卡 → 实例化真卡 → 推名牌文本/计数 → 按钮文本格式化）。EditMode 预览零代码：占位卡 baked 可见 + 名牌 `editModePreview`（previewName「？？？」）+ 按钮/文本 authored（09-26 判例：authored 文本首刷必被重写，不污染）。

## 8. 实施步（step-gate）

| 步 | 内容 | 验证 |
|---|---|---|
| P-S1 | `PaletteTint` append 敌方 3 槽 +（O-A 拍板后）`EnemyAccent` ColorSO/palette 字段/静态 | `refresh_unity` + 确认 assembly mtime；EditMode 全量回归零漂移 |
| P-S2 | `SelectButton.prefab` 变体（face 尺寸/槽位/label「选择 +$0」） | prefab 阶段目检；对照 RerollButton 变体结构 |
| P-S3 | `OpponentSelectPanel.prefab` 拼装（底板/锚点/名牌实例槽位 override/按钮实例/占位卡） | prefab 阶段目检 + EditMode 截图对照 demo v4（§5 对齐关系逐条核） |
| P-S4 | view 组件 refs 接线（拖引用） | 编译零错；EditMode 截图复核 |

每步完成停下汇报等确认；`.cs` 触点仅 P-S1/P-S4，改后按 AGENTS.md 09-19 判例 refresh + mtime 确认。

## 9. 不在本 prefab 范围（主计划 S2 页面侧）

入场 fade+slide+stagger、关键卡 hover 弹起/点击放大（shade + ShopInputGate 门）、选择 action → `ConsumeCandidate` + morph 起飞编排、候选数据泵（ghost 卡组伤害 top-3、bounty 占位 0）、N 面板摆位与 aspect 适配、morph 飞行 tween。

## 10. 风险与开放点

- **真卡实例 = 完整卡 prefab**（逻辑组件 dormant）：与商店货架卡同款 Instantiate 路径，预期能挂；若发现 CardScript OnEnable 类副作用，退路 = 只克隆卡面视觉子树（届时拍板，不在本步预做）。
- keyCardScale ≈0.328 偏离 demo 0.44：来源是真卡幅面（3.3×4.7 wu）≠ demo 卡幅（150×210px）；以「视觉幅面对齐 demo」为准绳而非照抄 0.44。
- O-A：按钮面 `EnemyAccent` 新槽位是否单独立 ColorSO（本方案按「立」写；若并入现有 Red 系资产需换算确认 #B71C2C）。
- O-B：按钮 label 白色槽位 `TooltipText` vs demo #ECECE8 的微差，Play 目检后再定。
- +$N 数据语义仍未拍板（主计划 §9 / demo NOTE）——prefab 只承担 authored 占位文本。
- 占位卡选哪张 prefab：prefab 阶段任选低频卡（如 1_R 常见卡），S3 时定。

## 11. 实施记录（2026-10-09）

- **P-S1 完成**：PaletteTint.Slot append 3 槽（16 HpPlateFaceEnemy / 17 HpPlateInkEnemy / 18 HpPlateNameEnemy）+ Resolve 行。颜色全用已有资产（用户裁定）：敌方名牌三色 = 调色板既有接线 **Red 1 / GreyWhite 2 / GreyWhite 2**，零新建 ColorSO；按钮面复用 HpPlateFaceEnemy 槽（即 Red 1），label 用 TooltipText。
- **P-S2 完成**：SelectButton.prefab（Tpl_WorldButton 变体，PrefabInstance wrapper）：face/shadow SR m_Size 2.211×0.778（child 0.8 惯例 → 渲染 1.769×0.622）、label「选择 +$0」fs5 TooltipText、label z −0.09、collider 1.894×0.747 / offset(−0.0125, 0.0125)、Tpl 的 HudActionBinding（LeaveShop）已 RemovedComponents——选择 action 由页面泵 SetWorldAction 接。
- **P-S3 完成**：OpponentSelectPanel.prefab。实施差值以本节为准（§5 表为设计稿）：
	- **占位卡 = PhysicalCardParent.prefab**（Assets/Prefabs/UXPrototype/）——判例：Cards/4.0 卡 prefab 是纯逻辑体（0 渲染器），卡面视觉由 CombatUXManager 运行时用 PhysicalCardParent 构建。prefab 内烘 3 个连接实例；S2 页面泵换 ghost 真卡时走同模板 + 推 name/desc。
	- **keyCardScale 自动拟合 = 0.7311**（槽 0.328 × 0.7311 ≈ 0.2398）：PhysicalCardParent 链带 1.25² = 1.5625 缩放，原始脸幅 3.25×4.71；拟合目标 = v4 对齐裁定「卡顶=名牌顶(+0.7616)、卡底=按钮底(−0.7611)」跨度 1.5227 wu。卡中心因此 = 面板正中（keyY 0，非 demo 的 64px）。
	- **按钮 x = 名牌根 x = 1.8933**：同宽 + 同右缘 → 中心重合（demo 自身右缘差 8px 不采纳）。名牌 face 右缘 2.7843（12px 内缩）。
	- 名牌 root scale 0.557（em 基准 fs10 → em 0.557 wu = 战斗 34px）；实例覆写：6 个 PaletteTint 槽位 + HpValue/HpMax「24」+ NameLabel **「???」（半角——全角「？」全字体无字形判例，mesh 恒空）**。
	- z：底板 0.5 / 卡 0.35 / 名牌·按钮 0.3。
- **P-S4 完成**：view refs 全接（panelBg / plateNameBinding / plateCountBindings[2] / selectButton / selectLabel / cardSlots[3] / placeholderCards[3]）。
- **对齐审计**（世界坐标实测）：cardTop−plateTop = 0.000、cardBottom−btnBottom = 0.000、plateRight−btnRight = 0.007（宽差残差 ≈0.4px）。
- **回归**：EditMode 全量 743 = 742 绿 / 0 失败 / 1 skip（既有嵌套协程豁免）——零漂移。
- **目检截图**：Assets/Screenshots/opponent-select-panel-editmode.png（全景）+ opponent-select-panel-closeup.png（名牌/按钮/卡特写）。
- **待办**：Play 目检（用户）；主计划 S2 页面泵（Fill 契约见 OpponentSelectPanel.cs 头注）。
