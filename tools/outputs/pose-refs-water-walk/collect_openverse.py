"""Openverse + Wikimedia Commons collection for pool-exit poses.
Both are open APIs (no key); follows the 10-13 public-domain retrieval precedent."""

import json
import os
import time
import urllib.parse
import urllib.request

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/126.0 Safari/537.36")
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs-pool")
OPEN = os.path.join(SCRATCH, "open")
WIKI = os.path.join(SCRATCH, "wiki")
for d in (OPEN, WIKI):
    os.makedirs(d, exist_ok=True)

OV_QUERIES = [
    "climbing out of swimming pool",
    "getting out of swimming pool",
    "climbing out of pool",
    "woman climbing pool ladder",
    "emerging from swimming pool",
]
WIKI_QUERIES = [
    "climbing out of swimming pool",
    "getting out of pool",
    "swimming pool ladder climb",
    "exiting swimming pool",
]


def get_json(url, tries=3):
    for a in range(tries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "application/json"})
            with urllib.request.urlopen(req, timeout=45) as r:
                return json.loads(r.read().decode("utf-8", "ignore"))
        except Exception as e:
            print("  retry", a + 1, type(e).__name__, str(e)[:60])
            time.sleep(5 + a * 5)
    return None


def fetch_bytes(url, tries=2):
    for a in range(tries):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.read()
        except Exception:
            time.sleep(4 + a * 4)
    return None


# --- Openverse (cc0 + pdm only, per retrieval precedent) ---
ov = {}
for q in OV_QUERIES:
    u = ("https://api.openverse.org/v1/images/?q=" + urllib.parse.quote(q)
         + "&license=cc0,pdm&page_size=20&mature=false")
    data = get_json(u)
    if not data:
        continue
    for res in data.get("results", []):
        pid = res.get("id", "")[:8]
        if not pid or pid in ov:
            continue
        ov[pid] = {
            "url": res.get("url"), "thumb": res.get("thumbnail"),
            "title": (res.get("title") or "")[:90], "license": res.get("license"),
            "creator": res.get("creator"), "foreign_landing_url": res.get("foreign_landing_url"),
            "query": q,
        }
    print("ov", q, len(data.get("results", [])), "->", len(ov))
    time.sleep(3)

for pid, m in ov.items():
    dest = os.path.join(OPEN, pid + ".jpg")
    if os.path.exists(dest):
        continue
    src = m.get("thumb") or m.get("url")
    if not src:
        continue
    data = fetch_bytes(src)
    if data:
        with open(dest, "wb") as f:
            f.write(data)
ok_open = [p for p in ov if os.path.exists(os.path.join(OPEN, p + ".jpg"))]

# --- Wikimedia Commons ---
wk = {}
for q in WIKI_QUERIES:
    u = ("https://commons.wikimedia.org/w/api.php?action=query&generator=search"
         "&gsrsearch=" + urllib.parse.quote(q)
         + "&gsrnamespace=6&gsrlimit=20&prop=imageinfo&iiprop=url|extmetadata"
         "&iiurlwidth=480&format=json")
    data = get_json(u)
    if not data:
        continue
    pages = (data.get("query") or {}).get("pages") or {}
    for p in pages.values():
        title = p.get("title", "")
        if not title.lower().endswith((".jpg", ".jpeg", ".png")):
            continue
        pid = "w" + str(abs(hash(title)) % 10**8)
        if pid in wk:
            continue
        infos = p.get("imageinfo") or [{}]
        ii = infos[0]
        wk[pid] = {
            "url": ii.get("url"), "thumb": (ii.get("thumburl") or ii.get("url")),
            "title": title[:100], "descriptionurl": ii.get("descriptionurl"),
            "query": q,
        }
    print("wiki", q, len(pages), "->", len(wk))
    time.sleep(2)

for pid, m in wk.items():
    dest = os.path.join(WIKI, pid + ".jpg")
    if os.path.exists(dest):
        continue
    src = m.get("thumb") or m.get("url")
    if not src:
        continue
    data = fetch_bytes(src)
    if data:
        with open(dest, "wb") as f:
            f.write(data)
ok_wiki = [p for p in wk if os.path.exists(os.path.join(WIKI, p + ".jpg"))]

with open(os.path.join(SCRATCH, "open_meta.json"), "w", encoding="utf-8") as f:
    json.dump(ov, f, ensure_ascii=False, indent=1)
with open(os.path.join(SCRATCH, "wiki_meta.json"), "w", encoding="utf-8") as f:
    json.dump(wk, f, ensure_ascii=False, indent=1)
print("openverse ok:", len(ok_open), "/", len(ov))
print("wikimedia ok:", len(ok_wiki), "/", len(wk))
