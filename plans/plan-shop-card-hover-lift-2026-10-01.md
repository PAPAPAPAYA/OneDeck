# Plan: 商店卡片 hover 弹起动画 — 货架 + 牌库 + 升级区 (2026-10-01)

- **Date**: 2026-10-01
- **Status**: IMPLEMENTED 2026-10-01（v2 基线 675/675 零失败；**v3 本会话实施**：斜向弹起 + 价签/大影子钉地 + 价签接管悬停；§6.5 统一方案按用户要求**搁置**（各消费者保留各自的量，卡片 hoverLift=0.25 独立字段）。v3 实施注记：用户拍板「完全分离」= 弹起时价签与大影子钉在原地（世界钉住，面滑回落地才重挂），光标悬停价签时卡片直接落回（`PriceButtonOwnsCursor` → `DropHoverLift`）；`ApplyPriceButtonPlacement` 在钉住期间跳过重写（`_liftPinActive && _pinButtonTr == buttonRoot`）防每帧互搏；`hoverLiftY/hoverLiftScaleBoost` 字段删除、`hoverLift` 单一斜向量、`hoverLiftDuration` 0.12（场景 YAML 同步改 0.12，用户点 Reload 重载场景）。**v3 EditMode 基线：675 总量 / 674 通过 / 0 失败 / 1 既有忽略**（`PlayRecordersCoroutine_SameSourceMultipleRecorders_PopsUpOnce`，全量套件嵌套协程时序、单跑通过，与本次改动无关）——零漂移。Play 目测待用户。
- **Request (user, 2026-10-01)**: 商店页面的卡片也加上鼠标 hover 时候的弹起动画，提供方案；**v2 追加：牌库和升级区的卡也要弹起**；**v3 追加：弹起动画改为「类似 button 那种」且「卡片弹起不要包含 price button」（用户澄清 = 完全分离）**。「也」的参照 = 卡片上价格按钮（PhysButton）hover 已有弹起，以及战斗页卡牌 hover pop-up。

## 1. Goal / non-goals

Goal:
1. 商店页**三类卡片**（货架卡 `_spawnedShopCards`、牌库卡 `_spawnedPlayerCards`、升级区卡 `_spawnedUtilityCards`）hover 时弹起：上移 `hoverLiftY` + 微放大 `(1 + hoverLiftScaleBoost)`，OutBack 弹起 / OutQuad 落下，与 PhysButton hover、空槽 spawn pop 的缓动语言一致。空槽底框（`_spawnedEmptySlots`，无 ShopCardView）不弹。
2. 复用 `CardPhysObjScript` 的 `TargetPosition / TargetScale` 体系驱动（不直接打 transform，否则会被 relayout 拽回去），`ShopUXManager` 挂 live-tunable 参数。
3. 与商店全部既有状态机不打架：点击放大、购买/售出飞行、三区重排、重掷翻牌、Inspector 实时调参、叠放重复卡、utility 脉冲强调。

Non-goals:
- 复用战斗 hover pop-up 系统（方案B，已否，见 §5）。
- `DOPunchPosition` 直接打 transform（与 Target 体系冲突，已否）。
- 悬停**价格按钮本身**时卡片不弹（按钮 collider 在卡片前面，`OnMouseEnter` 不落在卡上；按钮自己弹起已够反馈）。光标从卡身移到按钮上则卡片**保持**弹起（按钮在保留区内），属预期。
- 分区差异化弹起参数（三个区共用一组参数；需要时再加 per-zone 字段）。

## 2. Current state (verified facts, 2026-10-01)

