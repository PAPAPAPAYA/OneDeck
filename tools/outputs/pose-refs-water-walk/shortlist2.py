"""Verify the pool-exit / running-exit shortlist at 1400px and build a review grid."""

import os
import urllib.request

from PIL import Image, ImageDraw, ImageFont

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
FINALS = os.path.join(SCRATCH, "finals2")
os.makedirs(FINALS, exist_ok=True)

IDS = [
    "38626500", "18436621", "19298750", "19299123",
    "2995478", "14187611", "2968388", "19299278",
]

for pid in IDS:
    dest = os.path.join(FINALS, f"{pid}.jpg")
    if os.path.exists(dest) and os.path.getsize(dest) > 50000:
        print("cached", pid)
        continue
    try:
        req = urllib.request.Request(
            f"https://images.pexels.com/photos/{pid}/pexels-photo-{pid}.jpeg"
            "?auto=compress&cs=tinysrgb&w=1400",
            headers={"User-Agent": UA},
        )
        with urllib.request.urlopen(req, timeout=40) as r:
            data = r.read()
        with open(dest, "wb") as f:
            f.write(data)
        print("ok", pid, len(data))
    except Exception as e:
        print("fail", pid, type(e).__name__, str(e)[:60])

CELL = 470
COLS, ROWS = 4, 2
canvas = Image.new("RGB", (COLS * CELL, ROWS * (CELL + 28)), (22, 22, 26))
d = ImageDraw.Draw(canvas)
font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 20)
for i, pid in enumerate(IDS):
    path = os.path.join(FINALS, f"{pid}.jpg")
    if not os.path.exists(path):
        continue
    r, c = divmod(i, COLS)
    x, y = c * CELL, r * (CELL + 28)
    with Image.open(path) as im:
        im = im.convert("RGB")
        w, h = im.size
        s = min((CELL - 8) / w, (CELL - 8) / h)
        im = im.resize((int(w * s), int(h * s)), Image.LANCZOS)
    canvas.paste(im, (x + (CELL - im.width) // 2, y + (CELL - im.height) // 2))
    d.text((x + 6, y + CELL + 2), pid, fill=(255, 220, 120), font=font)

out = os.path.join(SCRATCH, "verify2.jpg")
canvas.save(out, quality=90)
print("grid:", out)
