# OneDeck [Linger] 卡牌检查

检查范围：`Assets/Prefabs/Cards/3.0 no cost (current)/` 下所有 `.prefab` 的 `myTags` 字段。

Tag 枚举（来自 `EnumStorage.cs`）：
- `0` = None
- `1` = Linger
- `2` = ManaX
- `3` = DeathRattle

## 实际带有 Linger (值 1) 的卡牌

| 卡牌 | 路径 | myTags | 是否配置了 `CheckCost_IndexBeforeStartCard` | 是否在模拟器中已加 Linger 判定 |
|---|---|---|---|---|
| LARGE_SCALE_DEATH | Bury and buried\Bury\2_Rare\LARGE_SCALE_DEATH.prefab | 01000000 | ❌ 否 | ❌ 否（Unity 本身也没配） |
| RIFT_COFFIN | Conjure\1_Uncommon\RIFT_COFFIN.prefab | 01000000 | ✅ 是 | ✅ 是 |
| DEATHBED_CURSE | Conjure\2_Rare\DEATHBED_CURSE.prefab | 01000000 | ✅ 是 | ✅ 是 |
| CURSE_ENCHANTMENT | Curse\2_Rare\CURSE_ENCHANTMENT.prefab | 01000000 | ✅ 是 | ✅ 是 |
| QUICK_RESPONSE_PROTOCOL | General\1_Uncommon\QUICK_RESPONSE_PROTOCOL.prefab | 01000000 | ✅ 是 | ✅ 是 |
| WEAPON_SPIRIT | General\1_Uncommon\WEAPON_SPIRIT.prefab | 01000000 | ✅ 是 | ✅ 是 |
| ETERNAL_GHOST | General\2_Rare\ETERNAL_GHOST.prefab | 01000000 | ✅ 是 | ✅ 是 |
| GRUDGE | _DONT INCLUDE\_Recycle Bin\GRUDGE.prefab | 01000000 | ✅ 是 | N/A（不在当前卡池） |

## 说明

- 当前卡池里 **7 张**带有 Linger 标签。- 其中 **6 张**在 `CostNEffectContainer.checkCostEvent` 里配置了 `CheckCost_IndexBeforeStartCard`，模拟器已全部按此条件判定。- **LARGE_SCALE_DEATH** 虽然也有 Linger 标签，但 prefab 里**没有**配置 `CheckCost_IndexBeforeStartCard`，所以模拟器保持原样（揭示即触发）。如果这是设计遗漏，可以单独补上。- 其余卡牌（如 GRAVE_PORTAL、AVENGER 等）的 `myTags` 是 `03000000`（DeathRattle），不触发 Linger 判定。
