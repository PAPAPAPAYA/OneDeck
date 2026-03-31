# 中文字体配置指南

## 已引入字体

已将 **思源黑体 Source Han Sans CN** 放入项目：

```
Assets/TextMesh Pro/Fonts/SourceHanSansCN/
├── SourceHanSansCN-Regular.otf   (正文、卡牌描述)
└── SourceHanSansCN-Bold.otf      (标题、强调)
```

这是 Adobe 开源的简体中文子集版，覆盖 GB2312/GBK 常用汉字，免费商用。

---

## Unity 中配置步骤

### 1. 生成 TextMesh Pro 字体资源

1. 在 Unity Project 窗口中，选中 `SourceHanSansCN-Regular.otf`
2. 打开菜单：`Window -> TextMeshPro -> Font Asset Creator`
3. 在 Font Asset Creator 窗口中设置：

| 设置项 | 推荐值 | 说明 |
|--------|--------|------|
| Source Font File | SourceHanSansCN-Regular | 已自动选中 |
| Sampling Point Size | 72 | 字号越大越清晰，但贴图越大 |
| Padding | 5 | SDF 边缘缓冲 |
| Packing Method | Fast | 生成速度快 |
| Atlas Resolution | **4096 x 4096** | 必须，否则装不下中文字符 |
| Character Set | **Chinese Simplified** | 包含完整简体中文字符集 |
| Render Mode | SDFAA | 抗锯齿 SDF |

4. 点击 `Generate Font Atlas`，等待生成（约 30~120 秒）
5. 生成成功后，点击 `Save` 按钮，保存到：
   ```
   Assets/TextMesh Pro/Resources/Fonts & Materials/SourceHanSansCN-Regular SDF.asset
   ```
6. 对 `SourceHanSansCN-Bold.otf` 重复上述步骤，保存为：
   ```
   SourceHanSansCN-Bold SDF.asset
   ```

> **体积提示**：完整 `Chinese Simplified` 生成的 SDF Asset 约 15~30MB（含 4096 贴图）。如果包体敏感，见下文「子集化优化」。

---

### 2. 配置 Fallback 字体链（推荐）

这样英文/数字保持原来的 LiberationSans，中文自动 fallback 到思源黑体。

1. 在 Project 窗口找到：
   ```
   Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset
   ```
2. 选中它，在 Inspector 中展开 `Fallback Font Assets` 列表
3. 点击 `+` 添加一个元素
4. 将 `SourceHanSansCN-Regular SDF` 拖入该槽位
5. **保存场景/项目**

原理：TMP 遇到 LiberationSans 不支持的字符（如中文）时，会自动查找 Fallback 字体渲染。

---

### 3. 应用到场景中的文本组件

#### UI 文本（TextMeshProUGUI）
- 保持现有 UI 文本的字体为 `LiberationSans SDF` 即可（Fallback 会自动处理中文）
- 如果需要专门用黑体显示某段中文，可直接指定 `SourceHanSansCN-Regular SDF`

#### 3D 卡牌文本（TextMeshPro）
- 打开卡牌预制体（如 `Assets/Prefabs/Cards/...` 下的预制体）
- 找到 `cardNamePrint`、`cardDescPrint`、`cardPricePrint`、`cardCostPrint` 等 TMP 组件
- 将它们的 Font Asset 改为 `LiberationSans SDF`（利用 Fallback）
- 或直接改为 `SourceHanSansCN-Regular SDF`

---

### 4. 验证中文显示

1. 在任意 TMP 文本组件中输入一些中文，例如：
   ```
   生命值: 10
   护盾: 5
   攻击!
   ```
2. 运行场景，确认没有方块（□）或乱码
3. 检查中西文混排效果是否协调

---

## 子集化优化（可选）

如果完整中文字体贴图导致包体过大或内存占用高，可以只生成项目实际用到的汉字。

### 方法：使用 Custom Characters

1. 收集项目中所有会用到的唯一汉字（卡牌描述、UI 文案等）
2. 将这些字整理成一个纯文本字符串，例如保存到：
   ```
   Assets/TextMesh Pro/Fonts/SourceHanSansCN/custom_chars.txt
   ```
3. 在 Font Asset Creator 中：
   - Character Set 选择 `Custom Characters`
   - 将 `custom_chars.txt` 的内容粘贴到 `Custom Characters` 输入框
4. 生成并保存

这样生成的 SDF Asset 通常只有 **2~5MB**。

### 辅助脚本

项目根目录提供了 `extract_chinese_chars.py`，可以扫描 `Assets/` 下所有文本资源（如 `.cs`、`.asset`、`.prefab`、`.json` 等），提取其中出现过的所有唯一中文字符，自动输出为 `custom_chars.txt`。

用法：
```bash
python extract_chinese_chars.py
```

输出文件位置：
```
Assets/TextMesh Pro/Fonts/SourceHanSansCN/custom_chars.txt
```

---

## 常见问题

**Q: 为什么中文显示为方块 □？**
A: 说明 TMP 字体资源中没有包含该汉字。检查 Font Asset Creator 的 Character Set 是否选对了，或 Fallback 是否配置正确。

**Q: 生成时提示 Atlas 空间不足？**
A: 必须将 Atlas Resolution 设为 4096x4096。如果仍然不足，说明 Sampling Point Size 设得太大，可以适当调小（如 64 或 48）。

**Q: 卡牌上的描述还是英文，需要改代码吗？**
A: 不需要改代码。`cardDesc` 是 `CardScript` 上的公开字段，直接在 Unity Inspector 里把卡牌预制体的 `Card Desc` 改成中文即可。
