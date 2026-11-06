"""Makes small pictures for the stun guns into Assets/ART/StunGun:
- StunBoltIcon.png  a minimal lightning bolt (yellow, dark outline): the stun gun waiting on the
                    floor to be picked up, and the tutorial's stun gun page
(StunStar.png, the dizzy star that orbits a stunned thief's head, was cut once out of a ring of
stars Codex drew; that picture is gone, so it is not remade here.)
- StunReticleBack.png / StunReticleFill.png
                    the lock-on reticle round a thief: a thin ring with four ticks pointing in
                    (the back, always shown dim) and a thick ring the radial fill reveals as the
                    lock builds; white, tinted in the game

usage: python Tools/StunGunArt/make_stun_icons.py
Needs numpy, scipy and Pillow.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "ART", "StunGun")

# The bolt's outline, in a 100 x 140 box (y down), drawn 4 times larger and shrunk for smooth edges.
BOLT = [(58, 4), (14, 78), (46, 78), (34, 136), (86, 52), (54, 52), (72, 4)]
YELLOW = (255, 214, 64, 255)
OUTLINE = (24, 26, 52, 255)
SCALE = 4
# The dark outline's width round the bolt, in the box's units.
OUTLINE_WIDTH = 6


def bolt_icon():
    width, height, pad = 100, 140, 10
    size = ((width + 2 * pad) * SCALE, (height + 2 * pad) * SCALE)
    points = [((x + pad) * SCALE, (y + pad) * SCALE) for x, y in BOLT]
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    # The dark outline first (the shape grown), then the yellow face over it.
    outline = Image.new("L", size, 0)
    ImageDraw.Draw(outline).polygon(points, fill=255)
    outline = outline.filter(ImageFilter.MaxFilter(OUTLINE_WIDTH * SCALE * 2 + 1))
    image.paste(Image.new("RGBA", size, OUTLINE), (0, 0), outline)
    draw.polygon(points, fill=YELLOW)
    image = image.resize((size[0] // SCALE, size[1] // SCALE), Image.LANCZOS)
    image.save(os.path.join(ART, "StunBoltIcon.png"))


def reticle():
    size = 256
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    centre = (size - 1) / 2
    radius = np.hypot(x - centre, y - centre) / (size / 2)
    angle = np.arctan2(y - centre, x - centre)

    def band(inner, outer):
        """1 between two radii (shares of the half width), with a pixel of soft edge."""
        soft = 1.5 / (size / 2)
        return np.clip((radius - inner) / soft + 0.5, 0, 1) * np.clip((outer - radius) / soft + 0.5, 0, 1)

    # Four ticks pointing in at the top, bottom, left and right, outside the ring.
    across = np.abs(np.sin(angle * 2)) * radius
    ticks = np.clip((0.035 - across) / 0.01 + 0.5, 0, 1) * band(0.86, 0.99)
    back = np.maximum(band(0.74, 0.79), ticks)
    fill = band(0.70, 0.83)
    for name, alpha in (("StunReticleBack.png", back), ("StunReticleFill.png", fill)):
        pixels = np.dstack([np.full((size, size, 3), 255, np.uint8), (alpha * 255).astype(np.uint8)])
        Image.fromarray(pixels, "RGBA").save(os.path.join(ART, name))


if __name__ == "__main__":
    bolt_icon()
    reticle()
    print("  StunBoltIcon.png, StunReticleBack.png, StunReticleFill.png")
