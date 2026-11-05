"""Render OpenPose-style pose diagrams for "walking out of water with bent knees".

The pose is a 3D skeleton in metres (x right, y up, z forward), rotated around Y and
projected orthographically, so the side / three-quarter / front views stay consistent.
"""

import math
import os

from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.environ["TEMP"], "pose-refs", "figures")
os.makedirs(OUT, exist_ok=True)

SS = 3
W, H = 820, 1120
SCALE = 470.0
WATER_Y = 0.40
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


def pose(kind="contact"):
    p = {
        "pelvis": (0.00, 0.80, 0.00),
        "neck": (0.00, 1.36, 0.20),
        "head": (0.00, 1.49, 0.26),
        "r_shoulder": (0.19, 1.33, 0.20),
        "l_shoulder": (-0.19, 1.35, 0.16),
        "r_hip": (0.10, 0.80, 0.00),
        "l_hip": (-0.10, 0.80, 0.02),
        "r_elbow": (0.27, 1.05, -0.04),
        "r_wrist": (0.25, 0.80, -0.16),
        "l_elbow": (-0.29, 1.06, 0.34),
        "l_wrist": (-0.26, 0.82, 0.50),
        "r_knee": (0.15, 0.45, 0.30),
        "r_ankle": (0.13, 0.05, 0.16),
        "l_knee": (-0.14, 0.44, -0.28),
        "l_ankle": (-0.12, 0.03, -0.50),
    }
    if kind == "push":
        p.update({"pelvis": (0.00, 0.78, -0.04), "neck": (0.00, 1.34, 0.16),
                  "head": (0.00, 1.47, 0.22),
                  "r_knee": (0.15, 0.50, 0.34), "r_ankle": (0.13, 0.06, 0.14),
                  "l_knee": (-0.14, 0.40, -0.34), "l_ankle": (-0.12, 0.02, -0.56)})
    if kind == "lift":
        p.update({"pelvis": (0.00, 0.82, 0.04), "neck": (0.00, 1.38, 0.24),
                  "head": (0.00, 1.51, 0.30),
                  "r_knee": (0.16, 0.60, 0.40), "r_ankle": (0.14, 0.22, 0.34),
                  "l_knee": (-0.14, 0.46, -0.20), "l_ankle": (-0.12, 0.04, -0.40)})
    if kind == "knee_up":
        # stepping up out of the water: front thigh horizontal, shin hanging, knee ~95deg interior
        p.update({"pelvis": (0.00, 0.95, 0.05), "neck": (0.00, 1.48, 0.24),
                  "head": (0.00, 1.61, 0.30),
                  "r_shoulder": (0.19, 1.45, 0.24), "l_shoulder": (-0.19, 1.47, 0.20),
                  "r_hip": (0.10, 0.95, 0.03), "l_hip": (-0.10, 0.95, 0.03),
                  "r_knee": (0.16, 0.86, 0.42), "r_ankle": (0.13, 0.46, 0.36),
                  "l_knee": (-0.13, 0.53, -0.06), "l_ankle": (-0.15, 0.02, -0.12),
                  "r_elbow": (0.27, 1.16, 0.34), "r_wrist": (0.29, 0.92, 0.52),
                  "l_elbow": (-0.28, 1.16, 0.02), "l_wrist": (-0.26, 0.90, -0.12)})
    return p


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


def render(yaw, kind, title, subtitle, note=None):
    img = Image.new("RGB", (W * SS, H * SS), (16, 18, 24))
    d = ImageDraw.Draw(img, "RGBA")
    cx, cy = W * SS / 2, H * SS * GROUND_PX

    pts = pose(kind)
    proj = {k: project(v, yaw, cx, cy) for k, v in pts.items()}

    water_y = cy - WATER_Y * SCALE * SS
    d.rectangle([0, water_y, W * SS, H * SS], fill=(28, 58, 78))
    d.rectangle([0, cy, W * SS, H * SS], fill=(20, 38, 50))

    # faint ground shadow
    d.ellipse([cx - 150 * SS, cy - 16 * SS, cx + 150 * SS, cy + 26 * SS],
              fill=(8, 16, 22, 150))

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

    # head: filled disc + ring, tied to the neck
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

    # ripples + splash
    for i in range(3):
        rx = (0.42 + 0.32 * i) * SCALE * SS
        ry = (0.05 + 0.018 * i) * SCALE * SS
        d.arc([cx - rx, water_y - ry, cx + rx, water_y + ry],
              start=195, end=345, fill=(150, 214, 238, 160 - 40 * i), width=2 * SS)
    for dx, dy in ((0.9, 0.34), (1.0, 0.56), (0.84, 0.66), (0.72, 0.44)):
        px = cx + dx * SCALE * SS * 0.5
        py = water_y - dy * SCALE * SS * 0.18
        d.ellipse([px, py, px + 7 * SS, py + 7 * SS], fill=(202, 236, 250, 190))

    # travel direction arrow: forward (z+) projects to the left in side / 3-4 views
    dx_dir = -math.sin(math.radians(yaw))
    if abs(dx_dir) > 0.2:
        span = 90 * SS * (1 if dx_dir > 0 else -1)
        ax = W * SS * 0.5 - span * 0.9
        ay = water_y - 46 * SS
        d.line([(ax, ay), (ax + span, ay)], fill=(198, 208, 222, 200), width=3 * SS)
        d.polygon([(ax + span + (10 * SS if span > 0 else -10 * SS), ay),
                   (ax + span, ay - 9 * SS), (ax + span, ay + 9 * SS)],
                  fill=(198, 208, 222, 200))
        d.text((min(ax, ax + span), ay - 30 * SS), "out of the water", font=F_NOTE,
               fill=(198, 208, 222))

    veil = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ImageDraw.Draw(veil).rectangle([0, water_y, W * SS, H * SS], fill=(34, 82, 108, 118))
    img = Image.alpha_composite(img.convert("RGBA"), veil).convert("RGB")
    d = ImageDraw.Draw(img, "RGBA")
    d.line([0, water_y, W * SS, water_y], fill=(146, 208, 236), width=3 * SS)
    d.text((W * SS - 175 * SS, water_y + 12 * SS), "waterline", font=F_NOTE,
           fill=(150, 214, 238))

    d.text((28 * SS, 26 * SS), title, font=F_TITLE, fill=(240, 242, 248))
    d.text((28 * SS, 62 * SS), subtitle, font=F_SUB, fill=(168, 198, 218))
    if note:
        y = H * SS - 34 * SS
        d.text((28 * SS, y), note, font=F_SUB, fill=(236, 226, 168))

    return img


