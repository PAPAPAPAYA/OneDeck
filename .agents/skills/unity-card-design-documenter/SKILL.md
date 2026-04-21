---
name: unity-card-design-documenter
description: Batch inspect Unity card prefabs under a folder and generate a structured card design Markdown document. Use when the user asks to catalog, document, or export card designs from a prefab directory.
---

# Unity Card Design Documenter

This skill provides a reusable workflow for scanning a folder of card prefabs, extracting their serialized configuration (CardScript, CostNEffectContainers, Effect components), and producing a human-readable card design document.

## Prerequisites

- The project uses the `unity-read-prefab-serialized` skill (already available under `.agents/skills/unity-read-prefab-serialized/`).
- Card prefabs are located under `Assets/Prefabs/Cards/` (or any user-specified subtree).
- Unity Editor is connected via MCP and `execute_code` works.

## Workflow

### Step 1: Discover Prefabs

Use Shell to list all `.prefab` files under the target directory recursively.

Example (PowerShell):
```powershell
Get-ChildItem -Path "Assets/Prefabs/Cards/3.0 no cost (current)" -Recurse -Filter "*.prefab" | Select-Object FullName | Sort-Object FullName
```

> **Tip:** The `Get-ChildItem` output may be truncated with `... and N more`. Use `Measure-Object` to confirm the exact count:
> ```powershell
> Get-ChildItem -Path "Assets/Prefabs/Cards/3.0 no cost (current)" -Recurse -Filter "*.prefab" | Measure-Object
> ```

Record the paths; you will feed them into the batch inspection script.

### Step 2: Batch Inspect Prefabs

Use `execute_code` (with `compiler: "codedom"`) to load every prefab, read `CardScript` and child `CostNEffectContainer`s, and write the data to `docs/CardDesign_GenerationLog.txt`.

> **Constraint reminder:** `codedom` does not support `$""` interpolation, `?.` null-conditional, file-level `using`, or `return;` (void). Use fully-qualified names, explicit null checks, and always `return <value>;`.

> **Batch size:** Because output goes to a file instead of the console, you can inspect all prefabs in a single run. No need to split into multiple batches.

**What to extract for each prefab:**

| Component | Fields |
|-----------|--------|
| `CardScript` (root) | `cardTypeID`, `displayName`, `cardDesc`, `isMinion`, `buryCost`, `delayCost`, `exposeCost`, `minionCostCount`, `minionCostCardTypeID`, `minionCostOwner`, `myStatusEffects`, `myTags` |
| `CostNEffectContainer` (children) | `name`, **trigger event** (from `GameEventListener`), `checkCostEvent` bindings, `preEffectEvent` bindings, `effectEvent` bindings |
| `HPAlterEffect` | `baseDmg.value`, `extraDmg`, `isStatusEffectDamage`, `statusEffectToCheck` |
| `ShieldAlterEffect` | (presence only; methods take arguments at call time) |
| `BuryEffect` | `tagToCheck` |
| `StageEffect` | `tagToCheck`, `targetFriendly`, `statusEffectToCheck` |
| `ExileEffect` | `tagToCheck` |
| `CurseEffect` | `cardTypeID.value`, `cardPrefab.name`, `powerCoefficient` |
| `AddTempCard` | `cardCount`, `curseCardTypeID.value` |
| `TransferStatusEffectEffect` | `isFromFriendly`, `statusEffectToTransfer`, `curseCardTypeID.value` |
| `CardManipulationEffect` | `tagToCheck` |
| `ChangeCardTarget` | (presence only) |
| `ChangeHpAlterAmountEffect` | (presence only) |
| `HPMaxAlterEffect` | (presence only) |
| `StatusEffectGiverEffect` | `statusEffectToGive`, `statusEffectToCount`, `target`, `includeSelf`, `lastXCardsCount`, `xFriendlyCount`, `statusEffectLayerCount`, `yFriendlyLayerCount` |
| `StatusEffectAmplifierEffect` | 继承自上方，额外有 `statusEffectMultiplier` |
| `PowerReactionEffect` | `powerAmount`, `excludeSelf` |
| `ConsumeStatusEffect` | `statusEffectToConsume` |

**Serialization access pattern:**
- Public fields on components: direct access (`hpAlter.baseDmg.value`).
- UnityEvent bindings: `SerializedObject` + `FindProperty("effectEvent")` -> `m_PersistentCalls.m_Calls` -> iterate and read `m_TargetAssemblyTypeName`, `m_MethodName`, `m_Arguments.m_IntArgument`, `m_Arguments.m_StringArgument`.
- Arrays/Lists: `SerializedObject` + `FindProperty("myStatusEffects")` -> `arraySize` + `GetArrayElementAtIndex(i)` -> `enumDisplayNames[enumValueIndex]`.

