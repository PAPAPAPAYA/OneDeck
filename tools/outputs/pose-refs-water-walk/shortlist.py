"""Download the shortlisted photos at review size and build one verification grid."""

import json
import os
import re
import urllib.request

from PIL import Image, ImageDraw, ImageFont

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
FINALS = os.path.join(SCRATCH, "finals")
os.makedirs(FINALS, exist_ok=True)

SHORTLIST = [
    "31016389", "36714514", "15533661", "31633278", "12586100",
    "26625398", "29251256", "13559054", "1705518", "14519333",
]


def get(url, timeout=45):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


meta = {}
for pid in SHORTLIST:
    dest = os.path.join(FINALS, f"{pid}.jpg")
    try:
        data = get(
            f"https://images.pexels.com/photos/{pid}/pexels-photo-{pid}.jpeg"
            "?auto=compress&cs=tinysrgb&w=1400"
        )
        with open(dest, "wb") as f:
            f.write(data)
        with Image.open(dest) as im:
            meta[pid] = {"size": im.size, "bytes": len(data)}
        print(f"ok {pid} {im.size}")
    except Exception as e:
        print(f"fail {pid}: {type(e).__name__} {str(e)[:60]}")

with open(os.path.join(SCRATCH, "finals_meta.json"), "w", encoding="utf-8") as f:
    json.dump(meta, f, indent=1)

# verification grid 5 x 2
CELL = 460
COLS, ROWS = 5, 2
canvas = Image.new("RGB", (COLS * CELL, ROWS * (CELL + 30)), (22, 22, 26))
d = ImageDraw.Draw(canvas)
try:
    font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 20)
except Exception:
    font = ImageFont.load_default()
for i, pid in enumerate(SHORTLIST):
    path = os.path.join(FINALS, f"{pid}.jpg")
    if not os.path.exists(path):
        continue
    r, c = divmod(i, COLS)
    x, y = c * CELL, r * (CELL + 30)
    with Image.open(path) as im:
        im = im.convert("RGB")
        w, h = im.size
        s = min((CELL - 8) / w, (CELL - 8) / h)
        im = im.resize((int(w * s), int(h * s)), Image.LANCZOS)
    canvas.paste(im, (x + (CELL - im.width) // 2, y + (CELL - im.height) // 2))
    d.text((x + 6, y + CELL + 2), f"{pid}", fill=(255, 220, 120), font=font)

out = os.path.join(SCRATCH, "verify_grid.jpg")
canvas.save(out, quality=90)
print("grid:", out)
