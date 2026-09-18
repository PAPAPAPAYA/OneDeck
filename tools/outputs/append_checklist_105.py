# -*- coding: utf-8 -*-
line = (
    "| 105 | Visual fix (2026-09-18): concurrent pop-up holds landed on the SAME peak — "
    "PopUpCard anchored the peak once at pop time from the card's deck-slot index, and "
    "mid-cascade buries reorder physicalCardsInDeck (layout sweep AND animation-advance list "
    "mutations that run no sweep), so a later popper resolved the same index while an earlier "
    "card was still held there; log-proven with RELIC_CHAIN_BURIAL pop 6.79s + DEATHBED_GRANT "
    "pop 9.02s both computing index=1 count=7, peaks (3.99, 0.61, -0.50) vs (3.98, 0.60, -0.50) "
    "— one card stacked on the other for the whole hold. Fix = peak live-follow: while held "
    "(popUpFollowsDeckSlot set by PopUpCard flight completion, cleared by SlotInCard / re-pop), "
    "each frame re-derives the peak from the card's CURRENT index via "
    "CombatUXManager.TryGetPopUpPeakForCard and glides there; indices unique per card so two "
    "held pop-ups can never share a peak. Paused while the deck is peel-focused; holds last "
    "peak when the card leaves the deck list | `CardPhysObjScript` (`popUpFollowsDeckSlot` + "
    "`popUpFollowLerpSpeed` fields, `UpdatePopUpFollow` per-frame re-anchor + VISUAL-FIX"
    "(2026-09-18) block), `CombatUXManager` (`TryGetPopUpPeakForCard` extraction shared by "
    "PopUpCard flight + follow; follow flag set/clear in PopUpCard/SlotInCard) | 2026-09-18 | ⚠️ | "
    "**Step:** In Combat run the 913 trio (RELIC_CHAIN_BURIAL + DEATHBED_GRANT + "
    "SOLDIER_SKELETON_4.0) so multiple passives pop in one cascade; watch each held pop-up "
    "while buries shift the deck.<br>**Check:** Every held pop-up rides its own slot — the "
    "later popper never lands on an earlier one (no card hidden behind another); held cards "
    "glide smoothly as buries reorder the deck; slot-in returns each card to its live slot; "
    "row 104 driver-popup feedback intact; hover pop-up of a deck card still tracks its slot; "
    "reveal-zone pops unchanged. |"
)
with open("docs/RegressionChecklist.md", "ab") as f:
    f.write(("\r\n" + line + "\r\n").encode("utf-8"))
print("appended")
