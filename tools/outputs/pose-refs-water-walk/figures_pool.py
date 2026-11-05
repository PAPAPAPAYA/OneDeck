"""Pool-exit pose diagrams: deck edge + water, knee-up step, deep flexion (~90deg interior).

Harness copied from figures.py; adds a pool wall / deck slab so the figures read as
"climbing out of a pool" instead of "walking out of the sea". WATER stays at 0.40 m,
deck top at 0.44 m, wall plane at z = 0.30 m (the figure straddles it mid-exit)."""

import math
import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.environ["TEMP"], "pose-refs-pool", "figures")
os.makedirs(OUT, exist_ok=True)

SS = 3
W, H = 820, 1120
SCALE = 470.0
WATER_Y = 0.40
DECK_TOP = 0.44
WALL_Z = 0.30
GROUND_PX = 0.945

BONES = [
    ("neck", "r_shoulder"), ("neck", "l_shoulder"),
    ("r_shoulder", "r_elbow"), ("r_elbow", "r_wrist"),
    ("l_shoulder", "l_elbow"), ("l_elbow", "l_wrist"),
    ("neck", "pelvis"), ("pelvis", "r_hip"), ("pelvis", "l_hip"),
    ("r_hip", "r_knee"), ("r_knee", "r_ankle"),
    ("l_hip", "l_knee"), ("l_knee", "l_ankle"),
]
RIGHT_BONES = {("r_shoulder", "r_elbow"), ("r_elbow", "r_wrist"),
               ("r_hip", "r_knee"), ("r_knee", "r_ankle")}
LEFT_BONES = {("l_shoulder", "l_elbow"), ("l_elbow", "l_wrist"),
              ("l_hip", "l_knee"), ("l_knee", "l_ankle")}

COL_TORSO = (238, 240, 246)
COL_RIGHT = (86, 202, 222)
COL_LEFT = (246, 164, 88)
COL_JOINT = (255, 255, 255)
COL_OUTLINE = (24, 26, 34)


def pose(kind):
    if kind == "contact":
        # beach walk-out baseline for the A/B strip (front knee ~120deg interior)
        p = {
            "pelvis": (0.00, 0.80, 0.00), "neck": (0.00, 1.36, 0.20),
            "head": (0.00, 1.49, 0.26),
            "r_shoulder": (0.19, 1.33, 0.20), "l_shoulder": (-0.19, 1.35, 0.16),
            "r_hip": (0.10, 0.80, 0.00), "l_hip": (-0.10, 0.80, 0.02),
            "r_elbow": (0.27, 1.05, -0.04), "r_wrist": (0.25, 0.80, -0.16),
            "l_elbow": (-0.29, 1.06, 0.34), "l_wrist": (-0.26, 0.82, 0.50),
            "r_knee": (0.15, 0.45, 0.30), "r_ankle": (0.13, 0.05, 0.16),
            "l_knee": (-0.14, 0.44, -0.28), "l_ankle": (-0.12, 0.03, -0.50),
        }
        return p
    if kind == "knee_up":
        # stepping up onto the deck: front thigh horizontal, shin hanging, ~94deg interior
        return {
            "pelvis": (0.00, 0.95, 0.05), "neck": (0.00, 1.48, 0.24),
            "head": (0.00, 1.61, 0.30),
            "r_shoulder": (0.19, 1.45, 0.24), "l_shoulder": (-0.19, 1.47, 0.20),
            "r_hip": (0.10, 0.95, 0.03), "l_hip": (-0.10, 0.95, 0.03),
            "r_knee": (0.16, 0.86, 0.42), "r_ankle": (0.13, 0.46, 0.36),
            "l_knee": (-0.13, 0.53, -0.06), "l_ankle": (-0.15, 0.02, -0.12),
            "r_elbow": (0.27, 1.16, 0.34), "r_wrist": (0.29, 0.92, 0.52),
            "l_elbow": (-0.28, 1.16, 0.02), "l_wrist": (-0.26, 0.90, -0.12),
        }
    if kind == "deck":
        # front foot planted on the deck in a deep lunge, rear leg pushing off the floor
        return {
            "pelvis": (0.00, 0.88, 0.06), "neck": (0.00, 1.42, 0.26),
            "head": (0.00, 1.54, 0.32),
            "r_shoulder": (0.19, 1.39, 0.26), "l_shoulder": (-0.19, 1.41, 0.22),
            "r_hip": (0.10, 0.88, 0.04), "l_hip": (-0.10, 0.88, 0.02),
            "r_knee": (0.16, 0.90, 0.42), "r_ankle": (0.13, 0.44, 0.44),
            "l_knee": (-0.14, 0.50, -0.14), "l_ankle": (-0.12, 0.06, -0.46),
            "r_elbow": (0.28, 1.12, 0.36), "r_wrist": (0.30, 0.88, 0.54),
            "l_elbow": (-0.29, 1.10, 0.02), "l_wrist": (-0.27, 0.84, 0.10),
        }
    raise ValueError(kind)


