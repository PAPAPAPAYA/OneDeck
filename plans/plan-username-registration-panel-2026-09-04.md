# 计划：用户名注册面板（首次启动输入用户名）

日期：2026-09-04
上游：`plans/plan-async-pvp-client-2026-09-03.md` §2 身份模型 —— batch A 落了 `PlayerIdentity`（注册 API、409 语义、`player_identity.json` 持久化）和 `RegistrationInputNeeded` 事件，batch B 的 UI 层一直空缺。

## 1. 背景与目标

- 现状：没有任何代码 raise 或订阅 `RegistrationInputNeeded`；首次启动玩家没有输入用户名的入口，无身份时全部上传路径静默跳过。
- 目标：首次启动（服务器开关开启且无身份）弹出输入框；2–16 字符；可跳过 → 随机 `玩家#XXXX`；409 提示换名；可"稍后再说"，之后每次进店重试。
- 明确不做：账号切换 UI（已定零代码方案：`tools/identity_swap.bat` 换文件 + `BuildIsolatedDataDir` 构建 `-B` 分目录）；服务器 register 幂等等语义改动。

## 2. 方案

### 2.1 触发点（PhaseManager 两处）

- `OnEnable()`（场景启动）：`UsernameRegistrationPanel.EnsureCreated()` 建面板并订阅事件，然后 `RaiseIfNeeded()`。
- `EnteringShopPhase()`（每次进店）：`RaiseIfNeeded()` 重试 —— 对应 `PlayerIdentity` 注释里预留的 "shop-visit retries"，也是"稍后再说"的恢复点。
- `RaiseIfNeeded()` 条件：`ServerConfig.Active.enabled && !PlayerIdentity.HasIdentity`。重复 raise 由面板侧幂等吸收（已打开则只重置状态）。

### 2.2 面板（`Assets/Scripts/Net/UsernameRegistrationPanel.cs`，新增）

- 纯运行时构建，照 `ResultStatsPanel` 模式：自有 Canvas（`overrideSorting`、sortingOrder 300，高于游戏画布与结算面板 200）+ CanvasScaler(1080×1920, match width) + 全屏半透明黑底（`raycastTarget = true` 挡住底下游戏点击）+ 居中对话框。不改任何场景/Prefab。
- 字体：显式 `Resources.Load` `Fonts & Materials/SourceHanSansCN-Regular SDF` —— TMP 默认字体是 RobotoCondensed（无中文字形），`ResultStatsPanel` 全英文标签掩盖了这一点，本面板是第一个运行时构建的中文 UI，必须显式指定。
- 颜色：全部走 `GameColorPalette`（`resultPanelText` 正文、`damage` 错误提示、`tooltipBg` 对话框底、`slotRecess` 输入框底、`shield` 按钮底），`Me` 或 ColorSO 为 null 时用文档化兜底色，不硬编码 hex。
- 元素：标题、`TMP_InputField`（characterLimit=16）、错误提示行、三个按钮：确认 / 随机名字 / 稍后再说。EventSystem 缺失时防御性自建（正常场景 Shop UX 已有）。
- 教程守卫：`TutorialManager.IsTutorialActive` 时不弹（首次启动会先进教程），教程结束转入商店时由进店 raise 补弹。

### 2.3 按钮语义与成功路径

- 确认：`PlayerIdentity.Register(inputField.text, HandleResult)`；`busy` 期间禁点防双发。
- 随机名字：`Register(RandomFallbackName())`，409 时换随机数最多重试 3 次（撞名概率可忽略，纯防御）。
- 稍后再说：隐藏浮层不注册，等下次进店 raise。
- 成功：销毁面板 + 记日志 + `CardCatalogUploader.MaybeUpload()`（补上启动时因无身份跳过、且每版本只跑一次的目录上传）+ `UploadOutbox.Flush()`。
- 失败提示（静态纯函数 `HintFor`，EditMode 可测）：`username_taken` → 换名；`invalid_username` → 2–16 字符；`bad_response` → 响应异常；其余 → 网络错误。输入内容保留不重输。

### 2.4 数据正确性

- 所有入队路径已自带 `HasIdentity` 守卫（`DeckSaver`/`CardCatalogUploader`/`StatsSnapshotUploader`），`RunRecorder` 入队前回填 playerId —— 注册成功后队列里不存在空 playerId 的脏数据，补 Flush 无害。
- 身份文件读写与网络回调都在主线程 Unity API 语境，无并发问题。

## 3. 文件改动

| 文件 | 改动 |
|------|------|
| `plans/plan-username-registration-panel-2026-09-04.md` | 本文档（新增） |
| `Assets/Scripts/Net/UsernameRegistrationPanel.cs` | 新增：注册面板（运行时 UI + `HintFor` 纯函数 + `RaiseIfNeeded`/`EnsureCreated`） |
| `Assets/Scripts/Managers/PhaseManager.cs` | `OnEnable` / `EnteringShopPhase` 各加一处调用（各约 2 行） |
| `Assets/Scripts/Editor/Tests/UsernameRegistrationPanelTests.cs` | 新增：`HintFor` 映射 EditMode 测试（hermetic，无网络无 UI） |

## 4. 测试

- EditMode：`HintFor` 四分支映射；不触碰真实 `player_identity.json`（纯函数，无需 override seam）。
- 手工（批 F 一并）：删 `player_identity.json` → 启动弹窗 → 确认/随机/重名/稍后再说 → 进店重弹 → 成功后 admin 端 catalog 出现该玩家目录。
- 回归：`ServerConfig.enabled=false` 或已有身份时，面板创建但从不显示；现有 EditMode 全量防回归。

## 5. 执行状态

| 步骤 | 状态 |
|------|------|
| 计划文档 | ✅ 本文档 |
| 面板实现 | ✅ `UsernameRegistrationPanel.cs`。**踩坑**：CS0070 —— C# event 只能在声明类型内 Invoke，"按条件 raise"移为 `PlayerIdentity.RaiseRegistrationInputNeededIfNeeded()`（2026-09-04） |
| 布局修复（验收发现） | ✅ **踩坑**：运行时在根对象上 `AddComponent<Canvas>()` 默认落在 **WorldSpace**（UI 变成世界原点处的 780×720 平面，Game 视图错乱）；`ResultStatsPanel` 没踩到是因为它嵌在场景 Canvas 下。修复：显式 `renderMode = ScreenSpaceOverlay`（2026-09-04） |
| PhaseManager 接线 | ✅ `OnEnable` EnsureCreated + RaiseIfNeeded；`EnteringShopPhase` RaiseIfNeeded |
| CJK 字体 | ✅ 确认 TMP 默认字体是 RobotoCondensed（无中文字形），面板显式 `Resources.Load` 思源黑体 `Fonts & Materials/SourceHanSansCN-Regular SDF`。运行时构建中文 UI 的通用前提 |
| EditMode 测试 | ✅ `UsernameRegistrationPanelTests` 2/2 |
| 编译 + 回归 | ✅ 无编译错误；全量 EditMode 466 中 465 过 / 1 既有跳过（`RecorderAnimationPlayerTests` 嵌套协程时序，非本次引入） |
| 手工 Play Mode 验证 | ⬜ 弹窗全流程（确认/随机/重名/稍后再说/进店重弹）并入批 F 双端联调 |