| 位置 | 现状 |
|---|---|
| `CardPhysObjScript.BeginHover` (:1855) | `if (!IsInCombatPhase()) return; // Shop: tooltip only, no pop-up` — 商店阶段共享 hover 系统只出标签 tooltip，卡片本体不弹 |
| `ShopCardView` (:316-345, :381-421) | 只有**点击**放大预览（`EnlargeCard`/`RestoreCard`），无 hover 反馈；`_enlargeCooldown` 0.5s 只防放大抖动 |
| `ShopCardView.EnlargeCard` (:385) | `_originalPosition = _cardPhysObj.TargetPosition` —— 若 hover 已抬起，TargetPosition 带 offset，恢复落点会被污染，**必须改为从 hover base 取** |
| `PhysButton` (:32-34, :302-320) | 价格按钮 hover 已有弹起：offset `(-0.1085, +0.1085)`、OutBack 0.12s 弹起 / OutQuad 回落 —— 本方案的体感参照 |
| `CardPhysObjScript.StartPositionTween` (:700-728) | 位置 tween 硬编码 `moveDuration=0.3 / OutQuad`，对 hover 太慢；**没有** ease/duration 重载。对照：`SetTargetScale(target, Ease, float, float)` 重载已存在（:655-668，空槽 spawn :1092 与 PulseMatchingCard :1340 在用） |
| `CardPhysObjScript.IsPositionTweenPlaying` (:80-84) | 公开属性，可直接用作「卡在飞行中禁止臂起」的门槛（入场飞入、购买飞入牌库、售出飞出） |
| 战斗 hover 关键经验 (:1691-1694, :1719-1723) | **不用 OnMouseExit 结束 hover**（卡片位移会让光标脱出引发抖动循环），改为逐帧轮询 + **槽位锚定**保留区（投影光标到槽位 z 平面测试），卡片被抬起后判定不变 |
| `ShopUXManager.RelayoutPlayerDeckCards` (:463-488) | 牌库 + 升级区两段循环，`StackSlotAssigner.Assign` 算槽位（含叠放 copy offset）后直接 `SetTargetPosition` —— hover 态卡片会被拽回 base，需改走同步分支 |
| `ShopUXManager.OnCardPurchased` (:878-950) | 购买后 `shopItemIndex = -1`（:906-910 / :944-947）+ 归入牌库/升级区列表 + 重排；被买卡可能正处于 hover 态（光标在按钮上=保留区内） |
| `ShopUXManager.OnCardSold` (:961-1028) | 售出卡 `SetTargetPosition(起点)+SetTargetScale(0)` 飞出后销毁（:1010-1019），随后重排剩余卡 —— 售出卡可能正处于 hover 态 |
| `ShopUXManager.RelayoutAll` (:496-509) | Inspector 实时调参对非放大货架卡 `SetPositionImmediate`（snap）——hover 态会被 snap 回去，需与 `IsEnlarged` 同样跳过 |
| `ShopUXManager.RemoveFromShopCards` (:604-617) | 货架重排：放大卡走 `NotifySlotMoved` 同步恢复点，其余卡直接重设 target —— hover 态需要同样处理 |
| `PulsePlayerCard` (:1324-1345) | utility 生效时对牌库实例做 1.2x OutBack 脉冲，读 `TargetScale` 作 base —— 与 hover 抬起共存无漂移（见 §3.4 注） |
| 重掷 (`OnReroll`/`FlipShopCardsFaceDown`) | 全程 `ShopInputGate.Blocked`，货架卡翻背（`isFaceUp=false`、`isFlipPlaying`）；只动货架，不触牌库 |
| Collider | 商店卡与战斗卡同用 PhysicalCard.prefab，带 Collider2D（战斗 hover 的 `IsCursorOverCard` 依赖它），2D mouse events 可用 |

## 3. Implementation

### 3.1 `CardPhysObjScript`：`SetTargetPosition` 加 ease/duration 重载（一行级，战斗路径零改动）

镜像已有的 `SetTargetScale` 重载先例；`StartPositionTween` 加可选参数（全部默认值 = 现行为逐字节不变）：

```csharp
/// <summary>Set target position with explicit ease/duration (e.g. hover lift), same
/// override pattern as SetTargetScale. Default overloads keep moveDuration/moveEase.</summary>
public void SetTargetPosition(Vector3 target, Ease easeOverride, float durationOverride, Action onComplete = null)
{
    TargetPosition = target;
    if (SpecialAnimationPinsPosition) { onComplete?.Invoke(); return; }
    StartPositionTween(easeOverride, durationOverride, onComplete);
}

// StartPositionTween 签名改为 (Ease? easeOverride = null, float? durationOverride = null, Action onComplete = null)
// duration = durationOverride ?? GetCombatScaledDuration(moveDuration)
// ease     = easeOverride ?? moveEase
```

### 3.2 `ShopUXManager` 新增参数块（live-tunable，延续 panels 手调惯例）

