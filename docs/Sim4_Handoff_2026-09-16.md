# Sim4 进度快照与交接(2026-09-16)

> 真值源:机制裁定 = `docs/4.0_Glossary.md`;近似与逐批执行记录 = `docs/Sim4_Approximation_Ledger.md`(§二·五~二·九);
> 步门协议记录 = `plans/plan-python-sim-4.0-upgrade-2026-08-31.md`。本文只做状态快照与交接,不重复真值源内容。

## 一、状态一句话

**全池 112 张(normal+uncommon+rare)已进模拟:对数全绿、89 个 handler 六段自测绿、血量档 HP25/HP50 双档报告已重跑。归因三步(逐场数据导出 → 两卡组合枚举 → 特征回归)已确认方向、待开工。Step 7(种群共演化)后议。**

## 二、交付物与入口

| 项 | 位置 |
|---|---|
| 模拟器(单文件 Python) | `tools/scripts/one_deck_damage_sim.py` |
| CLI | `--selftest-40`(六段自测)/ `--report-40 --sessions N`(四配置×8批次)/ `--dump-pool common40\|trial40` |
| 桌面入口 | `run_sim4.bat`(仓库根;桌面放**快捷方式**勿拷贝) |
| 卡表导出 | `tools/outputs/sim4/prefab_card_table_trial.json`(112 张) |
| 对数工具 | `tools/scripts/sim4_reconcile_pool.py` + 快照 `notion_full_snapshot_2026-09-16.json`(当前 112=112 全绿) |
| 报告 | `tools/outputs/sim4/report_{6v6, 6v6_hp50, 10v10_hp25, 10v10_hp50}.md` |

## 三、引擎机制口径速查(细节以台账为准)

- **回合 = AlwaysBottom**:全堆洗 + 起始卡回底(index 0);每张卡每轮恰好揭晓一次;**每卡每触发时机每回合一次门**(`round_triggered`,洗牌清)——这是自复活/再揭晓不发散的根
- **区域**:墓地 = index < 起始卡 = 循环等待区(下轮洗回);被动卡每次洗牌强制置于起始卡下方墓地侧,永不被揭晓、不可移动/复活/埋葬;**一切生成卡(信徒/诅咒/复制自身)一律进墓地侧**(CurseEffect 源码:读回 combinedDeckZone[0])
- **攻击账本三槽位**(镜像 Unity CardScript):`printed_atk + enhance_total(永久,带符号) + atk_mod_round(本回合,回合开始清)`;攻击段数双槽位(base/本回合);【被强化】谓词 = `enhance_total > 0`(严格正,弱化可打穿账本)
- **攻击事件每段触发**(2026-09-05 裁定);血契「攻击转化强化」也按每段
- **诅咒(JU_ON)**:非组内原生、每卡组至多 1 张、仅由「强化敌方诅咒」生成(无则生成入墓地侧 + 强化,有则原地强化,放逐后可再生成);base ATK=0
- **信徒(RIFT)**:纯复活器(「复活 1 友方,去除自身」=放逐);RELIC_RIFT_OVERRIDE 旗标改写其效果
- **计数器(本回合,回合开始清)**:放逐友方数(狂信徒,÷3)、埋葬生物数(血账)、埋葬友方数(什一抽杀,4−n)、复活友方数(百鬼夜行)
- **性能折叠两处**(台账已注记):噬咒萨满逐点强化扇出 → n>200 按比例分配+随机余数(**近似**);咒刃逐点强化敌方诅咒 → 同目标纯 +N 精确折叠为单次(**等价非近似**)
- **旗标被动机制**:`PASSIVE_PRESENCE_DEF_40` + `state.passive_flags`,setup 时扫描(被动不可移动故整场精确);已挂钩:提前发作/血契/伪经/白骨王座/积尸气

## 四、当前数据信号(设计侧值得看)

1. **收蛊人 10^18 量级信号**(HP50 诅咒轴批次):咒刃×噬咒萨满×蛊婆互馈环把敌方诅咒攻指数推高,吞蛊人「攻击力=敌方诅咒攻击力」一刀即天文伤害。归因/回归**必须截尾或稳健统计**;同时这本身可能就是"找异常"的第一命中项(咒刃+萨满+吞蛊人三卡组合)
2. 商店被动卡 win% 28-33%(uniform 批)——占卡位稀释的代价被量化
3. 厉鬼等永续成长型(遗言强化自身/渴血者攻次+1)在 HP50 长局滚雪球明显

## 五、未决问题(用户侧)

1. **6 张 xN 削减卡**(连体人/厉鬼/剜心祭/鞭笞者/蛊王/模仿犯):双侧 desc 已是普通「攻击」,prefab `extraAttackTimes` 残留 1。sim 用 `EXTRA_ATTACK_TIMES_OVERRIDE` 显式归零——**Unity 侧字段清零后请删除该覆盖表**
2. **百臂巨人**:desc 掉了「x4」但字段 extra=3 + 双侧 tag=多次攻击,sim 保留字段(x4)——desc 与字段修一边
3. 诅咒互馈环(咒刃+萨满+吞蛊人)是否设计意图,或需成长上限
4. QUAD_STRIKER prefab 系直改 YAML 补 MultiAttack tag(MCP 宕机时的授权降级)——Unity 聚焦后 import 验证一次即可

## 六、下一步(归因链,已确认)

