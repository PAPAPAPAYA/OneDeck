"""Round 2: surfers / wading / emerging queries, where bent knees and thigh-deep water are common."""

import json
import os
import re
import time
import urllib.parse
import urllib.request

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
THUMBS = os.path.join(SCRATCH, "thumbs2")
os.makedirs(THUMBS, exist_ok=True)

QUERIES = [
    "surfer walking out of the water",
    "surfer walking out of sea with board",
    "walking through the waves",
    "coming out of the ocean",
    "wading through water",
    "walking in shallow water",
]

ID_RE = re.compile(r"images\.pexels\.com/photos/(\d+)/")
ALT_RE = re.compile(r'alt="([^"]{5,160})"')
ALT_RE2 = re.compile(r'"alt":"([^"]{5,160})"')


def fetch_text(url, tries=3):
    for attempt in range(tries):
        try:
            req = urllib.request.Request(
                url,
                headers={
                    "User-Agent": UA,
                    "Accept": "text/html",
                    "Accept-Language": "en-US,en;q=0.9",
                    "Referer": "https://www.pexels.com/",
                },
            )
            with urllib.request.urlopen(req, timeout=45) as r:
                return r.read().decode("utf-8", "ignore")
        except Exception as e:
            print(f"    retry {attempt + 1} ({type(e).__name__}: {str(e)[:70]})")
            time.sleep(5 + attempt * 5)
    return ""


candidates = {}
for q in QUERIES:
    print(f"[query] {q}")
    html = fetch_text("https://www.pexels.com/search/" + urllib.parse.quote(q) + "/")
    if not html:
        print("    -> no html")
        continue
    alts = [(m.start(), m.group(1)) for m in ALT_RE.finditer(html)] or [
        (m.start(), m.group(1)) for m in ALT_RE2.finditer(html)
    ]
    ids = [(m.start(), m.group(1)) for m in ID_RE.finditer(html)]
    added = 0
    for pos, pid in ids:
        if pid in candidates:
            continue
        near = sorted(alts, key=lambda a: abs(a[0] - pos))[:1]
        candidates[pid] = {
            "id": pid,
            "alt": near[0][1] if near else "",
            "query": q,
            "page": f"https://www.pexels.com/photo/{pid}/",
        }
        added += 1
    print(f"    chars={len(html)} ids={len(ids)} new={added} total={len(candidates)}")
    time.sleep(5)

items = list(candidates.values())
for item in items:
    dest = os.path.join(THUMBS, f"{item['id']}.jpg")
    if os.path.exists(dest) and os.path.getsize(dest) > 2000:
        item["thumb_bytes"] = os.path.getsize(dest)
        continue
    url = (
        f"https://images.pexels.com/photos/{item['id']}/pexels-photo-{item['id']}.jpeg"
        "?auto=compress&cs=tinysrgb&w=420"
    )
    try:
        req = urllib.request.Request(url, headers={"User-Agent": UA})
        with urllib.request.urlopen(req, timeout=40) as r:
            data = r.read()
        with open(dest, "wb") as f:
            f.write(data)
        item["thumb_bytes"] = len(data)
    except Exception:
        item["thumb_bytes"] = 0

with open(os.path.join(SCRATCH, "candidates2.json"), "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=1)

ok = [i for i in items if i.get("thumb_bytes")]
print(f"\ncollected={len(items)} thumbs_ok={len(ok)}")
for i in ok:
    if i["alt"]:
        print(f"{i['id']} | {i['alt'][:80]}")
