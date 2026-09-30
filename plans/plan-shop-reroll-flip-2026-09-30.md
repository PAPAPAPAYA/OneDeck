# Plan: 商店重掷动画 → 两段式原地翻牌 (2026-09-30)

- **Date**: 2026-09-30
- **Status**: IMPLEMENTED 2026-09-30（本会话实施；EditMode 全量基线零漂移；Play 目测待用户）
- **Request (user, 2026-09-30)**: 重掷时现在的卡片动画改为卡片翻转。拍板（AskUserQuestion）：**两段式原地翻**（旧卡原地翻背 → 原位替换 → 新卡原地翻正），翻正**逐张错开**。

## 1. Goal / non-goals

Goal:
1. 重掷视觉从「旧卡飞回起点缩小 → 新卡从起点飞入槽位」改为「旧卡在槽位原地翻成卡背 → 原位换新卡（仍是背面，接缝不可见）→ 新卡逐张错开翻正」。
2. 翻面完全复用 `CardPhysObjScript` 既有 FlipRoot / `SetFaceUp` 基建，`CardPhysObjScript` 零改动。
3. 三个时长参数挂 `ShopUXManager` 可调字段（延续 panels 可独立手调偏好）。

Non-goals:
- 商店**首次入场**动画不变（`InstantiateShopPhysCards` 保持飞入，与重掷路径分离）。
- 卡背美术（占位卡背 = 卡面 sprite + `OwnerCardColor` 着色，与战斗一致）。
- 单翻转半程换内容方案（每槽一卡中点换内容）——需要内容重绑 + 张数增减处理，风险不成比例，已否。
- 折中方案（旧卡飞走 + 新卡翻正）——用户已排除。

## 2. Current state (verified facts, 2026-09-30)

| 位置 | 现状 |
|---|---|
| `ShopUXManager.OnReroll` (:1101) | `ShopInputGate.Block()` + `SetRerollRolling(true)` → `AnimateShopCardsExit`（旧卡 `SetTargetPosition(起点)`+`SetTargetScale(0)` 飞走）→ 协程等 `moveDuration+0.05` → 销毁 → `SpawnShopCardsInternal`（新卡起点出生 scale 0→OutBack 飞入）→ 解锁 |
| `ShopUXManager.InstantiateShopPhysCards` (:144) | 商店入场生成，与重掷完全分离，本次不动 |
| `CardPhysObjScript.SetFaceUp` (:1104) | squash 翻面 scaleX 1→0→1，各半 `flipDuration*0.5`（0.3s），中点 `ApplyFaceVisibility`；只动 FlipRoot 不碰根 transform；商店阶段不吃战斗加速 |
| never-cover 守卫 (:1126) | 拦 `everRevealed==true` 的翻背；商店卡出生即面朝上、从未调过 SetFaceUp，`everRevealed=false`，普通翻背放行，**无需 force** |
| `BuildFlipRoot` (:864) | Awake 运行时建 FlipRoot + CardBack（卡面 sprite + `OwnerCardColor`），商店卡同款 |
| `ShopCardView.UpdatePriceDisplay` (:62) | 每帧驱动价格显示；`cardPricePrint` 是 FlipRoot face 元素会随翻面隐藏，但 **PhysButton 价格按钮是懒建独立对象，不在 FlipRoot 里，翻背时会浮在卡背上** |

## 3. Implementation

### 3.1 ShopUXManager 新增字段（Reroll Flip 组，Inspector 可调）

```csharp
[Header("Reroll Flip (plan-shop-reroll-flip-2026-09-30)")]
public float rerollFlipDuration = 0.3f;   // 一次翻面总时长（翻背/翻正各半）
public float rerollFlipStagger = 0.04f;   // 翻正逐张错开间隔；0=全同步
public float rerollFlipHold = 0f;         // 翻背落地到第一张翻正的停顿
```

### 3.2 Phase 1 — 旧卡原地翻背（`AnimateShopCardsExit` → `FlipShopCardsFaceDown`）

每张旧卡 `flipDuration = rerollFlipDuration` 后 `SetFaceUp(false, true)`。不再飞行/缩放，留在槽位。翻背全体同步。

### 3.3 Phase 2 — 原位替换 + 逐张翻正

- 协程等待 = `rerollFlipDuration + rerollFlipHold`（货架为空时跳过翻背等待）；弃用 moveDuration 推算。
- `SpawnShopCardsInternal`（仅重掷路径调用）改原地背面出生：`Instantiate` 直接落 `GetShopItemSlotPosition(i)`，`SetPositionImmediate` + `SetScaleImmediate(physCardSize)`，出生即 `SetFaceUp(false, false)` 瞬切背面；`cardImRepresenting`/`shopItemIndex`/`SetShopCardDescription` 照旧（文字写在隐藏面上，翻正即正确）。
- 新协程 `FlipSpawnedCardsFaceUpStaggered`：第 i 张延时 `i * rerollFlipStagger` 后 `SetFaceUp(true, true)`，等最后一张落地后才返回。
- 全部翻完才 `ShopInputGate.Unblock()` + `SetRerollRolling(false)`。

### 3.4 ShopCardView 价格守卫（一行级）

`UpdatePriceDisplay` 的 `showPrice` 条件追加 `&& _cardPhysObj.isFaceUp` —— 复用现有 `!showPrice` 隐藏分支（print + PhysButton + SetFaceDimmed(false)），旧卡翻背与新卡背面出生两条路径同被覆盖，翻正后自动恢复。

## 4. Timing / 验证

- 默认时序 ≈ 0.3（翻背）+ 0（hold）+ 0.3（翻正）+ 4×0.04（stagger）≈ **0.76s**（原 ≈0.65s），三字段 Inspector 手调。
- EditMode 全量回归基线 664/663/1（纯表现层改动，无测试覆盖动画本身）。
- **Play 验证点（待用户）**：① 重掷观感 = 翻牌刷新；② 翻背/背面出生期间价格按钮不浮在卡背；③ 空货架重掷（直接背面出生+翻正）；④ 商店入场动画未变；⑤ 折扣/买卖/stack 价格显示不受影响。
