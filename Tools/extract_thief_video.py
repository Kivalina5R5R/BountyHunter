"""Cut a thief's animations out of its <Name>_Walk_Loop.mp4 (green screen, 60 fps).

- Walk: one seamless walk cycle (frames walk_start..walk_end, found by comparing frames for the
  smallest jump back to the start, avoiding the blink), every second frame -> <Name>Walk_XX.png.
- Idle: one still frame (idle_frame) turned into a breathing loop -> <Name><IdleName>_XX.png.
All frames share one crop box, so the thief keeps its place between walking and standing.

Usage: python extract_thief_video.py <frames folder> <walk out> <idle out> <Name> <walk_start>
       <walk_end> <idle_frame> <IdleName>
  Chicken: ... Chicken 232 288 56 Sit      Cat: ... Cat 212 262 8 Idle      Crow: ... Crow 96 144 8 Idle
Frames are extracted first with: ffmpeg -i <Name>_Walk_Loop.mp4 <folder>/v_%03d.png
"""
import math
import os
import sys

from PIL import Image

frames_dir, walk_dir, idle_dir, NAME = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
# 0-based frame numbers; WALK_END repeats WALK_START. IDLE_FRAME is the stillest standing/sitting frame.
WALK_START, WALK_END, IDLE_FRAME = int(sys.argv[5]), int(sys.argv[6]), int(sys.argv[7])
IDLE_NAME = sys.argv[8]
WALK_STEP = 2                        # keep every second frame (30 fps from 60 fps)
BREATH_FRAMES = 8
BREATH_LIFT = 5.0                    # pixels the top of the sitting chicken rises
IDLE_FIXED_FRACTION = 0.22            # bottom part of the sitting chicken that never moves
MARGIN = 24


def load(index):
    return Image.open(os.path.join(frames_dir, f'v_{index + 1:03d}.png')).convert('RGB')


def key(rgb):
    """Green-screen key: alpha from how much green beats red and blue, green spill removed."""
    r, g, b = rgb.split()
    rp, gp, bp = r.load(), g.load(), b.load()
    w, h = rgb.size
    out = Image.new('RGBA', (w, h))
    op = out.load()
    for y in range(h):
        for x in range(w):
            rr, gg, bb = rp[x, y], gp[x, y], bp[x, y]
            excess = gg - max(rr, bb)
            # Clearly green -> transparent; slightly green -> soft edge; else opaque.
            if excess >= 90:
                alpha = 0
            elif excess <= 25:
                alpha = 255
            else:
                alpha = int(255 * (90 - excess) / 65)
            # Spill: pull green down to the other channels on kept pixels.
            if alpha > 0 and gg > max(rr, bb):
                gg = max(rr, bb)
            op[x, y] = (rr, gg, bb, alpha)
    return out


def crop_box(indices):
    box = None
    for i in indices:
        rgb = load(i).resize((480, 270))
        alpha = key(rgb).split()[3].point(lambda v: 255 if v > 128 else 0).getbbox()
        if alpha is None:
            continue
        sx = 1918 / 480
        sy = 1080 / 270
        b = (int(alpha[0] * sx), int(alpha[1] * sy), int(math.ceil(alpha[2] * sx)), int(math.ceil(alpha[3] * sy)))
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    return (max(0, box[0] - MARGIN), max(0, box[1] - MARGIN), min(1918, box[2] + MARGIN), min(1080, box[3] + MARGIN))


def breathe(img, lift):
    """Stretch the upper body up by up to `lift` pixels; the bottom part stays exactly put."""
    w, h = img.size
    alpha_box = img.split()[3].point(lambda v: 255 if v > 128 else 0).getbbox()
    top, bottom = alpha_box[1], alpha_box[3]
    fixed_from = bottom - (bottom - top) * IDLE_FIXED_FRACTION
    mesh = []
    strip = 6
    for y0 in range(0, h, strip):
        y1 = min(h, y0 + strip)

        def src(y):
            if y >= fixed_from:
                return float(y)
            k = lift / (fixed_from - top)
            # Rows above the fixed part rise in proportion to their height above it.
            return y + k * (fixed_from - y) / (1 + k)

        mesh.append(((0, y0, w, y1), (0, src(y0), 0, src(y1), w, src(y1), w, src(y0))))
    warped = img.convert('RGBa').transform((w, h), Image.MESH, mesh, resample=Image.BICUBIC).convert('RGBA')
    warped.paste(img.crop((0, int(fixed_from), w, h)), (0, int(fixed_from)))
    return warped


if __name__ == '__main__':
    os.makedirs(walk_dir, exist_ok=True)
    os.makedirs(idle_dir, exist_ok=True)
    walk_indices = list(range(WALK_START, WALK_END, WALK_STEP))
    box = crop_box(walk_indices + [IDLE_FRAME])
    print('crop box', box)

    for n, i in enumerate(walk_indices):
        key(load(i).crop(box)).save(os.path.join(walk_dir, f'{NAME}Walk_{n:02d}.png'))
    print('walk frames', len(walk_indices))

    idle = key(load(IDLE_FRAME).crop(box))
    for n in range(BREATH_FRAMES):
        amount = 0.5 - 0.5 * math.cos(2 * math.pi * n / BREATH_FRAMES)
        frame = idle if n == 0 else breathe(idle, BREATH_LIFT * amount)
        frame.save(os.path.join(idle_dir, f'{NAME}{IDLE_NAME}_{n:02d}.png'))
    print('idle frames', BREATH_FRAMES)
