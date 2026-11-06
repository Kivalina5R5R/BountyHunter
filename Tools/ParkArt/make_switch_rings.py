"""Makes the ring round the things a player stands on to set off (FloorSwitch: the park's sprinkler
valves, and later the market's crates and shutter switch) into Assets/ART/Props, in the look of the
players' own floor rings: a cream outer ring with four small points and a faint inner ring (the
back, always shown), and a soft band just inside it that fills round as the player stands on it
(the fill, white, tinted in the game).

usage: python Tools/ParkArt/make_switch_rings.py
Needs numpy and Pillow.
"""
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ART", "Props")
SIZE = 256
CREAM = (242, 232, 210)


def band(radius, inner, outer, soft):
    """1 between two radii (shares of the half width), fading over soft at each side."""
    return np.clip((radius - inner) / soft + 0.5, 0, 1) * np.clip((outer - radius) / soft + 0.5, 0, 1)


def main():
    os.makedirs(OUT, exist_ok=True)
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float64)
    centre = (SIZE - 1) / 2
    dx, dy = (x - centre) / (SIZE / 2), (y - centre) / (SIZE / 2)
    radius = np.hypot(dx, dy)
    pixel = 2 / SIZE

    # Four small points just outside the ring, at the top, bottom, left and right.
    across = np.minimum(np.abs(dx), np.abs(dy))
    along = np.maximum(np.abs(dx), np.abs(dy))
    points = np.clip((0.06 * (0.99 - along) / 0.09 - across) / pixel + 0.5, 0, 1) * (along > 0.86) * (along < 0.99)
    outer = band(radius, 0.82, 0.88, pixel * 1.5)
    inner = band(radius, 0.70, 0.73, pixel * 1.5) * 0.45
    back = np.clip(np.maximum(np.maximum(outer, points), inner), 0, 1)
    fill = band(radius, 0.735, 0.815, pixel * 2)

    for name, alpha, colour in (("SwitchRingBack.png", back, CREAM), ("SwitchRingFill.png", fill, (255, 255, 255))):
        rgb = np.zeros((SIZE, SIZE, 3), np.uint8) + np.array(colour, np.uint8)
        Image.fromarray(np.dstack([rgb, (alpha * 255).astype(np.uint8)]), "RGBA").save(os.path.join(OUT, name))
    print("  SwitchRingBack.png, SwitchRingFill.png")


if __name__ == "__main__":
    main()
