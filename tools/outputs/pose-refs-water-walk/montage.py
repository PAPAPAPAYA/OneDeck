"""Build labelled contact sheets from downloaded thumbnails so the picks can be made visually."""

import glob
import json
import os

from PIL import Image, ImageDraw, ImageFont

SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
THUMBS = os.path.join(SCRATCH, "thumbs")
SHEETS = os.path.join(SCRATCH, "sheets")
os.makedirs(SHEETS, exist_ok=True)

CELL_W, CELL_H = 260, 340
COLS, ROWS = 5, 4
PER_SHEET = COLS * ROWS

files = sorted(glob.glob(os.path.join(THUMBS, "*.jpg")))
print("thumbs:", len(files))

try:
    font = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 22)
except Exception:
    font = ImageFont.load_default()

index = []
for sheet_no in range(0, (len(files) + PER_SHEET - 1) // PER_SHEET):
    chunk = files[sheet_no * PER_SHEET : (sheet_no + 1) * PER_SHEET]
    canvas = Image.new("RGB", (COLS * CELL_W, ROWS * CELL_H + 40), (24, 24, 28))
    draw = ImageDraw.Draw(canvas)
    draw.text((10, 8), f"sheet {sheet_no + 1}", fill=(240, 240, 240), font=font)
    for i, path in enumerate(chunk):
        n = sheet_no * PER_SHEET + i
        r, c = divmod(i, COLS)
        x, y = c * CELL_W, 40 + r * CELL_H
        try:
            im = Image.open(path).convert("RGB")
        except Exception:
            continue
        w, h = im.size
        scale = min(CELL_W / w, (CELL_H - 34) / h)
        im = im.resize((int(w * scale), int(h * scale)), Image.LANCZOS)
        canvas.paste(im, (x + (CELL_W - im.width) // 2, y))
        pid = os.path.splitext(os.path.basename(path))[0]
        draw.rectangle([x, y, x + CELL_W - 1, y + CELL_H - 1], outline=(70, 70, 80))
        draw.text((x + 6, y + CELL_H - 30), f"[{n}] {pid}", fill=(255, 220, 120), font=font)
        index.append({"n": n, "id": pid})
    out = os.path.join(SHEETS, f"sheet{sheet_no + 1}.jpg")
    canvas.save(out, quality=88)
    print("wrote", out)

with open(os.path.join(SCRATCH, "index.json"), "w", encoding="utf-8") as f:
    json.dump(index, f, indent=1)
