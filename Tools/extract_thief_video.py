"""Cut a thief's animations out of its <Name>_Walk_Loop.mp4 (green screen, 60 fps).

- Walk: one seamless walk cycle (frames walk_start..walk_end, found by comparing frames for the
  smallest jump back to the start, avoiding the blink), every second frame -> <Name>Walk_XX.png.
- Idle: one still frame (idle_frame) turned into a breathing loop -> <Name><IdleName>_XX.png.
All frames share one crop box, so the thief keeps its place between walking and standing.
With --plant-feet every frame is moved up or down so its lowest point (the foot on the ground)
sits on the same row: for videos where the whole character bobs up and down while walking, which
looks like hopping in the game. Use it for every walking NPC.
With --hop <prefab> the character really hops (Pig, Raccoon): the frames keep their jump, and how
high each one is off the ground becomes its share of the forward travel (0 with the feet down,
1 in the air), written into the prefab's ThiefFrameAnimation as hopStride. The game then plays
those frames by time and moves the character only while it is in the air. Long stretches with the
feet down (the video standing about) are cut short so the hops flow on. The prefab's runFrames,
hopFramesPerSecond and the frames' .meta files (copied from frame 00's) are updated to match, so
use every second frame (walk_step 2, 30 fps) for smooth hops. Frame 00 needs a .meta already: let
Unity import a first cut, or copy one in.
With --prefab <prefab> a walker's prefab is kept in step the same way (runFrames, the .meta files,
frames left over from a longer cut removed) and an NPC's cycleLength is set from its walkSpeed and
the cycle's length in the video. Keep the cycle to stretches where the legs move: a video that
stands still for a while (Capybara's last second) makes the character slide while walking.

Usage: python extract_thief_video.py <frames folder> <walk out> <idle out> <Name> <walk_start>
       <walk_end> <idle_frame> <IdleName> [walk_step] [--plant-feet] [--prefab <prefab> | --hop <prefab>]
  Chicken: ... Chicken 232 288 56 Sit      Cat: ... Cat 212 262 8 Idle      Crow: ... Crow 96 144 8 Idle
  Sloth (NPC, its slow 4-second cycle every 4th frame): ... Sloth 0 240 0 Idle 4 --plant-feet
  Goat (NPC, goat_walk.mp4 is 30 fps: its 1-second cycle, every frame): ... Goat 0 30 0 Idle 1 --plant-feet
  Otter (NPC, a 6-second video that loops end to start: the whole video every 4th frame):
  ... Otter 0 360 0 Idle 4 --plant-feet
  Capybara (NPC; its video stands still from frame ~306, so the cycle is 60..272, 3.5 s):
  ... Capybara 60 272 0 Idle 4 --plant-feet --prefab Assets/Prefabs/NPC/Capybara.prefab
  Pig, Raccoon (hop instead of walk): ... Pig 0 360 0 Idle 2 --hop Assets/Prefabs/NPC/Pig.prefab
Frames are extracted first with: ffmpeg -i <Name>_Walk_Loop.mp4 <folder>/v_%03d.png
"""
import math
import os
import re
import sys
import uuid

from PIL import Image

PLANT_FEET = '--plant-feet' in sys.argv
HOP_PREFAB = sys.argv[sys.argv.index('--hop') + 1] if '--hop' in sys.argv else None
# Prefab whose frames are kept in step with this cut (--hop names it too).
PREFAB = HOP_PREFAB or (sys.argv[sys.argv.index('--prefab') + 1] if '--prefab' in sys.argv else None)
args = [a for a in sys.argv[1:] if a not in ('--plant-feet', '--hop', '--prefab', PREFAB)]
frames_dir, walk_dir, idle_dir, NAME = args[0], args[1], args[2], args[3]
# 0-based frame numbers; WALK_END repeats WALK_START. IDLE_FRAME is the stillest standing/sitting frame.
WALK_START, WALK_END, IDLE_FRAME = int(args[4]), int(args[5]), int(args[6])
IDLE_NAME = args[7]
# Keep every second frame (30 fps from 60 fps) unless told otherwise.
WALK_STEP = int(args[8]) if len(args) > 8 else 2
BREATH_FRAMES = 8
BREATH_LIFT = 5.0                    # pixels the top of the sitting chicken rises
IDLE_FIXED_FRACTION = 0.22            # bottom part of the sitting chicken that never moves
MARGIN = 24
GROUND_MIN_PIXELS = 6                 # a row needs this many solid pixels to count as the ground
HOP_AIRBORNE = 0.2                    # share of the highest lift above which a hop moves at full stride
HOP_GROUNDED = 4                      # pixels off the ground that still count as feet down
HOP_GROUND_HOLD = 4                   # most frames a hop stays with its feet down
SOURCE_FPS = 60


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


def ground_row(img):
    """The lowest row with a few solid pixels (the planted foot), ignoring stray specks."""
    alpha = img.split()[3].point(lambda v: 255 if v > 128 else 0)
    w, h = img.size
    for y in range(h - 1, -1, -1):
        if alpha.crop((0, y, w, y + 1)).histogram()[255] >= GROUND_MIN_PIXELS:
            return y
    return h - 1


def plant(frames):
    """Moves every frame down so its ground row matches the lowest one; nothing leaves the box."""
    rows = [ground_row(f) for f in frames]
    baseline = max(rows)
    print('ground rows', min(rows), '..', baseline)
    planted = []
    for frame, row in zip(frames, rows):
        moved = Image.new('RGBA', frame.size, (0, 0, 0, 0))
        moved.paste(frame, (0, baseline - row))
        planted.append(moved)
    return planted


