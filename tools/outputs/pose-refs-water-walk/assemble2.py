"""Add the bigger-knee-bend (pool-exit / run-out) series to the reference folder."""

import os
import shutil

from PIL import Image, ImageDraw, ImageFont

SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs")
FINALS = os.path.join(SCRATCH, "finals2")
TARGET = r"D:\Unity Projects\OneDeck\pose_refs_water_walk"

PICKS = [
    ("38626500", "20_run_out_front.jpg", "running out, front, knee lifted to hip height"),
    ("19299065", "21_run_exit_three_quarter.jpg", "three-quarter view, front knee lifted, splash at the shin"),
    ("19299123", "22_run_exit_front_splash.jpg", "front, tall knee lift, heavy splash"),
    ("19299278", "23_run_exit_splash.jpg", "front, wetsuit, water sheeting off the legs"),
    ("2995478", "24_run_out_knee_up.jpg", "older runner, relaxed high knee"),
    ("2968388", "25_foot_strike_detail.jpg", "foot plant close-up, splash ring"),
    ("14187611", "26_silhouette_splash.jpg", "backlit silhouette, splash halo"),
]

rows = []
for pid, name, note in PICKS:
    src = os.path.join(FINALS, f"{pid}.jpg")
    if not os.path.exists(src):
        print("missing", pid)
        continue
    shutil.copyfile(src, os.path.join(TARGET, name))
    rows.append((name, note))
    print("copied", name)

CELL = 500
COLS = 4
canvas_rows = (len(rows) + COLS - 1) // COLS
canvas = Image.new("RGB", (COLS * CELL, canvas_rows * (CELL + 46) + 64), (18, 18, 22))
d = ImageDraw.Draw(canvas)
f_title = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 32)
f_cap = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
d.text((22, 16), "bigger knee bend - running / pool-style exit", font=f_title,
       fill=(238, 240, 246))
for i, (name, note) in enumerate(rows):
    r, c = divmod(i, COLS)
    x, y = c * CELL, 64 + r * (CELL + 46)
    with Image.open(os.path.join(TARGET, name)) as im:
        im = im.convert("RGB")
        w, h = im.size
        s = min((CELL - 10) / w, (CELL - 10) / h)
        im = im.resize((int(w * s), int(h * s)), Image.LANCZOS)
    canvas.paste(im, (x + (CELL - im.width) // 2, y))
    d.text((x + 8, y + CELL + 4), name, fill=(255, 220, 120), font=f_cap)
    d.text((x + 8, y + CELL + 24), note[:62], fill=(180, 190, 205), font=f_cap)

out = os.path.join(TARGET, "20_overview_run_exit.jpg")
canvas.save(out, quality=90)
print("wrote", out)