1. **逐场数据导出**:`run_batch_40` 加 JSONL 落盘 → `tools/outputs/sim4/matches/`;每行一场对局:双方卡组 cid 列表、胜负、rounds、每卡 dmg/reveals/awakens/burials/curse_enh
2. **两卡组合枚举**:全池 C(112,2)+同卡对 ≈ 6.3k 组合 × 若干种子;抓超模/死锁;工具校准 = 验证它能抓到已知的咒刃×萨满环
3. **特征回归归因**,三防护:重尾截尾/稳健统计;批次来源作协变量(轴条件化故意制造共线);台账近似项作为已知偏差声明写入报告

## 七、会话交接坑

- sim 是纯 Python,改 `.py` 不涉及 Unity;改 prefab 走 Unity MCP,宕机降级 = 直改 YAML(注意 grep 锚点行尾可能带空格)
- **Unity MCP 上次会话末尾宕机(fetch failed)**——恢复后先对 QUAD_STRIKER 做 import 验证,再继续 Unity 侧工作
- reconcile 快照是抓取时刻的静态文件;DB 改动后需重拉;**Notion SQL 有分页截断**——全量查询要用 `BETWEEN` 分段或按 rarity 查,否则静默丢行
- 生成卡落点 = 墓地侧(源码实锤),勿再猜活区
- 后台任务被杀会残留孤儿 python 进程抢 CPU(`tasklist //fi "IMAGENAME eq python.exe"` 查杀)
- `run_sim4.bat` 单行管道输入正常;git-bash 多行 LF 管道喂 bat 有伪故障,真实键盘不受影响
- 附注:2026-09-16 会话曾有一个后台计时探针以 exit 1073807364 收场——它是被 profile 分析取代的旧探针,被杀属预期,无影响

## 八、血量口径备忘

四配置 = 6v6/10v10 × HP25/HP50(2026-09-16 用户拍板,无上限档已废除并删除旧报告);沉魇(原静态 +4)已购买化(「购买: 生命值上限+8, 放逐自身」)不进战斗池,HP 上限只在 HP 档内有意义。

## 九、2026-10-06 增补(漂移同步收官,详见台账二·十)

- **§五.1(6 张 xN)已拍板路线 B**:prefab/DB 保持现状(extra=1 在 10-06 口径下合规),sim 删 `EXTRA_ATTACK_TIMES_OVERRIDE`,6 张按 ×2 段模拟
- **§五.2(百臂巨人)已闭环**:10-06 拍板后「desc 裸写 + extra=3 + 多次攻击 tag」即合规终态,无需二选一;sim 保留 x4
- **§五.4(QUAD YAML import 验证)已消化**:09-17 提交后 Unity 持续重度使用,10-06 装载器解析零警告,无需单独验证
- **§五.3(诅咒互馈环上限)仍开放**:卡牌设计决定,等归因产出再拍板;sim 侧已有 Power≤20 折叠 + 归因重尾截尾防护,不阻塞
- **新完成**:每回合复活门禁精确镜像(14 卡,N 按 prefab 实测;空坟不耗充能、按批计费)、RIFT_REVIVER 门镜像 cost(门耗尽或无信徒→整容器不触发)、添龛出池(池子维持 112)、cid 改名同步(*_4.0)、快照 notion_full_snapshot_2026-10-06.json 重拉 + reconcile(reconcile 现已全绿 exit=0)、六段自测全绿、四配置报告 200 sessions 重跑
- **AVENGER_4.0 多次攻击 tag 已补**(10-06,Unity MCP prefabContents 管线);tag 全库扫描:4.0 全部一致,仅 4 张 3.0 旧池卡缺(GRAVE_PUNCH/CURSED_CORPSE/SPIKE_SKELETON/POWER_SIPHONER,3.0 池不在 4.0 tag 规范范围,未动待用户定)
- **§六.1 逐场导出已落地**:`--export-matches-40 --sessions N` → `tools/outputs/sim4/matches/matches_<config>.jsonl`,4 配置×(uniform+7 轴)×N 场,与报告同种子可复现;每行=双方 cid 列表/胜负/rounds/hp_final/healthy+divergence/每卡 dmg-reveals-awakens-burials-curse_enh-believers;200 sessions 已跑(4×1600 行),聚合与报告逐位对账一致
- **§六 归因三步全部收官(10-06)**:Step 1 逐场导出(`--export-matches-40`,对账一致)→ Step 2 两卡枚举(`--pairs-40`,36630 场,**非终止核对成立**,蛊噬自镜像 1.24e+51 = 超模 No.1)→ Step 3 特征回归(`--regress-40`,伤害 R²=0.465 + 胜率 R²=0.269 + 超模/欠模榜;对手强化轴 -7.8pp 最强胜率威胁;模仿犯中位 24 全场最高、连体人同位显著偏弱;三防护全落地)。产出目录:matches/ pairs/ pairs_summary.md regression/
- **待用户拍板的设计输入**(按优先级):①蛊噬「强化2+每3攻再强化1」自乘环上限(2 卡即爆,Top10 全含);②血契「攻击转化强化」放大器地位(厉鬼+血契/遗赠+血契双 blowout+单刀 104);③连体人 vs 模仿犯同位差;④Rare 池胜率构成为负(rarity_rare -3.2pp,池级信号);⑤诅咒互馈环上限(§五.3 原问题,蛊噬数据已就位)
- **HTML 摘要报告**:`tools/outputs/sim4/attribution_report.html`(归因三步简明版:KPI/系数/超模欠模榜/组合信号/P1-P5 拍板清单;CRLF+tab,浏览器直开)
