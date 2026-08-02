# OneDeck mechanic stability calculations.
# All models are built from the confirmed code facts:
# - Every active card reveals exactly once per round; revealed cards go below the
#   Start Card (graveyard); round ends when Start Card surfaces, then reshuffle.
# - Bury: uniform random over target faction's ACTIVE-zone non-minion cards.
#   Victim loses this round's reveal (denial) but procs DeathRattle.
# - New cards (rift/JU_ON) enter at index 0 (graveyard), active next round,
#   but consumable from anywhere in the deck.
# - EnhanceCurse auto-spawns JU_ON if absent; all enhances stack on first-found JU_ON.
# - Power: +1 dmg per stack on every damage instance of that card, persists all combat.
# - Baseline: ZOMBIE = 2 dmg/reveal/round. HP = 20.

import math
import random
from math import comb

def binom_pmf(n, p):
    return [comb(n, k) * p**k * (1-p)**(n-k) for k in range(n+1)]

def binom_tail0(n, p):
    return (1-p)**n

print("="*70)
print("M1: BURY / DEATHRATTLE  (per round, B burials, d DeathRattle of P friendly)")
print("="*70)
print(f"{'P':>3} {'d':>3} {'B':>3} | {'E[procs]':>8} {'P(0proc)':>8} {'P(>=2)':>7} {'CV':>5}")
for P in (6, 10, 14):
    for d in (2, 3, 5):
        for B in (1, 2, 3, 4):
            if d > P: continue
            p = d / P
            pmf = binom_pmf(B, p)
            e = B * p
            p0 = pmf[0]
            p2 = 1 - pmf[0] - (pmf[1] if B >= 1 else 0)
            var = B * p * (1-p)
            cv = math.sqrt(var)/e if e > 0 else 0
            print(f"{P:>3} {d:>3} {B:>3} | {e:>8.2f} {p0:>8.2f} {p2:>7.2f} {cv:>5.2f}")

print()
print("P(key card, e.g. your one MARTYR, buried at least once in a round) = 1-(1-1/P)^B")
print(f"{'P':>3} {'B':>3} | {'P(hit key card)':>15}")
for P in (6, 10, 14):
    for B in (1, 2, 3, 4):
        print(f"{P:>3} {B:>3} | {1-(1-1/P)**B:>15.2f}")

print()
print("Opportunity cost of one friendly burial: victim loses this round's reveal.")
print("Zombie-denominated cost = 2 dmg. DeathRattle must beat 2 dmg to net positive.")

print()
print("="*70)
print("M2: CURSE RAMP  (e enhance stacks/round, JU_ON spawns round1, active round2+)")
print("="*70)
print("Power at reveal in round r ~ e*(r-2) + Binomial(e, 1/2)   (r>=2)")
print("Cumulative curse damage by end of round R vs 2 zombies = 4R? No: vs 1 zombie 2R")
print(f"{'e':>3} {'R':>3} | {'E[cum curse]':>12} {'E[zombie]':>9} {'ratio':>6}")
for e in (1, 2, 3):
    for R in (3, 4, 5, 6):
        # rounds 2..R each deal expected power = e*(r-2) + e/2
        cum = sum(e*(r-2) + e/2 for r in range(2, R+1))
        z = 2*R
        print(f"{e:>3} {R:>3} | {cum:>12.1f} {z:>9} {cum/z:>6.2f}")

print()
print("Round-to-round delivery variance: power at reveal ~ Binomial(e,1/2) around trend,")
print("but POWER PERSISTS, so variance does NOT accumulate: only the current round's")
print("split matters. Std of damage in any round = sqrt(e)/2.")
for e in (1,2,3,4):
    print(f"  e={e}: per-round damage std from reveal order = {math.sqrt(e)/2:.2f}")

print()
print("Enemy counterplay: u enemy burials/round over E active enemy cards ->")
print("P(curse denied its reveal this round) = 1-(1-1/E)^u")
for E in (5, 8, 12):
    for u in (1, 2):
        print(f"  E={E:>2}, u={u}: {1-(1-1/E)**u:.2f}")

