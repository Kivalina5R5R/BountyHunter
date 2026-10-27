"""Cut one of the boss's moves (Chameleon_<Move>_Loop.mp4, green screen, 60 fps) into a sprite sheet.

Every <step>th frame from <start> to <end> (end left out, so a loop does not show its first picture
twice) is keyed out of the green, scaled by <scale> and laid in a grid, <columns> wide, left to right
and top to bottom. All cells of one sheet share one crop box (the move's extent over the whole cut
plus a margin), so the boss keeps its place from frame to frame.

The boss stands on the same spot in all four videos, so ANCHOR (where the feet meet the ground in
the video) is the same for every move. The pivot printed at the end is that spot inside a cell, as
a share of the cell (0..1, from the bottom left as Unity counts it): slice every sheet with its own
pivot and switching moves does not make the boss jump.

A preview GIF of the cut is written next to the sheet's name in <preview dir> for checking the loop.
Next to the sheet goes <name>.json with its layout (columns, frames, framesPerSecond, pivot), which
the editor (BossChaseBuilder) copies into the Boss Chase each time it loads, so a new cut needs no
typing in the Inspector.

With --cloud-only <first>-<last> those cells (numbered in the sheet, from 0) keep only the smoke
cloud: the boss's head and hand poking out over it are cut away, so it looks gone inside the cloud.
With --emerge <cell> as well, from that cell to <last> what was cut away comes back bit by bit (see
through at first, nearly solid at <last>), so the boss shows up gradually in the cloud instead of
popping up whole in the first cell after it.

With --fade-left <first>-<last> those cells fade out towards their left edge (FADE_WIDTH pixels):
the water the boss shoots runs off the side of the video, and fading it makes it trail away there
instead of ending in a straight cut.

Usage: python make_boss_sheet.py <video> <out png> <preview dir> <start> <end> <step> <scale> <columns>
       [--cloud-only <first>-<last> [--emerge <cell>]] [--fade-left <first>-<last>]
  Walk (the 4-second stretch where it really walks; the video stands still at its start and end,
  and 47 and 287 match so the cut loops): ... Chameleon_Walk_Loop.mp4 BossWalk.png <tmp> 47 287 4 0.4 10
  Shoot: ... Chameleon_Shoot_Loop.mp4 BossShoot.png <tmp> 0 360 4 0.4 7 --fade-left 16-20
  SmokeBomb: ... Chameleon_SmokeBomb_Loop.mp4 BossSmokeBomb.png <tmp> 0 360 4 0.4 10 --cloud-only 38-66
             --emerge 56
  CapeWrap: ... Chameleon_CapeWrap_Loop.mp4 BossCapeWrap.png <tmp> 0 360 4 0.4 7
  CapeReveal (the cape thrown off, cut at 30 a second so it stays smooth played at half speed):
  ... Chameleon_CapeWrap_Loop.mp4 BossCapeReveal.png <tmp> 288 348 2 0.4 6
  (sheets go in Assets/ART/Boss/Sheets/). 0.4 is about the size the boss shows on the floor
  (268 pixels a metre); the cloud cut-out is tuned for it.
Needs ffmpeg and scipy.
"""
import json
import os
import subprocess
import sys
import tempfile

import numpy as np
from PIL import Image
from scipy import ndimage

def option(name):
    return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else None


def cell_range(text):
    return range(int(text.split('-')[0]), int(text.split('-')[1]) + 1) if text else range(0)


CLOUD_ONLY = option('--cloud-only')
FADE_LEFT = option('--fade-left')
EMERGE = option('--emerge')
args = [a for a in sys.argv[1:] if a not in ('--cloud-only', CLOUD_ONLY, '--fade-left', FADE_LEFT, '--emerge', EMERGE)]
video, out_png, preview_dir = args[0], args[1], args[2]
START, END, STEP = int(args[3]), int(args[4]), int(args[5])
SCALE, COLUMNS = float(args[6]), int(args[7])
CLOUD_CELLS = cell_range(CLOUD_ONLY)
FADE_CELLS = cell_range(FADE_LEFT)
EMERGE_FROM = int(EMERGE) if EMERGE else None
FADE_WIDTH = 110
SOURCE_FPS = 60
MARGIN = 16
# Where the boss's feet meet the ground in the videos (source pixels, from the top left).
ANCHOR = (973, 985)


def key(rgb):
    """Green-screen key: alpha from how much green beats red and blue, green spill removed."""
    a = rgb.astype(np.int16)
    r, g, b = a[:, :, 0], a[:, :, 1], a[:, :, 2]
    other = np.maximum(r, b)
    excess = g - other
    # Clearly green -> transparent; slightly green -> soft edge; else opaque.
    alpha = np.clip(255.0 * (90 - excess.astype(np.float32)) / 65, 0, 255).astype(np.uint8)
    g = np.where((alpha > 0) & (g > other), other, g)
    return np.dstack([r, g, b, alpha]).astype(np.uint8)


