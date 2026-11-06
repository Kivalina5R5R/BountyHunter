# Pads sprite PNGs so their imported size is a multiple of 4 on both sides. Block compression
# (DXT/BC) needs that; otherwise Unity silently keeps the sprite as uncompressed RGBA32, which is
# 4x the memory and makes the scene slow to load. Edge pixels are repeated into the padding.
# Single sprites are padded evenly around the centre (pivot moves at most half a pixel). Boss sheets
# are padded on the right and bottom only, because BossSheet cuts cells from the top-left.
# Usage: python Tools/pad_textures_to_4.py [--apply]   (without --apply it only lists the files)
import os, re, struct, sys
import numpy as np
from PIL import Image

ROOT = 'Assets/ART'
SHEETS = os.path.normpath('Assets/ART/Minigame/Boss/Sheets')


def png_size(path):
    with open(path, 'rb') as f:
        return struct.unpack('>II', f.read(24)[16:24])


def setting(block, key):
    m = re.search(r'\n\s*' + key + r': (-?\d+)', block)
    return int(m.group(1)) if m else None


def max_size(meta):
    parts = meta.split('buildTarget: Standalone')
    if len(parts) > 1 and setting(parts[1][:700], 'overridden') == 1:
        return setting(parts[1], 'maxTextureSize')
    return setting(meta.split('buildTarget: DefaultTexturePlatform')[1], 'maxTextureSize')


def round_up(value, step):
    return -(-value // step) * step


def plan(path):
    meta = open(path + '.meta', encoding='utf-8').read()
    if setting(meta, 'textureType') != 8 or setting(meta, 'spriteMode') != 1:
        return None
    w, h = png_size(path)
    limit, scale = max_size(meta), 1
    while max(w, h) / scale > limit:
        scale *= 2
    step = 4 * scale
    tw, th = round_up(w, step), round_up(h, step)
    if (tw, th) == (w, h):
        return None
    padx, pady = tw - w, th - h
    if os.path.dirname(os.path.normpath(path)) == SHEETS:
        return (0, padx), (0, pady)
    return (padx // 2, padx - padx // 2), (pady // 2, pady - pady // 2)


def pad(path, x, y):
    image = Image.open(path)
    if image.mode not in ('RGBA', 'RGB', 'LA', 'L'):
        print('skip mode', image.mode, path)
        return
    pixels = np.asarray(image)
    widths = (y, x) + (((0, 0),) if pixels.ndim == 3 else ())
    Image.fromarray(np.pad(pixels, widths, mode='edge'), image.mode).save(path, optimize=False, compress_level=6)


def main():
    apply = '--apply' in sys.argv
    count = 0
    for folder, _, names in os.walk(ROOT):
        for name in sorted(names):
            if not name.lower().endswith('.png'):
                continue
            path = os.path.join(folder, name)
            padding = plan(path)
            if padding is None:
                continue
            count += 1
            if apply:
                pad(path, *padding)
            else:
                print(path, png_size(path), padding)
    print(('padded ' if apply else 'would pad ') + str(count))


if __name__ == '__main__':
    main()