print()
print("="*70)
print("M3: RIFT RACE  (R active rifts + C consumers in random reveal order)")
print("="*70)
def simulate_rift(R, C, need, sims=200000):
    # R rifts (self-exile on reveal), C consumers each needing `need` live rifts.
    # Order of the R+C relevant cards uniform random. Consumers consume greedily.
    # Returns per-consumer success probability and expected rifts consumed.
    succ = 0
    consumed_total = 0
    for _ in range(sims):
        seq = ['R']*R + ['C']*C
        random.shuffle(seq)
        live = R
        for s in seq:
            if s == 'R':
                live -= 1            # rift reveals itself, self-exiles
            else:
                if live >= need:
                    live -= need     # consumer eats `need` rifts
                    succ += 1
                    consumed_total += need
    return succ/(C*sims), consumed_total/(C*sims)

print("Consumer = RIFT_MONSTER (need 1 -> 4 dmg) or RIFT_DRAGON (need 2 -> 6 dmg)")
print(f"{'R':>3} {'C':>3} {'need':>4} | {'P(succ)':>8} {'E[dmg|4/6]':>10}")
for R in (1, 2, 3, 4):
    for C in (1, 2, 3):
        for need, dmg in ((1, 4), (2, 6)):
            p, _ = simulate_rift(R, C, need, sims=60000)
            print(f"{R:>3} {C:>3} {need:>4} | {p:>8.2f} {dmg*p:>10.2f}")

print()
print("Stable supply: rifts generated this round sit in graveyard and ARE consumable.")
print("So a generator revealed before a consumer guarantees ammo: with g generators,")
print("P(consumer has ammo) ~ P(at least one generator revealed earlier) + leftovers.")
# Simple sequential model: g generators (each +1 rift), C consumers (need 1), random order.
def simulate_gen(g, C, start_live=0, sims=200000):
    succ = 0
    for _ in range(sims):
        seq = ['G']*g + ['C']*C
        random.shuffle(seq)
        live = start_live
        for s in seq:
            if s == 'G':
                live += 1
            else:
                if live >= 1:
                    live -= 1
                    succ += 1
    return succ/(C*sims)
print(f"{'g':>3} {'C':>3} | {'P(succ, no leftover)':>20}")
for g in (1, 2, 3):
    for C in (1, 2, 3):
        print(f"{g:>3} {C:>3} | {simulate_gen(g, C):>20.2f}")

print()
print("="*70)
print("M4: POWER UTILIZATION")
print("="*70)
print("Per stack on a random FRIENDLY card: E[value] = (d_dmg/P) * R_left  [dmg]")
print("d_dmg = friendly damage-dealing cards, R_left = rounds remaining in combat")
print(f"{'P':>3} {'d_dmg':>5} {'R_left':>6} | {'E[value/stack]':>14}")
for P in (6, 10, 14):
    for d in (2, 4, 6):
        if d > P: continue
        for R in (1, 2, 3):
            print(f"{P:>3} {d:>5} {R:>6} | {d/P*R:>14.2f}")

print()
print("MAD_SCIENTIST (next 3 cards in deck order, faction-blind, 2 stacks each):")
print("E[net dmg] = 2 * sum over 3 cards [ +P(friendly dmg card) - P(enemy dmg card) ]")
print("Deck: F friendly (df dmg), E enemy (de dmg), hypergeometric next-3 draw")
def hyper_mean(N, K, n):
    return n * K / N
for (F, df, E, de) in ((5,3,5,3), (8,4,8,4), (8,5,8,3), (12,6,12,6), (5,2,5,4)):
    N = F + E
    exp_net = 2 * (hyper_mean(N, df, 3) - hyper_mean(N, de, 3))
    print(f"  F={F:>2}(df={df}) E={E:>2}(de={de}): E[net]={exp_net:+.2f} dmg/cast, "
          f"friendly stacks={2*hyper_mean(N,df,3):.2f}, enemy stacks leaked={2*hyper_mean(N,de,3):.2f}")
print()
print("Utilization rate U = (d_dmg/P): fraction of power that ever converts to damage")
for P in (6, 10, 14):
    for d in (2, 4, 6):
        if d > P: continue
        print(f"  P={P:>2}, d_dmg={d}: U={d/P:.0%}")
