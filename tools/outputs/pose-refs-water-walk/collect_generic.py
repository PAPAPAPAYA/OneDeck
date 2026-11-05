"""Generic Pexels search crawler: collect_generic.py <thumb-dir> <queries separated by |>."""

import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
thumb_dir = sys.argv[1]
queries = [q.strip() for q in sys.argv[2].split("|") if q.strip()]
THUMBS = os.path.join(SCRATCH, thumb_dir)
os.makedirs(THUMBS, exist_ok=True)

ID_RE = re.compile(r"images\.pexels\.com/photos/(\d+)/")
ALT_RE = re.compile(r'alt="([^"]{5,160})"')


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
            print(f"    retry {attempt + 1} ({type(e).__name__}: {str(e)[:70]})", flush=True)
            time.sleep(8 + attempt * 8)
    return ""


candidates = {}
for q in queries:
    print(f"[query] {q}", flush=True)
    html = fetch_text("https://www.pexels.com/search/" + urllib.parse.quote(q) + "/")
    if not html:
        print("    -> no html", flush=True)
        continue
    alts = [(m.start(), m.group(1)) for m in ALT_RE.finditer(html)]
    ids = [(m.start(), m.group(1)) for m in ID_RE.finditer(html)]
    added = 0
    for pos, pid in ids:
        if pid in candidates:
            continue
        near = sorted(alts, key=lambda a: abs(a[0] - pos))[:1]
        candidates[pid] = {"id": pid, "alt": near[0][1] if near else "", "query": q}
        added += 1
    print(f"    ids={len(ids)} new={added} total={len(candidates)}", flush=True)
    time.sleep(7)

items = list(candidates.values())
for item in items:
    dest = os.path.join(THUMBS, f"{item['id']}.jpg")
    if os.path.exists(dest) and os.path.getsize(dest) > 2000:
        item["thumb_bytes"] = os.path.getsize(dest)
        continue
    try:
        req = urllib.request.Request(
            f"https://images.pexels.com/photos/{item['id']}/pexels-photo-{item['id']}.jpeg"
            "?auto=compress&cs=tinysrgb&w=420",
            headers={"User-Agent": UA},
        )
        with urllib.request.urlopen(req, timeout=40) as r:
            data = r.read()
        with open(dest, "wb") as f:
            f.write(data)
        item["thumb_bytes"] = len(data)
    except Exception:
        item["thumb_bytes"] = 0

with open(os.path.join(SCRATCH, f"candidates_{thumb_dir}.json"), "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=1)

ok = [i for i in items if i.get("thumb_bytes")]
print(f"\ncollected={len(items)} thumbs_ok={len(ok)}")
for i in ok:
    print(f"{i['id']} | {i['alt'][:75]}")
