# -*- coding: utf-8 -*-
line = (
    "| 104 | Visual/feedback gap (2026-09-17): proxy-strike driver cards (DEATHBED_GRANT 垂死反扑 / GRAVE_PUPPETEER) "
    "triggered with zero self feedback — the Attack request is captured on the ATTACKED creature's segment recorder "
    "(per-segment attack events 2026-09-05), so the driver's own invocation recorder stayed reqs=0 and the playback "
    "off-reveal source-card popup (keyed on animationRequests.Count > 0) never fired; fix captures a self PopUp request "
    "for the driver card into the still-current invocation recorder at the top of EffectScript.PerformAttackAs "
    "(playback sourceNeedsPopup path pops + emphasizes the driver, holds it at peak while the victim's attack segment "
    "plays, slots it back in; the captured request itself is dedup-skipped at playback; revealed-card drivers excluded) "
    "| `EffectScript` (`PerformAttackAs` + new `CaptureDriverPopUpForStrikeFeedback` + VISUAL-FIX(2026-09-17) block) "
    "| 2026-09-17 | ⚠️ | **Step:** In Combat with DEATHBED_GRANT unrevealed in deck, have a friendly creature buried "
    "so the strike fires — GRANT pops up + emphasizes while the buried creature attacks, then slots back in; same "
    "feedback for GRAVE_PUPPETEER graveyard strike; driver already popped by an earlier recorder → no double-pop; a "
    "driver that IS the revealed card must not pop.<br>**Check:** Strike damage/events/per-card stats unchanged "
    "(attributed to the victim, row 84 unregressed); guard-persistence behaviour unregressed (row 103 lineage); no "
    "ghost popup when the driver is destroyed mid-cascade. |"
)
with open("docs/RegressionChecklist.md", "ab") as f:
    f.write(("\r\n" + line + "\r\n").encode("utf-8"))
print("appended")
