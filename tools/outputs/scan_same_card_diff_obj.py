import re, io, sys

LOG = r"C:\Users\damen\AppData\Local\Unity\Editor\Editor.log"
OUT = r"D:\Unity Projects\OneDeck\tools\outputs\same_card_diff_obj_report.txt"

make_re = re.compile(r"\[EffectChainManager\] MakeANewEffectRecorder chain#(\d+) card=(.*?) effect=(.*?) isRoot=")
close_re = re.compile(r"\[EffectChainManager\] CloseOpenedChain closing (\d+) recorders")
chain_id_re = re.compile(r"chain#(\d+)\[")

opened = {}  # chainID -> (card, effect)
hits = []
raw_lines = []

with io.open(LOG, "r", encoding="utf-8", errors="replace") as f:
    for lineno, line in enumerate(f, 1):
        m = make_re.search(line)
        if m:
            cid = int(m.group(1)); card = m.group(2); eff = m.group(3)
            offenders = [(oc, (c, e)) for oc, (c, e) in opened.items() if c == card and e != eff]
            if offenders:
                hits.append((lineno, cid, card, eff, offenders))
                raw_lines.append((lineno, line.rstrip()))
            opened[cid] = (card, eff)
            continue
        c = close_re.search(line)
        if c:
            for cid in chain_id_re.findall(line):
                opened.pop(int(cid), None)

uniq = {}
for lineno, cid, card, eff, offenders in hits:
    for oc, (c, e) in offenders:
        key = (card, eff, c, e)
        uniq.setdefault(key, []).append(lineno)

with io.open(OUT, "w", encoding="utf-8") as out:
    out.write("SameCardDifferentObject candidate hits (log replay approximation)\n")
    out.write("total Make lines hits: %d\n" % len(hits))
    out.write("unique (new card/effect <- open card/effect) pairs: %d\n\n" % len(uniq))
    for (card, eff, c, e), linenos in sorted(uniq.items(), key=lambda kv: -len(kv[1])):
        out.write("%4dx  NEW card=[%s] effect=[%s]  <-  OPEN card=[%s] effect=[%s]\n" % (len(linenos), card, eff, c, e))
    out.write("\n--- raw excerpts (first 40) ---\n")
    for lineno, line in raw_lines[:40]:
        out.write("L%d: %s\n" % (lineno, line))

print("hits:", len(hits), "unique pairs:", len(uniq))
for (card, eff, c, e), linenos in sorted(uniq.items(), key=lambda kv: -len(kv[1]))[:25]:
    print("%4dx  [%s|%s] <- open [%s|%s]" % (len(linenos), card, eff, c, e))
