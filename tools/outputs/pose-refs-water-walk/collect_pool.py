"""Pool-exit round: bigger knee flexion, like climbing out of a swimming pool.
Same scrape pattern as collect3.py (Pexels search page -> photo ids -> thumbs)."""

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
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs-pool")
THUMBS = os.path.join(SCRATCH, "thumbs")
os.makedirs(THUMBS, exist_ok=True)

QUERIES = [
    "climbing out of swimming pool",
    "getting out of the pool",
    "climb out pool edge",
    "woman climbing out of pool",
    "man getting out of swimming pool",
    "stepping out of swimming pool",
    "emerging from swimming pool",
    "exiting pool water",
]

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
            print(f"    retry {attempt + 1} ({type(e).__name__}: {str(e)[:70]})")
            time.sleep(6 + attempt * 6)
    return ""


def fetch_bytes(url, tries=2):
    for attempt in range(tries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=45) as r:
                return r.read()
        except Exception:
            time.sleep(4 + attempt * 4)
    return None


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
        alt = ""
        for apos, atext in alts:
            if apos > pos:
                break
            alt = atext
        low = alt.lower()
        if any(bad in low for bad in ("logo", "icon", "avatar", "collection")):
            continue
        candidates[pid] = {"alt": alt, "query": q}
        added += 1
        if added >= 10:
            break
    print(f"    +{added} (total {len(candidates)})")

for pid in candidates:
    dest = os.path.join(THUMBS, f"{pid}.jpg")
    if os.path.exists(dest):
        continue
    data = fetch_bytes(
        f"https://images.pexels.com/photos/{pid}/pexels-photo-{pid}.jpeg"
        "?auto=compress&cs=tinysrgb&w=420"
    )
    if data:
        with open(dest, "wb") as f:
            f.write(data)

ok = [p for p in candidates if os.path.exists(os.path.join(THUMBS, f"{p}.jpg"))]
with open(os.path.join(SCRATCH, "candidates.json"), "w", encoding="utf-8") as f:
    json.dump(ok, f, indent=1)
print(f"thumbs ok: {len(ok)}/{len(candidates)}")