```csharp
[Header("Card Hover Lift (plan-shop-card-hover-lift-2026-10-01)")]
[Tooltip("商店页卡片 hover 弹起总开关（货架 + 牌库 + 升级区）")]
public bool hoverLiftEnabled = true;
[Tooltip("hover 时卡片上移的世界单位（+Y）")]
public float hoverLiftY = 0.4f;
[Tooltip("hover 时放大增量（0.05 = 放大到 1.05 倍）")]
public float hoverLiftScaleBoost = 0.05f;
[Tooltip("弹起/落下单程时长（秒）；OutBack 弹起 / OutQuad 落下，与 PhysButton 一致")]
public float hoverLiftDuration = 0.15f;
[Tooltip("hover 保留区在卡片静止 bounds 四周外扩的世界单位（防抬起后光标在卡缘抖动循环；网格间距若变密需调小）")]
public float hoverRetentionMargin = 0.25f;
```

### 3.3 `ShopCardView`：hover 状态机（三区共用）

新增字段：`_isHoverLifted`、`_hoverBase`（Vector3，静止槽位锚点）、`_hoverBaseScale`、`_hoverRestBounds`（arm 时刻的 `collider.bounds`，此时卡在静止位）、缓存的 `_hoverCollider`/`_hoverCamera`。

**臂起（`OnMouseEnter`，与战斗共用 collider 事件，互不干扰——战斗路径在商店只出 tooltip）：**

```csharp
// 门槛全过才弹起（三区统一，不再按 shopItemIndex 区分）：
//   ShopUXManager.Instance != null && hoverLiftEnabled
//   && 当前阶段 == Shop && !ShopInputGate.Blocked          // 重掷/转场期间不弹
//   && !PhaseTransitionDriver.IsTransitioning              // 转场飞行不弹
//   && !_cardPhysObj.IsPositionTweenPlaying                // 入场/购买/重排飞行中不弹，
//                                                          //   且保证 bounds 在静止位采样
//   && _cardPhysObj.isFaceUp && !_cardPhysObj.isFlipPlaying // 翻牌期间不弹
//   && !_isEnlarged && _enlargeCooldown <= 0f              // 放大态/恢复滑行期间不弹
// 记录：_hoverBase = _cardPhysObj.TargetPosition（此时==该卡静止落点）
//       _hoverBaseScale = _cardPhysObj.TargetScale
//       _hoverRestBounds = collider.bounds（静止位采样，保留区锚点）
// 弹起：
_cardPhysObj.SetTargetPosition(_hoverBase + Vector3.up * hoverLiftY, Ease.OutBack, hoverLiftDuration);
_cardPhysObj.SetTargetScale(_hoverBaseScale * (1f + hoverLiftScaleBoost), Ease.OutBack, hoverLiftDuration);
_isHoverLifted = true;
```

**维持/退出（Update 逐帧轮询，不用 OnMouseExit —— 战斗 :1691 的核心教训）：**

```csharp
if (!_isHoverLifted) return;
// 1) 静默退出（不回写 target —— target 此刻归别的所有者）：
//    - _isEnlarged（放大态拥有 target）
//    - 被买/被卖（OnCardPurchased / OnCardSold 调 NotifyTargetTaken 显式移交，见 3.4-2）
//    - !isFaceUp || isFlipPlaying || ShopInputGate.Blocked（重掷/转场）
//    - PhaseTransitionDriver.IsTransitioning（转场驱动接管牌库卡）
// 2) 光标退出保留区 → 落下：
//    投影光标到 _hoverBase.z 平面（战斗 :1721-1723 同款 ScreenToWorldPoint 投影），
//    命中测试 = _hoverRestBounds.Expand(hoverRetentionMargin * 2)。
//    静止位锚定保证抬起位移不会让判定翻面 → 无「抬起→脱出→回落→再抬起」循环。
//    落下 = SetTargetPosition(_hoverBase, Ease.OutQuad, hoverLiftDuration)
//         + SetTargetScale(_hoverBaseScale, Ease.OutQuad, hoverLiftDuration)
```

**公开接口（供 ShopUXManager 移交/同步）：**

