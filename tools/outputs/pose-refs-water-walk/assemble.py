"""Assemble the final reference folder inside the workspace and fetch photographer credits."""

import json
import os
import re
import shutil
import urllib.request

from PIL import Image, ImageDraw, ImageFont

UA = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/126.0 Safari/537.36"
)
SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
FINALS = os.path.join(SCRATCH, "finals")
FIGS = os.path.join(SCRATCH, "figures")
TARGET = r"D:\Unity Projects\OneDeck\pose_refs_water_walk"
os.makedirs(TARGET, exist_ok=True)

PHOTOS = [
    ("1705518", "01_walking_out_front.jpg",
     "front 3/4, walking out of the surf, splash around the shins"),
    ("36714514", "02_side_knee_deep.jpg",
     "profile, knee-deep, weight on the front leg, head down"),
    ("15533661", "03_silhouette_stride.jpg",
     "hard silhouette, side stride, the clearest bent-knee read"),
    ("29251256", "04_thigh_deep_front.jpg",
     "thigh-deep, front view, arms out for balance"),
    ("12586100", "05_legs_closeup.jpg",
     "leg close-up, shin and ankle in moving water"),
    ("13559054", "06_silhouette_back.jpg",
     "back view silhouette, exiting towards wet sand"),
    ("14519333", "07_side_with_board.jpg",
     "side view with a board, shallow water, long shadow"),
]

FIGS_MAP = [
    ("fig_0_three_views.png", "10_fig_three_views.png"),
    ("fig_1_side.png", "11_fig_side.png"),
    ("fig_2_34_front.png", "12_fig_three_quarter.png"),
    ("fig_3_front.png", "13_fig_front.png"),
    ("fig_4_sequence.png", "14_fig_sequence.png"),
]

meta = json.load(open(os.path.join(SCRATCH, "finals_meta.json"), encoding="utf-8"))


def credit(pid):
    try:
        req = urllib.request.Request(
            f"https://www.pexels.com/photo/{pid}/", headers={"User-Agent": UA}
        )
        with urllib.request.urlopen(req, timeout=20) as r:
            html = r.read().decode("utf-8", "ignore")
        m = re.search(r'"photographer"\s*:\s*\{[^}]*?"name"\s*:\s*"([^"]{2,60})"', html)
        if m:
            return m.group(1)
        m = re.search(r'<a[^>]+href="/@([a-zA-Z0-9\-]+)"', html)
        return "@" + m.group(1) if m else ""
    except Exception as e:
        print("credit fail", pid, type(e).__name__)
        return ""


rows = []
for pid, name, note in PHOTOS:
    src = os.path.join(FINALS, f"{pid}.jpg")
    if not os.path.exists(src):
        print("missing", src)
        continue
    shutil.copyfile(src, os.path.join(TARGET, name))
    rows.append({"file": name, "pid": pid, "note": note, "credit": credit(pid),
                 "page": f"https://www.pexels.com/photo/{pid}/"})
    print("copied", name)

for src_name, dst_name in FIGS_MAP:
    src = os.path.join(FIGS, src_name)
    if os.path.exists(src):
        shutil.copyfile(src, os.path.join(TARGET, dst_name))
        print("copied", dst_name)

with open(os.path.join(SCRATCH, "rows.json"), "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)

# overview grid: 7 photos, 4 + 3 layout
CELL = 520
COLS, ROWS = 4, 2
canvas = Image.new("RGB", (COLS * CELL, ROWS * (CELL + 48) + 70), (18, 18, 22))
d = ImageDraw.Draw(canvas)
try:
    f_title = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 34)
    f_cap = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 19)
except Exception:
    f_title = f_cap = ImageFont.load_default()
d.text((24, 18), "walking out of water - photo shortlist", font=f_title, fill=(238, 240, 246))
for i, row in enumerate(rows):
    r, c = divmod(i, COLS)
    x, y = c * CELL, 70 + r * (CELL + 48)
    with Image.open(os.path.join(TARGET, row["file"])) as im:
        im = im.convert("RGB")
        w, h = im.size
        s = min((CELL - 12) / w, (CELL - 12) / h)
        im = im.resize((int(w * s), int(h * s)), Image.LANCZOS)
    canvas.paste(im, (x + (CELL - im.width) // 2, y))
    d.text((x + 8, y + CELL + 4), row["file"], fill=(255, 220, 120), font=f_cap)
    d.text((x + 8, y + CELL + 26), row["note"][:64], fill=(180, 190, 205), font=f_cap)

overview = os.path.join(TARGET, "00_overview.jpg")
canvas.save(overview, quality=90)
print("wrote", overview)
print("folder:", TARGET)
