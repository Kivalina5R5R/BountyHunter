"""Makes the path map a mini game cut-in's red streak is drawn along (PathReveal): for every point of
the streak, how far along it that point lies, from 0 where it starts at the left to 1 where it ends
at the right, following its zigzag (up to the top, down to the bottom, up again) rather than going
straight across. Each pixel takes the place along the streak's middle line nearest to it, so the
drawing's leading edge crosses the streak square on, round every corner.

The middle line is the streak's middle in each column, smoothed. The map is a 16-bit grey picture at
an eighth of the art's size (the reveal blends between its pixels). The middle line itself is saved
beside it (the same name, .json): points along it as shares of the picture (x to the right, y up) with
how far along it each lies, so the cut-in can follow the drawing's tip (its sparks).

usage: python Tools/TransitionArt/make_streak_path.py [streak.png] [out.png]
(defaults: the lineup cut-in's OM1_Line5.png -> OM1_Line5_Path.png)
Needs numpy, scipy and Pillow.
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "ART", "Transition", "Open Minigame 1")
SHRINK = 8
# How much the middle line is smoothed (map pixels), so it follows the zigzag, not its rough edge.
SMOOTH = 3


def main():
    source = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ART, "OM1_Line5.png")
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ART, "OM1_Line5_Path.png")

    picture = Image.open(source).convert("RGBA")
    size = (picture.width // SHRINK, picture.height // SHRINK)
    alpha = np.asarray(picture.getchannel("A").resize(size, Image.BILINEAR)).astype(np.float64) / 255

    # The streak's middle in each column it covers, smoothed along the way.
    rows = np.arange(size[1])[:, None]
    weight = alpha.sum(axis=0)
    columns = np.where(weight > 0.5)[0]
    middle = (alpha * rows).sum(axis=0)[columns] / weight[columns]
    middle = ndimage.gaussian_filter1d(middle, SMOOTH)
    line = np.stack([columns.astype(np.float64), middle], axis=1)

    # How far along the middle line each of its points is, 0 to 1.
    steps = np.linalg.norm(np.diff(line, axis=0), axis=1)
    along = np.concatenate([[0.0], np.cumsum(steps)])
    along /= along[-1]

    # Every pixel takes the place along the line nearest to it.
    ys, xs = np.mgrid[0:size[1], 0:size[0]]
    _, nearest = cKDTree(line).query(np.stack([xs.ravel(), ys.ravel()], axis=1))
    place = along[nearest].reshape(size[1], size[0])
    place = ndimage.gaussian_filter(place, 1.0)

    Image.fromarray(np.round(np.clip(place, 0, 1) * 65535).astype(np.uint16)).save(out)
    print(f"  {out}  {size[0]}x{size[1]}")

    # Every few map pixels along the middle line: x and y as shares of the picture, and how far along.
    keep = np.arange(0, len(line), 6).tolist() + [len(line) - 1]
    points = [[round(line[i, 0] / size[0], 5), round(1 - line[i, 1] / size[1], 5), round(along[i], 5)] for i in keep]
    middle_out = os.path.splitext(out)[0] + ".json"
    with open(middle_out, "w", encoding="utf-8") as file:
        json.dump({"points": points}, file)
    print(f"  {middle_out}  {len(points)} points")


if __name__ == "__main__":
    main()
