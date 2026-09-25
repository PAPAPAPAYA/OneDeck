# Plan: 商店头像/HP 镜像 Prefab 化 — 删拷贝机 + canvas 锚点改世界驱动 (2026-09-24)

- **Date**: 2026-09-24
- **Status**: IMPLEMENTED (2026-09-24 执行完毕:checklist 1-9;镜像 85 字段 diff NONE——pill 根重定义与 canvas 反演 ulp 均已文档化并验证;EditMode 新基线 **664/663/1 Ignore**(删 ShopPageHudTests −7,符合预期);console 无新告警;仅剩 §6.2 Play 矩阵待用户目测)
- **Request (user, 2026-09-24)**: 顶栏 v5 prefab 化落地后,头像/HP 仍是运行时拷贝机(455 行 `ShopPageHud`)。出 §8.2 独立计划:手做 `Tpl_Avatar` / `Tpl_HpPill` 进 `ShopHudPage.prefab`,删拷贝机,转场锚点配对 + canvas 停靠改世界位置驱动。
- **Reads**: `plans/plan-shop-hud-prefab-widgets-2026-09-23.md` (§3 组件族、§5 探针纪律、§8.2 本计划出处), `docs/ShopSystems.md` (v5 节 + 镜像 handoff 规则), `Assets/Scripts/UXPrototype/ShopPageHud.cs` (全文 455 行,已核), `Assets/Scripts/UXPrototype/ShopTopBarLayout.cs`, canvas 消费点 `CombatIconPresenter.cs` / `HPNumericDisplayHorizontal.cs`

## 1. Goal / non-goals

Goal:
1. 头像块与 HP 丸变成 `ShopHudPage.prefab` 里的**手作 prefab 子件**(`Tpl_Avatar` / `Tpl_HpPill` 模板 + page 内实例):位置、尺寸、sprite、字号、颜色全部 prefab 数据,编辑法与顶栏 chip 一致(prefab stage 所见即所得)。
2. 数据走绑定层:`Username` 用 `HudTextBinding`(预留 key 转正);HP 对(`hp`/`hpMax`)用新组件 `HudCountBinding`(0.3s DOTween count-up + diff-guard + OnDisable 杀 tween,行为等价现拷贝机)。
3. **canvas 锚点配对改世界驱动**:canvas 件的商店停靠点从「SO 6 字段 → `LeftOffsetToCanvasAnchored` 代数换算」改为「已建 page 实例的头像/HP 丸**世界位置** → 新 `WorldToCanvasAnchored` 反演」——单一视觉源成立,prefab 挪哪里转场就飞哪里。
4. `ShopPageHud`(455 行)整体删除;`ShopLayoutConfigSO` + 资产删除(镜像 6 字段是它最后的内容);`ShopTopBarLayout` 瘦身为 canvas 侧所需;`ShopPageHudTests` 随被测静态量一并删除。

Non-goals:
- 战斗 canvas HUD 本体(`CombatIconPresenter` / `HPNumericDisplayHorizontal` 的战斗期表现)一行不动——只改它们「商店锚点」的来源。
- 敌方 HUD(商店无敌方镜像)、§8.1 面板模板化、count-up 时长/曲线调参。
- 顶栏 7 chip + 2 按钮(v5 已交付)零回归——本计划的 page 改动只**增**两个子件。

## 2. Current state (verified facts, 2026-09-24)

### 2.1 拷贝机现状(`ShopPageHud.cs`,455 行)

