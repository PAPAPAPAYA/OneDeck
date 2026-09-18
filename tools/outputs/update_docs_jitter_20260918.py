# -*- coding: utf-8 -*-
import io

# 1. plan doc: amend status line + append jitter section
plan_path = "plans/plan-popup-peak-live-follow-2026-09-18.md"
with io.open(plan_path, "r", encoding="utf-8") as f:
    text = f.read()

old_status = "2026-09-18 实施,编译 0 错,未提交,Play 待验。"
new_status = ("2026-09-18 实施,编译 0 错,未提交。Play 验证:重叠已消除,但发现移动中 jitter"
              "(见下节「已知问题」);jitter 修复方案已设计、用户暂缓不修改;[PopUpFollowDiag] 探针在位。")
assert old_status in text, "status line not found"
text = text.replace(old_status, new_status)

jitter_section = """
## 已知问题:移动中 jitter(2026-09-18 复现,修复方案已设计、用户暂缓)

live-follow 上线后验证发现:卡片在移动动画过程中抖动。

### 证据([PopUpFollowDiag] 探针,Editor.log 16:56 场)

- `TWEEN-START while follow` × 12,全部命中 SOLDIER_SKELETON_4.0,目标为牌堆底槽位
  (如 to=(0.05, -0.85, -3.00));堆栈点名调用方 `CombatUXManager.MoveCardWithAnimation`
  (b__0 -> SetTargetPosition:890 -> StartPositionTween:723)。
- `FIGHT` = 0:探针盯错了把手——移动走自己的 moveSequence,不经过
  `IsPositionTweenPlaying` 看的 `_positionTween`,故采样期从未命中。
- `EXTERNAL SetTargetPosition` × 20、`FOLLOW` 10Hz 线 × 68。

### 机理

移动飞行期间,MoveCardWithAnimation 的 moveSequence 与 UpdatePopUpFollow 的逐帧直写
对同一张卡逐帧拔河 -> 移动中 jitter;移动完成回调
(`isPlayingSpecialAnimation = false; isPoppedUp = false;`)使跟随守卫失效,jitter 停止。
深层失配:移动完成回调清两个标志但不认识 `popUpFollowsDeckSlot`(写于行 104 之前),
"新特殊动画接管卡片"没有统一的跟随释放点。

### 修复方案(已设计,用户 2026-09-18 拍板暂不修改)

`CardPhysObjScript.BeginSpecialAnimation` 加一行:
`if (drivesPosition) popUpFollowsDeckSlot = false;`
语义:任何接管卡片位置的新特殊动画自动释放过期跟随声明。覆盖 MoveCardWithAnimation /
SlotInCard / 揭晓飞行等全部 Begin(true) 调用点(788/1151/1886/3024/3060/3080/3161/3180/
3239/3301);emphasize 是 Begin(false)(只缩放不占位)不会误杀跟随——否则弹起落地后的
emphasize 会立刻掐死跟随。附带堵死移动完成回调的标志失配。

### 探针清单(随 jitter 修复一并拆除)

- `[CardPhysObjScript][PopUpFollowDiag] FOLLOW`(10Hz/卡:pos/peak/dist/tweenPlaying/seqActive)
- `[CardPhysObjScript][PopUpFollowDiag] FIGHT` / `TWEEN-START while follow`(Warning)
- `[CardPhysObjScript][PopUpFollowDiag] EXTERNAL SetTargetPosition`
- 路由:TestManager.InferCategory 中 `[PopUpFollowDiag]` -> VisualSync(与 [RevealZDiag] 同行)。
"""
text = text.rstrip("\r\n") + "\r\n" + jitter_section.replace("\n", "\r\n")
with io.open(plan_path, "wb") as f:
    f.write(text.encode("utf-8"))
print("plan updated")

# 2. checklist row 105: append follow-up note before trailing " |"
cl_path = "docs/RegressionChecklist.md"
with io.open(cl_path, "r", encoding="utf-8", newline="") as f:
    content = f.read()
marker = "<br>**Check:** Strike damage/events/per-card stats unchanged"
idx = content.find("| 105 |")
assert idx >= 0, "row 105 not found"
line_end = content.find("\r\n", idx)
row = content[idx:line_end]
assert row.endswith(" |"), "row 105 unexpected tail: " + row[-40:]
addition = ("<br>**Follow-up (2026-09-18):** Play validation cleared the overlap but found "
            "move-flight jitter — deck-move tweens (MoveCardWithAnimation, bury-to-bottom "
            "to=(0.05,-0.85,-3.00)) fight the per-frame follow write on the same held card; "
            "root cause = no unified release point for popUpFollowsDeckSlot on special-animation "
            "takeover (mover completion clears isPlayingSpecialAnimation/isPoppedUp but not this "
            "flag). One-line fix designed (BeginSpecialAnimation drivesPosition release), NOT "
            "applied — user deferred 2026-09-18. [PopUpFollowDiag] probes (VisualSync) left in "
            "place; details: plans/plan-popup-peak-live-follow-2026-09-18.md jitter section.")
new_row = row[:-1] + addition + " |"
content = content[:idx] + new_row + content[line_end:]
with io.open(cl_path, "wb") as f:
    f.write(content.encode("utf-8"))
print("checklist updated")