A ready-to-use batch inspection template is provided in:
[references/batch-inspect-template.cs](references/batch-inspect-template.cs)

**Log format:**
Each prefab is written as one line prefixed with `CARD|`:
```
CARD|<prefab_name>|<asset_path>|<field1>=<val>;<field2>=<val>;[CONTAINER_0 ...][EFFECT_...][HPALTER_...]
```

> **Escaping `cardDesc`:** `cardDesc` often contains newlines (`\n`). The template escapes them to `\\n` so every card remains a single line in the log file.

### Step 3: Read Log File

After `execute_code` finishes, read the generated log file directly:

```powershell
Get-Content -Path "docs/CardDesign_GenerationLog.txt" -Raw
```

Or use `ReadFile` to read it line by line.

> **Tip:** Because the log is already persisted to disk, you do not need `read_console` and do not need to worry about console buffer limits or `\n` escaping issues.

### Step 4: Parse and Generate Markdown

Transform the raw log strings into a structured Markdown document.

> **Tip:** Write the parser as a standalone Python script (e.g., `docs/generate_card_design.py`) and save it to disk. Do **not** delete it after generation; it is invaluable for re-running, debugging, and iterating on the output format without re-extracting Unity data.

**Suggested document structure:**

1. **Overview** - total count, category breakdown, glossary of key terms.
2. **Category sections** - one section per sub-folder.
3. **Per-card entry** (compact format, one table per card to minimize vertical space):

   | Field | Value |
   |-------|-------|
   | Name | `displayName` (`cardTypeID`) |
   | Flags | Minion=`isMinion` / Tags=`myTags` / Status=`myStatusEffects` |
   | Costs | Bury=`buryCost` / Delay=`delayCost` / Expose=`exposeCost` / Minion=`minionCostCount`(`minionCostCardTypeID`/`minionCostOwner`) |
   | Desc | `cardDesc` |
   | Containers | For each container: `name` + **Trigger** + CheckCost/PreEffect/Effect calls |
   | Key Fields | `baseDmg`, `extraDmg`, `powerCoefficient`, `statusEffectMultiplier`, etc. |

**Parsing pitfalls:**

| Issue | Cause | Fix |
|-------|-------|-----|
| `cardDesc` truncated | `cardDesc` itself contains `;`, breaking naive `key=value;` splitting | Use a dedicated regex such as `re.search(r'cardDesc=(.*?);isMinion=', kv_part)` to extract the full description |
| Missing newlines in description | `\n` in the log file is escaped as `\\n` | Replace `\\n` -> `\n` -> actual newline (or `<br>`) in the parser |
| Container names truncated | Container `name` may contain literal `]` (e.g., `hostile [curse] + power`), breaking `[^\]]+` regex | Use a lazy-match with lookahead: `\[CONTAINER_(\d+) name=(.+?)\](?=\[|$)` |
| Noisy assembly names in method calls | UnityEvent's `m_TargetAssemblyTypeName` includes `, Assembly-CSharp` (e.g., `HPAlterEffect, Assembly-CSharp->DecreaseTheirHp`) | Strip `, Assembly-CSharp` during parsing to keep only the class name: `HPAlterEffect->DecreaseTheirHp` |
| Chinese characters garbled in PowerShell stdout | Windows default code page (e.g., GBK) conflicts with UTF-8 | Avoid printing Chinese to stdout for inspection; write results directly to the Markdown file and read that file instead |

A ready-to-use Markdown template is provided in:
[references/markdown-template.md](references/markdown-template.md)

**Output path convention:**
Write the final document to `docs/<FolderName>_CardDesign.md` (or a user-specified path).

> **Do not keep old versions.** When regenerating the document, overwrite the existing file directly. Do not create versioned copies (e.g., `v5`, `v6`) or maintain a separate archive folder. If obsolete versioned files or empty archive folders exist from prior runs, delete them.

## Reference

- **Batch inspection C# template:** [references/batch-inspect-template.cs](references/batch-inspect-template.cs)
- **Markdown output template:** [references/markdown-template.md](references/markdown-template.md)
- **Prefab reader skill:** `.agents/skills/unity-read-prefab-serialized/SKILL.md`
- **C# templates reference:** `.agents/skills/unity-read-prefab-serialized/references/csharp-templates.md`
