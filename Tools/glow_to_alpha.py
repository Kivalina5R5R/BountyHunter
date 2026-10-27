"""Turn a glow effect drawn on black into a sprite with see-through glow.

The brightest channel of each pixel becomes its alpha and the colour is divided back up by it, so
the sprite drawn normally (alpha blended) over the dark floor looks like the picture added as light:
black parts vanish, soft glow fades out smoothly, white-hot cores stay solid. The result is trimmed
to what shows, padded to a square (or kept to its own shape with --keep-shape) and scaled to <size>.

Usage: python glow_to_alpha.py <in> <out png> <size> [--keep-shape]
  python Tools/glow_to_alpha.py raw_ring.png Assets/ART/Boss/Water/WaterRing.png 512
  python Tools/glow_to_alpha.py raw_bolt_a.png Assets/ART/Boss/Water/Lightning1.png 512 --keep-shape
"""
import sys

import numpy as np
from PIL import Image

KEEP_SHAPE = '--keep-shape' in sys.argv
args = [a for a in sys.argv[1:] if a != '--keep-shape']
source, out_png, SIZE = args[0], args[1], int(args[2])
# Below this brightness (0..255) counts as the black background (noise, jpeg dirt).
FLOOR = 10
MARGIN = 8

rgb = np.asarray(Image.open(source).convert('RGB')).astype(np.float32)
alpha = np.clip((rgb.max(axis=2) - FLOOR) / (255.0 - FLOOR), 0.0, 1.0)
colour = np.where(alpha[..., None] > 0.001, np.clip(rgb / 255.0 / np.maximum(alpha[..., None], 0.001), 0.0, 1.0), 0.0)
image = Image.fromarray(np.dstack([colour * 255.0, alpha * 255.0]).round().astype(np.uint8), 'RGBA')

box = image.split()[3].point(lambda v: 255 if v > 6 else 0).getbbox()
image = image.crop((max(0, box[0] - MARGIN), max(0, box[1] - MARGIN), min(image.width, box[2] + MARGIN), min(image.height, box[3] + MARGIN)))
if KEEP_SHAPE:
    scale = SIZE / max(image.size)
    image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.LANCZOS)
else:
    side = max(image.size)
    square = Image.new('RGBA', (side, side), (0, 0, 0, 0))
    square.paste(image, ((side - image.width) // 2, (side - image.height) // 2))
    image = square.resize((SIZE, SIZE), Image.LANCZOS)

image.save(out_png, optimize=True)
print(out_png, image.size)