def project(pt, yaw, cx, cy):
    x, y, z = pt
    a = math.radians(yaw)
    xr = x * math.cos(a) - z * math.sin(a)
    return (cx + xr * SCALE * SS, cy - y * SCALE * SS)


def capsule(draw, a, b, width, fill):
    draw.line([a, b], fill=fill, width=int(width * SS), joint="curve")
    r = width * SS / 2
    for p in (a, b):
        draw.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=fill)


def angle3(a, b, c):
    v1 = [a[i] - b[i] for i in range(3)]
    v2 = [c[i] - b[i] for i in range(3)]
    n1 = math.sqrt(sum(v * v for v in v1))
    n2 = math.sqrt(sum(v * v for v in v2))
    cos = sum(v1[i] * v2[i] for i in range(3)) / (n1 * n2)
    return math.degrees(math.acos(max(-1.0, min(1.0, cos))))


def font(name, size):
    try:
        return ImageFont.truetype(f"C:/Windows/Fonts/{name}", int(size * SS))
    except Exception:
        return ImageFont.load_default()


F_TITLE = font("arialbd.ttf", 25)
F_SUB = font("arial.ttf", 19)
F_NOTE = font("arial.ttf", 18)


def render(yaw, kind, title, subtitle, note=None, arrow_text="onto the deck"):
    img = Image.new("RGB", (W * SS, H * SS), (16, 18, 24))
    d = ImageDraw.Draw(img, "RGBA")
    cx, cy = W * SS / 2, H * SS * GROUND_PX

    a = math.radians(yaw)
    front_mode = abs(math.sin(a)) < 0.2  # looking dead-on: deck runs across the frame
    y_deck = cy - DECK_TOP * SCALE * SS
    y_water = cy - WATER_Y * SCALE * SS

    if front_mode:
        x_deck_left, x_deck_right = 0, W * SS
    else:
        x_deck = cx - WALL_Z * math.sin(a) * SCALE * SS
        x_deck_left, x_deck_right = 0, x_deck  # deck side is screen-left of the wall

    # background: air above waterline, water right of the wall, dark under-deck left
    d.rectangle([0, 0, W * SS, y_water], fill=(16, 18, 24))
    d.rectangle([x_deck_right, y_water, W * SS, H * SS], fill=(28, 58, 78))
    d.rectangle([x_deck_left, y_water, x_deck_right, H * SS], fill=(20, 38, 50))

    # deck slab + tile edge
    d.rectangle([0, y_deck, x_deck_right if front_mode else x_deck,
                 y_deck + 26 * SS], fill=(206, 210, 219))
    d.line([0, y_deck, x_deck_right if front_mode else x_deck, y_deck],
           fill=(242, 244, 249), width=3 * SS)

    pts = pose(kind)
    proj = {k: project(v, yaw, cx, cy) for k, v in pts.items()}

    # ripples + splash, only in the water region
    for i in range(3):
        rx = (0.42 + 0.32 * i) * SCALE * SS
        ry = (0.05 + 0.018 * i) * SCALE * SS
        d.arc([cx - rx, y_water - ry, cx + rx, y_water + ry],
              start=195, end=345, fill=(150, 214, 238, 160 - 40 * i), width=2 * SS)
    for dx, dy in ((0.9, 0.34), (1.0, 0.56), (0.84, 0.66), (0.72, 0.44)):
        px = cx + dx * SCALE * SS * 0.5
        py = y_water - dy * SCALE * SS * 0.18
        d.ellipse([px, py, px + 7 * SS, py + 7 * SS], fill=(202, 236, 250, 190))

    sil = (132, 140, 156, 34)
    capsule(d, proj["neck"], proj["pelvis"], 62, sil)
    for a_key, b_key in BONES:
        capsule(d, proj[a_key], proj[b_key], 24, sil)
    for a_key, b_key in BONES:
        key = (a_key, b_key)
        if key in RIGHT_BONES:
            colour, width = COL_RIGHT, 6
        elif key in LEFT_BONES:
            colour, width = COL_LEFT, 6
        else:
            colour, width = COL_TORSO, 7
        capsule(d, proj[a_key], proj[b_key], width, colour + (255,))

    hx, hy = proj["head"]
    hr = 0.104 * SCALE * SS
    d.line([proj["neck"], (hx, hy + hr * 0.55)], fill=COL_TORSO + (255,), width=7 * SS)
    d.ellipse([hx - hr, hy - hr, hx + hr, hy + hr], fill=(30, 33, 42),
              outline=(255, 122, 122), width=5 * SS)
    for k, p in proj.items():
        if k == "head":
            continue
        r = (10 if k in ("r_knee", "l_knee") else 8) * SS
        d.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=COL_JOINT,
                  outline=COL_OUTLINE, width=2 * SS)

    # travel arrow (side / 3-4 views only)
    dx_dir = -math.sin(a)
    if abs(dx_dir) > 0.2:
        span = 90 * SS * (1 if dx_dir > 0 else -1)
        ax = W * SS * 0.5 - span * 0.9
        ay = y_water - 46 * SS
        d.line([(ax, ay), (ax + span, ay)], fill=(198, 208, 222, 200), width=3 * SS)
        d.polygon([(ax + span + (10 * SS if span > 0 else -10 * SS), ay),
                   (ax + span, ay - 9 * SS), (ax + span, ay + 9 * SS)],
                  fill=(198, 208, 222, 200))
        d.text((min(ax, ax + span), ay - 30 * SS), arrow_text, font=F_NOTE,
               fill=(198, 208, 222))

    # water veil, kept off the deck side
    veil = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(veil).rectangle([x_deck_right, y_water, W * SS, H * SS],
                                   fill=(34, 82, 108, 118))
    img = Image.alpha_composite(img.convert("RGBA"), veil).convert("RGB")
    d = ImageDraw.Draw(img, "RGBA")
    d.line([x_deck_right, y_water, W * SS, y_water], fill=(146, 208, 236), width=3 * SS)
    d.text((W * SS - 175 * SS, y_water + 12 * SS), "waterline", font=F_NOTE,
           fill=(150, 214, 238))
    d.text((10 * SS, y_deck + 34 * SS), "deck edge", font=F_NOTE, fill=(226, 229, 236))

    d.text((28 * SS, 26 * SS), title, font=F_TITLE, fill=(240, 242, 248))
    d.text((28 * SS, 62 * SS), subtitle, font=F_SUB, fill=(168, 198, 218))
    if note:
        d.text((28 * SS, H * SS - 34 * SS), note, font=F_SUB, fill=(236, 226, 168))
    return img


