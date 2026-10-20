"""Turns the green-screen result videos (Assets/ART/BG/Result/VDO) into looping sprite frames.

For every clip it keys out the green, crops it tight around the character (feet at the bottom, so
the sprite's bottom centre is where it stands), and picks the loop seam where the motion comes back
closest to where it started. Every clip keeps the same scale, so the characters keep their sizes.
Frames are written to Assets/ART/BG/Result/Sprites/<Clip>/<Clip>_000.png. ResultSetup (in Unity)
turns them into animation clips. Run again after changing a video or a setting below:

    python Tools/ResultSprites/make_result_sprites.py            (all clips)
    python Tools/ResultSprites/make_result_sprites.py Shiba      (only clips whose name starts so)

Needs ffmpeg on the PATH and Pillow.
"""

import os
import shutil
import subprocess
import sys
import tempfile

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VIDEOS = os.path.join(ROOT, "Assets", "ART", "BG", "Result", "VDO")
OUT = os.path.join(ROOT, "Assets", "ART", "BG", "Result", "Sprites")

# Frames per second written; the videos are 60, so 30 takes every second frame.
FPS = 30
# Size of the frames against the video (0.5 = half the video's pixels).
SCALE = 0.5
# Green of the screen, and how far from it still counts as screen (ffmpeg chromakey).
KEY = "0x11F612"
KEY_SIMILARITY = 0.13
KEY_BLEND = 0.06
# Output frames at the end of the loop that fade into its first frame. 0 by default: the seams
# found are already closer than one frame to the next, and a fade between two poses ghosts.
SEAM_FADE = 0
# Empty pixels left around the character in the crop.
MARGIN = 6

# Character, clip name, video file, last second used (None = all of it) and how far in the loop may
# start (seconds). Shiba's dance only uses its first 3 seconds (0:03); it loops inside them.
CLIPS = [
    ("Shiba", "Shiba_Dance", "Shiba_Dance_Loop.mp4", 3.0, 1.0),
    ("Shiba", "Shiba_Clap", "Shiba_Clap_Loop.mp4", None, 0.0),
    ("Pom", "Pom_Dance", "Pom_Dance_Loop.mp4", None, 0.0),
    ("Pom", "Pom_Clap", "Pom_Clap_Loop.mp4", None, 0.0),
    ("Chiwawa", "Chiwawa_Dance", "Chiwawa_Dance_Loop.mp4", None, 0.0),
    ("Chiwawa", "Chiwawa_Clap", "Chiwawa_Clap_Loop.mp4", None, 0.0),
    ("Saibirian", "Saibirian_Dance", "Saibirian_Dance_Loop_v2.mp4", None, 0.0),
    ("Saibirian", "Saibirian_Clap", "Saibirian_Clap_Loop.mp4", None, 0.0),
]

PROBE_W, PROBE_H = 96, 54
KEY_FILTER = f"chromakey={KEY}:{KEY_SIMILARITY}:{KEY_BLEND},despill=type=green:mix=0.5:expand=0"


def run(args, **kwargs):
    return subprocess.run(args, check=True, capture_output=True, **kwargs).stdout


def video_info(path):
    out = run(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries",
               "stream=width,height,r_frame_rate,nb_frames", "-of", "csv=p=0", path]).decode().strip()
    width, height, rate, frames = out.split(",")
    num, den = rate.split("/")
    return int(width), int(height), float(num) / float(den), int(frames)


def gray_frames(path):
    raw = run(["ffmpeg", "-v", "error", "-i", path, "-vf", f"scale={PROBE_W}:{PROBE_H}",
               "-f", "rawvideo", "-pix_fmt", "gray", "-"])
    size = PROBE_W * PROBE_H
    return [raw[i:i + size] for i in range(0, len(raw) - size + 1, size)]


def difference(a, b):
    return sum(abs(x - y) for x, y in zip(a, b)) / len(a)


# The loop as (first, end) video frames: end is the frame that looks most like first, so playing
# first .. end-1 and going back to first does not jump.
def find_loop(path, rate, last_second, latest_start):
    frames = gray_frames(path)
    usable = len(frames) if last_second is None else min(len(frames), int(round(last_second * rate)) + 1)
    step = max(1, int(round(rate / FPS)))
    starts = range(0, int(latest_start * rate) + 1, step)
    # The loop is at least two thirds of what is usable, so it keeps most of the motion.
    shortest = int(usable * 2 / 3)
    best = None
    for first in starts:
        for end in range(first + shortest, usable, 1):
            if (end - first) % step:
                continue
            score = difference(frames[first], frames[end])
            if best is None or score < best[0]:
                best = (score, first, end)
    return best


