"""Step 2 ShopManager wiring: utility recompute, baseline growth, free reroll, board discount."""
import sys

P = 'Assets/Scripts/Managers/ShopManager.cs'
s = open(P, encoding='utf-8', newline='').read()
EOL = '\r\n' if '\r\n' in s else '\n'
T = '\t'
fails = []


def sub(anchor, replacement, label):
    global s
    if anchor not in s:
        fails.append(label + ': anchor not found')
        return
    s = s.replace(anchor, replacement, 1)
    print('OK ' + label)


def block(lines):
    return EOL.join(lines)


# ---- A) fields after sellMode ----
fields_anchor = T + 'public bool sellMode = false; // if it\'s not sell mode then its buy mode'
fields_new = block([
    T + 'public bool sellMode = false; // if it\'s not sell mode then its buy mode',
    '',
    T + '[Header("Utility Baseline Growth (plan v2)")]',
    T + '[Tooltip("Payday bonus added per session number.")]',
    T + 'public int incomePerSession = 2;',
    T + '[Tooltip("hpMax added per session number, applied on top of hpMaxOg at shop entry.")]',
    T + 'public int hpMaxPerSession = 2;',
    T + '[Tooltip("Deck size added per session number, applied on top of deckSizeOg at shop entry.")]',
    T + 'public int deckSizePerSession = 1;',
    T + '[Tooltip("Price of the first deck-slot purchase of a run; each prior purchase adds deckSlotPriceStep.")]',
    T + 'public int deckSlotBasePrice = 4;',
    T + '[Tooltip("Price increase per already-made deck-slot purchase this run.")]',
    T + 'public int deckSlotPriceStep = 2;',
    T + '[Tooltip("Run-persistent deck slot purchase counter (meter price + deckSize formula). Reset at run start.")]',
    T + 'public IntSO deckSlotPurchasesRef;',
    T + 'private UtilityShopBonus.Bonus _utilityBonus;',
    T + 'private int _rerollsThisVisit;',
    T + 'private int _freeRerollsUsedThisVisit;',
    T + 'private readonly Dictionary<CardScript, int> _boardDiscounts = new Dictionary<CardScript, int>();',
])
sub(fields_anchor, fields_new, 'A fields')

# ---- B) EnterShop payday ----
payday_anchor = block([
    T * 2 + '// payday',
    T * 2 + 'purse.value += payCheck.value;',
])
payday_new = block([
    T * 2 + '// payday + baseline growth (utility recompute first: deckSize, hpMax, then payday)',
    T * 2 + 'ResetVisitCounters();',
    T * 2 + 'RefreshUtilityBonus();',
    T * 2 + 'ApplyBaselineGrowth();',
    T * 2 + 'purse.value += UtilityShopBonus.ComputePayday(payCheck.value, GetSessionNum(), incomePerSession, _utilityBonus);',
])
sub(payday_anchor, payday_new, 'B EnterShop payday')

# ---- C) BuyFunc price + refresh after add ----
sub(T * 2 + 'int buyPrice = GetCardPrice(cardToBuyScript);',
    T * 2 + 'int buyPrice = GetEffectiveBuyPrice(cardToBuyScript);',
    'C1 buy price')
buyadd_anchor = block([
    T * 2 + '// Add the card to player deck regardless of whether it takes up space',
    T * 2 + 'playerDeckRef.deck.Add(cardToBuy);',
])
buyadd_new = block([
    T * 2 + '// Add the card to player deck regardless of whether it takes up space',
    T * 2 + 'playerDeckRef.deck.Add(cardToBuy);',
    T * 2 + 'RefreshUtilityBonus();',
    T * 2 + 'ApplyHpMaxFromDeck();',
])
sub(buyadd_anchor, buyadd_new, 'C2 buy refresh')

# ---- D) SellFunc refresh ----
sell_anchor = T * 2 + 'playerDeckRef.deck.Remove(cardToSell); // remove it from player deck'
sell_new = block([
    T * 2 + 'playerDeckRef.deck.Remove(cardToSell); // remove it from player deck',
    T * 2 + 'RefreshUtilityBonus();',
    T * 2 + 'ApplyHpMaxFromDeck();',
])
sub(sell_anchor, sell_new, 'D sell refresh')

