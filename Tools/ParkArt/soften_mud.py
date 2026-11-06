"""Makes Assets/ART/Park/MudPuddle.png sit into the park floor instead of on it: from the mud Codex
drew (kept as Tools/ParkArt/MudPuddle_source.png), its outer band with the grass rim fades away
in a soft, uneven edge, so no outline shows and the floor's own grass meets the mud, and the
grass tufts are painted over with the mud's colours round them.

usage: python Tools/ParkArt/soften_mud.py
Needs numpy, scipy and Pillow.
"""
import colorsys
import os
import shutil

import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ART", "Park", "MudPuddle.png")
SOURCE = os.path.join(HERE, "MudPuddle_source.png")

# Grass tufts: bright green blades (hue in degrees, saturation 0 to 1); the water is far greyer.
TUFT_HUE = (44, 150)
TUFT_SATURATION = 0.58
# The grass rim is the outer band of the drawing: the mud shows from RIM pixels in and is full
# FEATHER pixels further in, the line wandering in and out by up to WOBBLE pixels.
RIM = 34
FEATHER = 46
WOBBLE = 16

rng = np.random.default_rng(11)


def soft_noise(shape, sigma):
    field = ndimage.gaussian_filter(rng.standard_normal(shape), sigma)
    return field / (np.abs(field).max() + 1e-9)


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0, 1)
    return t * t * (3 - 2 * t)


def main():
    if not os.path.exists(SOURCE):
        shutil.copyfile(OUT, SOURCE)

    rgba = np.asarray(Image.open(SOURCE).convert("RGBA")).astype(np.float64) / 255
    rgb, alpha = rgba[..., :3], rgba[..., 3]
    hsv = np.vectorize(colorsys.rgb_to_hsv)(rgb[..., 0], rgb[..., 1], rgb[..., 2])
    hue, saturation = hsv[0] * 360, hsv[1]
    tuft = (hue >= TUFT_HUE[0]) & (hue <= TUFT_HUE[1]) & (saturation >= TUFT_SATURATION)
    tuft = ndimage.binary_dilation(tuft, iterations=3)

    # How far each pixel is inside the drawing, with the line wandering; the edge fades over a band.
    drawn = ndimage.binary_fill_holes(alpha > 0.5)
    inside = ndimage.distance_transform_edt(drawn)
    wobble = soft_noise(alpha.shape, 16) * WOBBLE + soft_noise(alpha.shape, 5) * WOBBLE * 0.3
    shape = smoothstep(RIM, RIM + FEATHER, inside + wobble)
    shape = ndimage.gaussian_filter(shape, 3)

    # Where a tuft was, the mud's colours nearby, spread in.
    keep = (~tuft).astype(np.float64)
    weight = ndimage.gaussian_filter(keep, 8)[..., None] + 1e-6
    spread = np.stack([ndimage.gaussian_filter(rgb[..., c] * keep, 8) for c in range(3)], -1) / weight
    colour = np.where(tuft[..., None], spread, rgb)

    out = np.dstack([np.clip(colour, 0, 1), np.clip(shape, 0, 1)])
    Image.fromarray((out * 255).astype(np.uint8), "RGBA").save(OUT)
    print(f"  {OUT}")


if __name__ == "__main__":
    main()
