"""Drag-and-drop halftone tool, three modes (MODE knob).

"cmyk": four CMYK screens overprinted, each with its own angle and phase
shift - colors are re-synthesized by dot overlap (classic process-print
rosette look, like CSP's Color Halftone effect).
"single": one screen whose dots take the ORIGINAL pixel color, gaps fall
back to the paper color (the original mode).
"riso": flat spot-color poster - every pixel joins its nearest color of
RISO_COLORS or the paper; each ink layer gets paper show-through grain and
a small misregistration shift (risograph / stencil-print look). Set
RISO_KEEP_COLOR = True to keep every pixel's ORIGINAL color instead of the
spot palette (grain and paper knock-out still apply).

Drop images onto the .bat/.py in Explorer; output lands next to each source
as <name>_halftone_color.jpg (quality 92), auto-numbered when the name is
taken. Terminal usage: python halftone_tool.py img1.jpg img2.png ...
Optional first argument --preset <name.json> loads a preset (a JSON knob
snapshot, see presets/) before any image is processed - that is what the
per-style .bat wrappers pass. Interactive tuning: halftone_gui.bat.
"""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageFilter, ImageOps

# ---- 风格参数（改完保存，重新拖图即可生效，不用动其他代码） ------------------
MODE = "riso"        # 网点模式："cmyk" = 四色叠印（CMYK 四块网版各自加网，靠叠色合成颜色，像真实彩色印刷/教程的カラーハーフトン）；"single" = 单屏网点（每个点直接取原图色，原模式）；"riso" = 孔版印刷（全图按 RISO_COLORS 专色平涂分色 + 透纸颗粒 + 套印错位）。下面 CMYK_* / K_STRENGTH 仅 cmyk 生效，RISO_* 仅 riso 生效；默认 "cmyk"
CMYK_ANGLES = (15.0, 75.0, 0.0, 45.0)   # C/M/Y/K 四块网版各自的角度：四角错开 = 玫瑰纹（正常彩色印刷的样子）；四角相同 = 四色点叠成一摞（对应教程 Match values）；默认 (15.0, 75.0, 0.0, 45.0) 印刷业标准角组
CMYK_SHIFT = ((0.0, 0.0), (0.0, 0.0), (0.0, 0.0), (0.0, 0.0))  # C/M/Y/K 各版网点相位偏移（像素，模拟套印不准 = 教程 Shift values）：如 ((4,0),(-4,2),(0,-3),(2,2)) 会出现明显错版彩边；全 0 = 套准；默认全 0
K_STRENGTH = 0.35     # 黑版强度 0~1：0 = 纯 CMY 三色（更鲜艳，接近草莓教程图）；1 = 暗部大量黑墨（更沉、像报纸）；默认 0.5
RISO_COLORS = ((203, 34, 48), (34, 48, 102), (172, 172, 164))  # riso 专色盘（RGB，2~4 个）：riso 模式全图按"最近专色"平涂分色，每个颜色一块墨版（排后面的颜色叠在上层），颜色是替换不是叠印合成——任何照片都会变成固定专色的海报；经典 riso 双色如荧光红+蓝 ((240, 62, 110), (38, 80, 210))；默认 红/藏蓝/灰 三色
RISO_SHIFT = ((2.0, 1.0), (-1.5, 0.5), (1.0, -1.5))  # 各墨版套印错位（输出像素，x 向右 y 向下；元组个数须和 RISO_COLORS 一致）：模拟手印套不准的彩边，全 0 = 完美套准；默认各 ±2px 内错开
RISO_GRAIN = 0.55     # 透纸颗粒强度 0~1：墨层里露出纸色的程度（颗粒是"在墨里打洞透纸"，不是往画面叠暗噪点）。0 = 平涂无颗粒的矢量海报感；越大越像旧 riso/丝网印刷；默认 0.55
RISO_GRAIN_SCALE = 1.6  # 颗粒斑块尺度（输出像素）：小 = 细砂纸感，大 = 纤维纸纹/浪斑；默认 1.6
RISO_GRAIN_CONTRAST = 1.8  # 颗粒对比度（S 曲线，0~3）：把颗粒分布往"近打穿"和"近实墨"两端推，形成结块斑块——参考图红光里那种斑驳感主要靠它。0 = 旧版均匀细砂（无结块）；1.8 = 明显斑驳；2.2 以上 = 重斑驳/局部打穿纸面；默认 1.8
RISO_KEEP_COLOR = True  # riso 是否保留原图颜色：True = 不用 RISO_COLORS 专色盘，墨色直接取原图色（原色丝网：透纸颗粒和纸色镂空仍生效；套印错位自动跳过——没有独立色版可错位）；False = 按专色盘平涂替换（经典 riso 海报）；默认 False
RISO_KEEP_PAPER_DIST = 36.0  # 仅 RISO_KEEP_COLOR = True 生效：离纸色多远算"上墨"（RGB 欧氏距离 0~441）：≤约四成值 = 纯纸不上墨，≥此值 = 满墨取原色，之间平滑过渡；调大 = 更多浅色区域归入纸（更素净），调小 = 更贴原图；默认 36.0
SEED = 33            # 随机种子：固定值 = 同一张图每次处理结果完全一致（网点抖动、脏点位置都不变）；换一个数字 = 整体换一套随机纹理；默认 33
CELL = 30             # 网点间距（输出图上相邻网点中心的像素距离）：越小网点越细密、越像精细印刷品；越大网点越粗、zine 味越重。常用 4~12；默认 7
ANGLE = 15.0         # 加网角度（度）：网点阵列整体旋转的方向，斜排可避免点阵和像素网格干涉出摩尔纹；印刷业常用 15/30/45/75，随意换不影响风格；默认 15.0
JITTER = 0.05        # 网点边缘抖动（点半径的随机偏差比例）：模拟手工丝网印刷的歪扭毛边。0 = 完美光滑的数学圆点；0.2 以上"手作感"很重；默认 0.10
EDGE_SOFT = 0.5      # 网点边缘柔化宽度（像素）：墨点边界的过渡带。越大边缘越糊、像墨水洇进纸里；越小越锐利、像激光打印。低于 0.5 会出现锯齿；默认 1.1
DUST_RATE = 0.002      # 复印脏点密度（随机变成脏斑的像素比例）：0.0004 = 万分之四。设 0 = 无脏点；0.002 以上纸面脏斑很明显；默认 0.0004
DUST_POWER = 0.30    # 脏点浓度：脏点处像素亮度乘以该系数（0.3 = 压暗到原来的三成）。越小脏点越黑；1 = 无效果；不要超过 1（会变白点）；默认 0.30
GRAIN_SIGMA = 12    # 纸张颗粒噪声强度（高斯噪声σ值，只加在没墨的纸缝上）：0 = 纸面干净；越大纸纹越糙，10 以上像粗毛边纸；默认 4.5
PAPER = np.array([228.0, 224.0, 210.0])  # 纸白颜色 RGB（网点之间的底色），决定画面整体明暗基调：米黄 = 做旧纸，[200,200,200] 左右 = 中性灰纸；纸色也是网点大小基准（颜色离纸越远点越大，与纸同色 = 不印点）；默认 [228.0, 224.0, 210.0]
SS = 2               # 超采样倍率：先放大 SS 倍算网点再缩回来，让点边缘平滑抗锯齿。2 够用；调大更精细但更慢更吃内存，一般不用动；默认 2
MAX_EDGE = 2400      # 长边上限（像素）：超过的图先等比缩小再处理，防止大图把内存撑爆。想保更多细节可调到 3000，但处理时间和内存占用都会涨；默认 2400
OUT_SUFFIX = "_halftone_color.jpg"  # 输出文件名的后缀（加在原文件名后面，输出到图片同目录，撞名自动加 _1/_2 序号）；默认 "_halftone_color.jpg"
# ------------------------------------------------------------------------------


