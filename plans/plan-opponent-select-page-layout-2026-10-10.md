# Plan: 选敌页布局（OpponentSelectPanel v2 · 双候选并排）

日期：2026-10-10
状态：**布局数值已实测（用户手摆 mockup），实施挂主计划 S2，动代码前待「修改代码」**。
关联：`plans/plan-opponent-select-page-2026-10-06.md`（主计划：页面流 / S2 页面泵 / §4.4 转场）、`plans/plan-opponent-select-panel-prefab-2026-10-08.md`（v1 面板 prefab，§11 实施记录；**2026-10-10 起由 v2 取代**）、`Assets/Prefabs/ShopHud/OpponentSelectPanel v2.prefab`（用户手摆并固化的面板 prefab，本计划的面板基准）

## 0. 拍板与事实（2026-10-10 对话）

| # | 内容 |
|---|---|
| L1 | **面板 prefab = `OpponentSelectPanel v2`**（用户手摆 mockup 固化；view refs 已接）。v1 `OpponentSelectPanel.prefab` 弃用，文件与场景实例清理时机见 O-L3 |
| L2 | **候选数 N = 2**（主计划 O1 落定：`selectionCandidateCount` 默认 2，保持可配） |
| L3 | 布局数值 = **场景 mockup 实测**（本次编辑器量测，非估算）；mockup 实例（场景根 `OpponentSelectPanel v2` / `OpponentSelectPanel v2 (1)`）实施 S2 前保留不动 |
| L4 | 面板内部布局由 demo v4（卡左 / 名牌按钮右列）改为 v2（**名牌+按钮上排、3 卡下排**）——用户 mockup 裁定 |

## 1. 量测基准

主相机 ortho 6.06、aspect 16:9 → 视口 **21.55 × 12.12 wu**（x ∈ ±10.77，y ∈ ±6.06）。本计划全部用**页面局部坐标** = 相机居中选敌页时的世界坐标；生产实施时由页面构建器统一加 `_selectionPageY` 偏移（主计划 §4.4，+1 屏 = +12.12）。用户 mockup 摆在当前页面平面上，数值即页面局部值。

## 2. 页面布局（实测 wu）

| 项 | 值 | 备注 |
|---|---|---|
| 左面板根 | (−4.90, +0.36, 0) | `OpponentSelectPanel v2` |
| 右面板根 | (+4.90, +0.36, 0) | `OpponentSelectPanel v2 (1)`；与左对称 |
| 面板间距 | pitch 9.80，渲染间隙 0.55 | 幅面各 9.25×6.96（含卡硬阴影出挑） |
| 面板带 | y ∈ [−3.60, +3.36] | 视口 ±6.06；上留 2.70、下留 2.46 给底行 |
| 玩家名牌 | (−8.64, −4.94) | 既有 `PlayerHPDisplayH`（Combat Canvas，战斗位）——页面**不建不动** |
| 敌方战斗位 | (+8.64, +5.27) | `EnemyHPDisplayH`——morph 飞行落点参考 |
| 入场 | 承 demo v4：fade+slide+stagger，左→右 60ms | §6 O-L4 |

页面背景（红/灰 topo 二分，图中上半红下半灰）属页面背景系统（主计划 §4.4 + morph/camera 模式裁定），不在本布局计划内。

## 3. 面板内部（v2 prefab 固化值，面板局部 wu，已实测）

| 部件 | 局部位置 | 尺寸/缩放 | 备注 |
|---|---|---|---|
| PanelBg | (0, 0, 0.5) | 9.25 × 6.00 | RoundedCorner 切片 + ShopPanelBg |
| KeyCard0/1/2 槽 | (−3.00, −1.37) / (0, −1.40) / (+3.00, −1.41)，z 0.35 | slot scale **1.10** | 占位 = PhysicalCardParent（卡渲染 2.88×5.11 含硬阴影出挑） |
| EnemyPlate | (−2.70, +2.20)，z 0.30 | root scale **1.00**（em 1.0 wu） | 内容 24/24 + 「???」（半角）；敌方三色槽位继承 v1 覆写 |
| SelectButton | (+2.30, +2.00)，z 0.30 | face 渲染 4.00 × 1.28 | label「选择 +$N」（+$N 占位照主计划 P3） |

## 4. 衔接注意

- **morph 飞行字号**：v2 面板名牌 em 1.0 wu ≠ demo 34px（0.557）口径，morph「纯平移无缩放」假设被打破 → S3 落 morph 时需实测战斗位名牌渲染尺寸：对不上则飞行补 scale tween 或调 v2 名牌字号（O-L1）。
- N=1 降级：单面板 x=0 居中（O-L2）；N>2 不做（`selectionCandidateCount` 上限按布局取 2）。
- 选择 action / 数据泵 / 输入门：全在主计划 S2，本计划只管摆位与面板基准。

## 5. 实施步（挂主计划 S2，step-gate）

| 步 | 内容 | 验证 |
|---|---|---|
| SL1 | `PhaseTransitionConfigSO.selectionCandidateCount` 默认 2；页面构建器按 §2 摆 N 个 v2 实例（世界 y + `_selectionPageY`） | EditMode bypass 矩阵 + 编辑器目检 |
| SL2 | 场景 mockup 清理（`OpponentSelectPanel v2` / `v2 (1)` / v1 实例）——**删前经用户确认**；v1 prefab 文件去留同问 | 场景干净、无孤立引用 |
| SL3 | Play 全流程目检（对照本计划 §2/§3 数值与用户 mockup 图） | Play 截图对比 |

## 6. 开放点

- O-L1 morph 飞行名牌字号/缩放衔接（§4）。
- O-L2 N=1/N>2 布局策略（先按 N=1 居中、上限 2）。
- O-L3 v1 prefab 文件与场景 v1 实例清理时机（SL2 一并问）。
- O-L4 入场动画具体参数（fade/slide 距离/时长——承 v4 的 18px slide、60ms stagger 折算 wu 后实调）。

## 7. 补充裁定（2026-10-10 第二轮对话，细节落主计划 §4.3/§4.4）

- **入场方式 = camera 模式拍板**（demo 推荐的 morph 不实施）：选敌面板固定 +1 页随镜头揭示——**O-L4 关闭：面板无独立入场动画**。
- **选敌页背景 = 玩家灰全页延伸、无红**（mockup 图顶部红色系借战斗位背景拍摄，不采纳）；背景覆盖需 +1 页（主计划 O4 同步落定）。
- **选项按钮要管**：leg1 随行程从屏幕右上（商店钉死位）平移到右下战斗位（玩家名牌同队）；之后选敌/战斗两页不动。
- 玩家名牌 handoff（商店名牌→战斗名牌交接）提前到 leg1；敌名牌 leg2 面板位→战斗位平移（O-L1 保留）。
- 关键卡选卡规则、entrance 三组来源（关键卡面板位起飞 / 剩余敌卡+start card 屏外上 / 玩家卡屏外下）、bountyPlaceholder：详见主计划 §4.3/§4.4/§4.6（2026-10-10 修订版）。
