"""Measures the tapes of a mini game cut-in, for their lettering to slide along them (TapeScroll):
which way each tape runs (a unit step along it, in picture pixels, x to the right and y up, pointing
right), how far apart its pattern repeats along it (one STOP to the next, one stripe to the next),
so sliding it by exactly that much loops without a seam, and the picture's size those pixels are
counted in. Writes them to a .json beside the art.

The way along is the line through the tape's middle in each column; the repeat is the strongest
echo of the colours sampled along that line.

usage: python Tools/TransitionArt/measure_tapes.py [art folder] [tape.png ...]
(defaults: the lineup cut-in's OM1_Line1.png to OM1_Line4.png; the .json is named after the first
tape's prefix: OM1_Line1.png -> OM1_Tapes.json)
Needs numpy and Pillow.
"""
import json
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(ROOT, "Assets", "ART", "Transition", "Open Minigame 1")
TAPES = ["OM1_Line1.png", "OM1_Line2.png", "OM1_Line3.png", "OM1_Line4.png"]
# Repeats shorter than this (pixels) are taken for the texture's grain, not its pattern.
SHORTEST = 120


def measure(path):
    picture = np.asarray(Image.open(path).convert("RGBA")).astype(np.float64) / 255
    alpha = picture[..., 3]
    rows = np.arange(alpha.shape[0])[:, None]
    weight = alpha.sum(axis=0)
    columns = np.where(weight > 2)[0]
    middle = (alpha * rows).sum(axis=0)[columns] / weight[columns]
    # The tape's cut ends lean the line; only its middle stretch is fitted.
    span = columns[-1] - columns[0]
    fitted = (columns > columns[0] + span * 0.1) & (columns < columns[-1] - span * 0.1)
    slope, offset = np.polyfit(columns[fitted], middle[fitted], 1)

    # One step along the tape, pointing right; y up as in the shader.
    step = np.array([1.0, -slope]) / np.hypot(1.0, slope)

    # The colours along the middle line, a pixel apart.
    length = int((columns[-1] - columns[0]) / step[0])
    xs = columns[0] + np.arange(length) * step[0]
    ys = offset + slope * xs
    inside = (xs >= 0) & (xs < alpha.shape[1] - 1) & (ys >= 0) & (ys < alpha.shape[0] - 1)
    xs, ys = xs[inside].astype(int), ys[inside].astype(int)
    shade = picture[ys, xs, :3].mean(axis=1)
    shade -= shade.mean()

    echo = np.correlate(shade, shade, mode="full")[len(shade) - 1:]
    echo /= echo[0]
    lags = np.arange(len(echo))
    usable = (lags >= SHORTEST) & (lags < len(echo) // 2)
    period = int(lags[usable][np.argmax(echo[usable])])
    return {"direction": [round(float(step[0]), 5), round(float(step[1]), 5)], "period": period,
            "size": [alpha.shape[1], alpha.shape[0]], "echo": round(float(echo[period]), 3)}


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else ART
    names = sys.argv[2:] or TAPES
    found = {name: measure(os.path.join(folder, name)) for name in names}
    for name, result in found.items():
        print(f"  {name}: {result}")
    out = os.path.join(folder, names[0].split("_")[0] + "_Tapes.json")
    with open(out, "w", encoding="utf-8") as file:
        json.dump(found, file, indent=1)
    print(f"  {out}")


if __name__ == "__main__":
    main()
