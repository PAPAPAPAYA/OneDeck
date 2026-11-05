"""Assemble the pool-exit reference folder inside pose_refs_water_walk/."""

import json
import os
import shutil

from PIL import Image, ImageDraw, ImageFont

SCRATCH = os.path.join(os.environ["TEMP"], "pose-refs-pool")
FINALS = os.path.join(SCRATCH, "finals")
FIGS = os.path.join(SCRATCH, "figures")
TARGET = r"D:\Unity Projects\OneDeck\pose_refs_water_walk\pool_exit"
os.makedirs(TARGET, exist_ok=True)

PHOTOS = [
    ("6011941", "01_ladder_climb_knee_up_bw.jpg",
     "b/w front, knee lifted over the rail - the clearest deep-flexion read"),
    ("6011948", "02_ladder_climb_side_bw.jpg",
     "b/w, side-ish, knee bent with thigh breaking the surface"),
    ("6011943", "03_ladder_front_both_rails.jpg",
     "color front, both hands on the rails, torso emerging"),
    ("6011940", "04_ladder_34_from_deck.jpg",
     "color 3/4 from the deck side, wet tiles in the foreground"),
    ("6012014", "05_pushup_exit_male_color.jpg",
     "male push-up exit, arms locked, torso high above the edge"),
    ("6012021", "06_pushup_exit_male_profile_bw.jpg",
     "b/w push-up exit profile, water sheeting off"),
    ("7222167", "07_ladder_elderly_red.jpg",
     "older swimmer in red, ladder climb, distinct body type"),
    ("40139108", "08_ladder_outdoor_day.jpg",
     "outdoor daylight context, climbing between the rails"),
    ("14464900", "09_ladder_dark_mood.jpg",
     "dark moody ladder climb, face lit from above"),
    ("7041584", "10_ladder_snow_steam.jpg",
     "snowfall + steam atmosphere on the ladder"),
]
FIGS_MAP = [
    ("pool_fig_0_three_views.png", "20_fig_three_views.png"),
    ("pool_fig_1_side.png", "21_fig_side_annotated.png"),
    ("pool_fig_2_34_front.png", "22_fig_three_quarter.png"),
    ("pool_fig_3_front.png", "23_fig_front.png"),
    ("pool_fig_4_sequence.png", "24_fig_sequence.png"),
    ("pool_fig_5_ab_compare.png", "25_fig_knee_depth_ab.png"),
]

for pid, name, _ in PHOTOS:
    src = os.path.join(FINALS, pid + ".jpg")
    if os.path.exists(src):
        shutil.copyfile(src, os.path.join(TARGET, name))
        print("copied", name)
    else:
        print("missing", pid)

for src_name, dst_name in FIGS_MAP:
    src = os.path.join(FIGS, src_name)
    if os.path.exists(src):
        shutil.copyfile(src, os.path.join(TARGET, dst_name))
        print("copied", dst_name)

# overview grid: 10 photos, 5 x 2, with captions
CELL, COLS = 520, 5
try:
    f_title = ImageFont.truetype("C:/Windows/Fonts/arialbd.ttf", 34)
    f_cap = ImageFont.truetype("C:/Windows/Fonts/arial.ttf", 18)
except Exception:
    f_title = f_cap = ImageFont.load_default()
rows = 2
canvas = Image.new("RGB", (COLS * CELL, rows * (CELL + 44) + 70), (18, 18, 22))
d = ImageDraw.Draw(canvas)
d.text((24, 18), "pool exit - deep knee flexion reference set", font=f_title,
       fill=(238, 240, 246))
for i, (pid, name, note) in enumerate(PHOTOS):
    r, c = divmod(i, COLS)
    x, y = c * CELL, 70 + r * (CELL + 44)
    p = os.path.join(TARGET, name)
    if os.path.exists(p):
        with Image.open(p) as im:
            im = im.convert("RGB")
            w, h = im.size
            s = min((CELL - 12) / w, (CELL - 12) / h)
            im = im.resize((int(w * s), int(h * s)), Image.LANCZOS)
        canvas.paste(im, (x + (CELL - im.width) // 2, y))
    d.text((x + 8, y + CELL + 2), name, fill=(255, 220, 120), font=f_cap)
    d.text((x + 8, y + CELL + 24), note[:66], fill=(180, 190, 205), font=f_cap)
canvas.save(os.path.join(TARGET, "00_overview.jpg"), quality=90)
print("wrote 00_overview.jpg")

# README with CRLF endings
L = []
add = L.append
add("# Pool Exit - Deep Knee Flexion Pose References")
add("")
add("2026-10-31. Companion set to the beach walk-out folder (parent directory):")
add("same brief (walking out of water, bent knee) but with the LARGER knee angle")
add("of a swimming-pool exit - climbing the ladder or pressing up over the edge.")
add("")
add("- `00_overview.jpg` - all ten photos on one sheet (start here)")
add("- `01`-`10` - photo references, ordered by how well the deep knee bend reads")
add("- `20`-`25` - generated OpenPose-style diagrams with a deck edge + waterline")
add("")
add("All photos from Pexels (free to use, no attribution required); page links at the end.")
add("")
add("## Key angles (from `21_fig_side_annotated.png`)")
add("")
add("| Part | Pool exit | vs beach walk-out |")
add("|---|---|---|")
add("| Front (support) knee | interior ~94 deg, flexion ~86 deg | interior ~120 deg, flexion ~60 deg |")
add("| Front thigh | nearly horizontal, foot clears the deck edge | angled down at ~45 deg |")
add("| Rear leg | pushing off the pool floor, near straight | heel lifted off the sand |")
add("| Hips | low - sitting almost at waterline | ~8 cm below standing |")
add("| Torso | leaning forward over the front thigh | ~20 deg |")
add("")
add("`25_fig_knee_depth_ab.png` shows the 60 deg vs 86 deg flexion side by side;")
add("`24_fig_sequence.png` is the two-beat climb: knee over the edge -> foot plants,")
add("hips drive up.")
add("")
add("## How to use the photos")
add("")
add("| File | What it gives you |")
add("|---|---|")
for pid, name, note in PHOTOS:
    add(f"| `{name}` | {note} |")
add("")
add("Prompt add-on for the deeper angle (append to the beach-set prompts):")
add("")
add("```")
add("climbing out of a swimming pool, front knee bent over 85 degrees, thigh")
add("horizontal, one hand gripping the pool ladder, water streaming off the leg,")
add("deck edge at hip level")
add("```")
add("")
add("Chinese models:")
add("")
add("```")
add("\u4ece\u6cf3\u6c60\u91cc\u722c\u51fa\u6c34\u9762\uff0c\u524d\u819d\u5f2f\u66f2\u8d85\u8fc785\u5ea6\uff0c\u5927\u817f\u62ac\u5e73\uff0c")
add("\u4e00\u624b\u628a\u4f4f\u6cf3\u6c60\u6276\u624b\uff0c\u6c34\u4ece\u817f\u4e0a\u6d41\u4e0b\uff0c\u6c60\u6cbf\u5728\u9ad8\u9ac0\u4f4d\u7f6e")
add("```")
add("")
add("## Sources (Pexels)")
add("")
for pid, name, _ in PHOTOS:
    add(f"- {name} https://www.pexels.com/photo/{pid}/")
add("")
add("Diagrams generated by `tools/outputs/pose-refs-water-walk/figures_pool.py`.")
readme = "\r\n".join(L) + "\r\n"
with open(os.path.join(TARGET, "README.md"), "wb") as f:
    f.write(readme.encode("utf-8"))
print("wrote README.md")
print("folder:", TARGET)
