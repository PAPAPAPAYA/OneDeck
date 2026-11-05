import json
import os
import re
import urllib.parse
import urllib.request

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
OUT = os.path.dirname(os.path.abspath(__file__))

QUERIES = [
    "walking out of water",
    "walking out of the sea",
    "emerging from water",
    "wet woman walking beach",
    "man walking out of ocean",
    "wading in water shore",
]

ID_RE = re.compile(r"images\.pexels\.com/photos/(\d+)/")
ALT_RE = re.compile(r'alt="([^"]{6,120})"')


def fetch(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=40) as r:
        return r.read().decode("utf-8", "ignore")


found = {}
for q in QUERIES:
    url = "https://www.pexels.com/search/" + urllib.parse.quote(q) + "/"
    try:
        html = fetch(url)
    except Exception as e:
        print("query failed", q, type(e).__name__, e)
        continue
    # pair each image id with the nearest preceding/following alt text
    ids = [(m.start(), m.group(1)) for m in ID_RE.finditer(html)]
    alts = [(m.start(), m.group(1)) for m in ALT_RE.finditer(html)]
    for pos, pid in ids:
        context = [a for apos, a in alts if abs(apos - pos) < 1200]
        label = context[0] if context else ""
        if pid not in found:
            found[pid] = {"id": pid, "alt": label, "queries": [q]}
        else:
            if q not in found[pid]["queries"]:
                found[pid]["queries"].append(q)
    print(f"{q!r}: page chars={len(html)} ids={len(ids)} total={len(found)}")

with open(os.path.join(OUT, "candidates.json"), "w", encoding="utf-8") as f:
    json.dump(list(found.values()), f, ensure_ascii=False, indent=1)

for item in found.values():
    print(item["id"], "|", item["alt"])
