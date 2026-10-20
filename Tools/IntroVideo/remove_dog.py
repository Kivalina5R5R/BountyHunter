"""Removes the fifth dog (in the car's rear side window) from the intro arrival video.

The parked-car picture as cut from this video (Car_Parked_with_dogs.png here: two dogs sit in its
front windows, but its rear side window is empty) is the car of this video at rest. Each frame where the car is in view and its door is still shut (FIRST..LAST)
is aligned to that picture around the rear window (position and scale), the area the dog takes in
any of those frames is masked, and the empty window is pasted over it, softly blended.

Once the door opens the same dog shows through its glass for a moment (DOOR_FIRST..DOOR_LAST).
There a crop of frame 158 with the dog painted out (by Codex, door158_clean_384.png next to this
script) is laid over it the same way, following the door from frame to frame.

Run it on the original video (the one in the project already has the dog removed). Needs ffmpeg on
the PATH, numpy and opencv-python.

usage: remove_dog.py <car picture> <video in> <video out> <preview png>
"""
import os
import subprocess
import sys

import cv2
import numpy as np

CAR, VIDEO_IN, VIDEO_OUT, PREVIEW = sys.argv[1:5]
# Before FIRST the rear window is still past the frame's right edge.
FIRST, LAST = 64, 153
DOOR_FIRST, DOOR_LAST = 154, 165
# Where the door crop was taken from frame 158 (x, y), and its pictures with and without the dog.
DOOR_AT = (1000, 520)
HERE = os.path.dirname(os.path.abspath(__file__))
DOOR_DOG = cv2.imread(os.path.join(HERE, "door158.png"))
DOOR_CLEAN = cv2.imread(os.path.join(HERE, "door158_clean_384.png"))
# Where the car picture sits in the frames once parked.
REST = (232.0, 493.0)
# The rear side window and around it, in car picture pixels (x0, y0, x1, y1).
WINDOW = (880, 55, 1075, 240)
# Around the window for aligning: roof, pillars and door below it.
ALIGN = (840, 30, 1140, 330)

car = cv2.imread(CAR)
cap = cv2.VideoCapture(VIDEO_IN)
fps = cap.get(cv2.CAP_PROP_FPS)
# Only the frames to patch are kept; the rest are streamed through when encoding.
frames = {}
n = 0
while True:
    ok, frame = cap.read()
    if not ok:
        break
    if FIRST <= n <= DOOR_LAST:
        frames[n] = frame
    n += 1
cap.release()
height, width = frames[FIRST].shape[:2]

ax0, ay0, ax1, ay1 = ALIGN
wx0, wy0, wx1, wy1 = WINDOW
template = car[ay0:ay1, ax0:ax1]
# The window itself is left out when aligning, the dog in it would pull the match about.
align_mask = np.full(template.shape[:2], 255, np.uint8)
align_mask[wy0 - ay0 + 10:wy1 - ay0 - 10, wx0 - ax0 + 10:wx1 - ax0 - 10] = 0


