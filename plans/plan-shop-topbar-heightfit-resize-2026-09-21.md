# Plan: 商店顶栏 avatar+HP「高度定标」+ 窗口自由拉伸自适应 (2026-09-21)

方案 A 落地计划。结论先行：商店页除 avatar+HP 外全部元素是世界空间（尺寸跟窗口高度），这两件是仅有的 Canvas 元素（尺寸跟窗口宽度，CanvasScaler match-width 1080x1920），且摆放是相位边沿的一次性快照 —— 所以自由拉伸窗口时它们与其他元素脱钩、垂直位置漂移。本计划把两件改为「按视口高度定标」并加 resize 重摆。

## 0. 问题根因（已核实，file:line）

1. **双缩放轴**：世界 chips/面板 = SpriteRenderer 固定世界单位，正交相机 `orthographicSize` 全项目无脚本改动（`grep orthographicSize` 仅布局/过场读取），屏幕尺寸 ∝ 窗口**高度**；avatar+HP 挂 Combat Canvas（Scale With Screen Size, 1080x1920, `m_MatchWidthOrHeight: 0` = match **width**, GameScene.unity:1113-1118/10951-10956），屏幕尺寸 ∝ 窗口**宽度**。超宽窗口下 HP 药丸（width-fit）相对 chips（height-fit）爆大，极窄窗口反之。
2. **一次性快照**：placement 只在相位 diff 边沿执行（`CombatIconPresenter.cs:86` → `ApplyPhase`；`HPNumericDisplayHorizontal.cs:272` → `ApplyPhasePlacement`），之后无任何 resize 重算路径（`grep Screen.width` 全项目仅 `ShopTopBarLayout.ViewportToCanvasAnchored` 与 tooltip）。match-width 下水平方向天然稳定（canvas 宽恒 = 1080 参考像素），**垂直方向** canvas 高 = 1080/aspect 随拉伸变化 → 进店后拉伸窗口，顶栏 y 漂移、不重新贴顶。
3. **手调常数**：`ShopTopBarLayout.PlayerIconShopScale = 0.28`（上限被 `small shadow` 222x306 装饰块顶视口顶约束）、`HpDisplayShopScale = 0.5`，按出货 aspect 手调且互不成比例（截图中 icon 块与 HP 数字失衡；用户名标签随 icon 继承 0.28 显得过小）。

## 1. 方案核心：视口高度定标（height-fit）

目标语义：两件在商店阶段的**屏幕高度 = 视口高度 × 固定比例**，与世界 chips 的缩放轴（worldHeight / 2·orthoSize）等价（比例 = worldHeight/(2·orthoSize)）。

核心公式（纯函数，不读全局态，可 EditMode 直测）：

```
// element screen px height = designHeight * s * scaleFactor == frac * screenH
// => s = frac * screenH / (designHeight * scaleFactor)
public static float ShopScaleForViewportFraction(
	float designHeightRefPx, float viewportFrac, float screenH, float canvasScaleFactor)
```

- `designHeightRefPx`：icon = rect 高（校准探针确认，预期 100）；HP = `displayRoot.sizeDelta.y` = `_em`（Awake 后的字号参考像素）。
- `viewportFrac`：校准常数（§3），回填后出货 aspect 下外观与现状逐像素等价。
- 防御：`screenH <= 1` 或 `scaleFactor <= 0.0001f` 时返回 0（调用方跳过，沿用 `ViewportToCanvasAnchored` 的守卫风格）。
- **剪顶约束自动消解**：块顶 = 固定视口比例（校准自现状 0.28，本就未剪顶），任何 aspect 下比例不变，不再依赖「0.35 就剪顶」这类 aspect 敏感上限。

新常量（替换旧两个；全项目消费者仅 CombatIconPresenter / HPNumericDisplayHorizontal，已 grep 核实，无测试引用）：

```
PlayerIconViewportHeightFrac	// ≈ 100 * 0.28 * scaleFactor0 / screenH0
HpDisplayViewportHeightFrac	// ≈ em * 0.5 * scaleFactor0 / screenH0
```

## 2. 改动点（3 文件，均在 UXPrototype/）

### 2.1 ShopTopBarLayout.cs
- 新增 §1 纯函数 + `public static bool ScreenSizeChanged(ref int lastW, ref int lastH)`（任一为负初始化视为「变过」→ 首调 true）。
- 删除 `PlayerIconShopScale` / `HpDisplayShopScale`，注释更新：常量语义改为视口高度比例；保留 2026-09-19 的装饰块说明并补「height-fit 下剪顶约束按视口比例恒成立」。

### 2.2 CombatIconPresenter.cs
- `ApplyPhase` 中 shop 分支的 `targetScale` 改为 `ShopScaleForViewportFraction(iconDesignH, PlayerIconViewportHeightFrac, Screen.height, _canvas.scaleFactor)`；`iconDesignH` 于 Awake 从 `_playerIconRt.rect.height` 缓存。
- 抽取 placement 主体（targetPos/targetScale 计算 + tween/snap 二选一）为 `ApplyPlayerIconPlacement(bool shop)`，相位边沿与 resize 路径共用；`ApplyPhase` 退化为「算 bool、调 placement」。
- `Update` 增加 resize 轮询：`phase == Shop && !PhaseTransitionDriver.IsTransitioning && ScreenSizeChanged(ref _lastW, ref _lastH)` → snap 重摆（不走 tween）。`_lastW/_lastH` 初始 -1。
- 相机依赖为零（公式不含 orthoSize），不引入 Camera.main。