| 块 | 行段 | 内容 | 去留 |
|---|---|---|---|
| `Bootstrap`/`Build` | :87-122 | 找 canvas 源件、`CanvasPxToWorld` 校准、建 Avatar/HpPill 两块 | 删(prefab 取代) |
| `ApplyMirrorLayout`/`RebuildMirrors`/`_mirrorDirty` | :130-150, :299-306 | SO 实时通道 + 旅行中推迟 | 删(无 SO 可调;位置=prefab 数据) |
| `BuildAvatar` | :154-185 | 拷贝 icon 子件(Image/TMP)、anchoredPosition→局部偏移、z 序 `(count-1-i)*ZStep+ZText` | 删 → prefab 数据 |
| `BuildHpPill`/`InitHpText` | :189-253 | 同上 + HP 初值 | 删 → prefab + binding |
| `Refresh`/`PlayCountUp`/`WriteHpText` | :261-297 | 买/卖 hpMax 变化 → 0.3s count-up(`CountUpSeconds=0.3`) | **保留语义** → `HudCountBinding` |
| `PollUsername` | :311-321 | `PlayerIdentity.Username` ?? `"???"`,diff-guard | **保留语义** → binder + `HudTextBinding.Username` |
| `CanvasPxToWorld`/`SlicedWorldSize`/`SlicedWorldScale`/`MirrorSprite`/`CopyText` | :54-61, :345-398 | canvas px 校准 + 边框精确 9-slice 换算(VISUAL-FIX 2026-09-21) | 删——手作 prefab 一次定格,不再逐帧换算;`ShopPageHudTests` 全部金针是被测这些静态量,随之删 |

- 数据源:`PlayerIdentity.Username`(Assets/Scripts/Net/,静态);`CombatManager.Me.ownerPlayerStatusRef.hp/hpMax`。
- 头像 sprite 是场景静态资产,`CombatIconPresenter` 无运行时换图代码(grep 零命中)→ prefab 静态化安全。
- z 约定:同块内 `ZStep=0.01` 递减(远=后),文本 `ZText=0.01`;烘焙时按探针现值定格。

### 2.2 消费点(删除波及面)

| 消费者 | 位置 | 处置 |
|---|---|---|
| `ShopChrome` | `_pageHud` 字段 + `Bootstrap(root.transform)` :113 + `Refresh` 尾 :168-169 | 删字段与调用;HP 刷新由 binder 覆盖 |
| `ShopLayoutConfigSO.OnValidate` | :62 `ApplyMirrorLayout` | 随 SO 整体删除 |
| `ShopPageHudTests` | `CanvasPxToWorld`/`SlicedWorldSize`/`SlicedWorldScale` 金针(~8 用例) | 删文件(被测量消亡;EditMode 基线 671/670 **预计降至 ~663/662**,实施时记录新基线) |
| canvas 件注释提及 | CiP :12/:69/:100/:134、HPN :30/:261/:268 | 仅注释,顺手更新措辞 |

### 2.3 canvas 商店锚点现状(改世界驱动的对象)

- `CombatIconPresenter` :148-152:inShop 时 `ShopTopBarLayout.LeftOffsetToCanvasAnchored(PlayerIconXFromLeftEdge, PlayerIconViewportY, …)` + `PlayerIconShopScale`。
- `HPNumericDisplayHorizontal` :443-450(parkAtShop)+ :458-466(ApplyPhasePlacement):同换算,`HpDisplay*` 三量。
- `ShopTopBarLayout` :48-53 六个解析属性(读 SO)+ `LeftOffsetToCanvasAnchored`(:100-109)+ `MainOrthoSize`/`FallbackOrthoSize`。转场飞行由 `PhaseTransitionConfigSO.ApplyEase` tween 到这些锚点,驱动器与 `ShopPageHud` **无直接耦合**(grep 零命中)。

### 2.4 资产与现状值

- 镜像 SO 6 字段现值(`Assets/Resources/ShopLayoutConfig.asset:15-20`):icon 2.57 / 0.88 / 0.41,hp 4.02 / 0.88 / 0.74(用户 Play 调参,代码默认 6.29/0.956/0.28、9.42/0.959/0.5 已过期——**烘焙一律用探针现值**)。
- `ShopHudPage.OnDrawGizmos` 的镜像占位框(`ShopHudPage.cs:40-48`)读这 6 字段——本计划后**删除**(真实子件在 prefab stage 直接可见,gizmo 失去存在意义)。
- v5 交付的复用件:`PaletteTint`(槽位色)、`HudTextBinding{key,format}`(diff-guard)、`ShopHudBinder`(Collect/Refresh/Execute);`HudTextBinding.BindingKey` 已预留 `Username`/`Hp`/`HpMax` 枚举位。

## 3. Design

### 3.1 总览