```csharp
public bool IsHoverLifted => _isHoverLifted;
/// <summary>target 所有权移交（购买/售出/转场）：解除 hover，不回写 target。</summary>
public void NotifyTargetTaken() { if (_isHoverLifted) { _isHoverLifted = false; } }
/// <summary>卡片静止落点变更（三区重排）：同步 base；抬起中则重算 target = 新 base + 弹起量。</summary>
public void NotifySlotMoved(Vector3 newBasePosition)
{
    _hoverBase = newBasePosition;
    if (_isEnlarged) _originalPosition = newBasePosition;   // 现有行为保留
    if (_isHoverLifted)
    {
        _cardPhysObj.SetTargetPosition(_hoverBase + Vector3.up * hoverLiftY, ...);
    }
}
```

### 3.4 三区重排 / 买 / 卖 的接线（`ShopUXManager`）

1. **`RelayoutPlayerDeckCards` (:463-488)**：两段循环抽一个私有 helper `RetargetDeckCard(card, assigner)`——`Assign` 算出新槽位后，`view.IsHoverLifted` 走 `view.NotifySlotMoved(slotPos)`（base 同步 + 保持抬起），否则照旧 `SetTargetPosition(slotPos)`；`SetPriceSuppressed` 与两段循环顺序不变（assigner 是有状态的，顺序决定槽位分配，必须保持 occupying → utility 的既有次序）。覆盖全部触发点：买（:925/:949）、卖（:1002/:1027）、升级区 growth（`SpawnAdditionalEmptySpaces` :1066）、牌库带 shift（`RelayoutDeckBand` :539）。
2. **`OnCardPurchased` (:878) / `OnCardSold` (:961)**：对被买/被卖卡先取 `ShopCardView` 调 `NotifyTargetTaken()`（显式移交，不再靠 shopItemIndex 反推）——购买卡随后飞向牌库新槽、售出卡飞向起点销毁，hover 不回写、不打架；落位后用户重新移入光标即可再次臂起（`IsPositionTweenPlaying` 门槛保证飞行中不臂起）。
3. **`RemoveFromShopCards` (:611) / `RelayoutAll` (:504)** 的跳过条件从 `view.IsEnlarged` 扩为 `view.IsEnlarged || view.IsHoverLifted`：重排走 `NotifySlotMoved`（新 base + 保持抬起），`RelayoutAll` 跳过 snap 改走同步，避免把抬起中的卡 snap 回槽位。

**注（utility 脉冲共存）**：`PulsePlayerCard` 读 `TargetScale` 作脉冲 base——hover 抬起中触发时，脉冲峰值 = 抬起 scale × 1.2、落回 = 抬起 scale，随后 hover 落下再回 `_hoverBaseScale`，全程无漂移，无需特殊处理。

**注（保留区相邻重叠）**：默认网格间距（xOffset/yOffset）远大于卡宽 + 2×margin，相邻卡保留区不重叠；若日后调密网格出现双卡同抬，调小 `hoverRetentionMargin` 即可。

## 4. Timing / 验证

- 默认体感 ≈ PhysButton（0.12s）与空槽 pop 之间：**0.15s** 弹起 + 0.15s 回落，四参数 + 总开关全部 Inspector live-tunable，三区共用。
- EditMode 全量回归基线（`StartPositionTween` 只加默认参数，战斗路径行为不变；重排循环只换分支不改分配顺序，预期零漂移）。
- **Play 验证点**：
  1. 三区各自 hover：上移 + 微放大（OutBack 回弹感），移开回落到各自槽位；
  2. 快速扫过同区相邻卡 A→B→C 及沿卡缘慢扫：各弹一次，无抖动循环、无双卡同抬；
  3. hover 中点击货架卡放大 → 飞到放大位；再点恢复 → 落回**原槽位**（不带偏移），0.5s 冷却内不重新弹起；
  4. hover 货架卡时点价格按钮购买：卡解除 hover、飞入牌库新槽无回拽；**其余卡重排时抬起中的卡跟随新槽位继续抬着**；
  5. hover 牌库卡时点「售出」：卡解除 hover、飞出销毁；剩余牌库卡重排，抬起中的卡跟随新槽位；
  6. 牌库卡 hover 中触发生效脉冲（utility 购买）：脉冲以当前抬起 scale 为 base，结束后无 scale 漂移；
  7. hover 中重掷：货架卡正常翻背刷新，无位置抖动；牌库卡不受影响；
  8. 转场进战斗：hover 强制解除，牌库卡交给 PhaseTransitionDriver，飞行无干扰；
  9. 叠放重复卡（货架与牌库）：光标压在叠上只有最前一张弹（2D mouse events 只命中最前 collider）；
  10. Inspector live tuning：`hoverLiftY` 即时生效（逐帧比较 re-assert）；`hoverLiftScaleBoost` / `hoverLiftDuration` 下一个 hover 周期生效（缩放不 re-assert，避免与 PulsePlayerCard 的 scale 脉冲互杀）；战斗页 hover pop-up / 洗牌回归不变。

