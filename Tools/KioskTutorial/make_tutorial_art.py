"""Makes the HOW TO PLAY art in Assets/ART/UI/Kiosk:
- WallTutorialPanel.png: the wide board the tutorial shows on the wall, the same night-blue board
  with the brushed rim and police tape as the kiosk's exit question's
  (Tools/KioskConfirm/make_confirm_panel.py), only wide and drawn larger;
- KioskTutorialBurst.png: a comic sunburst that turns slowly behind each page's picture: soft white
  rays round a glowing middle, fading out to the edge (tinted in the game).

usage: python Tools/KioskTutorial/make_tutorial_art.py
Needs numpy, scipy and Pillow.
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(HERE, "..", "KioskConfirm"))
import make_confirm_panel as panel  # noqa: E402

KIOSK = os.path.join(panel.ROOT, "Assets", "ART", "UI", "Kiosk")

BURST_SIZE = 512
RAYS = 14
# How much of each ray's turn the ray covers.
RAY_SHARE = 0.42


def make_panel():
    panel.WIDTH, panel.HEIGHT = 2400, 840
    panel.MARGIN, panel.RADIUS, panel.RIM = 34, 60, 17
    panel.TAPE_TOP, panel.TAPE_HEIGHT, panel.STRIPE = 40, 70, 56
    panel.OUT = os.path.join(KIOSK, "WallTutorialPanel.png")
    panel.main()


def make_burst():
    half = BURST_SIZE / 2
    y, x = np.mgrid[0:BURST_SIZE, 0:BURST_SIZE].astype(np.float64)
    dx, dy = x - half + 0.5, y - half + 0.5
    radius = np.hypot(dx, dy) / half
    angle = np.arctan2(dy, dx)

    # Each ray is a soft-edged wedge; the middle glows and everything fades out before the edge.
    turn = np.mod(angle / (2 * np.pi) * RAYS, 1.0)
    ray = np.clip((RAY_SHARE / 2 - np.abs(turn - 0.5)) / 0.03, 0, 1)
    fade = np.clip(1 - radius, 0, 1) ** 1.4
    glow = np.exp(-(radius / 0.38) ** 2)
    alpha = np.clip(ray * fade * 0.55 + glow * 0.8, 0, 1)

    rgba = np.zeros((BURST_SIZE, BURST_SIZE, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (alpha * 255).astype(np.uint8)
    out = os.path.join(KIOSK, "KioskTutorialBurst.png")
    Image.fromarray(rgba, "RGBA").save(out)
    print(f"  {out}  {BURST_SIZE}x{BURST_SIZE}")


if __name__ == "__main__":
    make_panel()
    make_burst()
