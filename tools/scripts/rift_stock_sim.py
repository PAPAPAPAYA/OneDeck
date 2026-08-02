# Rift stock process, event-accurate per round:
# Round start stock S (in deck). Shuffle: each rift active w.p. 0.5 (will reveal & self-exile
# this round unless eaten first) or safe (graveyard, available all round).
# Events in random order: G (generator revealed w.p. 0.5 each, +1 fresh rift to graveyard,
# consumable immediately), C (consumer revealed w.p. 0.5 each, eats `need` from any live rift),
# X (active rift's own reveal: it dies). All active rifts dead by round end regardless.
# Survivors to next round = safe + fresh - consumed.
import random

def rift_round_sim(g, C, need, rounds=8, sims=60000, start_stock=0):
    whiff = reveals = 0
    consumed_total = 0.0
    stock_end = 0.0
    for _ in range(sims):
        S = start_stock
        for _ in range(rounds):
            A = sum(1 for _ in range(S) if random.random() < 0.5)  # active (doomed)
            safe = S - A
            fresh = 0
            events = []
            for _ in range(g):
                if random.random() < 0.5: events.append('G')
            for _ in range(C):
                if random.random() < 0.5: events.append('C')
            events += ['X'] * A
            random.shuffle(events)
            undead_active = A
            for ev in events:
                if ev == 'G':
                    fresh += 1
                elif ev == 'X':
                    undead_active -= 1
                else:  # consumer
                    reveals += 1
                    if safe + fresh + undead_active >= need:
                        # consume: prefer safe, then fresh, then undead active
                        left = need
                        take = min(safe, left); safe -= take; left -= take
                        take = min(fresh, left); fresh -= take; left -= take
                        take = min(undead_active, left); undead_active -= take; left -= take
                        consumed_total += need
                    else:
                        whiff += 1
            S = safe + fresh  # undead active die at round end
        stock_end += S
    return (whiff / max(reveals, 1), consumed_total / max(reveals, 1), stock_end / sims)

print(f"{'g':>3} {'C':>3} {'need':>4} | {'P(whiff)':>8} {'P(succ)':>7} {'E[end stock]':>12}")
for g, C in ((1,1),(2,1),(3,1),(2,2),(3,2),(4,2),(3,3)):
    for need in (1,2):
        w, s, st = rift_round_sim(g, C, need)
        print(f"{g:>3} {C:>3} {need:>4} | {w:>8.2f} {1-w:>7.2f} {st:>12.2f}")

print()
print("Single rift destiny (how a generated rift ends up):")
def destiny(C, need=1, sims=200000):
    consumed = 0
    for _ in range(sims):
        S = 1  # fresh rift in graveyard
        for _ in range(30):
            A = 1 if random.random() < 0.5 else 0
            safe = S - A
            events = ['X'] * A
            for _ in range(C):
                if random.random() < 0.5: events.append('C')
            random.shuffle(events)
            undead = A
            done = False
            for ev in events:
                if ev == 'X':
                    undead -= 1
                else:
                    if safe + undead >= need:
                        consumed += 1
                        done = True
                        break
            if done:
                break
            S = safe
            if S <= 0:
                break  # self-exiled unrevealed-consumed
            # if rift still alive continue rounds
        # loop end
    return consumed / sims
for C in (1,2,3):
    pc = destiny(C)
    print(f"  C={C}: P(consumed eventually)={pc:.2f}, P(self-exile recycle)={1-pc:.2f}")

print()
print("Consumer expected damage/round vs zombie 1.0 dmg/round:")
print("consumer reveals w.p. 0.5/round; success pays 4 (monster, need1) or 6 (dragon, need2)")
for g, C, need, payoff in ((2,1,1,4),(3,1,1,4),(2,1,2,6),(3,1,2,6),(3,2,1,4)):
    w, s, st = rift_round_sim(g, C, need)
    print(f"  g={g} C={C} need={need}: E[dmg/round] = 0.5*{payoff}*{1-w:.2f} = {0.5*payoff*(1-w):.2f}")