def place_of(frame, guess):
    """The car picture's top-left and scale in this frame, searched around guess."""
    gx, gy, gs = guess
    best = None
    for scale in np.arange(gs - 0.02, gs + 0.0201, 0.005):
        t = cv2.resize(template, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
        m = cv2.resize(align_mask, (t.shape[1], t.shape[0]), interpolation=cv2.INTER_NEAREST)
        px, py = gx + ax0 * scale, gy + ay0 * scale
        x0, y0 = int(max(0, px - 40)), int(max(0, py - 40))
        x1, y1 = int(min(width, px + t.shape[1] + 40)), int(min(height, py + t.shape[0] + 40))
        region = frame[y0:y1, x0:x1]
        if region.shape[0] < t.shape[0] or region.shape[1] < t.shape[1]:
            continue
        r = cv2.matchTemplate(region, t, cv2.TM_CCORR_NORMED, mask=m)
        r[~np.isfinite(r)] = 0
        _, score, _, loc = cv2.minMaxLoc(r)
        if best is None or score > best[0]:
            best = (score, x0 + loc[0] - ax0 * scale, y0 + loc[1] - ay0 * scale, scale)
    return best


def to_frame(scale, x, y):
    return np.float32([[scale, 0, x], [0, scale, y]])


# Aligned backwards from the parked car, each frame starting from the one after it.
places = {}
guess = (REST[0], REST[1], 1.0)
for n in range(LAST, FIRST - 1, -1):
    found = place_of(frames[n], guess)
    if found is None:
        break
    score, x, y, scale = found
    places[n] = (x, y, scale, score)
    guess = (x, y, scale)

# The rear side window's glass (the dog only shows through it), in car pixels, measured on the
# parked car: from the pillar behind the front door to the rear pillar.
GLASS = np.array([(938, 82), (1002, 84), (1030, 120), (1052, 175), (1058, 210), (966, 212)], np.int32)
glass = np.zeros(car.shape[:2], np.float32)
cv2.fillPoly(glass, [GLASS], 1.0)
# A little past the glass's edges, so nothing of the dog peeks out along the pillars.
dog = cv2.dilate(glass, np.ones((7, 7), np.uint8))
# A band just around the glass (the pillars and roof), to match the patch's light to each frame.
ring = (cv2.dilate(dog, np.ones((17, 17), np.uint8)) - cv2.dilate(dog, np.ones((5, 5), np.uint8))) > 0.5
dog = cv2.GaussianBlur(dog, (0, 0), 2.5)
cv2.imwrite(PREVIEW.replace(".png", "_mask.png"), (dog * 255).astype(np.uint8))

def refined(frame, matrix):
    """The car-to-frame matrix made exact (any small turn or shear as well) by matching the
    pillars and roof around the window, the glass left out."""
    size = (car.shape[1], car.shape[0])
    back = cv2.warpAffine(frame, cv2.invertAffineTransform(matrix), size)
    a = cv2.cvtColor(car[ay0:ay1, ax0:ax1], cv2.COLOR_BGR2GRAY).astype(np.float32)
    b = cv2.cvtColor(back[ay0:ay1, ax0:ax1], cv2.COLOR_BGR2GRAY).astype(np.float32)
    keep = (1 - cv2.dilate(glass[ay0:ay1, ax0:ax1], np.ones((9, 9), np.uint8))).astype(np.uint8) * 255
    # Only what is inside the frame counts (the car may still be coming in past its edge).
    inside = cv2.warpAffine(np.full(frame.shape[:2], 255, np.uint8), cv2.invertAffineTransform(matrix), size)
    keep = cv2.bitwise_and(keep, cv2.erode(inside[ay0:ay1, ax0:ax1], np.ones((9, 9), np.uint8)))
    warp = np.eye(2, 3, dtype=np.float32)
    try:
        _, warp = cv2.findTransformECC(a, b, warp, cv2.MOTION_AFFINE,
                                       (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 100, 1e-5), keep, 5)
    except cv2.error:
        return matrix
    # warp maps the crop of the car onto the same crop of back; brought into whole-picture pixels.
    shift = np.float32([[1, 0, ax0], [0, 1, ay0], [0, 0, 1]])
    local = np.vstack([warp, [0, 0, 1]])
    car_to_back = shift @ local @ np.linalg.inv(shift)
    return (np.vstack([matrix, [0, 0, 1]]) @ car_to_back)[:2].astype(np.float32)


# Until the car is fully in view its rear is cut by the frame's edge and the search above cannot
# place it; there the car is found from its front half and the part on screen is matched.
IN_VIEW = 68
matrices = {n: refined(frames[n], to_frame(scale, x, y)) for n, (x, y, scale, _) in places.items() if n >= IN_VIEW}
front = car[230:540, 20:900]


def front_place(frame):
    """The car's place from its front half (hood, grille, wheel, door), which is in view first."""
    best = None
    for scale in np.arange(0.92, 1.06, 0.01):
        t = cv2.resize(front, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
        if t.shape[1] >= width or t.shape[0] >= height:
            continue
        r = cv2.matchTemplate(frame, t, cv2.TM_CCOEFF_NORMED)
        _, score, _, loc = cv2.minMaxLoc(r)
        if best is None or score > best[0]:
            best = (score, loc[0] - 20 * scale, loc[1] - 230 * scale, scale)
    return to_frame(best[3], best[1], best[2])


def rear_place(frame):
    """The car's place from the pillars and roof around the rear window alone, searched over the
    right of the frame; the part of them past the frame's edge is left out."""
    best = None
    for right in (ax1, 1060, 1000):
        t0 = car[ay0:ay1, ax0:right]
        m0 = align_mask[:, :right - ax0]
        for scale in np.arange(0.9, 1.12, 0.01):
            t = cv2.resize(t0, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
            m = cv2.resize(m0, (t.shape[1], t.shape[0]), interpolation=cv2.INTER_NEAREST)
            region = frame[300:900, width - 900:]
            if t.shape[0] > region.shape[0] or t.shape[1] > region.shape[1]:
                continue
            r = cv2.matchTemplate(region, t, cv2.TM_CCORR_NORMED, mask=m)
            r[~np.isfinite(r)] = 0
            _, score, _, loc = cv2.minMaxLoc(r)
            if best is None or score > best[0]:
                best = (score, width - 900 + loc[0] - ax0 * scale, 300 + loc[1] - ay0 * scale, scale)
    return to_frame(best[3], best[1], best[2])


for n in range(FIRST, IN_VIEW):
    matrices[n] = rear_place(frames[n])

for n, matrix in matrices.items():
    # The car's light changes as the light bar flashes: the window takes each frame's light.
    back = cv2.warpAffine(frames[n], cv2.invertAffineTransform(matrix), (car.shape[1], car.shape[0]))
    gain = (back[ring].mean(axis=0) + 1) / (car[ring].mean(axis=0) + 1)
    lit = np.clip(car.astype(np.float32) * gain, 0, 255)
    patch = cv2.warpAffine(lit, matrix, (width, height), flags=cv2.INTER_LINEAR)
    alpha = cv2.warpAffine(dog, matrix, (width, height), flags=cv2.INTER_LINEAR)[..., None]
    frames[n] = (frames[n] * (1 - alpha) + patch * alpha).astype(np.uint8)

# The door: the painted-out part only, where the two crops differ, with a soft edge.
door_mask = (np.abs(DOOR_DOG.astype(np.int16) - DOOR_CLEAN.astype(np.int16)).max(axis=2) > 30).astype(np.float32)
door_mask[:, :140] = 0
door_mask = cv2.morphologyEx(door_mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
door_mask = cv2.GaussianBlur(cv2.dilate(door_mask, np.ones((11, 11), np.uint8)), (0, 0), 3)
door_keep = ((1 - cv2.dilate(door_mask, np.ones((9, 9), np.uint8))) * 255).astype(np.uint8)
dx, dy = DOOR_AT
dh, dw = DOOR_CLEAN.shape[:2]
door_ring = (cv2.dilate(door_mask, np.ones((15, 15), np.uint8)) > 0.05) & (door_mask < 0.05)
for n in range(DOOR_FIRST, DOOR_LAST + 1):
    crop = frames[n][dy:dy + dh, dx:dx + dw]
    warp = np.eye(2, 3, dtype=np.float32)
    try:
        _, warp = cv2.findTransformECC(cv2.cvtColor(DOOR_DOG, cv2.COLOR_BGR2GRAY).astype(np.float32),
                                       cv2.cvtColor(crop, cv2.COLOR_BGR2GRAY).astype(np.float32), warp,
                                       cv2.MOTION_AFFINE, (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 100, 1e-5), door_keep, 5)
    except cv2.error:
        pass
    patch = cv2.warpAffine(DOOR_CLEAN.astype(np.float32), warp, (dw, dh), borderMode=cv2.BORDER_REPLICATE)
    alpha = cv2.warpAffine(door_mask, warp, (dw, dh))[..., None]
    ring = cv2.warpAffine(door_ring.astype(np.uint8), warp, (dw, dh)) > 0
    gain = (crop[ring].mean(axis=0) + 1) / (patch[ring].mean(axis=0) + 1)
    frames[n][dy:dy + dh, dx:dx + dw] = (crop * (1 - alpha) + np.clip(patch * gain, 0, 255) * alpha).astype(np.uint8)

print("aligned", len(places), "frames; worst match", round(min(p[3] for p in places.values()), 3))
for n in sorted(places)[::10]:
    print(n, [round(v, 3) for v in places[n]])

# Before/after strip of a few frames, around the window.
picks = [70, 90, 110, 125, 140, 153]
rows = []
for n in picks:
    x, y, scale, _ = places.get(n, (REST[0], REST[1], 1.0, 0))
    cx, cy = int(x + 700 * scale), int(y)
    rows.append(frames[n][max(0, cy):cy + 330, max(0, cx):cx + 460])
cv2.imwrite(PREVIEW, np.vstack([cv2.resize(r, (460, 330)) for r in rows]))

# Encoded like the original (H.264, 60 fps, yuv420p) with its sound copied.
encode = subprocess.Popen([
    "ffmpeg", "-v", "error", "-y",
    "-f", "rawvideo", "-pix_fmt", "bgr24", "-s", f"{width}x{height}", "-r", str(fps), "-i", "-",
    "-i", VIDEO_IN, "-map", "0:v", "-map", "1:a?", "-c:a", "copy",
    "-c:v", "libx264", "-preset", "slow", "-crf", "15", "-pix_fmt", "yuv420p", "-movflags", "+faststart",
    VIDEO_OUT,
], stdin=subprocess.PIPE)
cap = cv2.VideoCapture(VIDEO_IN)
n = 0
while True:
    ok, frame = cap.read()
    if not ok:
        break
    encode.stdin.write(frames.get(n, frame).tobytes())
    n += 1
encode.stdin.close()
encode.wait()
print("wrote", VIDEO_OUT, "exit", encode.returncode)
