"""Turns a green-screen video into sprite sheet pages the game plays as tall as the wall
(TransitionClip): each frame is cropped top and bottom, keyed (green taken out, green spill and the
green in see-through parts such as a white flash removed), its left and right ends fade out so the
banner does not stop in a hard line on the wall, and the frames are laid left to right, top to bottom
on as many pages as it takes. The pages go in Assets/Resources so the game loads them only while the
clip is needed.

usage: python Tools/BossVideoArt/make_video_sheet.py <video.mp4> <out folder> <name> [--preview <folder>]
Needs ffmpeg, numpy, scipy and Pillow. With --preview it only writes a few keyed frames to look at.
"""
import subprocess
import sys

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

FPS = 60
# The video is brought to this size before it is cropped.
VIDEO_WIDTH, VIDEO_HEIGHT = 1600, 900
# A frame keeps the video's whole width and this much of its height (two thirds, so the picture is
# 1.5 times as big as the whole video fitted to the wall's height), with its middle this far down
# (where the banner and the boss are). Multiples of 4, so the pages can be block compressed.
FRAME_WIDTH, FRAME_HEIGHT = 1600, 600
FRAME_MIDDLE = 0.53
COLUMNS, ROWS = 2, 6
# Share of the frame's width over which each end fades out.
FEATHER = 0.07
# Greenness (green over the larger of red and blue, 0 to 1) from which a pixel is wholly the screen,
# and under which it is wholly the picture; a little room each way for the video's compression.
SCREEN_FROM = 0.80
PICTURE_UNDER = 0.10


def read_frames(video):
    command = ["ffmpeg", "-v", "error", "-i", video, "-vf", f"fps={FPS},scale={VIDEO_WIDTH}:{VIDEO_HEIGHT}:flags=area",
               "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
    size = VIDEO_WIDTH * VIDEO_HEIGHT * 3
    process = subprocess.Popen(command, stdout=subprocess.PIPE, bufsize=size)
    while True:
        data = process.stdout.read(size)
        if len(data) < size:
            break
        yield np.frombuffer(data, np.uint8).reshape(VIDEO_HEIGHT, VIDEO_WIDTH, 3).astype(np.float32) / 255.0
    process.wait()


def key(frame, screen):
    greenness = frame[..., 1] - np.maximum(frame[..., 0], frame[..., 2])
    screen_greenness = screen[1] - max(screen[0], screen[2])
    alpha = 1.0 - np.clip((greenness / screen_greenness - PICTURE_UNDER) / (SCREEN_FROM - PICTURE_UNDER), 0.0, 1.0)
    # What is left once the screen showing through is taken back out.
    colour = (frame - (1.0 - alpha)[..., None] * screen) / np.maximum(alpha, 0.02)[..., None]
    colour = np.clip(colour, 0.0, 1.0)
    # Any green still over the larger of red and blue is spill.
    colour[..., 1] = np.minimum(colour[..., 1], np.maximum(colour[..., 0], colour[..., 2]) + 0.02)
    return colour, alpha


def finish(colour, alpha):
    # See-through pixels take the colour of the picture next to them, for the bilinear filter's sake.
    weight = gaussian_filter(alpha, 4.0)
    near = np.dstack([gaussian_filter(colour[..., i] * alpha, 4.0) for i in range(3)]) / np.maximum(weight, 0.001)[..., None]
    colour = np.where(alpha[..., None] > 0.02, colour, near)
    # The two ends fade out, and the outermost pixels are clear.
    ramp = np.clip(np.minimum(np.arange(FRAME_WIDTH), np.arange(FRAME_WIDTH)[::-1]) / (FRAME_WIDTH * FEATHER), 0.0, 1.0)
    alpha = alpha * (ramp * ramp * (3.0 - 2.0 * ramp))[None, :]
    return (np.dstack([np.clip(colour, 0.0, 1.0), alpha]) * 255.0 + 0.5).astype(np.uint8)


def main():
    video, out, name = sys.argv[1:4]
    preview = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    per_page = COLUMNS * ROWS
    top = round(VIDEO_HEIGHT * FRAME_MIDDLE - FRAME_HEIGHT / 2)
    page = None
    count = 0
    pages = 0
    screen = None
    for frame in read_frames(video):
        if screen is None:
            # The top corners of the whole frame are the screen in every clip's first frame.
            screen = np.median(np.vstack([frame[:40, :40].reshape(-1, 3), frame[:40, -40:].reshape(-1, 3)]), axis=0)
            print("screen colour", (screen * 255).round())
        if preview and count % 15 != 0:
            count += 1
            continue
        cell = finish(*key(frame[top:top + FRAME_HEIGHT, :FRAME_WIDTH], screen))
        if preview:
            Image.fromarray(cell, "RGBA").save(f"{preview}/{name}_{count:03}.png")
            count += 1
            continue
        if count % per_page == 0:
            page = np.zeros((FRAME_HEIGHT * ROWS, FRAME_WIDTH * COLUMNS, 4), np.uint8)
        x, y = (count % COLUMNS) * FRAME_WIDTH, (count // COLUMNS % ROWS) * FRAME_HEIGHT
        page[y:y + FRAME_HEIGHT, x:x + FRAME_WIDTH] = cell
        count += 1
        if count % per_page == 0:
            Image.fromarray(page, "RGBA").save(f"{out}/{name}_{pages}.png", compress_level=6)
            pages += 1
    if not preview and count % per_page != 0:
        Image.fromarray(page, "RGBA").save(f"{out}/{name}_{pages}.png", compress_level=6)
        pages += 1
    print(f"{name}: {count} frames at {FPS} fps on {pages} pages")


if __name__ == "__main__":
    main()