def annotate(img, yaw):
    d = ImageDraw.Draw(img, "RGBA")
    pts = pose("knee_up")
    knee = angle3(pts["r_hip"], pts["r_knee"], pts["r_ankle"])
    rknee = angle3(pts["l_hip"], pts["l_knee"], pts["l_ankle"])
    lean = math.degrees(math.atan2(pts["neck"][2] - pts["pelvis"][2],
                                   pts["neck"][1] - pts["pelvis"][1]))
    lines = [
        f"front knee interior {knee:.0f}deg (flexion {180 - knee:.0f}deg)",
        f"front thigh nearly horizontal, foot clears the deck edge",
        f"rear knee {rknee:.0f}deg pushing off the pool floor",
        f"torso lean {lean:.0f}deg forward, hips low",
    ]
    y = 110 * SS
    for line in lines:
        d.text((28 * SS, y), line, font=F_SUB, fill=(236, 226, 168))
        y += 32 * SS
    kx, ky = project(pts["r_knee"], yaw, W * SS / 2, H * SS * GROUND_PX)
    d.line([(kx, ky), (kx - 40 * SS, ky - 26 * SS)], fill=(236, 226, 168), width=2 * SS)
    d.text((kx - 150 * SS, ky - 62 * SS), f"{knee:.0f}deg", font=F_SUB, fill=(236, 226, 168))


views = []
configs = [
    (90, "knee_up", "SIDE", "knee clears the deck edge, shin hanging"),
    (38, "knee_up", "3/4 FRONT", "the camera to use for card art"),
    (0, "knee_up", "FRONT", "hips low, front knee high and apart"),
]
for i, (yaw, kind, title, sub) in enumerate(configs):
    img = render(yaw, kind, f"POOL EXIT - {title}", sub)
    if i == 0:
        annotate(img, yaw)
    p = os.path.join(OUT, f"pool_fig_{i + 1}_{title.replace('/', '').replace(' ', '_').lower()}.png")
    img.resize((W, H), Image.LANCZOS).save(p)
    print("wrote", p)
    views.append(img.resize((W, H), Image.LANCZOS))

strip = Image.new("RGB", (W * 3 + 40, H), (12, 14, 18))
for i, v in enumerate(views):
    strip.paste(v, (i * (W + 20), 0))
strip.save(os.path.join(OUT, "pool_fig_0_three_views.png"))
print("wrote pool_fig_0_three_views.png")

seq = []
for kind, cap in (("knee_up", "1  step up, knee over the edge"),
                  ("deck", "2  front foot plants, drive the hips up")):
    seq.append(render(38, kind, cap, "same camera, two beats").resize((W, H), Image.LANCZOS))
strip2 = Image.new("RGB", (W * 2 + 20, H), (12, 14, 18))
for i, v in enumerate(seq):
    strip2.paste(v, (i * (W + 20), 0))
strip2.save(os.path.join(OUT, "pool_fig_4_sequence.png"))
print("wrote pool_fig_4_sequence.png")

cmp_a = render(75, "contact", "A  beach walk-out", "front knee flexion ~60deg",
               arrow_text="out of the water").resize((W, H), Image.LANCZOS)
cmp_b = render(75, "knee_up", "B  pool-exit knee lift", "front knee flexion ~85deg").resize((W, H), Image.LANCZOS)
cmp = Image.new("RGB", (W * 2 + 20, H), (12, 14, 18))
cmp.paste(cmp_a, (0, 0))
cmp.paste(cmp_b, (W + 20, 0))
cmp.save(os.path.join(OUT, "pool_fig_5_ab_compare.png"))
print("wrote pool_fig_5_ab_compare.png")
