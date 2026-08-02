# OneDeck mechanic stability v2: Start Card position ~ Gaussian(center=N/2, sd=0.15N).
# Consequences (verified in StartCardShuffleEffect.cs):
# - Each card reveals w.p. q=1/2 per round (Bernoulli); round reveal count ~ N/2 +- 0.15N.
# - "Graveyard" (below Start Card) holds ~half the deck every round:
#   Linger active w.p. ~1/2; grave-scaling gets P/2 baseline; rifts stored there are safe.
# - Bury/exile-consume target pools: bury = ACTIVE zone only (~P/2 cards);
#   rift consume/check = whole deck (incl. graveyard).
# - Zombie baseline: 2 * q = 1 dmg/round per zombie. Combat ~2x more rounds.

import math, random
from math import comb

def binom_pmf(n, p):
    return [comb(n, k) * p**k * (1-p)**(n-k) for k in range(n+1)]

q = 0.5

print("="*70)
print("M1v2: BURY / DEATHRATTLE with q=1/2")
print("Bury-effect cards reveal w.p. 1/2 -> burials/round ~ Binomial(B,1/2)*k")
print("Victim drawn from active friendly pool (~P/2); hit rate still d/P")
print("="*70)
print(f"{'P':>3} {'d':>3} {'Bcards':>6} {'k':>2} | {'E[proc]':>7} {'P(0)':>6}")
for P in (6, 10, 14):
    for d in (2, 3, 5):
        if d > P: continue
        for Bcards, k in ((2,1),(3,1),(4,1),(2,2)):
            # burials ~ Binomial(Bcards, .5) * k ; each burial hits DR w.p. d/P
            e = Bcards*0.5*k*(d/P)
            p0 = 0.0
            for bcards_revealed in range(Bcards+1):
                pb = comb(Bcards, bcards_revealed)*0.5**Bcards
                burials = bcards_revealed*k
                p0 += pb * (1-d/P)**burials
            print(f"{P:>3} {d:>3} {Bcards:>6} {k:>2} | {e:>7.2f} {p0:>6.2f}")

print()
print("P(key DeathRattle card buried per round) = P(active) * E[burials]/P_active")
print("= 0.5 * (B*k*0.5)/(P/2) = B*k/(2P)")
for P in (6, 10, 14):
    for Bcards, k in ((2,1),(3,1),(4,1)):
        print(f"  P={P:>2}, bury-cards={Bcards}, k={k}: {Bcards*k/(2*P):.2f}")

print()
print("GRAVEYARD BONUS (new): every round ~P/2 friendly cards sit below Start Card.")
print("Linger cards are active w.p. ~1/2; grave-scaling gets P/2 baseline for free:")
print("  BODY_CANON dmg = 3*(P/2 baseline + active buried) ~ 3*(P-1) when revealed")
print("  CURSED_SKELETON enhance = P/2 baseline + burials")

print()
print("="*70)
print("M2v2: CURSE with q=1/2 delivery AND q=1/2 ramp")
print("enhances/round ~ Binomial(e_cards, 1/2); curse reveals w.p. 1/2 per round")
print("="*70)
print("E[power at round r] = (e/2)*(r-2);  E[dmg round r] = (e/2)*(r-2) * 1/2")
print(f"{'e':>3} {'R':>3} | {'E[cum curse]':>12} {'E[1 zombie]':>11} {'ratio':>6}")
for e in (1, 2, 3, 4):
    for R in (4, 6, 8, 10):
        cum = sum((e/2)*(r-2)*0.5 for r in range(2, R+1))
        z = 2*0.5*R
        print(f"{e:>3} {R:>3} | {cum:>12.1f} {z:>11.1f} {cum/z:>6.2f}")

print()
print("Delivery variance (Bernoulli 1/2 does NOT average out over few rounds):")
print("Total curse dmg over R rounds ~ sum of power_r * Bernoulli(1/2)")
print("std/mean (CV) for e=2:")
for R in (6, 8, 10):
    powers = [(2/2)*(r-2) for r in range(2, R+1)]  # e=2
    mean = sum(p*0.5 for p in powers)
    var = sum((p**2)*0.25 for p in powers)
    print(f"  R={R:>2}: mean={mean:5.1f} std={math.sqrt(var):5.1f} CV={math.sqrt(var)/mean if mean else 0:.2f}")

print()
print("="*70)
print("M3v2: RIFT STOCK PROCESS over rounds (Monte Carlo)")
print("Per round: generators reveal w.p. 1/2 -> +stock (to graveyard);")
print("each stocked rift self-exiles w.p. 1/2 (lands in active zone & reveals);")
print("consumers reveal w.p. 1/2 each and eat `need` from stock (whole deck).")
print("="*70)
def rift_sim(g, C, need, rounds=8, sims=40000):
    whiff = 0; reveals = 0; stock_end = 0.0; consumed_tot = 0.0
    for _ in range(sims):
        stock = 0
        for _ in range(rounds):
            # generation (generators that reveal this round)
            for _ in range(g):
                if random.random() < 0.5: stock += 1
            # self-exile of stocked rifts landing active
            died = sum(1 for _ in range(stock) if random.random() < 0.5)
            stock -= died
            # consumers (revealed w.p. .5), eat if possible
            for _ in range(C):
                if random.random() < 0.5:
                    reveals += 1
                    if stock >= need:
                        stock -= need; consumed_tot += need
                    else:
                        whiff += 1
        stock_end += stock
    return whiff/max(reveals,1), consumed_tot/max(reveals,1), stock_end/sims

print(f"{'g':>3} {'C':>3} {'need':>4} | {'P(whiff)':>8} {'P(succ)':>7} {'E[end stock]':>12}")
for g, C in ((1,1),(2,1),(2,2),(3,2),(3,3)):
    for need in (1,2):
        w, s, st = rift_sim(g, C, need)
        print(f"{g:>3} {C:>3} {need:>4} | {w:>8.2f} {1-w:>7.2f} {st:>12.2f}")

print()
print("Rift destiny: P(consumed before self-exile) vs P(self-exile -> stage-1 recycle)")
def rift_destiny(C, sims=100000):
    cons = 0
    for _ in range(sims):
        # each round: rift self-exiles w.p. .5; consumed w.p. 1-(.5)^C if stock rival none
        for _ in range(12):
            if random.random() < 0.5: break          # self-exiled (revealed)
            if random.random() < 1 - 0.5**C:         # a consumer revealed & ate it
                cons += 1; break
    return cons/sims
for C in (1,2,3):
    print(f"  C={C}: P(consumed)={rift_destiny(C):.2f}, P(self-exile recycle)={1-rift_destiny(C):.2f}")

print()
print("="*70)
print("M4v2: POWER with longer combats (R_left doubled)")
print("="*70)
print("Per stack on random friendly: E[value] = U * R_left, U = damage-card share")
for P in (6, 10, 14):
    for d in (2, 4, 6):
        if d > P: continue
        for R in (2, 4, 6):
            print(f"  P={P:>2} d={d} R_left={R}: {d/P*R:4.2f}")
print()
print("MAD_SCIENTIST next-3 (top of deck = ACTIVE zone, reveals this round):")
print("faction split of top-3 ~ hypergeometric over active zone (~half/half) ->")
print("E[net] = 2*3*(df - de)/(P_active...) unchanged vs v1: symmetric deck => 0")
