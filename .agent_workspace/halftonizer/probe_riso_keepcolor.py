# -*- coding: utf-8 -*-
"""Numeric probe for riso_render's RISO_KEEP_COLOR branch (no edits to the tool)."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np
import halftone_tool as ht

assert ht.RISO_KEEP_COLOR is True
PAPER = ht.PAPER
print("PAPER =", PAPER, " RISO_KEEP_PAPER_DIST =", ht.RISO_KEEP_PAPER_DIST,
      " RISO_GRAIN =", ht.RISO_GRAIN, " CONTRAST =", ht.RISO_GRAIN_CONTRAST)

# --- 1) coverage ramp with grain disabled: distance -> cov -------------------
ht.RISO_GRAIN = 0.0
dists = [0, 5, 12.6, 20, 24.3, 30, 36, 50, 60.2, 100, 441]
rows = []
for d in dists:
    # pixel at exact distance d from PAPER along +R direction
    px = np.clip(PAPER + np.array([d, 0, 0]), 0, 255).astype(np.float32)
    rgb = np.tile(px[None, None, :], (8, 8, 1))
    out, cov = ht.riso_render(rgb, np.random.default_rng(ht.SEED))
    rows.append((d, float(cov[0, 0])))
print("\n[cov ramp, grain=0]  t0 =", 0.35 * ht.RISO_KEEP_PAPER_DIST)
for d, c in rows:
    print(f"  dist={d:6.1f} -> cov={c:.3f}")

# pure white pixel vs warm paper
white = np.full((8, 8, 3), 255, dtype=np.float32)
out, cov = ht.riso_render(white, np.random.default_rng(ht.SEED))
d_white = float(np.sqrt(((np.array([255, 255, 255]) - PAPER) ** 2).sum()))
print(f"  white[255,255,255]: dist={d_white:.1f} -> cov={cov[0,0]:.3f} (white gets 'inked')")

# --- 2) grain multiplier range with real RISO_GRAIN --------------------------
ht.RISO_GRAIN = 0.55
rng = np.random.default_rng(ht.SEED)
g = ht._fiber_grain(rng, 512, 512, ht.RISO_GRAIN_SCALE * ht.SS, ht.RISO_GRAIN_CONTRAST)
mult = 1.0 - ht.RISO_GRAIN * g
print("\n[fiber grain] min={:.3f} max={:.3f} mean={:.3f}".format(g.min(), g.max(), g.mean()))
print("  -> coverage multiplier range: [{:.3f}, {:.3f}]".format(mult.min(), mult.max()))
print("  -> deepest paper punch-through at full ink: cov_min = {:.3f}".format(mult.min()))
frac_low = (g < 0.05).mean(); frac_high = (g > 0.95).mean()
print(f"  grain clipped to ~0: {frac_low*100:.1f}% of area, ~1: {frac_high*100:.1f}% (S-curve clumping)")

# --- 3) full-ink pixel end-to-end through render() ---------------------------
from PIL import Image
dark = np.zeros((64, 64, 3), dtype=np.uint8)  # pure black = max distance
im = Image.fromarray(dark, "RGB")
res = np.asarray(ht.render(im), dtype=np.float32)
print("\n[render black square] out mean={:.1f}, min={:.1f}, max={:.1f}".format(
    res.mean(), res.min(), res.max()))
print("  (PAPER shows through even at 'full ink' -> grain holes visible in dark areas)")
