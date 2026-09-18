# plan-popup-peak-live-follow-2026-09-18

弹起峰值 live-follow:修复并发 popup 持有卡重叠在同一峰值的问题。

## 现象

913 三卡组合(连坐 RELIC_CHAIN_BURIAL + 垂死反扑 DEATHBED_GRANT + 骸骨哨兵 SOLDIER_SKELETON_4.0)同一场级联内多张被动卡先后弹起持有,垂死反扑与连坐的弹起卡完全重叠(一张藏在另一张正后方)。

## 证据(Editor.log 23:19 场)

| 时刻 | 事件 | 布局计算 | 弹起峰值 |
|---|---|---|---|
| 6.79s | 连坐弹起 | CalculatePositionAtIndex index=1 count=7 -> (0.00, 0.59, -0.50) | newTarget=(3.99, 0.61, -0.50) |
| 9.02s | 垂死反扑弹起 | CalculatePositionAtIndex index=1 count=7 -> (0.00, 0.59, -0.50) | newTarget=(3.98, 0.60, -0.50) |

两次弹起相隔 2.2s,算出同一槽位、同一峰值;连坐自 6.79s 起持续持有该峰值(收回等名下 recorder 播完),垂死反扑精确叠上。

## 根因

PopUpCard 在弹起瞬间一次性锚定峰值(IndexOf 当时值 -> GetFinalDeckPositionForCard + 三段偏移),持有期间不重锚。而级联中的埋牌会重排 physicalCardsInDeck,重排有两条路径:

1. 布局清扫 UpdateAllPhysicalCardTargets(埋葬效果路径,带全量 SetTargetPosition);
2. 动画推进路径(ApplyAnimationResult 类列表直改,**不走清扫**——两次弹起之间该窗口内清扫 0 次)。

重排后垂死反扑的 IndexOf 也算出 1(连坐弹出时占的槽位) -> 峰值重合。机制早已存在(任何"持有期间列表重排 + 另一张卡从同槽位弹起"都会撞),垂死反扑加入弹起行列(行 104)且固定在连坐之后触发,把偶发撞变成该组合每轮必撞。

## 方案取舍

- **让位(并发持有者错开偏移)**:不需要。用户拍板 2026-09-18。
- **只在清扫里补重锚**:不充分。列表重排存在无清扫路径(见根因 2),洞关不死。
- **采纳:峰值 live-follow**——持有期间每帧按当前 IndexOf 重锚。同一时刻 IndexOf 每卡唯一 -> 峰值唯一,重叠类被结构性消灭,不依赖任何特定变更路径的通知。

## 实施

- `CombatUXManager.TryGetPopUpPeakForCard`(新):活槽位 -> 布局+jitter+hover spread + popUpYOffset/XOffset/ZBoost;卡不在 physicalCardsInDeck 时返回 false。PopUpCard 初始飞行与跟随循环共用此公式。
- 标志生命周期:`popUpFollowsDeckSlot` 由 PopUpCard 飞行完成(DOTween OnComplete)置位,SlotInCard 入口与 re-pop 清除。
- `CardPhysObjScript.UpdatePopUpFollow`(Update 每帧):isPoppedUp + 特殊动画钉位 + 标志位时,重算峰值并指数趋近(popUpFollowLerpSpeed=12/s)。护栏:peel-focus(IsDeckFocused)期间暂停;卡移出牌库列表时保持最后峰值。
- 视觉副作用(预期):持有中的弹起卡会随埋牌重排平滑漂向自己的新槽位——它确实正被推着走,属正确反馈。hover 弹起共用 PopUpCard 路径,同样跟随;若圈定只有效果触发路径跟随,加一行 gate。

## 验证

docs/RegressionChecklist.md 行 105。口径:913 三卡组多被动同场级联,后弹者不压先弹者;持有卡随重排平滑漂移;收回各回各家;行 104 驱动卡反馈与 hover 弹起不回归。

## 状态

2026-09-18 实施,编译 0 错,未提交。Play 验证:重叠已消除,但发现移动中 jitter(见原「已知问题」节)。jitter 修复(见下)已于 2026-09-18 当日追加实施;[PopUpFollowDiag] 探针已随修复一并拆除。

## 已知问题:移动中 jitter(2026-09-18 复现 → 当日已修复)

live-follow 上线后验证发现:卡片在移动动画过程中抖动。

### 证据([PopUpFollowDiag] 探针,Editor.log 16:56 场,探针现已拆除)

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
深层失配:移动完成回调清两个标志但不认识 `popUpFollowsDeckSlot`,"新特殊动画接管卡片"
没有统一的跟随释放点。

### 修复(2026-09-18 当日实施)

`CardPhysObjScript.BeginSpecialAnimation` 加入:
`if (drivesPosition) popUpFollowsDeckSlot = false;`
语义:任何接管卡片位置的新特殊动画自动释放过期跟随声明。覆盖 MoveCardWithAnimation /
SlotInCard / 揭晓飞行等全部 Begin(true) 调用点(788/1151/1886/3024/3060/3080/3161/3180/
3239/3301);emphasize 是 Begin(false)(只缩放不占位)不会误杀跟随——否则弹起落地后的
emphasize 会立刻掐死跟随。附带堵死移动完成回调的标志失配。探针已拆除。
复验口径:913 三卡组,持有中的弹起卡自身被移动时无逐帧拔河抖动,跟随平滑停止。