def cloud_only(cell, rest=0.0):
    """Keeps the smoke cloud (the biggest grey patch, with its grey outline) and cuts away the rest,
    or keeps the rest at that share of its opacity (0..1)."""
    a = np.asarray(cell).astype(np.int16)
    rgb, alpha = a[:, :, :3], a[:, :, 3]
    saturation = rgb.max(2) - rgb.min(2)
    grey = (saturation < 28) & (alpha > 40)
    # Shrunk first so the white glove and hat touching the cloud come loose from it.
    core = ndimage.binary_erosion(grey, iterations=9)
    labels, count = ndimage.label(core)
    if count == 0:
        return cell
    sizes = ndimage.sum(core, labels, range(1, count + 1))
    keep = labels == 1 + int(np.argmax(sizes))
    keep = ndimage.binary_dilation(keep, iterations=9) & grey
    # Its outline and soft edge, but not the black lines of the head.
    keep = ndimage.binary_dilation(keep, iterations=3) & (saturation < 45) & (rgb.max(2) > 70) & (alpha > 0)
    keep = ndimage.binary_fill_holes(keep)
    labels, count = ndimage.label(keep)
    if count > 1:
        keep = labels == 1 + int(np.argmax(ndimage.sum(keep, labels, range(1, count + 1))))
    out = a.copy()
    out[:, :, 3] = np.where(keep, alpha, np.round(alpha * rest))
    return Image.fromarray(out.astype(np.uint8))


def emerging(n):
    """How much of the boss shows in cloud cell n: 0 before EMERGE_FROM, then rising to nearly 1."""
    if EMERGE_FROM is None or n < EMERGE_FROM:
        return 0.0
    t = (n - EMERGE_FROM + 1) / (CLOUD_CELLS.stop - EMERGE_FROM + 1)
    return t * t * (3.0 - 2.0 * t)


def fade_left(cell):
    """Fades the cell out towards its left edge, smoothly, so what runs off it trails away."""
    a = np.asarray(cell).astype(np.float32)
    x = np.clip(np.arange(a.shape[1]) / FADE_WIDTH, 0.0, 1.0)
    ramp = x * x * (3.0 - 2.0 * x)
    a[:, :, 3] *= ramp[None, :]
    return Image.fromarray(a.round().astype(np.uint8))


def crop_box(frames):
    box = None
    for frame in frames:
        ys, xs = np.nonzero(frame[:, :, 3] > 128)
        if len(xs) == 0:
            continue
        b = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    h, w = frames[0].shape[:2]
    return (max(0, box[0] - MARGIN), max(0, box[1] - MARGIN), min(w, box[2] + MARGIN), min(h, box[3] + MARGIN))


def main():
    with tempfile.TemporaryDirectory() as tmp:
        subprocess.run(['ffmpeg', '-v', 'error', '-i', video, os.path.join(tmp, 'v_%03d.png')], check=True)
        frames = [key(np.asarray(Image.open(os.path.join(tmp, f'v_{i + 1:03d}.png')).convert('RGB')))
                  for i in range(START, END, STEP)]

    box = crop_box(frames)
    cell_w = round((box[2] - box[0]) * SCALE)
    cell_h = round((box[3] - box[1]) * SCALE)
    rows = -(-len(frames) // COLUMNS)
    sheet = Image.new('RGBA', (cell_w * COLUMNS, cell_h * rows), (0, 0, 0, 0))
    cells = []
    for n, frame in enumerate(frames):
        cell = Image.fromarray(frame).crop(box).resize((cell_w, cell_h), Image.LANCZOS)
        if n in CLOUD_CELLS:
            cell = cloud_only(cell, emerging(n))
        if n in FADE_CELLS:
            cell = fade_left(cell)
        sheet.paste(cell, ((n % COLUMNS) * cell_w, (n // COLUMNS) * cell_h))
        cells.append(cell)

    os.makedirs(os.path.dirname(out_png) or '.', exist_ok=True)
    sheet.save(out_png, optimize=True)

    name = os.path.splitext(os.path.basename(out_png))[0]
    os.makedirs(preview_dir, exist_ok=True)
    backed = [Image.alpha_composite(Image.new('RGBA', c.size, (60, 60, 70, 255)), c).convert('RGB') for c in cells]
    backed[0].save(os.path.join(preview_dir, name + '.gif'), save_all=True, append_images=backed[1:],
                   duration=round(1000 * STEP / SOURCE_FPS), loop=0)

    pivot_x = (ANCHOR[0] - box[0]) / (box[2] - box[0])
    pivot_y = 1 - (ANCHOR[1] - box[1]) / (box[3] - box[1])
    with open(os.path.splitext(out_png)[0] + '.json', 'w', encoding='utf-8') as layout:
        json.dump({'columns': COLUMNS, 'frames': len(frames), 'framesPerSecond': SOURCE_FPS / STEP,
                   'pivot': [round(pivot_x, 4), round(pivot_y, 4)]}, layout)
    print(f'{name}: {len(frames)} frames at {SOURCE_FPS / STEP:g} fps, cell {cell_w}x{cell_h}, '
          f'{COLUMNS}x{rows} grid, sheet {sheet.size[0]}x{sheet.size[1]}, crop {box}, pivot ({pivot_x:.3f}, {pivot_y:.3f})')


main()