## 5. 被否方案

- **方案B：改 `CardPhysObjScript.BeginHover` 的 Shop 分支，让共享 hover 系统接管弹起。** 优点：z 仲裁/延迟计时复用。缺点：共享系统的 pop-up 全是战斗语义（`PopUpCard`/`SlotInCard`、输入块、让位锚点、大影子驱动），`UpdateHover` 的 force-hide 条件还要为商店重写；该文件 VISUAL-FIX 历史很长，为视觉小功能动它的回归风险不成比例。已否。
- **方案C：`DOPunchPosition` 直接打 transform。** relayout/购买重排会立刻把卡拽回，且 punch 无法被 target 体系感知。已否。
- **方案D（v1 曾考虑）：hover 只限货架卡、靠 `shopItemIndex >= 0` 反推 target 归属。** v2 范围扩展后该推断不再成立（牌库卡的 base 由 assigner 每次重排算出），改为显式的 `NotifyTargetTaken` / `NotifySlotMoved` 移交与同步。

## 6. v3 Addendum: 改为 PhysButton 同款斜向弹起 (2026-10-01, PROPOSED)

### 6.1 Request (user, 2026-10-01)

卡片的弹起动画要「类似 button 那种」。v2 现状 = 直上 +Y 0.4 + 放大 1.05x；button 弹起是另一套语言。

### 6.2 PhysButton 弹起的构成（verified facts，UIKitDemo §02 ".phys" / Guidelines §2）

| 构成 | 事实 | 出处 |
|---|---|---|
| 方向 | `HoverVector = (-hoverLift, +hoverLift)` 等量斜向，**朝左上光源** | `PhysButton.HoverVector` (:302-304) |
| 量 | `hoverLift = 0.1085`（face-local units，与 `restShadow` 同值） | (:32-33) |
| 缩放 | **无** —— 纯位移 | `EnterHoverVisual` (:312-315) |
| 时长/缓动 | OutBack 0.12s 弹起 / OutQuad 0.12s 回落 | (:33-34, :312-320) |
| 阴影 | **硬阴影钉地不动**（"hard shadow stays anchored on the ground"）；press 时 face 落到阴影上 —— face 动、影子不动才有「抬起」立体感 | 类头注释 (:9-11)、`SetShadowTransform` (:149) |
| hit target | 不动（static root + Visual group 分离）—— 我们已用「静止锚定保留区」等价解决，无需改 | 类头注释 (:12-14) |

商店卡上可钉的影子 = `PhysicalCardBigShadow`（大软影，投在地上那份；`PhysicalCardShadow` 是贴边视觉，随卡走）。`CardPhysObjScript.bigShadowRenderer` 已自动接线且 public（:263-266, :983），零查找成本。

### 6.3 改动（状态机零改动，纯表现层）

1. **参数迁移（ShopUXManager）**：
   - `hoverLiftY` + `hoverLiftScaleBoost` → 删除，换 **`hoverLift = 0.25f`**（单一量，等量用于 -x/+y，与 `PhysButton.HoverVector` 同约定；按钮 0.1085 是按钮 face 尺度，卡片更大按比例放大，Inspector 可调）。
   - `hoverLiftDuration` 0.15 → **0.12**（与 `PhysButton.hoverDuration` 一致）。
   - `hoverRetentionMargin` 不变。