### 2.3 HPNumericDisplayHorizontal.cs
- `ApplyPhasePlacement` shop 分支同改：`designHeight = _em`（即 `displayRoot.sizeDelta.y`，注意用运行时 `_em` 而非预览值）。
- `Update` 增加：`side == Side.Player && phase == Shop && !PhaseTransitionDriver.IsTransitioning && ScreenSizeChanged(ref _lastW, ref _lastH)` → `ApplyPhasePlacement(true)`（snap）。
- 敌方侧、战斗侧、`_combatScale`/`_combatAnchoredPos` 捕获恢复逻辑一律不动。

### 2.4 过场期间拉伸的边角（两个组件同规则）
事实链（PhaseTransitionDriver.cs 已核实）：`ResultToShopRoutine` 在 travel **开始时**就 `pm.AdvanceFromResultToShop()`（:267）→ 两组件相位 diff 触发、以**出发时刻** Screen 尺寸算 tween 目标；用户在 Result 页或 travel 中拉伸窗口，落地会停在旧目标（落地只调 `ShopChrome.ShowIfActive()` :277，不刷 canvas 两件）。`ShopToCombatRoutine` 同理但目标是 combat 常数快照，不受 resize 影响。
处理：两组件各记 `_lastTransitioning`；**下降沿**（true→false）时若当前在 Shop 且 `ScreenSizeChanged` → 补一次 snap 重摆。travel 中不干预（tween 持有 transform；0.8s 窗口，落地 snap 一帧可接受，记入回归清单）。

### 2.5 明确不改
- 战斗阶段 placement / Result 可见性（2026-09-21 规则）/ tween 结构与 easing。
- ShopChrome 世界 chips 自身的 build 时 aspect 快照（exit/options 贴边、rows 1-2 列 X）——出站事项 §5 Phase B。
- `ViewportToCanvasAnchored` 本体（水平方向已天然稳定）。

## 3. Step 0 校准探针（实施第一步，execute_code 只读）

读 live 值并回填两常数，断言出货 aspect 下新 scale ≈ 旧常数（±0.01，等价性自检）：
- `Camera.main.orthographicSize`（预期 6.06, GameScene.unity:1886）
- `_canvas.scaleFactor`、`Screen.width/height`（出货 Game view）
- `_playerIconRt.rect.height`（预期 100）与当前 shop scale 0.28
- HP `currentPlain.fontSize`（`_em`）与当前 shop scale 0.5（combat 捕获 scale 预期 0.8，与 shop 绝对值无关，勿混淆）

## 4. 测试

- 新 `Assets/Scripts/Editor/Tests/ShopTopBarLayoutHeightFitTests.cs`：
	1. 纯函数手算样例（给定 designHeight/frac/screenH/scaleFactor → 期望 s，含守卫分支：screenH=0 / scaleFactor=0 → 0）。
	2. 等价性：用 §3 校准常数回代，出货 aspect 下 height-fit scale == 旧常数（0.28 / 0.5，±0.01）。
	3. `ScreenSizeChanged`：首调 true / 同值 false / 变值 true / 复位后行为。
- 执行纪律（AGENTS.md 既有裁定）：.cs 编辑后 `refresh_unity (compile: request)` 并确认 `EditorApplication.isCompiling == false` **且** Assembly-CSharp.dll mtime > 源 mtime；`run_tests` 前一步 `EditorSceneManager.SaveOpenScenes()`（预批准）；`init_timeout: 180000`。EditMode 套件即可（本改动无 Play 逻辑依赖），全量回归跑一次。

## 5. 出站事项（本次不做，另行拍板）

- **Phase B — ShopChrome 拉伸适配**：`ShopChrome.Build()` 用构建时 `cam.aspect` 布局（exitX = -halfW + margin，ShopChrome.cs:169-170），拉伸后左右贴边按钮不再贴边、rows 1-2 列 X 偏移。若做：加 `Relayout()`（重算既有对象 localPosition + label sizeDelta，不重建），挂同一 resize diff。与本次改动正交。
- 战斗阶段 avatar/HP 的同款双轴问题（本次范围外）。
- 极窄 aspect 下 row 0 水平间隙挤压（HP ↔ $ chip，height-fit 后元素不再随宽度缩小）——出货 aspect 校准后仅极端窗口出现，真出现归 Phase B 的 row 0 重排。

## 6. 回归与文档

- `docs/RegressionChecklist.md` 追加一行（含「travel 中拉伸落地 snap 一帧」已知项）。
- AGENTS.md「Shop top bar v3」bullet 追加一句 height-fit 说明；编辑后 `wc -c AGENTS.md` 保持 ≤ 32KB（≥1KB 余量）。
- 本文档为现行方案；`plans/plan-shop-topbar-combat-hud-reuse-2026-09-19.md` 为历史计划不改。

## 7. 验收（用户 Play，按视觉诊断惯例不主动进 Play）

1. 出货默认窗口：与现状观感一致（校准保证）。
2. 自由拉伸（超宽 / 极窄 / 拉高）：HP+icon 与 chips 同步缩放（跟窗口高度），顶栏 y 重新贴位、不剪顶。
3. Result 页停留时拉伸 → 进店：落地一帧 snap 后位置正确。

## 8. 风险

- 极窄窗口 row 0 间隙（§5 第三条）。
- 用户名标签随 icon 定标，观感与 0.28 时代不同（预期行为，随 icon 走）。
- resize 轮询为每帧两次 int 比较，开销可忽略；不触碰 `Application.runInBackground` 相关陷阱（纯逻辑帧内计算）。
