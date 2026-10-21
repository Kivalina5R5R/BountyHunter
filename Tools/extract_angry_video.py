"""Cut an NPC's angry act out of its <Name>_Angry_Loop.mp4 (green screen, 60 fps) and put it on its prefab.

The clip goes calm -> angry -> calm, so it is played once, start to end, after a player has bumped
into the NPC (NpcWalker.GetAngry). Every frame is lined up with the NPC's idle picture
(<Name>Idle_00.png): the clip is scaled and moved so its last, calm frame covers the idle picture
as closely as possible (starting from matching feet and height), and the same applies to the whole
clip, so the act hands over to the idle picture without a jump. The pictures keep the idle
picture's centre (the sprite pivot), with room added on every side for raised arms and jumps, so
the NPC does not jump when it switches between walking and its tantrum. They are saved at half
size with half the pixels per unit (the same size in the game, a quarter of the file size).

Writes <walk dir>/../Angry/<Name>Angry_XX.png with .meta files (copied from the idle picture's),
and the prefab's ThiefFrameAnimation angryFrames and angryFramesPerSecond.

Usage: python extract_angry_video.py <frames folder> <npc art folder> <Name> <prefab> [step]
  Frames are extracted first with: ffmpeg -i <Name>_Angry_Loop.mp4 <folder>/v_%03d.png
  Pig: ... frames/Pig Assets/ART/NPC/Pig Pig Assets/Prefabs/NPC/Pig.prefab
  (Sloth's video is Slot_Angry_Loop.mp4.) step is every how many video frames (default 6: 10 fps).
"""
import os
import re
import sys
import uuid

from PIL import Image, ImageChops, ImageStat

frames_dir, art_dir, NAME, PREFAB = sys.argv[1:5]
STEP = int(sys.argv[5]) if len(sys.argv) > 5 else 6
SOURCE_FPS = 60
PAD = 0.15          # room added on every side, in parts of the idle picture's height
SHRINK = 0.5        # saved at this share of the size


def key(rgb):
    """Green-screen key (as extract_thief_video.py): alpha from how much green beats red and blue."""
    r, g, b = rgb.split()
    rp, gp, bp = r.load(), g.load(), b.load()
    w, h = rgb.size
    out = Image.new('RGBA', (w, h))
    op = out.load()
    for y in range(h):
        for x in range(w):
            rr, gg, bb = rp[x, y], gp[x, y], bp[x, y]
            excess = gg - max(rr, bb)
            if excess >= 90:
                alpha = 0
            elif excess <= 25:
                alpha = 255
            else:
                alpha = int(255 * (90 - excess) / 65)
            if alpha > 0 and gg > max(rr, bb):
                gg = max(rr, bb)
            op[x, y] = (rr, gg, bb, alpha)
    return out


def solid_box(img):
    return img.split()[3].point(lambda v: 255 if v > 128 else 0).getbbox()


def load(index):
    return Image.open(os.path.join(frames_dir, f'v_{index + 1:03d}.png')).convert('RGB')