2. **位移向量（ShopCardView.ReassertLiftedPose）**：`liftedPosition = _hoverBase + new Vector3(-hoverLift, +hoverLift, 0f)`（z 不动，保留遮挡关系）。`DropHoverLift` 不变（回 base）。
3. **阴影钉地（新增）**：`TryArmHoverLift` 缓存 `phys.bigShadowRenderer.transform` + 影子世界位；抬起期间每帧 `shadow.position = 缓存位`（face 斜向滑出、影子留在原地 = button 的立体感）；`NotifySlotMoved` 把缓存位随 base 平移（重排时影子跟槽位）；落下/解除即停写，face 滑回影子正上方。空转安全：影子若被压制不可见，写 transform 无副作用。
4. **不动的部分**：臂起门槛、保留区轮询、`NotifyTargetTaken` / `NotifySlotMoved` 移交、三区重排接线、`SetTargetPosition` 重载 —— 全部沿用 v2。

### 6.4 验证（v2 清单之外新增/修改）

1. hover：卡片朝**左上**斜移，底下大影**留在原地**（face 与影子分离的立体感），无缩放；
2. 移开：卡片斜向滑回影子正上方，影子全程未动；
3. 抬起中购买触发重排：卡片跟随新槽位，影子同步到新槽位；
4. 与价格按钮同屏 hover：卡片与按钮弹起方向、节奏一致（同一光源语言）。

### 6.5 v3.1：hoverLift 全局统一（2026-10-01，PROPOSED，随 v3 一并实施）

#### 分歧全景（verified facts）

所有 PhysButton 消费者的方向（`(-l,+l)` 朝左上光）、时长（0.12s）、缓动（OutBack/OutQuad）**已一致**，唯一分歧是**量**：

| 消费者 | hoverLift / restShadow | 来源 |
|---|---|---|
| 买/卖按钮 PriceButton.prefab | **0.1085** / 0.1085（+authoredFaceSize=1） | variant 覆盖值 |
| 重掷 RerollButton / 顶栏 ExitButton / OptionsButton | **0.05** / 0.05（继承） | 全是 Tpl_WorldButton 的无覆盖 variant，继承模板值 |
| Tpl_WorldButton.prefab（模板） | **0.05** / 0.05，duration 0.12 | 序列化值（PhysButton 代码默认是 0.1085） |
| 运行时构建（ShopWorldWidgets.CreateWorldButton，现仅重掷 fallback 在用） | `hoverLift = restShadow` = 调用方传 0.05 | ShopSectionPanels fallback (:393-397) |
| 卡片（v3 提案） | 0.25（提案值） | ShopUXManager 字段 |

约定事实：项目所有实例都遵守 **`hoverLift == restShadow`**（影子偏移 = 抬起量，press 时脸正落影子上，"lands on the shadow" R5）；`restShadow` 还联动 `ConfigureWorldFaceSize` 的影子摆位与 hitbox 扩边（:213-219），**两个字段必须同值同改**。

#### 统一方案

**统一值 = 0.1085**（买/卖按钮现值 = PhysButton 代码默认值 = face-local 约定值），**单一常量源**：

1. **新增 `UIKitMotion` 静态常量类**（沿用 `GameColorPalette` 模式）：`HoverLift = RestShadow = 0.1085f`、`HoverDuration = 0.12f`、`PressDuration = 0.06f` —— 全项目动效约定的唯一引用点。
2. **Tpl_WorldButton.prefab**：restShadow/hoverLift 0.05 → 0.1085（YAML 两处 float）。RerollButton / ExitButton / OptionsButton 是无覆盖 variant，自动跟随；PriceButton 已是 0.1085 不动。authoredFaceSize=0 的模板走 runtime re-fit，影子位置与 hitbox 随新值自动重摆，press 落点与影子保持重合。
3. **ShopWorldWidgets.CreateWorldButton**：删 `restShadow` 参数，内部 `button.restShadow = UIKitMotion.RestShadow; button.hoverLift = UIKitMotion.HoverLift;`（同值绑定的既有约定不变）；fallback 调用点同步。
4. **卡片（修订 v3 数值）**：位移向量 `(-hoverLift, +hoverLift)`；`ShopUXManager` 参数块收缩为 `hoverLiftEnabled` + `hoverLift`（默认 = `UIKitMotion.HoverLift`，字段保留仅作卡片特调逃生门）+ `hoverRetentionMargin`；`hoverLiftY / hoverLiftScaleBoost / hoverLiftDuration` 删除，时长直接用 `UIKitMotion.HoverDuration`。
5. **防漂移**：`PhysButton.OnValidate`（editor-only）在 `hoverLift != restShadow` 时 LogWarning —— 同值约定再分化时立刻可见。

