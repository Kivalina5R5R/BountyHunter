"""Cut the boss cut-in's flying pigeon out of Pigeon_Fly_Loop.mp4 (green screen, 60 fps).

One seamless loop of its flight (frames START..END, END matching START again; found by comparing
frames), every STEP-th frame, keyed off the green the same way as the thieves'
(extract_thief_video.py), all cropped to one box so the bird keeps its place and its bob, and
scaled to HEIGHT pixels tall -> <out>/PigeonFly_XX.png. BossTransition plays them on its pigeons.

Usage: python extract_pigeon_video.py <frames folder with v_001.png...> <out folder>
  (frames: ffmpeg -i Pigeon_Fly_Loop.mp4 <folder>/v_%03d.png)
"""
import os
import sys

import numpy as np
from PIL import Image

frames_dir, out_dir = sys.argv[1], sys.argv[2]
# Frames 30 and 170 match (0-based); the loop is everything before 170.
START, END, STEP = 30, 170, 2
HEIGHT = 360
MARGIN = 16


def keyed(index):
    """Green-screen key: alpha from how much green beats red and blue, green spill removed."""
    rgb = np.asarray(Image.open(os.path.join(frames_dir, f'v_{index + 1:03d}.png')).convert('RGB')).astype(np.int32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    excess = g - np.maximum(r, b)
    alpha = np.clip((90 - excess) * 255 // 65, 0, 255)
    alpha = np.where(excess >= 90, 0, np.where(excess <= 25, 255, alpha))
    g = np.where((alpha > 0) & (g > np.maximum(r, b)), np.maximum(r, b), g)
    return np.dstack([r, g, b, alpha]).astype(np.uint8)


indices = list(range(START, END, STEP))
frames = [keyed(i) for i in indices]

# One box round the bird in every frame.
box = None
for frame in frames:
    ys, xs = np.nonzero(frame[..., 3] > 128)
    found = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    box = found if box is None else (min(box[0], found[0]), min(box[1], found[1]), max(box[2], found[2]), max(box[3], found[3]))
height, width = frames[0].shape[:2]
box = (max(0, box[0] - MARGIN), max(0, box[1] - MARGIN), min(width, box[2] + MARGIN), min(height, box[3] + MARGIN))

os.makedirs(out_dir, exist_ok=True)
for name in os.listdir(out_dir):
    if name.startswith('PigeonFly_') and name.endswith('.png'):
        os.remove(os.path.join(out_dir, name))

scale = HEIGHT / (box[3] - box[1])
size = (round((box[2] - box[0]) * scale), HEIGHT)
for n, frame in enumerate(frames):
    picture = Image.fromarray(frame, 'RGBA').crop(box).resize(size, Image.LANCZOS)
    picture.save(os.path.join(out_dir, f'PigeonFly_{n:02d}.png'))

print(f'{len(frames)} frames, box {box}, {size[0]}x{size[1]}')