```
Tpl_Avatar.prefab(手作:暗底圆角图+奶油描边+小阴影+username TMP+PaletteTint)──┐
Tpl_HpPill.prefab(手作:底板+阴影+"HP"字+数值 TMP+PaletteTint+HudCountBinding)─┤
																				▼
										ShopHudPage.prefab(用户排版;+2 实例,位置按基线 1:1)
																				│ 运行时
							ShopHudBinder.Refresh():Username / Hp+HpMax 推送
																				│
							ShopTopBarLayout.WorldToCanvasAnchored(page 世界位) ← canvas 锚点新源
```

### 3.2 两个模板(手作,不做变体——各只有一份)

- **`Tpl_Avatar`**:根(空,仅排版)→ `Image`(暗底圆角 SpriteRenderer sliced)+ `Frame`(奶油描边)+ `SmallShadow` + `Username`(世界 TMP,`PaletteTint=IconNameLabel` 槽,`HudTextBinding{Username, "{0}"}`)。子件 sprite/尺寸/z 序按探针现值 1:1 定格;**边框比例不再换算**——手作一次定格,`SlicedWorldSize`/`MirrorSprite` 那套边框数学随拷贝机消亡。
- **`Tpl_HpPill`**:根(空)→ `Bg` + `Shadow`(sliced)+ `HpWord`("HP",TMP,`HpNormalPlayer` 色)+ `Value`(TMP,`HudCountBinding{format "{0}/{1}"}`)。z 序按探针。
- 模板根即排版根;page 内实例位置 = 基线探针的 Avatar/HpPill 根局部坐标(与 v5 同法:探针→烘焙,不信代码默认)。

### 3.3 绑定层增量(唯一新代码,~150 行)

- **`HudCountBinding`**(挂 `Tpl_HpPill` 根):字段 `TMP_Text target` + `string format = "{0}/{1}"`;`SetTarget(int hp, int hpMax)`:双 diff-guard(目标变才启动、显示值等值则跳过)→ 0.3s `DOTween` count-up → `OnDisable` 杀 tween(= 现 `Refresh`/`PlayCountUp`/`OnDisable` 三段语义平移,常量 `CountUpSeconds=0.3` 保留)。
- **`ShopHudBinder`**:`Refresh()` 增两路——`Username`(PlayerIdentity,diff-guard 在 binding 内)+ 找 `HudCountBinding` 推 `ownerPlayerStatusRef` 的 hp/max(`Mathf.Max(1, hpMax)` 口径保留);`Collect()` 照旧收 `GetComponentsInChildren`(含 inactive)。
- `HudTextBinding.BindingKey.Username` 转正;`Hp`/`HpMax` 两个预留 key **删去**(改由 CountBinding 承担,避免双通道)。

### 3.4 canvas 锚点改世界驱动(本计划风险核心)

- `ShopChrome` 增两个静态读点:`AvatarWorldCenter` / `HpPillWorldCenter`(已建 page 实例的对应子件 `Transform.position`;未建返回 `null?` 可空)。
- `ShopTopBarLayout` 增 `WorldToCanvasAnchored(Vector3 worldPos, Canvas canvas, float orthoSize)`:现 `LeftOffsetToCanvasAnchored` 的反演(pxPerWorldUnit = Screen.height/(2×ortho),先减 chrome 根世界位再乘),签名与 scale 兜底口径对齐现实现。
- canvas 消费点改法(4 处,语义分流):
	- `CombatIconPresenter` inShop 摆位(:148-152)与 `HPNumericDisplayHorizontal` parkAtShop(:443-450):chrome 已建 → 读世界位反演;**chrome 未建(无头/batch/早期帧)→ 回退现 SO 代数换算**(保留旧函数为 private fallback,不删行为)。
	- `ApplyPhasePlacement`(:458-466):同上。