# ---- 预设机制（GUI 和 --preset 共用；JSON = 旋钮名 → 值 的整表快照） -----------
PRESET_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "presets")
CONFIG_KEYS = (
    "MODE", "CMYK_ANGLES", "CMYK_SHIFT", "K_STRENGTH",
    "RISO_COLORS", "RISO_SHIFT", "RISO_KEEP_COLOR", "RISO_KEEP_PAPER_DIST",
    "RISO_GRAIN", "RISO_GRAIN_SCALE", "RISO_GRAIN_CONTRAST",
    "SEED", "CELL", "ANGLE", "JITTER", "EDGE_SOFT",
    "DUST_RATE", "DUST_POWER", "GRAIN_SIGMA", "PAPER", "SS", "MAX_EDGE",
    "OUT_SUFFIX",
)
_TUPLE_KEYS = ("CMYK_ANGLES", "CMYK_SHIFT", "RISO_COLORS", "RISO_SHIFT")
_INT_KEYS = ("SEED", "SS", "MAX_EDGE")
_VALID_MODES = ("cmyk", "single", "riso")


def _as_tuple(v):
    return tuple(_as_tuple(x) for x in v) if isinstance(v, (list, tuple)) else v


def current_config():
    """Snapshot of every style knob as JSON-safe values (tuples -> arrays)."""
    cfg = {}
    for k in CONFIG_KEYS:
        v = globals()[k]
        cfg[k] = [float(x) for x in v] if k == "PAPER" else v
    return cfg


