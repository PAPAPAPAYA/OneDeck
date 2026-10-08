# Sim4 Step2 两卡组合枚举汇总(2026-10-06)

- 覆盖:C(112,2)+同卡 = 6328 组合 × 3 种子 × 2 配置(6v6_hp25 / 10v10_hp50),共 36630 场
- **非终止核对**(门禁后预期无无限组合):diverged=2741 场,round_cap=14454 场,reveal_pressure=2759 场
- blowout:dmg_blowout=117,curse_blowout=2297,one_hit_blowout=2;no_winner=32571,long_game=15018

## 标记明细(每类至多 5 例)

- **curse_blowout** ×2297: AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+DETERIORATION_4.0 [6v6_hp25]; AWAKENED_REAPER+DETERIORATION_4.0 [6v6_hp25]
- **diverged** ×2741: AWAKENED_REAPER+MASS_SACRIFICE [6v6_hp25]; AWAKENED_REAPER+MASS_SACRIFICE [6v6_hp25]; AWAKENED_REAPER+MASS_SACRIFICE [6v6_hp25]; BATTLE_HORN+MASS_SACRIFICE [6v6_hp25]; BATTLE_HORN+MASS_SACRIFICE [6v6_hp25]
- **dmg_blowout** ×117: AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+RELIC_BLOOD_PACT [6v6_hp25]
- **long_game** ×15018: AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+RELIC_BLOOD_PACT [6v6_hp25]
- **no_winner** ×32571: AVENGER_4.0+AVENGER_4.0 [6v6_hp25]; AVENGER_4.0+AVENGER_4.0 [6v6_hp25]; AVENGER_4.0+AVENGER_4.0 [6v6_hp25]; AVENGER_4.0+AWAKENED_REAPER [6v6_hp25]; AVENGER_4.0+AWAKENED_REAPER [6v6_hp25]
- **one_hit_blowout** ×2: LAST_GIFT+RELIC_BLOOD_PACT [6v6_hp25]; LAST_GIFT+RELIC_BLOOD_PACT [6v6_hp25]
- **reveal_pressure** ×2759: AWAKENED_REAPER+MASS_SACRIFICE [6v6_hp25]; AWAKENED_REAPER+MASS_SACRIFICE [6v6_hp25]; AWAKENED_REAPER+MASS_SACRIFICE [6v6_hp25]; BATTLE_HORN+MASS_SACRIFICE [6v6_hp25]; BATTLE_HORN+MASS_SACRIFICE [6v6_hp25]
- **round_cap** ×14454: AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AVENGER_4.0+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+RELIC_BLOOD_PACT [6v6_hp25]; AWAKENED_REAPER+RELIC_BLOOD_PACT [6v6_hp25]

## blowout Top 15(dmg+curse_enh 总量排序)

| 量级 | 组合 | 配置 | flags |
|---|---|---|---|
| 1.24e+51 | DETERIORATION_4.0 + DETERIORATION_4.0 | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 1.24e+51 | DETERIORATION_4.0 + DETERIORATION_4.0 | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 1.24e+51 | DETERIORATION_4.0 + DETERIORATION_4.0 | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 1.27e+31 | DETERIORATION_4.0 + DETERIORATION_4.0 | 6v6_hp25 | no_winner,round_cap,curse_blowout,long_game |
| 1.27e+31 | DETERIORATION_4.0 + DETERIORATION_4.0 | 6v6_hp25 | no_winner,round_cap,curse_blowout,long_game |
| 1.27e+31 | DETERIORATION_4.0 + DETERIORATION_4.0 | 6v6_hp25 | no_winner,round_cap,curse_blowout,long_game |
| 5.21e+26 | DETERIORATION_4.0 + WEAPON_SPIRIT_4.0 | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 5.21e+26 | DETERIORATION_4.0 + WEAPON_SPIRIT_4.0 | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 5.21e+26 | DETERIORATION_4.0 + WEAPON_SPIRIT_4.0 | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 3.55e+26 | DETERIORATION_4.0 + DOOM_HERALD | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 3.55e+26 | DETERIORATION_4.0 + DOOM_HERALD | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 3.4e+26 | DETERIORATION_4.0 + DOOM_HERALD | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 2.83e+26 | DETERIORATION_4.0 + HEXER | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 2.67e+26 | DETERIORATION_4.0 + HEXER | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
| 2.39e+26 | DETERIORATION_4.0 + HEXER | 10v10_hp50 | no_winner,round_cap,curse_blowout,long_game |