- **缩放**:canvas 件商店期 scale 仍需一个数——`playerIconShopScale`/`hpDisplayShopScale` 随 SO 删除而失去家,迁为 `Tpl_Avatar`/`Tpl_HpPill` **模板根上的序列化字段**(canvas 件读取 chrome 转发的同名静态,默认=现值 0.41/0.74)。位置单源化,scale 显式共享。
- 删除清单:`ShopLayoutConfigSO.cs` + `ShopLayoutConfig.asset`;`ShopTopBarLayout` 的 6 个解析属性 + `LeftOffsetToCanvasAnchored` + `ChromeLocalXFromLeftOffset` + `ViewportToChromeLocalY`(:70-90,最后消费者=本计划删的拷贝机)+ `PlayerIcon*/HpDisplay*` 常量;保留 `MainOrthoSize`/`FallbackOrthoSize`/新 `WorldToCanvasAnchored`。
- `ShopHudPage` gizmo:镜像占位框两段删除(`SO V(...)` 读点随 SO 消亡),`DrawBandLine` 保留。

### 3.5 ShopChrome/ShopPageHud 收尾

- `ShopChrome`:`_pageHud` 字段、`Bootstrap` 里的 `ShopPageHud.Bootstrap(root.transform)`、`Refresh` 尾的 catch-up 全删;binder 的 HP 推送即刷新。头注释更新(`ShopPageHud` 引用改 `ShopHudPage` 子件)。
- `ShopPageHud.cs` 整文件删除;`ShopPageHudTests.cs` 整文件删除;两处 VISUAL-FIX(2026-09-21 边框/排序)随文件消亡——**RegressionChecklist 旧行不删**,按惯例其结论由 prefab 静态化继承。

## 4. 绑定 key 与 format

| 目标 | 数据源 | format(prefab 字段) | 刷新时机 |
|---|---|---|---|
| Username | `PlayerIdentity.Username` ?? `"???"` | `"{0}"` | binder Refresh(diff-guard) |
| HP 数值 | `ownerPlayerStatusRef.hp / Max(1,hpMax)` | `"{0}/{1}"` | binder Refresh → CountBinding count-up 0.3s |

## 5. Implementation checklist

1. 注册 `.agent_registry`;扫描无重叠。
2. 新文件 `HudCountBinding.cs`;`ShopHudBinder` 增 Username/Hp 推送;`HudTextBinding` 删 `Hp/HpMax` 预留枚举 → `refresh_unity` → 0 错。
3. Play 基线探针(`runInBackground=true` 纪律同 v5):`Shop Page Hud` 下 Avatar/HpPill **根局部坐标 + 每子件 localPosition/z + SpriteRenderer size/color/sorting/sprite 名 + TMP text/fontSize/color/rect + canvas 件当前商店锚点值**;导出 `tools/outputs/shop_mirror_baseline_before.json`。
4. 编辑器内手作 `Tpl_Avatar` / `Tpl_HpPill`(按探针定格),嵌进 `ShopHudPage.prefab`(编辑器建 .meta)。
5. `ShopChrome` 增 `AvatarWorldCenter`/`HpPillWorldCenter` + 删 `_pageHud` 三处;`ShopTopBarLayout` 增 `WorldToCanvasAnchored` + 删 §3.4 清单;canvas 4 消费点改读(带 fallback);`ShopHudPage` gizmo 删镜像框。
6. 删 `ShopPageHud.cs` / `ShopPageHudTests.cs` / `ShopLayoutConfigSO.cs` + 资产;`ShopUXManager` 字段注释更新(canvasSprite/chromeFont 语义不变)。
7. 重导探针对比 diff 为空(镜像根/子件几何+颜色+文本逐位);canvas 锚点值另列一组对比。
8. EditMode 全量(refresh→mtime→SaveScene→run_tests;**预期总数降 ~8**,记录新基线);离线编译 0 错。
9. 文档:`docs/ShopSystems.md` 镜像节重写;AGENTS.md 商店 bullet 增一句(31634B,余量 1134B,**净增须 ≤100B 或先挪细节**);RegressionChecklist row 116;plan Status 翻 IMPLEMENTED。

## 6. Verification

### 6.1 基线零位移(硬门槛)

