# OneDeck — Enemy Deck Recorder 产品需求文档（PRD）

> 版本：v1.0-draft  
> 日期：2026-05-27  
> 状态：待确认（含关键决策点）

---

## 1. 项目背景

玩家在 PlayMode 中使用的卡组（Player Deck）经过实战验证后，开发者希望将其**一键复制为 Enemy Deck**，用于后续战斗中作为敌人卡组使用。

Recorder 以**场景中的 GameObject** 形式存在，进 PlayMode 前通过 Inspector 勾选开关，PlayMode 结束时自动产出 `DeckSO` 资产文件。

---

## 2. 功能目标

| 目标 | 说明 |
|---|---|
| **复制玩家卡组** | 读取玩家当前牌局使用的 `DeckSO`，复制为新的 Enemy DeckSO |
| **产出可复用资产** | 生成的 DeckSO 保存到 `Assets/Decks/Enemy/`，可重复用于多场战斗 |
| **支持多次记录** | 每次记录产出独立命名的 DeckSO，不覆盖历史记录 |
| **零代码干预** | 全程通过 Inspector 配置，无需修改现有业务代码 |
| **精确卡牌匹配** | 通过 `CardScript` 上的 `cardTypeId` 确保卡牌对应关系 |

---

## 3. 现有系统分析

### 3.1 代码扫描结果（2026-05-27）

| 组件 | 现状 | 影响 |
|---|---|---|
| `DeckScript` | `List<GameObject> deckList`，无操作逻辑 | 运行时敌人仍依赖 GameObject 列表，需中间层转换 |
| `DeckManager` | `myDeck` + `List<DeckScript> dummyEnemyDecks` | Recorder **不直接写入**，产出 SO 后需手动或自动挂载 |
| `CardScript` | `cardName`（string）+ `cardDesc` | **未发现 `cardTypeId` 字段**（需确认是否已存在） |
| `CardEventTrigger` | `CardActivateEvent`（UnityEvent） | 无需监听，Recorder 直接从 SO 读取 |
| `GameManager` | 空壳 | 无影响 |

### 3.2 重要发现

**当前项目中未发现 `DeckSO` 或 `CardData` 的 ScriptableObject 实现**。现有代码全部为 MonoBehaviour，卡牌数据存储在场景中的 GameObject 上。

这意味着：
- 如果已有 `DeckSO`，需要用户指明文件路径
- 如果还没有，Recorder 的实现需要先补完 `DeckSO` 和 `CardData` 的数据结构

---

## 4. 假设的数据结构（待确认）

以下结构为 PRD 假设，需用户确认或提供实际代码。

### 4.1 卡牌数据 CardData

```csharp
[System.Serializable]
public class CardData
{
    public string cardTypeId;    // CardScript 上的唯一标识（如 "card_001_fireball"）
    public string cardName;      // 显示名称
    // 可选：费用、效果、图标引用、描述等
}
```

### 4.2 卡组 ScriptableObject DeckSO

```csharp
[CreateAssetMenu(fileName = "NewDeck", menuName = "OneDeck/Deck")]
public class DeckSO : ScriptableObject
{
    public string deckName;                  // 卡组名称
    public List<CardData> cards;             // 卡牌列表
    // 可选：卡组描述、创建时间、来源标签等
}
```

---

## 5. Recorder 详细设计

### 5.1 组件形态

| 属性 | 说明 |
|---|---|
| **类型** | `MonoBehaviour`，挂在一个场景 GameObject 上 |
| **名称建议** | `EnemyDeckRecorder` |
| **运行时行为** | 仅在 PlayMode 生效，EditorMode 无操作 |
| **命名空间** | `OneDeck.EditorTools`（建议） |

### 5.2 Inspector 接口

```
EnemyDeckRecorder (Script)
──────────────────────────
[✓] Record On Play              ← bool，勾选后下次进 PlayMode 触发

Source Player Deck
  [PlayerDeck_SO______________]  ← DeckSO 引用槽（必填）

Output Deck Name
  [EnemyDeck_Run1_____________]  ← 新 DeckSO 文件名（不含 .asset）

Output Path
  [Decks/Enemy________________]  ← 相对 Assets/ 的子目录
```

