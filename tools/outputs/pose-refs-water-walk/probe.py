import json
import urllib.parse
import urllib.request

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)


def head(url):
    req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "*/*"})
    try:
        with urllib.request.urlopen(req, timeout=25) as r:
            body = r.read(4000)
            return r.status, r.headers.get("content-type"), len(body), body[:200]
    except Exception as e:
        return "ERR", type(e).__name__, str(e)[:160], b""


def get_json(url):
    req = urllib.request.Request(
        url, headers={"User-Agent": UA, "Accept": "application/json"}
    )
    with urllib.request.urlopen(req, timeout=25) as r:
        return json.loads(r.read())


targets = {
    "openverse": "https://api.openverse.org/v1/images/?"
    + urllib.parse.urlencode({"q": "walking out of the sea", "page_size": 3}),
    "wikimedia": "https://commons.wikimedia.org/w/api.php?"
    + urllib.parse.urlencode(
        {
            "action": "query",
            "format": "json",
            "generator": "search",
            "gsrsearch": "walking out of the sea",
            "gsrnamespace": "6",
            "gsrlimit": "3",
            "prop": "imageinfo",
            "iiprop": "url|size",
            "iiurlwidth": "400",
        }
    ),
    "pexels_html": "https://www.pexels.com/search/walking%20out%20of%20water/",
    "unsplash_html": "https://unsplash.com/s/photos/walking-out-of-water",
    "pixabay_html": "https://pixabay.com/images/search/walking%20out%20of%20water/",
}

for name, url in targets.items():
    status, ctype, size, head_bytes = head(url)
    print(f"{name:14s} status={status} type={ctype} bytes={size}")
    if status == 200 and name in ("pexels_html", "unsplash_html", "pixabay_html"):
        text = head_bytes.decode("utf-8", "ignore")
        print("   head:", text[:160].replace("\n", " "))