def apply_config(cfg):
    """Set style knobs from a dict; unknown keys are returned, not fatal."""
    unknown = []
    for k, v in cfg.items():
        if k not in CONFIG_KEYS:
            unknown.append(k)
            continue
        if k == "PAPER":
            v = np.array([float(x) for x in v])
        elif k in _TUPLE_KEYS:
            v = _as_tuple(v)
        elif k in _INT_KEYS:
            v = int(v)
        if k == "MODE" and v not in _VALID_MODES:
            raise ValueError(f"MODE 必须是 {_VALID_MODES} 之一，收到 {v!r}")
        globals()[k] = v
    return unknown


def load_preset(name):
    """Apply a preset by path or short name; returns the path actually used."""
    here = os.path.dirname(os.path.abspath(__file__))
    cands = (
        name,
        os.path.join(here, name),
        os.path.join(PRESET_DIR, name),
        name + ".json",
        os.path.join(here, name + ".json"),
        os.path.join(PRESET_DIR, name + ".json"),
    )
    for c in cands:
        if os.path.isfile(c):
            with open(c, encoding="utf-8") as f:
                cfg = json.load(f)
            unknown = apply_config(cfg)
            if unknown:
                print(f"[预设] 忽略未知参数: {', '.join(map(str, unknown))}")
            return c
    raise FileNotFoundError(f"找不到预设: {name}")


def _fiber_grain(rng, h, w, blur_radius, contrast):
    # clustered multi-octave fiber grain: fine speckle + mid clumps (at
    # RISO_GRAIN_SCALE) + coarse mottling (the octave that survives the
    # output downscale), then an S-curve pushing the field toward
    # {bare paper, full ink} so the grain clumps instead of averaging out;
    # contrast 0 = the old uniform sand
    def octave(blur):
        im = Image.fromarray(
            (rng.random((h, w), dtype=np.float32) * 255).astype(np.uint8))
        return np.asarray(
            im.filter(ImageFilter.GaussianBlur(blur)), dtype=np.float32) / 255.0

    g = (0.45 * octave(0.0)
         + 0.35 * octave(blur_radius)
         + 0.20 * octave(blur_radius * 3.0))
    return np.clip((g - 0.5) * contrast + 0.5, 0.0, 1.0)


def riso_render(rgb, rng):
    """Riso mode: nearest-spot-color separation, grain show-through, misreg.

    Every pixel joins its nearest palette entry (paper included). Each ink
    layer is coverage-modulated by its own fiber grain (paper shows through
    the ink - NOT dark noise on top), shifted by RISO_SHIFT, then lerped
    over the layers below (later RISO_COLORS entries sit on top).

    With RISO_KEEP_COLOR the palette is skipped: every pixel keeps its
    ORIGINAL color as the ink, separation is only "how far from the paper
    color" (near-paper pixels drop out to bare paper), and misregistration
    does not apply (there are no separate spot layers to shift).
    """
    H, W = rgb.shape[:2]
    blur_r = max(RISO_GRAIN_SCALE * SS, 0.5)

    if RISO_KEEP_COLOR:
        d = np.sqrt(((rgb - PAPER[None, None, :]) ** 2).sum(-1))
        t0 = 0.35 * RISO_KEEP_PAPER_DIST
        cov = np.clip(
            (d - t0) / max(RISO_KEEP_PAPER_DIST - t0, 1e-6), 0.0, 1.0
        ).astype(np.float32)
        cov *= 1.0 - RISO_GRAIN * _fiber_grain(rng, H, W, blur_r,
                                               RISO_GRAIN_CONTRAST)
        out = PAPER[None, None, :] * (1 - cov[..., None]) + rgb * cov[..., None]
        return out, cov

    n = len(RISO_COLORS)
    if n == 0 or len(RISO_SHIFT) != n:
        raise ValueError(
            "RISO_SHIFT needs exactly one (x, y) pair per RISO_COLORS entry")
    palette = np.array((PAPER, ) + tuple(RISO_COLORS), dtype=np.float32)

    best = np.full((H, W), np.inf, dtype=np.float32)
    assign = np.zeros((H, W), dtype=np.int8)  # 0 = paper
    for i, col in enumerate(palette):
        d2 = ((rgb - col[None, None, :]) ** 2).sum(-1)
        closer = d2 < best
        assign[closer] = i
        best[closer] = d2[closer]

    out = np.empty((H, W, 3), dtype=np.float32)
    out[:] = PAPER[None, None, :]
    anyink = np.zeros((H, W), dtype=np.float32)
    for i in range(1, n + 1):
        cov = (assign == i).astype(np.float32)
        cov *= 1.0 - RISO_GRAIN * _fiber_grain(rng, H, W, blur_r,
                                               RISO_GRAIN_CONTRAST)
        shx = int(round(RISO_SHIFT[i - 1][0] * SS))
        shy = int(round(RISO_SHIFT[i - 1][1] * SS))
        if shx or shy:  # np.roll wraps; clear the wrapped-in edge strips
            cov = np.roll(cov, (shy, shx), axis=(0, 1))
            if shy > 0:
                cov[:shy, :] = 0.0
            elif shy < 0:
                cov[shy:, :] = 0.0
            if shx > 0:
                cov[:, :shx] = 0.0
            elif shx < 0:
                cov[:, shx:] = 0.0
        col = np.asarray(RISO_COLORS[i - 1], dtype=np.float32)
        out *= 1.0 - cov[..., None]
        out += col[None, None, :] * cov[..., None]
        np.maximum(anyink, cov, out=anyink)
    return out, anyink