# ---- E) Reroll rewrite + helper methods (Reroll is the last method; class brace follows) ----
reroll_anchor = block([
    T + 'public void Reroll()',
    T + '{',
    T * 2 + '// DIAG-LOG(2026-08-08): tracing why the shop Reroll button may appear dead',
    T * 2 + 'TestManager.Log("[ShopButton] Reroll() clicked. phase=" + (gamePhaseRef != null ? gamePhaseRef.currentGamePhase.ToString() : "null") + " purse=" + (purse != null ? purse.value : -1) + " price=" + (RerollPriceRef != null ? RerollPriceRef.value : -1));',
    T * 2 + 'if (RerollPriceRef == null || purse.value < RerollPriceRef.value)',
    T * 2 + '{',
    T * 2 + '\tTestManager.Log("[ShopButton] Reroll() early return: cost not met. purse=" + (purse != null ? purse.value : -1) + " price=" + (RerollPriceRef != null ? RerollPriceRef.value : -1));',
    T * 2 + '\treturn;',
    T * 2 + '}',
    '',
    T * 2 + '// First generate new shop item data',
    T * 2 + 'GenerateShopItems();',
    T * 2 + 'UpdateShopItemInfo();',
    T * 2 + '// record reroll',
    T * 2 + 'if (ShopStatsManager.Me != null)',
    T * 2 + '{',
    T * 2 + '\tShopStatsManager.Me.RecordReroll();',
    T * 2 + '}',
    T * 2 + 'purse.value -= RerollPriceRef.value;',
    T * 2 + 'TestManager.Log("[ShopButton] Reroll() succeeded. shopItems=" + (currentShopItemDeckRef != null ? currentShopItemDeckRef.deck.Count : -1) + " ShopUXManager.Instance=" + (ShopUXManager.Instance != null ? "exists" : "NULL"));',
    '',
    T * 2 + '// Notify ShopUXManager to handle reroll animation and regenerate physical cards',
    T * 2 + 'ShopUXManager.Instance?.OnReroll();',
    T + '}',
    '}',
])

