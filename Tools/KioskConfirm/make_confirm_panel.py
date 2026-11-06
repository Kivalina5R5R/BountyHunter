"""Makes Assets/ART/UI/Kiosk/KioskConfirmPanel.png: the board behind the kiosk's "exit the game?"
question, in the look of the kiosk's buttons (BT_Exit, BT-Tutorial): a rounded panel with a rough,
brushed black rim and a fine grain, here a deep night-blue with a soft sheen at the top, and a strip
of yellow-and-black police tape across its head. No words; the dialog writes its own.

usage: python Tools/KioskConfirm/make_confirm_panel.py
Needs numpy, scipy and Pillow.
"""
import os

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ART", "UI", "Kiosk", "KioskConfirmPanel.png")

WIDTH, HEIGHT = 900, 680
# The panel inside the picture (room round it for the rough rim), its corner radius and rim width.
MARGIN = 26
RADIUS = 46
RIM = 13
# The police tape: its top (from the panel's top edge, inside the rim), height and stripe width.
TAPE_TOP = 36
TAPE_HEIGHT = 62
STRIPE = 46

TOP_COLOUR = np.array([42, 40, 112])
BOTTOM_COLOUR = np.array([16, 22, 64])
TAPE_YELLOW = np.array([250, 204, 32])
TAPE_BLACK = np.array([24, 22, 28])

rng = np.random.default_rng(7)


def rounded_box(x, y, left, top, right, bottom, radius):
    """Signed distance to a rounded rectangle (negative inside)."""
    cx, cy = (left + right) / 2, (top + bottom) / 2
    hx, hy = (right - left) / 2 - radius, (bottom - top) / 2 - radius
    qx, qy = np.abs(x - cx) - hx, np.abs(y - cy) - hy
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    inside = np.minimum(np.maximum(qx, qy), 0)
    return outside + inside - radius


def soft_noise(sigma_x, sigma_y):
    field = ndimage.gaussian_filter(rng.standard_normal((HEIGHT, WIDTH)), (sigma_y, sigma_x))
    return field / (np.abs(field).max() + 1e-9)


def main():
    y, x = np.mgrid[0:HEIGHT, 0:WIDTH].astype(np.float64)
    distance = rounded_box(x, y, MARGIN, MARGIN, WIDTH - MARGIN, HEIGHT - MARGIN, RADIUS)

    # The rim's outer edge is brushed: streaky along the sides, with a few longer flicks.
    streaks = 0.6 * soft_noise(14, 2) + 0.4 * soft_noise(2, 14)
    flicks = np.clip(soft_noise(3, 3) * 3 - 1.6, 0, None) * 12
    outer = 4 + 8 * np.abs(streaks) + flicks
    shape = distance < outer
    face = distance < -RIM

    # The face: night blue darkening downwards, a soft sheen across the top, a fine grain, and a
    # thin lighter line just inside the rim.
    down = np.clip((y - MARGIN) / (HEIGHT - 2 * MARGIN), 0, 1)[..., None]
    colour = TOP_COLOUR * (1 - down) + BOTTOM_COLOUR * down
    sheen = np.clip(1 - (y - MARGIN - RIM) / 120, 0, 1) ** 2 * 0.18
    colour = colour + 255 * sheen[..., None] * np.array([0.6, 0.7, 1.0])
    grain = rng.standard_normal((HEIGHT, WIDTH)) * 9 + soft_noise(1.2, 1.2) * 10
    colour = colour + grain[..., None] * np.array([0.6, 0.8, 1.4])
    edge_line = np.exp(-((distance + RIM + 3) / 2.2) ** 2) * 0.35
    colour = colour + 255 * edge_line[..., None] * np.array([0.5, 0.55, 1.0])

    # The police tape across the head, inside the rim, its stripes leaning, a little worn.
    inner_top = MARGIN + RIM
    tape = (y >= inner_top + TAPE_TOP) & (y < inner_top + TAPE_TOP + TAPE_HEIGHT) & face
    stripes = np.mod(x + (y - inner_top) * 0.9, STRIPE * 2) < STRIPE
    tape_colour = np.where(stripes[..., None], TAPE_YELLOW, TAPE_BLACK).astype(np.float64)
    tape_colour = tape_colour * (0.9 + 0.1 * soft_noise(2, 2)[..., None]) + grain[..., None] * 0.6
    band = (y - (inner_top + TAPE_TOP)) / TAPE_HEIGHT
    tape_edge = np.exp(-(band / 0.06) ** 2) + np.exp(-((1 - band) / 0.06) ** 2)
    tape_colour = tape_colour * (1 - 0.35 * tape_edge[..., None])
    colour = np.where(tape[..., None], tape_colour, colour)
    # Its shadow on the board just below it.
    below = (y - (inner_top + TAPE_TOP + TAPE_HEIGHT))
    tape_shadow = np.where((below >= 0) & face, np.exp(-below / 10) * 0.45, 0)
    colour = colour * (1 - tape_shadow[..., None])

    rgb = np.where(face[..., None], colour, 0)
    rgb = np.clip(rgb, 0, 255).astype(np.uint8)
    alpha = np.where(shape, 255, 0).astype(np.uint8)
    # Softens the rim's outer edge by a pixel so it does not look cut out.
    alpha = np.clip(ndimage.gaussian_filter(alpha.astype(np.float64), 0.7), 0, 255).astype(np.uint8)

    Image.fromarray(np.dstack([rgb, alpha]), "RGBA").save(OUT)
    print(f"  {OUT}  {WIDTH}x{HEIGHT}")


if __name__ == "__main__":
    main()