def alpha_box(path, first, end, width, height):
    probe_w = 480
    probe_h = int(round(height * probe_w / width / 2)) * 2
    raw = run(["ffmpeg", "-v", "error", "-i", path, "-vf",
               f"select='between(n\\,{first}\\,{end - 1})',scale={probe_w}:{probe_h},format=rgb24,{KEY_FILTER},format=rgba",
               "-fps_mode", "passthrough", "-f", "rawvideo", "-pix_fmt", "rgba", "-"])
    size = probe_w * probe_h * 4
    box = None
    for i in range(0, len(raw) - size + 1, size):
        alpha = Image.frombytes("RGBA", (probe_w, probe_h), raw[i:i + size]).getchannel("A")
        found = alpha.point(lambda v: 255 if v > 60 else 0).getbbox()
        if found is None:
            continue
        box = found if box is None else (min(box[0], found[0]), min(box[1], found[1]),
                                         max(box[2], found[2]), max(box[3], found[3]))
    factor = width / probe_w
    return tuple(int(round(v * factor)) for v in box)


# A crop box padded by the margin, kept inside the video and sized so the scaled frame is a
# multiple of 4 on both sides (block compression needs that).
def crop_box(box, width, height):
    pad = int(round(MARGIN / SCALE))
    left, top = max(0, box[0] - pad), max(0, box[1] - pad)
    right, bottom = min(width, box[2] + pad), min(height, box[3] + pad)
    unit = int(round(4 / SCALE))
    w = -(-(right - left) // unit) * unit
    h = -(-(bottom - top) // unit) * unit
    left = max(0, min(left, width - w))
    top = max(0, min(top, height - h))
    return left, top, w, h


def extract(path, name, first, end, rate, crop):
    left, top, w, h = crop
    step = max(1, int(round(rate / FPS)))
    temp = tempfile.mkdtemp(prefix="result_sprites_")
    try:
        run(["ffmpeg", "-v", "error", "-y", "-i", path, "-vf",
             f"select='between(n\\,{first}\\,{end - 1})*not(mod(n-{first}\\,{step}))',"
             f"crop={w}:{h}:{left}:{top},scale={int(w * SCALE)}:{int(h * SCALE)}:flags=lanczos,"
             f"format=rgb24,{KEY_FILTER},format=rgba",
             "-fps_mode", "passthrough", os.path.join(temp, "f_%04d.png")])
        frames = [Image.open(os.path.join(temp, f)).convert("RGBA") for f in sorted(os.listdir(temp))]
    finally:
        shutil.rmtree(temp, ignore_errors=True)

    # The last frames fade into the first, so the jump back is carried over a few frames.
    count = len(frames)
    fade = min(SEAM_FADE, count // 4) if SEAM_FADE > 0 else 0
    for i in range(fade):
        index = count - fade + i
        frames[index] = Image.blend(frames[index], frames[0], (i + 1) / (fade + 1))

    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    for stale in os.listdir(folder):
        if stale.endswith(".png") and stale.startswith(name + "_"):
            os.remove(os.path.join(folder, stale))
            meta = os.path.join(folder, stale + ".meta")
            if os.path.exists(meta):
                os.remove(meta)
    for i, frame in enumerate(frames):
        frame.save(os.path.join(folder, f"{name}_{i:03d}.png"), optimize=True)
    return count, frames[0].size


def main():
    only = sys.argv[1] if len(sys.argv) > 1 else ""
    plans = []
    for _, name, file, last_second, latest_start in CLIPS:
        path = os.path.join(VIDEOS, file)
        width, height, rate, _ = video_info(path)
        score, first, end = find_loop(path, rate, last_second, latest_start)
        box = alpha_box(path, first, end, width, height)
        plans.append((name, path, first, end, rate, width, height, box))
        print(f"{name}: loop {first / rate:.2f}s .. {end / rate:.2f}s (seam difference {score:.2f})")

    for name, path, first, end, rate, width, height, box in plans:
        if not name.startswith(only):
            continue
        crop = crop_box(box, width, height)
        count, size = extract(path, name, first, end, rate, crop)
        print(f"{name}: {count} frames of {size[0]} x {size[1]} (crop {crop})")


if __name__ == "__main__":
    main()