### 5.3 执行流程

```
[勾选 Record On Play]
         │
         ▼
[进入 PlayMode]
         │
         ▼
[Awake] ──▶ 检查 recordOnPlay 为 true ──▶ 标记 _isRecording = true
         │                                    检查 sourcePlayerDeck 非空
         │
         ▼
[PlayMode 运行中] ──▶ Recorder 不干预任何游戏逻辑
         │
         ▼
[退出 PlayMode]
         │
         ▼
[OnDisable] ──▶ 检查 _isRecording ──▶ 调用 ExportEnemyDeck()
         │
         ▼
[ExportEnemyDeck() ── Editor-only]
    │
    ├──▶ 1. ScriptableObject.CreateInstance<DeckSO>()
    │
    ├──▶ 2. 复制卡组名称 + 卡牌列表
    │         │
    │         ├──▶ 遍历 sourcePlayerDeck.cards
    │         │         │
    │         │         ├──▶ 直接复制 CardData（值拷贝）
    │         │         │         或
    │         │         └──▶ 通过 cardTypeId 从全局卡池反查（如果有）
    │         │
    │         └──▶ 填充到 enemyDeck.cards
    │
    ├──▶ 3. 确保目录 Assets/{outputPath}/ 存在
    │
    ├──▶ 4. AssetDatabase.CreateAsset(enemyDeck, 路径)
    │         AssetDatabase.SaveAssets()
    │
    ├──▶ 5. 选中产出的资产（EditorUtility.FocusProjectWindow）
    │
    └──▶ 6. recordOnPlay = false（防止重复记录）
              EditorUtility.SetDirty(this)
```

### 5.4 产出示例

```
Assets/
└── Decks/
    └── Enemy/
        ├── EnemyDeck_Run1.asset      ← 第1次记录
        ├── EnemyDeck_BossTry.asset   ← 手动命名
        └── EnemyDeck_20260527.asset ← 日期命名
```

---

## 6. 关键决策点（需要用户确认）

### 6.1 关键问题 ①：CardScript 上的 cardTypeId

**当前代码扫描结果**：`CardScript.cs` 中只有 `cardName`（string）和 `cardDesc`（string），**未发现 `cardTypeId` 字段**。

| 选项 | 方案 | 影响 |
|---|---|---|
| **A** | `cardTypeId` **已存在**但未在已读文件里 | 用户指出实际路径，PRD 无需修改 |
| **B** | 给 `CardScript` **新增** `cardTypeId` | Recorder 需要依赖此字段做精确匹配 |
| **C** | 临时用 `cardName` 代替 ID | 名称冲突风险，不推荐 |

**→ 用户决策：_______________**

---

### 6.2 关键问题 ②：DeckSO 是否已存在

**当前代码扫描结果**：项目中未发现 `DeckSO` 或任何 `ScriptableObject` 卡组文件。

| 选项 | 方案 | 影响 |
|---|---|---|
| **A** | DeckSO **已存在** | 用户指明 `.cs` 文件路径，Recorder 直接复用 |
| **B** | 需要**新建** DeckSO + CardData | Recorder 开发需包含数据结构定义，工作量增加 |

**→ 用户决策：_______________**

---

### 6.3 关键问题 ③：CardData 里存的是完整数据还是仅 ID

假设 DeckSO 已存在，其 `cards` 列表的元素类型决定了 Recorder 的复制策略：

| 选项 | 方案 | Recorder 行为 |
|---|---|---|
| **A** | `List<CardData>` — 每个元素包含完整卡牌数据 | 直接值拷贝，最简单 |
| **B** | `List<string>` — 仅存 `cardTypeId` | 需从全局卡池反查完整数据再填充 |
| **C** | 混合结构 — ID + 部分运行时数据 | 视具体结构调整复制逻辑 |

**→ 用户决策：_______________**

---

### 6.4 关键问题 ④：全局卡池（CardDatabase）是否存在

如果 DeckSO 中只存 `cardTypeId`，Recorder 需要知道去哪找对应的完整 `CardData`。