#### 统一后验证

1. 买/卖、重掷、顶栏 Exit/Options 四类按钮 hover 位移、影子几何、时长完全一致（0.1085 / 0.12）；
2. 卡片 hover 与按钮同位移同节奏（同光源语言下的同一种"抬起"）；
3. 按住（press）任一按钮：脸落在影子上（restShadow 联动后仍自洽）；
4. 顶栏/重掷按钮观感变化 = 抬起更明显、影子更深（0.05→0.1085），即向买/卖按钮看齐——预期内。

## 7. v3.2 Addendum: 放大预览时价签留在原地 (2026-10-01, IMPLEMENTED)

- **Request (user)**: 「点击卡片放大居中的时候，购买/售出按钮能不动吗」——完全分离的延伸：价签是桌面元素，卡面飞去居中预览时价签留在货架上。

### 7.1 实现（ShopCardView，钉住机制重构）

1. **单一 `_liftPinActive` 拆成两条独立生命周期**：`_pinButtonActive`（价签）与 `_pinShadowActive`（大影子）。
   - **价签**贯穿 hover 弹起 + **放大预览全程** + 回程滑行；面落回原位才重挂（恢复 authored localPosition + localScale）。
   - **大影子**只在 hover 弹起/落回期间钉地；放大开始即重挂随卡飞（放大卡带影子是既有观感）。
2. **放大时的缩放补偿**：价签挂在 FlipRoot 下会随放大根缩放——钉住期间逐帧 `localScale = 捕获的世界缩放 / 父 lossyScale`，位置照旧世界钉住 → 价签在货架上保持与静止时逐像素一致，不随卡变大。
3. **捕获时机**：`CaptureButtonPin()` 统一入口（已激活则 no-op，无缝跨 hover→放大、回程→再 hover）；`EnlargeCard` 里调用，**直接点击未经过 hover 的卡**也能在起飞前捕获（飞行中点击则放弃捕获，价签随本次飞行，退化安全）。
4. **移交语义**：`NotifyTargetTaken`（购买/售出）→ 全部重挂随飞行（放大中购买 = 价签归位到卡面一起飞向牌库）；转场（`IsTransitioning`）→ 强制全部重挂，防止钉住的价签与转场驱动抢 transform。
5. `ApplyPriceButtonPlacement` 门控改为 `_pinButtonActive && _pinButtonTr == buttonRoot`。

### 7.2 交互流（实施后）

- 弹起：面斜向抬起，价签+影子在原地 → 光标到价签：面落回、价签自己弹 → 点价签购买：价签归位随卡飞入牌库。
- 点击卡面：面飞去居中放大，**价签留在货架原位原尺寸**（仍可悬停/点击购买，价格实时刷新），大影子随卡 → 再点卡面：面飞回原位落地，价签重挂无缝衔接。

### 7.3 验证

- EditMode 基线：675/675 完成、0 失败（compile 通过、零漂移）。
- **Play 验证点**：① 放大后价签在货架原位且尺寸不变；② 放大状态下价签可正常 hover（自己的弹起）与点击购买；③ 购买后价签随卡飞向牌库不残留；④ 恢复落地后价签无跳变；⑤ 放大状态下重掷/转场无残留对象。

## 8. v4 Addendum: 放大 = 模态预览 (2026-10-02, IMPLEMENTED)

- **Request (user)**: 放大状态下，hover/购买等其他一切交互 block 掉不生效——鼠标唯一能做的是点击取消放大。

### 8.1 实现（复用 ShopInputGate 引用计数总闸）