def load_image(path):
    """Open, orient, flatten alpha over PAPER, clamp to MAX_EDGE. RGB PIL."""
    with Image.open(path) as im:
        im = ImageOps.exif_transpose(im)  # honor camera rotation
        has_alpha = im.mode in ("RGBA", "LA") or (
            im.mode == "P" and "transparency" in im.info)
        if has_alpha:  # composite transparency over the paper color
            bg = Image.new("RGBA", im.size,
                           (int(PAPER[0]), int(PAPER[1]), int(PAPER[2]), 255))
            im = Image.alpha_composite(bg, im.convert("RGBA"))
        im = im.convert("RGB")

        w, h = im.size
        if max(w, h) > MAX_EDGE:
            scale = MAX_EDGE / max(w, h)
            im = im.resize((round(w * scale), round(h * scale)), Image.LANCZOS)
        return im


def render(im):
    """Full style pipeline on an RGB PIL image -> same-size RGB PIL image.

    The single entry the GUI preview also uses; knobs are read live from
    the module globals, so the GUI just sets attributes and calls this.
    """
    w, h = im.size
    big = im.resize((w * SS, h * SS), Image.LANCZOS)

    rgb = np.asarray(big, dtype=np.float32)
    # ITU-R 601 luminance, same as convert("L") in the duotone script
    L = (0.299 * rgb[..., 0] + 0.587 * rgb[..., 1] + 0.114 * rgb[..., 2]) / 255.0
    H, W = L.shape

    rng = np.random.default_rng(SEED)
    ang = np.deg2rad(ANGLE)
    ca, sa = np.cos(ang), np.sin(ang)
    cell = CELL * SS
    max_r = cell * 0.72
    # dot-size driver = distance FROM PAPER luminance (not darkness), normalized
    # per side so black AND white both reach full dots; colors near the paper
    # color shrink to nothing (same color as paper needs no ink)
    Lp = (0.299 * PAPER[0] + 0.587 * PAPER[1] + 0.114 * PAPER[2]) / 255.0

    stripe = 512
    anyink = np.zeros((H, W), dtype=np.float32)  # screen coverage, also the grain mask

    if MODE == "riso":
        out, anyink = riso_render(rgb, rng)

    if MODE == "cmyk":
        # naive GCR separation: K from the darkest component, C/M/Y rescaled by (1-K)
        R_, G_, B_ = rgb[..., 0] / 255.0, rgb[..., 1] / 255.0, rgb[..., 2] / 255.0
        kch = np.clip((1.0 - np.maximum(np.maximum(R_, G_), B_)) * K_STRENGTH, 0.0, 1.0)
        d = np.maximum(1.0 - kch, 1e-6)
        channels = (
            np.clip((1.0 - R_ - kch) / d, 0.0, 1.0),  # cyan (absorbs red)
            np.clip((1.0 - G_ - kch) / d, 0.0, 1.0),  # magenta (absorbs green)
            np.clip((1.0 - B_ - kch) / d, 0.0, 1.0),  # yellow (absorbs blue)
            kch,                                      # black (absorbs all)
        )
        kill = np.array(((1, 0, 0), (0, 1, 0), (0, 0, 1), (1, 1, 1)), dtype=np.float32)
        out = np.empty((H, W, 3), dtype=np.float32)
        # round dots must NOT exceed the cell: past 0.5*cell neighbours merge into
        # interlocking blobs (swirly artifact when 4 screens overlap); 0.5 = dots
        # just touch at 100% ink
        max_r = cell * 0.5

    if MODE != "riso":
        for y0 in range(0, H, stripe):
            y1 = min(y0 + stripe, H)
            yy, xx = np.mgrid[y0:y1, 0:W].astype(np.float32)
            if MODE == "cmyk":
                out[y0:y1] = PAPER[None, None, :]
                strong = np.zeros((y1 - y0, W), dtype=np.float32)
                for ch in range(4):
                    a = np.deg2rad(CMYK_ANGLES[ch])
                    shx, shy = CMYK_SHIFT[ch]
                    ca, sa = np.cos(a), np.sin(a)
                    xr = xx * ca + yy * sa + shx * SS
                    yr = -xx * sa + yy * ca + shy * SS
                    cxr = (xr % cell) - cell / 2
                    cyr = (yr % cell) - cell / 2
                    dist = np.sqrt(cxr * cxr + cyr * cyr)
                    r = max_r * np.sqrt(channels[ch][y0:y1])
                    r *= 1.0 + rng.normal(0, JITTER, r.shape).astype(np.float32)
                    inkv = np.clip((r - dist) / (SS * EDGE_SOFT), 0, 1)
                    out[y0:y1] *= 1.0 - inkv[..., None] * kill[ch][None, None, :]
                    np.maximum(strong, inkv, out=strong)
                anyink[y0:y1] = strong
            else:
                xr = xx * ca + yy * sa
                yr = -xx * sa + yy * ca
                cxr = (xr % cell) - cell / 2
                cyr = (yr % cell) - cell / 2
                dist = np.sqrt(cxr * cxr + cyr * cyr)
                denom = np.where(L[y0:y1] > Lp, 1.0 - Lp, Lp)
                amt = np.clip(np.abs(L[y0:y1] - Lp) / np.maximum(denom, 1e-6), 0.0, 1.0)
                r = max_r * np.sqrt(amt)
                r *= 1.0 + rng.normal(0, JITTER, r.shape).astype(np.float32)
                anyink[y0:y1] = np.clip((r - dist) / (SS * EDGE_SOFT), 0, 1)

    cov = anyink[..., None]
    if MODE == "single":
        out = PAPER[None, None, :] * (1 - cov) + rgb * cov

    dust = rng.random((H, W)) < DUST_RATE
    out[dust] *= DUST_POWER

    paper_noise = rng.normal(0, GRAIN_SIGMA, (H, W, 1)).astype(np.float32)
    out += paper_noise * (1 - cov) * 0.9

    out = np.clip(out, 0, 255).astype(np.uint8)
    res = Image.fromarray(out).resize((w, h), Image.LANCZOS)
    return res.filter(ImageFilter.GaussianBlur(0.3))