def lifts_of(frames):
    """How many pixels each frame's feet are above the lowest frame's."""
    rows = [ground_row(f) for f in frames]
    return [max(rows) - row for row in rows]


def trim_ground_holds(frames, lifts):
    """Cuts every stretch with the feet down to at most HOP_GROUND_HOLD frames (its first and last
    ones), so the hops flow on instead of stopping dead where the video stands about. The loop is
    turned to start at a landing, so the stretch across the video's end counts as one."""
    count = len(frames)
    grounded = [lift <= HOP_GROUNDED for lift in lifts]
    if all(grounded) or not any(grounded):
        return frames, lifts

    start = next(i for i in range(count) if grounded[i] and not grounded[i - 1])
    order = list(range(start, count)) + list(range(start))
    kept = []
    run = []
    for i in order + [None]:
        if i is not None and grounded[i]:
            run.append(i)
            continue
        if len(run) > HOP_GROUND_HOLD:
            head = (HOP_GROUND_HOLD + 1) // 2
            run = run[:head] + run[len(run) - (HOP_GROUND_HOLD - head):]
        kept += run
        run = []
        if i is not None:
            kept.append(i)

    print('hop frames', count, '->', len(kept))
    return [frames[i] for i in kept], [lifts[i] for i in kept]


def hop_strides(lifts):
    """Each frame's share of the forward travel: 0 on the ground, rising to 1 once it is clearly up."""
    full = max(1, max(lifts) * HOP_AIRBORNE)
    return [round(min(1.0, lift / full), 3) for lift in lifts]


def remove_stale_walk_frames(count):
    """Deletes walk frames (and their .meta) numbered count and up, left from an earlier longer cut."""
    n = count
    while os.path.exists(walk_path(n)):
        os.remove(walk_path(n))
        if os.path.exists(walk_path(n) + '.meta'):
            os.remove(walk_path(n) + '.meta')
        n += 1


def walk_path(n):
    return os.path.join(walk_dir, f'{NAME}Walk_{n:02d}.png')


def walk_guids(count, template):
    """The sprite guid of every walk frame; frames new to this cut get a .meta copied from template."""
    guids = []
    for n in range(count):
        meta = walk_path(n) + '.meta'
        if not os.path.exists(meta):
            text = re.sub(r'^guid: \w+', 'guid: ' + uuid.uuid4().hex, template, flags=re.M)
            open(meta, 'w', encoding='utf-8', newline='\n').write(text)
        guids.append(re.search(r'^guid: (\w+)', open(meta, encoding='utf-8').read(), re.M).group(1))
    return guids


def write_prefab(prefab, guids, strides):
    """Puts the new runFrames into the prefab's ThiefFrameAnimation. A hopper (strides) also gets its
    hopStride and hopFramesPerSecond; a walker loses any old ones, and an NPC's cycleLength is set
    so one cycle takes as long as in the video at its walkSpeed."""
    text = open(prefab, encoding='utf-8').read()
    frames = 'runFrames:\n' + ''.join(f'  - {{fileID: 21300000, guid: {g}, type: 3}}\n' for g in guids)
    text, replaced = re.subn(r'  runFrames:(?: \[\])?\n(?:  - .*\n)*', lambda m: '  ' + frames, text)
    if replaced != 1:
        sys.exit(f'{prefab}: no ThiefFrameAnimation runFrames')
    text = re.sub(r'  hopStride:(?: \[\])?\n(?:  - .*\n)*(?:  hopFramesPerSecond: .*\n)?', '', text)
    if strides:
        hop = ('hopStride:\n' + ''.join(f'  - {s}\n' for s in strides) +
               f'  hopFramesPerSecond: {SOURCE_FPS / WALK_STEP:g}\n')
        text = re.sub(r'(  runFramesBlink:(?: \[\])?\n(?:  - .*\n)*)', lambda m: m.group(1) + '  ' + hop, text)
    else:
        speed = re.search(r'^  walkSpeed: ([\d.]+)$', text, re.M)
        if speed:
            cycle = float(speed.group(1)) * (WALK_END - WALK_START) / SOURCE_FPS
            text = re.sub(r'^  cycleLength: [\d.]+$', f'  cycleLength: {cycle:.3f}', text, flags=re.M)
            print('cycleLength', round(cycle, 3))
    open(prefab, 'w', encoding='utf-8', newline='\n').write(text)
    print('frames written to', prefab)


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

    walk = [key(load(i).crop(box)) for i in walk_indices]
    idle = key(load(IDLE_FRAME).crop(box))
    if PLANT_FEET:
        *walk, idle = plant(walk + [idle])

    strides = None
    if HOP_PREFAB:
        walk, lifts = trim_ground_holds(walk, lifts_of(walk))
        print('lifts', lifts)
        strides = hop_strides(lifts)
    if PREFAB:
        meta_template = open(walk_path(0) + '.meta', encoding='utf-8').read()

    for n, frame in enumerate(walk):
        frame.save(walk_path(n))
    print('walk frames', len(walk))
    if PREFAB:
        remove_stale_walk_frames(len(walk))
        write_prefab(PREFAB, walk_guids(len(walk), meta_template), strides)

    for n in range(BREATH_FRAMES):
        amount = 0.5 - 0.5 * math.cos(2 * math.pi * n / BREATH_FRAMES)
        frame = idle if n == 0 else breathe(idle, BREATH_LIFT * amount)
        frame.save(os.path.join(idle_dir, f'{NAME}{IDLE_NAME}_{n:02d}.png'))
    print('idle frames', BREATH_FRAMES)
