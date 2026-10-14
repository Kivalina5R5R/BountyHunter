"""Make blink versions of a thief's walk frames (<Name>Walk_XX.png).

The videos only blink outside the seamless walk loop, so the blink is drawn onto each walk frame
in the art's own style:
- white eyes (Chicken): the eye whites (pupils included) get an eyelid in the colour of the
  skin above the eye, with a dark lid edge (half closed) or a dark closed-eye curve;
- red eyes (Crow): the red eyes get the dark brown lid already drawn under them, rising up.
- tracked eyes (Cat): the cat's cream eyes touch its cream muzzle, so they cannot be found by
  colour. Their ovals are set once on frame 00 (TRACKED_EYES), followed to each frame by matching
  the head, and covered with the colour of the cat's own upper lid.
Outputs <Name>WalkHalf_XX.png and <Name>WalkClosed_XX.png next to the walk frames.

Usage: python make_blink_frames.py <walk folder> <Name> <white|red|tracked> [frame numbers to test]
"""
import glob
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter

folder, NAME, MODE = sys.argv[1], sys.argv[2], sys.argv[3]
HEAD_FRACTION = 0.45      # the head is in the top part of each frame
OUTLINE = (40, 26, 24)    # dark line colour of the art
LINE_WIDTH = 9
MIN_EYE = 40              # smallest eye width and height, in pixels
# Eye ovals on <Name>Walk_00.png for the tracked mode (left, top, right, bottom).
TRACKED_EYES = {'Cat': [(207, 247, 317, 345), (395, 237, 520, 340)]}
TRACK_SEARCH = 40         # pixels searched each way when following the head


def eye_mask(img):
    """The eyes in the head area: white eyes with their pupils, or the red crow eyes."""
    r, g, b, a = img.split()
    if MODE == 'red':
        red = ImageChops.subtract(r, ImageChops.lighter(g, b)).point(lambda v: 255 if v > 70 else 0)
        mask = red
    else:
        mask = ImageChops.darker(ImageChops.darker(r, g), b).point(lambda v: 255 if v > 232 else 0)
    head = Image.new('L', img.size)
    head.paste(255, (0, 0, img.width, int(img.height * HEAD_FRACTION)))
    mask = ImageChops.darker(mask, head)
    # Close the holes pupils and highlights leave.
    return mask.filter(ImageFilter.MaxFilter(25)).filter(ImageFilter.MinFilter(25))


