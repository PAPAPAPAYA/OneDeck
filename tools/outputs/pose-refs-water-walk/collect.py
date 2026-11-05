"""Collect Pexels photo candidates for a "walking out of water, bent knees" pose reference.

Writes candidate metadata + small thumbnails into a scratch directory (TEMP by default),
because the sandbox only allows writes to the session temp dir and the workspace root.
"""

import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
THUMBS = os.path.join(SCRATCH, "thumbs")
os.makedirs(THUMBS, exist_ok=True)

QUERIES = [
    "walking out of the sea",
    "walking out of water",
    "woman walking out of ocean",
    "man walking out of sea",
    "emerging from the water",
    "wading through shallow water",
]

ID_RE = re.compile(r"images\.pexels\.com/photos/(\d+)/")
ALT_RE = re.compile(r'"alt":"([^"]{5,140})"')


def fetch_text(url, tries=3):
    for attempt in range(tries):
        try:
            req = urllib.request.Request(
                url,
                headers={
                    "User-Agent": UA,
                    "Accept": "text/html,application/xhtml+xml",
                    "Accept-Language": "en-US,en;q=0.9",
                    "Referer": "https://www.pexels.com/",
                },
            )
            with urllib.request.urlopen(req, timeout=45) as r:
                return r.read().decode("utf-8", "ignore")
        except Exception as e:
            print(f"    retry {attempt + 1}/{tries} ({type(e).__name__}: {str(e)[:80]})")
            time.sleep(4 + attempt * 4)
    return ""


candidates = {}
for q in QUERIES:
    print(f"[query] {q}")
    html = fetch_text("https://www.pexels.com/search/" + urllib.parse.quote(q) + "/")
    if not html:
        print("    -> no html")
        continue
    alts = [(m.start(), m.group(1)) for m in ALT_RE.finditer(html)]
    ids = [(m.start(), m.group(1)) for m in ID_RE.finditer(html)]
    added = 0
    for pos, pid in ids:
        if pid in candidates:
            continue
        near = [a for apos, a in alts if abs(apos - pos) < 2500]
        label = near[0] if near else ""
        candidates[pid] = {"id": pid, "alt": label, "query": q}
        added += 1
    print(f"    chars={len(html)} ids={len(ids)} new={added} total={len(candidates)}")
    time.sleep(4)

items = list(candidates.values())
with open(os.path.join(SCRATCH, "candidates.json"), "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=1)

# thumbnail download (cheap, for the contact sheet)
for i, item in enumerate(items):
    dest = os.path.join(THUMBS, f"{item['id']}.jpg")
    if os.path.exists(dest) and os.path.getsize(dest) > 2000:
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
    except Exception as e:
        item["thumb_bytes"] = 0
        print(f"  thumb fail {item['id']}: {type(e).__name__} {str(e)[:70]}")

with open(os.path.join(SCRATCH, "candidates.json"), "w", encoding="utf-8") as f:
    json.dump(items, f, ensure_ascii=False, indent=1)

ok = [i for i in items if i.get("thumb_bytes")]
print(f"\ncollected={len(items)} thumbs_ok={len(ok)} scratch={SCRATCH}")
for i in ok:
    print(f"{i['id']} | {i['alt'][:90]}")