步骤 3/7 探针 diff 为空:镜像两根及全部子件 localPosition、SR size、TMP fontSize/rect 逐位相等,颜色逐位相等,文本逐字相等("HP"/"3/3"/username);canvas 商店锚点 anchoredPosition 数值相等。证明「prefab 化 = 纯载体更换」。
**已知免责**:镜像位置从「SO 视口比例语义」变「设计分辨率烘焙语义」(v5 §7.5 同款,用户已确认接受该语义方向);烘焙自当前窗口现值故本窗口逐位相等,跨 aspect 表现由 canvas 反演跟随世界位保证一致。

**实施结果(2026-09-24)**:探针 JSON 在 `tools/outputs/shop_mirror_baseline_before/after.json`,85 字段 **DIFFS=NONE**。三条文档化偏差/发现:
1. **HpPill 根语义重定义**:旧拷贝机的 pill 根 = 锚点 + displayRoot 偏移,而 canvas 停靠的是裸锚点——直接反演根位置会右偏 91px。已把子件在根内平移 dispOff(根 = 裸锚点),**4 个子件的世界位置逐位验证相等**;根 localPosition 差异(−0.8655 → −1.8518)为文档化变更。
2. **canvas 反演 ulp**:两个 anchored X 值差 3e-5 px(世界→anchored 反演的浮点不可逆),视觉为零。
3. **旧拷贝机确有潜伏重建伪影**:基线会话中出现两对镜像(一对代码默认锚+未登录名+错误 canvas 单位)——重建路径的 `Destroy` 在重导入/OnValidate 链路中未落地;prefab 化后该 bug 类整类消失。基线烘焙取结算正确态(pair-2)。

### 6.2 Play 验证矩阵(用户)

| 操作 | 预期 |
|---|---|
| 进店 scroll 0 | 头像/HP 丸与改前逐像素同位(prefab 件);滚轮随页面滚走/回滚 |
| 买/卖 HP utility | 血条丸 0.3s count-up,不闪烁;hpMax 变化即时 |
| 离开商店 → 战斗 → Result → 再进店 ×2 | 转场飞行起止点与改前一致(世界位反演);落地无跳变;canvas 件停靠=镜像位 |
| prefab stage 挪 Tpl_Avatar/Tpl_HpPill | 下次进店生效,且**转场飞行终点跟着走**(单源验证) |
| 改模板 ShopScale 字段 | canvas 件商店期缩放跟随 |

### 6.3 回归

EditMode 新基线全绿;console 无新告警;战斗/结算期 canvas HUD 表现不变;卡牌价格按钮/重掷按钮(运行时 PhysButton 路径)不受影响。

## 7. Risks / notes

1. **转场锚点时序**:canvas park 发生在 shop settle 时(chrome 必已建);但 `ApplyPhasePlacement` 可能在 Bootstrap 前的早期帧调用——fallback 到旧代数换算必须保留,且 fallback 分支的 SO 常量以 `const` 形式留在 `ShopTopBarLayout`(不依赖已删资产)。
2. **"未连接 ???":** username 依赖 `PlayerIdentity`;离线/未登录时显示 `???` 的行为不变(binding 只推字符串)。
3. **count-up 与 prefab 活动性**:page 隐藏时 CountBinding 的 tween 在 `OnDisable` 杀——语义与现拷贝机一致,转场期间不飞字。
4. **EditMode 总数下降**:删 `ShopPageHudTests` 后全量 ~663/662,是**预期减法**不是回归;实施时在 checklist row 记录新基线防误读。
5. **双 gate**:本计划动手前,确认 v5 的 row 115 Play 矩阵已验——若 v5 有未爆雷,会被本计划二次掩盖。
6. 工作量:新增 ~150 行(CountBinding ~70 / binder ~30 / WorldToCanvasAnchored+chrome 读点 ~50);删除 ~600 行(ShopPageHud 455 + 测试 ~75 + SO ~70 + TopBarLayout 助手);prefab 数据两模板。

## 8. 后续独立小步(不在本计划)

1. **§8.1 面板模板化**(v5 plan 遗留,优先级不变)。
2. 战斗/结算 HUD 同族迁移(v5 §8.5 长期项;本计划后头像/HP 已是单一视觉源,该步只剩 canvas 战斗件)。
3. `Tpl_HpPill` 数值色阶(low-hp 变色)若要与世界件统一,走 PaletteTint 新槽位,不在本计划。