| 选项 | 方案 | 影响 |
|---|---|---|
| **A** | 已有全局 `CardDatabaseSO` / `CardLibrary` | Recorder 通过它反查卡牌 |
| **B** | 没有全局卡池 | Recorder 只能复制现有 DeckSO 中的 CardData，无法补全缺失信息 |

**→ 用户决策：_______________**

---

### 6.5 关键问题 ⑤：敌人如何使用产出的 DeckSO

Recorder 产出 DeckSO 后，敌人运行时仍依赖 `DeckScript.deckList`（`List<GameObject>`）。

| 选项 | 方案 | 工作量 |
|---|---|---|
| **A** | **手动挂载** — 产出后用户手动把 DeckSO 拖入 DeckManager | 最小 |
| **B** | **自动 Loader** — 加一个 `EnemyDeckLoader` 组件，在战斗初始化时将 DeckSO 转成 `List<GameObject>` | 中等 |
| **C** | **重构 DeckManager** — 让 `dummyEnemyDecks` 直接引用 DeckSO，运行时按需实例化 | 较大 |

**→ 用户决策：_______________**

---

### 6.6 关键问题 ⑥：命名冲突处理

多次记录时，如果 `outputDeckName` 相同：

| 选项 | 方案 | 影响 |
|---|---|---|
| **A** | **自动重命名** — 后缀加 `_1`、`_2` | 不覆盖，但可能堆积 |
| **B** | **覆盖提示** — 同名时弹窗询问 | 需要 Editor 弹窗代码 |
| **C** | **强制覆盖** — 直接替换旧文件 | 简单但有数据丢失风险 |

**→ 用户决策：_______________**

---

### 6.7 关键问题 ⑦：是否需要在产出时自动关联 DeckManager

| 选项 | 方案 | 影响 |
|---|---|---|
| **A** | 不关联 — DeckManager 仍由用户手动配置 | Recorder 只负责产出 SO |
| **B** | 可选关联 — Inspector 加 `targetDeckSlot`（int），产出后自动塞入 `dummyEnemyDecks[index]` | 需要运行时兼容 |

**→ 用户决策：_______________**

---

## 7. 使用流程（最终形态）

### 7.1 首次配置（一次性）

1. 在场景中创建一个空物体，命名为 `EnemyDeckRecorder`
2. 挂上 `EnemyDeckRecorder` 脚本
3. 拖入 `sourcePlayerDeck`（Player Deck SO）
4. 填写 `outputDeckName`（如 `EnemyDeck_Warrior`）
5. 确认 `outputPath`（默认 `Decks/Enemy`）

### 7.2 每次记录流程

1. **勾选** `Record On Play`
2. 进入 PlayMode（正常游戏）
3. 退出 PlayMode
4. 自动产出 `Assets/Decks/Enemy/EnemyDeck_Warrior.asset`
5. Inspector 上 `Record On Play` 自动取消勾选
6. 将新 DeckSO 拖入 `DeckManager.dummyEnemyDecks`（或自动关联）

---

## 8. 非功能需求

| 项 | 要求 |
|---|---|
| **平台** | Editor-only（`#if UNITY_EDITOR`），不打入构建包 |
| **依赖** | `UnityEditor` 命名空间，不依赖外部包 |
| **性能** | 导出操作在 PlayMode 退出时执行，不影响运行时性能 |
| **兼容性** | 与现有 URP、Input System、Unity 6000.0.62f1 无冲突 |
| **版本控制** | 产出的 `.asset` 文件默认纳入 Git，需确保 `Assets/Decks/Enemy/` 目录已加入 `.gitignore` 或已追踪 |

---

## 9. 下一步行动

1. **用户回答 6.1 ~ 6.7 的决策点**
2. 根据答案调整 PRD（如果有必要）
3. 确认数据结构和现有代码对齐
4. 开始实现 `EnemyDeckRecorder.cs`
5. 如有需要，同步实现/补齐 `DeckSO`、`CardData`、`CardDatabase`
6. 产出使用说明文档

---

*文档生成时间：2026-05-27*  
*状态：草稿 — 待用户确认决策点后定稿*