1. **`ShopCardView.EnlargeCard`**：`ShopInputGate.Block()`（与转场驱动/重掷的块安全嵌套）——所有咨询总闸的交互当场死亡；随后 `_cardPhysObj.EndHoverForShopModal()` 清掉点击前已在显示的标签 tooltip。**`RestoreCard`**：`ShopInputGate.Unblock()`（与 Block 严格配对）。
2. **豁免取消点击**：`HandleClickToRestore` 的门从 `!ShopInputGate.Blocked` 改为 `!PhaseTransitionDriver.IsTransitioning`——放大自持的闸不拦自己的取消点击；转场驱动自持的闸仍拦截（转场飞行中不响应）。
3. **`OnDestroy` 安全阀**：放大卡被销毁（重掷重建、阶段清理）时若仍持有闸必须释放，否则引用计数永久失衡、商店再也解不了锁。
4. **`PhysButton.PointerEnterCommon`**：闸住时不臂起 hover（无 IsPointerOver、无 hover/deny 视觉）——按下与释放激活原本就已咨询总闸（OnMouseDown / FinishPress），至此按钮的 hover/press/deny 全链路在模态下失效。
5. **`CardPhysObjScript`**：tooltip 触发点加 `!ShopInputGate.Blocked`（模态下不弹标签 tooltip；计时器继续走，闸开且仍悬停则照常显示）+ 新公开 `EndHoverForShopModal()`（清 `_currentHoverOwner` 后 EndHover，纯信息态 hover，不碰 target）。
6. **卡片本体按压**：`OnMouseDown` 原有 `!ShopInputGate.Blocked` 门在放大态生效 → 不能再点第二张卡放大（顺带修掉「多卡同时放大」的旧洞）。
7. **滚轮**：并行会话已实现 `blockScrollWhileEnlarged`（默认 true，VISUAL-FIX 2026-10-02），模态下滚轮冻结已覆盖，本次不重复加。

### 8.2 被闸住的交互清单（放大态）

买/卖价签 hover+press、重掷按钮、顶栏按钮、其他卡 hover 弹起、其他卡点击放大、标签 tooltip、（滚轮由 blockScrollWhileEnlarged 冻结）。唯一可用：任意左键点击 = 取消放大（转场中除外）。

### 8.3 验证

- EditMode 基线：675/675 完成、0 失败、1 既有忽略——零漂移。
- **Play 验证点**：① 放大后悬停其他卡无弹起、价签无 hover/按下视觉、点击买/卖/重掷无效果；② 放大卡自己的 tooltip 不再弹出；③ 任意位置左键点击 = 取消放大，落下后一切交互恢复；④ 转场期间点击不取消；⑤ 放大状态下不会出现第二张放大卡。

### 8.4 v4.1：放大态常驻显示 tips (2026-10-02, IMPLEMENTED)

- **Request (user)**: 卡片放大的状态下，常驻显示原本要鼠标悬浮才显示的 tips（标签 tooltip）。

**实现（CardTagTooltip 加钉住模式，tooltip 本就每帧自跟随卡片，钉住 = ShowFor 一次 + 挡住 hover 侧的隐藏）**：

1. **`CardTagTooltip.PinFor(card)`**：构建文本并显示，置 `_pinned = true`；无类型/标签信息的卡（Token、Start Card、无 tag）文本为空 → no-op，无事可钉。
2. **`HideFor` 加守卫**：`_pinned` 期间忽略 hover 侧隐藏（EndHover → HideFor 被挡）——光标移出放大卡、hover 系统任何 teardown 都不会藏掉常驻 tips。
3. **`UnpinFor(card)`**：解除钉住并隐藏（仅当当前显示的就是该卡）。`Hide()` 清 `_pinned`——Update 的强制隐藏（源销毁/翻背/阶段变）自动解除钉住。
4. **`ShopCardView.EnlargeCard`**：`ShopInputGate.Block()` 之后、`EndHoverForShopModal()` 之前 `PinFor`——先钉再结束 hover，teardown 的 HideFor 被守卫挡住，tips 无缝常驻（若点击前 tooltip 已在显示，视觉零跳变）。
5. **`ShopCardView.RestoreCard`**：`UnpinFor` 随模态一起收掉。放大卡被销毁（防御路径）时无需显式解钉：tooltip 自身 Update 的 force-hide（source == null）下一帧收掉并清 `_pinned`。
6. v4 的 tooltip 触发点闸（`!ShopInputGate.Blocked`）保持——放大态下 hover 系统不会再触发 ShowFor 与钉住竞争；闸开且仍悬停时正常 hover 行为（含 tooltip）恢复。

**Play 验证点**：① 放大即显示标签/类型 tips，无需悬浮，整段预览常驻；② tips 跟随卡面（含滚轮跟随位移）；③ 无 tag/类型信息的卡（Token/Start Card）放大无 tips（无内容可显示）；④ 取消放大 tips 立即消失；⑤ 恢复后再正常 hover 该卡，tooltip 行为如常（0.2s 延迟显示）。