def eye_boxes(mask):
    """Bounding boxes of the two biggest connected eye areas."""
    step = 4
    small = mask.resize((mask.width // step, mask.height // step))
    px = small.load()
    w, h = small.size
    seen = set()
    boxes = []
    for y in range(h):
        for x in range(w):
            if px[x, y] < 128 or (x, y) in seen:
                continue
            stack, region = [(x, y)], {(x, y)}
            x0 = x1 = x
            y0 = y1 = y
            while stack:
                cx, cy = stack.pop()
                x0, x1, y0, y1 = min(x0, cx), max(x1, cx), min(y0, cy), max(y1, cy)
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in region and px[nx, ny] >= 128:
                        region.add((nx, ny))
                        stack.append((nx, ny))
            seen |= region
            boxes.append((len(region), (x0 * step, y0 * step, (x1 + 1) * step, (y1 + 1) * step)))
    boxes.sort(reverse=True)
    return [box for _, box in boxes[:2] if box[2] - box[0] >= MIN_EYE and box[3] - box[1] >= MIN_EYE]


def median_colour(img, area, keep, fallback):
    sample = img.crop(area)
    flat = sample.get_flattened_data() if hasattr(sample, 'get_flattened_data') else sample.getdata()
    pixels = [p[:3] for p in flat if p[3] > 250 and keep(p)]
    if not pixels:
        return fallback
    pixels.sort(key=sum)
    return pixels[len(pixels) // 2]


def lid_colour(img, eye):
    x0, y0, x1, y1 = eye
    w, h = x1 - x0, y1 - y0
    if MODE == 'red':
        # The crow's own lid: the dark brown band right under the red.
        return median_colour(img, (x0 + w // 4, y1 + 2, x1 - w // 4, y1 + max(6, h // 3)),
                             lambda p: 40 < p[0] < 150 and p[0] > p[2], (82, 44, 38))
    # Fur or skin just above the eye, ignoring dark stripes, outlines and white.
    return median_colour(img, (x0 + w // 4, max(0, y0 - 45), x1 - w // 4, max(1, y0 - 18)),
                         lambda p: 330 < sum(p) < 690, (225, 170, 120))


def blink(img, closed):
    out = img.copy()
    mask = eye_mask(img)
    for eye in eye_boxes(mask):
        x0, y0, x1, y1 = eye
        h = y1 - y0
        cover = Image.new('L', img.size)
        if MODE == 'red':
            # The brown lid rises from below: half way, or all the way.
            lid_from = y0 if closed else y0 + int(h * 0.45)
            cover.paste(mask.crop((x0, lid_from, x1, y1)), (x0, lid_from))
        else:
            lid_to = y1 if closed else y0 + int(h * 0.55)
            cover.paste(mask.crop((x0, y0, x1, lid_to)), (x0, y0))
        if closed:
            # Pupils touching the outline are not part of the mask; cover the whole eye oval.
            oval = Image.new('L', img.size)
            pad_x, pad_y = (x1 - x0) * 0.04, h * 0.06
            ImageDraw.Draw(oval).ellipse((x0 + pad_x, y0 + pad_y, x1 - pad_x, y1 - pad_y), fill=255)
            cover = ImageChops.lighter(cover, oval)
        cover = cover.filter(ImageFilter.GaussianBlur(1.2))
        out = Image.composite(Image.new('RGBA', img.size, lid_colour(img, eye) + (255,)), out, cover)
        draw = ImageDraw.Draw(out)
        inset = max(4, (x1 - x0) // 10)
        if closed:
            mid = y0 + h * 0.55
            draw.arc((x0 + inset, mid - h * 0.28, x1 - inset, mid + h * 0.28), 20, 160, fill=OUTLINE, width=LINE_WIDTH)
        elif MODE == 'red':
            edge = y0 + h * 0.45
            draw.arc((x0 + inset, edge - h * 0.18, x1 - inset, edge + h * 0.18), 190, 350, fill=OUTLINE, width=LINE_WIDTH)
        else:
            edge = y0 + h * 0.55
            draw.arc((x0 + inset, edge - h * 0.18, x1 - inset, edge + h * 0.18), 10, 170, fill=OUTLINE, width=LINE_WIDTH)
    return out


def head_grey(img):
    """Head area on grey, half size, for matching."""
    bg = Image.new('RGBA', img.size, (128, 128, 128, 255))
    bg.alpha_composite(img)
    head = bg.crop((0, 0, img.width, int(img.height * HEAD_FRACTION))).convert('L')
    return head.resize((head.width // 2, head.height // 2))


def head_offset(reference, img):
    """(dx, dy) that moves frame 00's head onto this frame's head."""
    ref, cur = head_grey(reference), head_grey(img)
    best, best_score = (0, 0), None
    reach = TRACK_SEARCH // 2
    for dy in range(-reach, reach + 1):
        for dx in range(-reach, reach + 1):
            moved = ImageChops.offset(ref, dx, dy)
            # Compare away from the borders that offset wraps around.
            box = (reach, reach, ref.width - reach, ref.height - reach)
            score = sum(i * c for i, c in enumerate(ImageChops.difference(moved, cur).crop(box).histogram()))
            if best_score is None or score < best_score:
                best, best_score = (dx, dy), score
    return best[0] * 2, best[1] * 2


def blink_tracked(img, eyes, closed):
    out = img.copy()
    for x0, y0, x1, y1 in eyes:
        w, h = x1 - x0, y1 - y0
        # The cat's upper lid is the fur-coloured top part inside the eye outline.
        colour = median_colour(img, (x0 + w // 4, y0 + h // 8, x1 - w // 4, y0 + h // 3),
                               lambda p: 330 < sum(p) < 690, (210, 147, 116))
        inset = 6
        cover = Image.new('L', img.size)
        ImageDraw.Draw(cover).ellipse((x0 + inset, y0 + inset, x1 - inset, y1 - inset), fill=255)
        if not closed:
            # Half closed: the lid comes down to three quarters of the eye.
            cover.paste(0, (0, y0 + int(h * 0.72), img.width, img.height))
        cover = cover.filter(ImageFilter.GaussianBlur(1.2))
        out = Image.composite(Image.new('RGBA', img.size, colour + (255,)), out, cover)
        draw = ImageDraw.Draw(out)
        edge = y0 + h * (0.62 if closed else 0.72)
        span = h * (0.26 if closed else 0.16)
        draw.arc((x0 + w * 0.12, edge - span, x1 - w * 0.12, edge + span), 15 if closed else 10,
                 165 if closed else 170, fill=OUTLINE, width=LINE_WIDTH)
    return out


only = [int(a) for a in sys.argv[4:]]
reference = Image.open(os.path.join(folder, f'{NAME}Walk_00.png')).convert('RGBA') if MODE == 'tracked' else None
for path in sorted(glob.glob(os.path.join(folder, f'{NAME}Walk_*.png'))):
    n = int(os.path.basename(path)[-6:-4])
    if only and n not in only:
        continue
    img = Image.open(path).convert('RGBA')
    if MODE == 'tracked':
        dx, dy = head_offset(reference, img)
        eyes = [(x0 + dx, y0 + dy, x1 + dx, y1 + dy) for x0, y0, x1, y1 in TRACKED_EYES[NAME]]
        blink_tracked(img, eyes, False).save(os.path.join(folder, f'{NAME}WalkHalf_{n:02d}.png'))
        blink_tracked(img, eyes, True).save(os.path.join(folder, f'{NAME}WalkClosed_{n:02d}.png'))
        print(n, 'head offset', (dx, dy))
        continue

    blink(img, False).save(os.path.join(folder, f'{NAME}WalkHalf_{n:02d}.png'))
    blink(img, True).save(os.path.join(folder, f'{NAME}WalkClosed_{n:02d}.png'))
    print(n, 'eyes', eye_boxes(eye_mask(img)))
