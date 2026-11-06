# Prepares the market's handcuff art Codex drew (police car, handcuffs) for Unity: crops each to
# its outline and scales it down.
# Usage: python prepare_market_art.py <folder with PoliceCar_raw.png, Handcuffs_raw.png> <Assets/ART/Market>
import sys
from PIL import Image

source, out = sys.argv[1], sys.argv[2]


def crop_scale(name, width, drawn=None):
    # drawn is Codex's file name when it is not <name>_raw.png.
    picture = Image.open(f"{source}/{drawn or name + '_raw'}.png").convert("RGBA")
    picture = picture.crop(picture.getbbox())
    height = round(picture.height * width / picture.width)
    picture.resize((width, height), Image.LANCZOS).save(f"{out}/{name}.png")




# The lucky wheel: the disc (green, yellow, blue and red quarters clockwise from the top right) is
# drawn round here, each result's icon is laid upright in the middle of its
# segment, and the pointer and icons are cropped and scaled on their own. Segment order, clockwise
# from the top: more hiding spots, the loudspeaker, stun guns for all (the stun gun's own bolt,
# copied in as IconBolt.png), the police cars moving (LuckyWheelResult).
# Usage: python prepare_market_art.py <folder> <Assets/ART/Market> wheel
WHEEL_ICONS = ["IconMoreHiding", "IconLoudspeaker", "IconBolt", "IconCarsMove"]
ICON_NAMES = ["LuckyIconMoreHiding", "LuckyIconCallOut", "LuckyIconStunGuns", "LuckyIconCarsMove"]


def trimmed(name):
    picture = Image.open(f"{source}/{name}.png").convert("RGBA")
    return picture.crop(picture.getbbox())


def wheel(size):
    import math
    from PIL import ImageDraw
    # Codex's disc came out clipped and squashed, so the disc is drawn here as a true circle in its
    # colours (wooden rim with bulbs, four quarters, brass hub), big, then scaled down smooth.
    big = 1600
    ink = (40, 26, 20, 255)
    square = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(square)
    c = big / 2
    outer, rim = big / 2 - 8, big * 0.075

    def circle(radius, fill, outline=ink, width=14):
        draw.ellipse((c - radius, c - radius, c + radius, c + radius), fill=fill, outline=outline, width=width)

    circle(outer, (165, 116, 77, 255), width=18)
    circle(outer - rim * 0.18, None, (196, 146, 98, 255), 10)
    face = outer - rim
    # Quarters clockwise from the top right: green, yellow, blue, red (PIL angles run clockwise
    # from the right, so the top is -90).
    for start, colour in zip((-90, 0, 90, 180), ((50, 174, 81, 255), (251, 197, 26, 255), (14, 118, 238, 255), (236, 44, 41, 255))):
        draw.pieslice((c - face, c - face, c + face, c + face), start, start + 90, fill=colour, outline=ink, width=14)
    circle(face, None, ink, 16)
    bulbs = 16
    for i in range(bulbs):
        angle = math.radians(i * 360 / bulbs)
        x, y = c + math.sin(angle) * (outer - rim / 2), c - math.cos(angle) * (outer - rim / 2)
        r = rim * 0.3
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(255, 236, 160, 255), outline=ink, width=8)
        h = r * 0.35
        draw.ellipse((x - r * 0.45 - h, y - r * 0.45 - h, x - r * 0.45 + h, y - r * 0.45 + h), fill=(255, 255, 240, 255))
    hub = big * 0.06
    circle(hub, (214, 158, 52, 255), width=12)
    circle(hub * 0.55, (240, 196, 92, 255), (170, 110, 38, 255), 6)
    diameter = big
    # Each icon sits in the biggest circle that fits its quarter of the face (touching both
    # dividers and the rim), its longest side 0.75 of that circle's width, so every icon is about
    # as big as the others and keeps clear of the lines round it.
    room = face / (1 + math.sqrt(2))
    for segment, name in enumerate(WHEEL_ICONS):
        icon = trimmed(name)
        longest = room * 2 * 0.75
        scale = longest / max(icon.width, icon.height)
        icon = icon.resize((round(icon.width * scale), round(icon.height * scale)), Image.LANCZOS)
        angle = math.radians(45 + 90 * segment)
        reach = room * math.sqrt(2)
        x = diameter / 2 + math.sin(angle) * reach - icon.width / 2
        y = diameter / 2 - math.cos(angle) * reach - icon.height / 2
        square.alpha_composite(icon, (int(round(x)), int(round(y))))
    square.resize((size, size), Image.LANCZOS).save(f"{out}/LuckyWheel.png")
    pointer = trimmed("WheelPointer")
    pointer.resize((256, round(pointer.height * 256 / pointer.width)), Image.LANCZOS).save(f"{out}/LuckyWheelPointer.png")
    for source_name, out_name in zip(WHEEL_ICONS, ICON_NAMES):
        icon = trimmed(source_name)
        icon.resize((256, round(icon.height * 256 / icon.width)), Image.LANCZOS).save(f"{out}/{out_name}.png")


if len(sys.argv) > 3 and sys.argv[3] == "wheel":
    wheel(640)
elif len(sys.argv) > 3 and sys.argv[3] == "box":
    # The mystery box on the floor (its orange panels turn rainbow in Unity: Sprite Rainbow).
    crop_scale("MysteryBox", 512, "MysteryBox")
else:
    crop_scale("PoliceCar", 512)
    crop_scale("Handcuffs", 384)
