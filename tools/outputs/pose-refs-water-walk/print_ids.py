"""Build a contact sheet for an explicit id list (pool-exit shortlist)."""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
THUMBS = os.path.join(SCRATCH, "thumbs4")
SHEETS = os.path.join(SCRATCH, "sheets")
os.makedirs(SHEETS, exist_ok=True)

ids = [line.strip() for line in sys.argv[1].split(",") if line.strip()]
tag = sys.argv[2] if len(sys.argv) > 2 else "pick"

CELL = 420
COLS = 4
PER = 12
try:
    font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 19)
except Exception:
    font = ImageFont.load_default()

for s in range(0, (len(ids) + PER - 1) // PER):
    chunk = ids[s * PER : (s + 1) * PER]
    rows = (len(chunk) + COLS - 1) // COLS
    canvas = Image.new("RGB", (COLS * CELL, rows * (CELL + 26) + 34), (22, 22, 26))
    d = ImageDraw.Draw(canvas)
    d.text((10, 6), f"{tag} sheet {s + 1}", fill=(240, 240, 240), font=font)
    for i, pid in enumerate(chunk):
        path = os.path.join(THUMBS, f"{pid}.jpg")
        if not os.path.exists(path):
            continue
        r, c = divmod(i, COLS)
        x, y = c * CELL, 34 + r * (CELL + 26)
        with Image.open(path) as im:
            im = im.convert("RGB")
            w, h = im.size
            scale = min((CELL - 8) / w, (CELL - 8) / h)
            im = im.resize((int(w * scale), int(h * scale)), Image.LANCZOS)
        canvas.paste(im, (x + (CELL - im.width) // 2, y))
        d.text((x + 6, y + CELL + 2), pid, fill=(255, 220, 120), font=font)
    out = os.path.join(SHEETS, f"{tag}_{s + 1}.jpg")
    canvas.save(out, quality=90)
    print("wrote", out)