def best_fit(end_alpha, idle_alpha, feet_video, feet_idle, scale):
    """Refines scale and a shift (dx, dy, idle pixels) so the clip's last, calm frame covers the idle
    picture as closely as possible: the act ends where the idle picture takes over, so a misfit
    there shows as a jump. The outer boxes alone are thrown off by things held out (the goat's comb).
    Searched coarse on quarter-size masks, then fine on half-size ones."""
    def mismatch(s, dx, dy, k):
        size = (idle_alpha.width // k, idle_alpha.height // k)
        moved = end_alpha.transform(size, Image.AFFINE, (
            k / s, 0, feet_video[0] - (feet_idle[0] + dx) / s,
            0, k / s, feet_video[1] - (feet_idle[1] + dy) / s), resample=Image.BILINEAR)
        return ImageStat.Stat(ImageChops.difference(moved, idle_alpha.resize(size))).mean[0]

    best = (mismatch(scale, 0, 0, 4), scale, 0, 0)
    for i in range(-8, 9):
        s = scale * (1 + i * 0.01)
        for dx in range(-24, 25, 4):
            for dy in range(-24, 25, 4):
                best = min(best, (mismatch(s, dx, dy, 4), s, dx, dy))
    _, s0, dx0, dy0 = best
    best = (mismatch(s0, dx0, dy0, 2), s0, dx0, dy0)
    for i in range(-4, 5):
        s = s0 * (1 + i * 0.0025)
        for dx in range(dx0 - 4, dx0 + 5):
            for dy in range(dy0 - 4, dy0 + 5):
                best = min(best, (mismatch(s, dx, dy, 2), s, dx, dy))
    return best


def video_box(rgb):
    """The character's box in a video frame, found on a quarter-size copy (fast), then scaled up."""
    small = rgb.resize((rgb.width // 4, rgb.height // 4))
    b = solid_box(key(small))
    return tuple(v * 4 for v in b)


if __name__ == '__main__':
    idle_path = os.path.join(art_dir, 'Idle', f'{NAME}Idle_00.png')
    idle = Image.open(idle_path)
    idle_box = solid_box(idle)
    width, height = idle.size
    ppu = float(re.search(r'spritePixelsToUnits: ([\d.]+)', open(idle_path + '.meta', encoding='utf-8').read()).group(1))

    count = len([f for f in os.listdir(frames_dir) if f.startswith('v_')])
    first = load(0)
    calm_box = video_box(first)
    # Video pixels -> idle picture pixels: same height, feet (bottom centre) on the same spot.
    scale = (idle_box[3] - idle_box[1]) / (calm_box[3] - calm_box[1])
    feet_video = ((calm_box[0] + calm_box[2]) / 2, calm_box[3])
    feet_idle = ((idle_box[0] + idle_box[2]) / 2, idle_box[3])
    idle_alpha = idle.split()[3]
    end_alpha = key(load(count - 1)).split()[3]
    fit, scale, dx, dy = best_fit(end_alpha, idle_alpha, feet_video, feet_idle, scale)
    feet_idle = (feet_idle[0] + dx, feet_idle[1] + dy)
    print('fitted to the idle picture: scale', round(scale, 4), 'shift', (dx, dy), 'mismatch', round(fit, 2))

    pad = int(height * PAD)
    out_w, out_h = width + pad * 2, height + pad * 2
    # The output canvas (idle picture plus padding) seen in video pixels.
    left = feet_video[0] - (feet_idle[0] + pad) / scale
    top = feet_video[1] - (feet_idle[1] + pad) / scale
    region = (int(left), int(top), int(left + out_w / scale), int(top + out_h / scale))
    print('video region', region)

    angry_dir = os.path.join(art_dir, 'Angry')
    os.makedirs(angry_dir, exist_ok=True)
    meta_template = open(idle_path + '.meta', encoding='utf-8').read()
    meta_template = re.sub(r'spritePixelsToUnits: [\d.]+', f'spritePixelsToUnits: {ppu * SHRINK:g}', meta_template)
    guids = []
    clipped = 0
    indices = list(range(0, count, STEP))
    for n, i in enumerate(indices):
        # Frames reaching past the video's edge are filled with green, so they key out.
        canvas = Image.new('RGB', (region[2] - region[0], region[3] - region[1]), (0, 255, 0))
        canvas.paste(load(i), (-region[0], -region[1]))
        picture = key(canvas).resize((int(out_w * SHRINK), int(out_h * SHRINK)), Image.LANCZOS)
        box = solid_box(picture)
        if box and (box[0] == 0 or box[1] == 0 or box[2] == picture.width or box[3] == picture.height):
            clipped += 1
        path = os.path.join(angry_dir, f'{NAME}Angry_{n:02d}.png')
        picture.save(path)
        if not os.path.exists(path + '.meta'):
            open(path + '.meta', 'w', encoding='utf-8', newline='\n').write(
                re.sub(r'^guid: \w+', 'guid: ' + uuid.uuid4().hex, meta_template, flags=re.M))
        guids.append(re.search(r'^guid: (\w+)', open(path + '.meta', encoding='utf-8').read(), re.M).group(1))

    n = len(indices)
    while os.path.exists(os.path.join(angry_dir, f'{NAME}Angry_{n:02d}.png')):
        stale = os.path.join(angry_dir, f'{NAME}Angry_{n:02d}.png')
        os.remove(stale)
        if os.path.exists(stale + '.meta'):
            os.remove(stale + '.meta')
        n += 1
    print('angry frames', len(indices), 'touching the edge', clipped)

    text = open(PREFAB, encoding='utf-8').read()
    block = ('angryFrames:\n' + ''.join(f'  - {{fileID: 21300000, guid: {g}, type: 3}}\n' for g in guids) +
             f'  angryFramesPerSecond: {SOURCE_FPS / STEP:g}\n')
    text = re.sub(r'  angryFrames:(?: \[\])?\n(?:  - .*\n)*(?:  angryFramesPerSecond: .*\n)?', '', text)
    text, replaced = re.subn(r'(  crossfadeSeconds: .*\n)', lambda m: m.group(1) + '  ' + block, text)
    if replaced != 1:
        sys.exit(f'{PREFAB}: no ThiefFrameAnimation crossfadeSeconds to put angryFrames after')
    open(PREFAB, 'w', encoding='utf-8', newline='\n').write(text)
    print('angryFrames written to', PREFAB)