reroll_new = block([
    T + 'public void Reroll()',
    T + '{',
    T * 2 + '// DIAG-LOG(2026-08-08): tracing why the shop Reroll button may appear dead',
    T * 2 + 'TestManager.Log("[ShopButton] Reroll() clicked. phase=" + (gamePhaseRef != null ? gamePhaseRef.currentGamePhase.ToString() : "null") + " purse=" + (purse != null ? purse.value : -1) + " price=" + (RerollPriceRef != null ? RerollPriceRef.value : -1));',
    T * 2 + 'int freeLeft = (_utilityBonus != null ? _utilityBonus.freeRerolls : 0) - _freeRerollsUsedThisVisit;',
    T * 2 + 'bool isFree = freeLeft > 0;',
    T * 2 + 'if (!isFree && (RerollPriceRef == null || purse.value < RerollPriceRef.value))',
    T * 2 + '{',
    T * 2 + '\tTestManager.Log("[ShopButton] Reroll() early return: cost not met (free left=" + freeLeft + "). purse=" + (purse != null ? purse.value : -1) + " price=" + (RerollPriceRef != null ? RerollPriceRef.value : -1));',
    T * 2 + '\treturn;',
    T * 2 + '}',
    '',
    T * 2 + '// Free rerolls are consumed first and still count toward discount / reserved-slot cadence.',
    T * 2 + '_rerollsThisVisit++;',
    T * 2 + 'if (isFree)',
    T * 2 + '{',
    T * 2 + '\t_freeRerollsUsedThisVisit++;',
    T * 2 + '}',
    T * 2 + 'else',
    T * 2 + '{',
    T * 2 + '\tpurse.value -= RerollPriceRef.value;',
    T * 2 + '}',
    '',
    T * 2 + '// First generate new shop item data',
    T * 2 + 'GenerateShopItems();',
    T * 2 + 'ApplyBoardDiscount();',
    T * 2 + 'UpdateShopItemInfo();',
    T * 2 + '// record reroll',
    T * 2 + 'if (ShopStatsManager.Me != null)',
    T * 2 + '{',
    T * 2 + '\tShopStatsManager.Me.RecordReroll();',
    T * 2 + '}',
    T * 2 + 'TestManager.Log("[ShopButton] Reroll() succeeded (free=" + isFree + "). shopItems=" + (currentShopItemDeckRef != null ? currentShopItemDeckRef.deck.Count : -1) + " ShopUXManager.Instance=" + (ShopUXManager.Instance != null ? "exists" : "NULL"));',
    '',
    T * 2 + '// Notify ShopUXManager to handle reroll animation and regenerate physical cards',
    T * 2 + 'ShopUXManager.Instance?.OnReroll();',
    T + '}',
    '',
    T + '/// <summary>',
    T + '/// Recomputes utility contributions from the current player deck. Call after any deck change.',
    T + '/// </summary>',
    T + 'private void RefreshUtilityBonus()',
    T + '{',
    T * 2 + '_utilityBonus = UtilityShopBonus.Compute(playerDeckRef != null ? playerDeckRef.deck : null);',
    T + '}',
    '',
    T + 'private int GetSessionNum()',
    T + '{',
    T * 2 + 'return sessionNum != null ? sessionNum.value : 0;',
    T + '}',
    '',
    T + '/// <summary>',
    T + '/// Applies baseline growth at shop entry: deckSize formula (deckSizeOg + per-session +',
    T + '/// purchases, clamped to the static maxDeckSize ceiling) and the hpMax recompute.',
    T + '/// </summary>',
    T + 'private void ApplyBaselineGrowth()',
    T + '{',
    T * 2 + 'int session = GetSessionNum();',
    T * 2 + 'if (deckSize != null)',
    T * 2 + '{',
    T * 3 + 'int purchases = deckSlotPurchasesRef != null ? deckSlotPurchasesRef.value : 0;',
    T * 3 + 'int ceiling = maxDeckSize != null ? maxDeckSize.value : 16;',
    T * 3 + 'deckSize.value = UtilityShopBonus.ComputeDeckSize(deckSize.valueOg, session, deckSizePerSession, purchases, ceiling);',
    T * 3 + 'ShopUXManager.Instance?.SpawnAdditionalEmptySpaces();',
    T * 2 + '}',
    T * 2 + 'ApplyHpMaxFromDeck();',
    T + '}',
    '',
    T + '/// <summary>',
    T + '/// hpMax = hpMaxOg + per-session baseline + sum(HP utility cards); hp clamps down if above',
    T + '/// max. Never lethal: hpMaxOg >= 1 and bonuses are >= 0.',
    T + '/// </summary>',
    T + 'private void ApplyHpMaxFromDeck()',
    T + '{',
    T * 2 + 'var status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;',
    T * 2 + 'if (status == null) return;',
    T * 2 + 'status.hpMax = UtilityShopBonus.ComputeHpMax(status.hpMaxOg, GetSessionNum(), hpMaxPerSession, _utilityBonus);',
    T * 2 + 'status.hp = Mathf.Min(status.hp, status.hpMax);',
    T + '}',
    '',
    T + 'private void ResetVisitCounters()',
    T + '{',
    T * 2 + '_rerollsThisVisit = 0;',
    T * 2 + '_freeRerollsUsedThisVisit = 0;',
    T * 2 + '_boardDiscounts.Clear();',
    T + '}',
    '',
    T + '/// <summary>',
    T + '/// Settles reroll discounts onto the freshly generated board: every discount spec whose',
    T + '/// cadence hits this reroll number adds its gold-off onto ONE random board card. Discounts',
    T + '/// never accumulate across rerolls (board regenerates, dict cleared) and never apply to the',
    T + '/// initial board of a visit (only Reroll() settles).',
    T + '/// </summary>',
    T + 'private void ApplyBoardDiscount()',
    T + '{',
    T * 2 + '_boardDiscounts.Clear();',
    T * 2 + 'if (_utilityBonus == null || currentShopItemDeckRef == null) return;',
    T * 2 + 'int totalOff = 0;',
    T * 2 + 'foreach (var spec in _utilityBonus.rerollDiscounts)',
    T * 2 + '{',
    T * 3 + 'if (spec.everyRerolls > 0 && _rerollsThisVisit % spec.everyRerolls == 0)',
    T * 3 + '{',
    T * 4 + 'totalOff += spec.goldOff;',
    T * 3 + '}',
    T * 2 + '}',
    T * 2 + 'if (totalOff <= 0 || currentShopItemDeckRef.deck.Count == 0) return;',
    T * 2 + 'int index = Random.Range(0, currentShopItemDeckRef.deck.Count);',
    T * 2 + 'var script = currentShopItemDeckRef.deck[index] != null ? currentShopItemDeckRef.deck[index].GetComponent<CardScript>() : null;',
    T * 2 + 'if (script != null)',
    T * 2 + '{',
    T * 3 + '_boardDiscounts[script] = totalOff;',
    T * 2 + '}',
    T + '}',
    '',
    T + '/// <summary>Buy price with any active board discount applied.</summary>',
    T + 'public int GetEffectiveBuyPrice(CardScript cardScript)',
    T + '{',
    T * 2 + 'int price = GetCardPrice(cardScript);',
    T * 2 + 'if (cardScript != null && _boardDiscounts.TryGetValue(cardScript, out int off))',
    T * 2 + '{',
    T * 3 + 'price = Mathf.Max(0, price - off);',
    T * 2 + '}',
    T * 2 + 'return price;',
    T + '}',
    '}',
])
sub(reroll_anchor, reroll_new, 'E reroll + helpers')

if fails:
    print('FAILURES:')
    for f in fails:
        print('  ' + f)
    sys.exit(1)
open(P, 'w', encoding='utf-8', newline='').write(s)
print('SAVED')