def process(path):
    stem, _ = os.path.splitext(path)
    # auto-number on collision: never overwrite an existing file (or the input)
    root, ext = os.path.splitext(OUT_SUFFIX)  # "_halftone_color" / ".jpg"
    out_path = stem + OUT_SUFFIX
    n = 1
    while os.path.exists(out_path) or os.path.abspath(out_path) == os.path.abspath(path):
        out_path = f"{stem}{root}_{n}{ext}"
        n += 1

    render(load_image(path)).save(out_path, quality=92)
    return out_path


def main():
    args = sys.argv[1:]
    preset = None
    paths = []
    i = 0
    while i < len(args):
        a = args[i]
        if a == "--preset" and i + 1 < len(args):
            preset = args[i + 1]
            i += 2
            continue
        if a.startswith("--preset="):
            preset = a.split("=", 1)[1]
            i += 1
            continue
        paths.append(a)
        i += 1
    if preset:
        try:
            print(f"[预设] 已加载 {os.path.basename(load_preset(preset))}")
        except Exception as e:
            print(f"[预设失败] {e}")
    if not paths:
        print(__doc__.strip())
        print("\n把一张或多张图片拖到本脚本图标上即可，结果输出到图片同目录。")
        print("可选: --preset 预设名.json  先加载预设再处理（presets/ 下的文件可直接写名字）。")
        print("调参窗口: python halftone_gui.py（或拖图进窗口）")
    else:
        for p in paths:
            if not os.path.isfile(p):
                print(f"[跳过] 不是文件: {p}")
                continue
            try:
                out = process(p)
                print(f"[完成] {os.path.basename(p)} -> {os.path.basename(out)}")
            except Exception as e:
                print(f"[失败] {os.path.basename(p)}: {type(e).__name__}: {e}")


if __name__ == "__main__":
    main()