def annotate(img, pts, yaw):
    d = ImageDraw.Draw(img, "RGBA")
    cx, cy = W * SS / 2, H * SS * GROUND_PX
    knee = angle3(pts["r_hip"], pts["r_knee"], pts["r_ankle"])
    rknee = angle3(pts["l_hip"], pts["l_knee"], pts["l_ankle"])
    lean = math.degrees(math.atan2(pts["neck"][2] - pts["pelvis"][2],
                                   pts["neck"][1] - pts["pelvis"][1]))
    lines = [
        f"front knee interior {knee:.0f}deg (flexion {180 - knee:.0f}deg)",
        f"rear knee {rknee:.0f}deg, heel lifted off the sand",
        f"torso lean {lean:.0f}deg forward, pelvis ~8cm lower than standing",
    ]
    y = 110 * SS
    for line in lines:
        d.text((28 * SS, y), line, font=F_SUB, fill=(236, 226, 168))
        y += 32 * SS
    kx, ky = project(pts["r_knee"], yaw, cx, cy)
    d.line([(kx, ky), (kx - 40 * SS, ky - 26 * SS)], fill=(236, 226, 168), width=2 * SS)
    d.text((kx - 150 * SS, ky - 62 * SS), f"{knee:.0f}deg", font=F_SUB,
           fill=(236, 226, 168))


views = []
configs = [
    (90, "contact", "SIDE", "front leg planted, rear leg pushing off"),
    (38, "contact", "3/4 FRONT", "the camera to use for card art"),
    (0, "contact", "FRONT", "pelvis level, both knees bent and apart"),
]

for i, (yaw, kind, title, sub) in enumerate(configs):
    img = render(yaw, kind, title, sub)
    if i == 0:
        annotate(img, pose(kind), yaw)
    path = os.path.join(OUT, f"fig_{i + 1}_{title.replace('/', '').replace(' ', '_').lower()}.png")
    img.resize((W, H), Image.LANCZOS).save(path)
    print("wrote", path)
    views.append(img.resize((W, H), Image.LANCZOS))

strip = Image.new("RGB", (W * 3 + 40, H), (12, 14, 18))
for i, v in enumerate(views):
    strip.paste(v, (i * (W + 20), 0))
strip_path = os.path.join(OUT, "fig_0_three_views.png")
strip.save(strip_path)
print("wrote", strip_path)

# sequence strip
seq = []
for kind, cap in (("push", "1  push-off"), ("contact", "2  weight transfer"),
                  ("lift", "3  step clear")):
    seq.append(render(38, kind, cap, "same camera, three beats").resize((W, H), Image.LANCZOS))
strip2 = Image.new("RGB", (W * 3 + 40, H), (12, 14, 18))
for i, v in enumerate(seq):
    strip2.paste(v, (i * (W + 20), 0))
seq_path = os.path.join(OUT, "fig_4_sequence.png")
strip2.save(seq_path)
print("wrote", seq_path)

# A/B comparison of knee flexion depth
cmp_a = render(75, "contact", "A  walk-out", "front knee flexion ~60deg").resize((W, H), Image.LANCZOS)
cmp_b = render(75, "knee_up", "B  pool-exit knee lift", "front knee flexion ~85deg").resize((W, H), Image.LANCZOS)
cmp = Image.new("RGB", (W * 2 + 20, H), (12, 14, 18))
cmp.paste(cmp_a, (0, 0))
cmp.paste(cmp_b, (W + 20, 0))
cmp_path = os.path.join(OUT, "fig_5_knee_depth_compare.png")
cmp.save(cmp_path)
print("wrote", cmp_path)
