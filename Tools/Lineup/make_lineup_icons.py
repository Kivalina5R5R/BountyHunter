"""Makes the suspect lineup's art in Assets/ART/Lineup: the clue icons, one for each thing a suspect
can carry, made from the item pictures themselves (Cap, Beanie, Mask, Glasses, FaceMask, Mustache,
Newspaper, Gloves, Bag, drawn by Codex; HAT shows both hats), and the tick and cross badges.
It also makes the wall panel's backdrop (WallBackground.png): the game wall's back layer
(Assets/ART/BG/WallPlay/BG.png) blurred hard and darkened, since that layer alone has a blurred patch
where the board goes and a hard window frame that look cut off without the pieces laid over it.
It also draws the clue card's background (Card.png, a white box with round corners, 9-sliced
by CARD_BORDER in its import settings), the cream strip with a dark rim behind the items over a
suspect's head (Pill.png, 9-sliced the same way) and the reveal's soft spotlight (Spotlight.png).

Run again after changing anything below:  python Tools/Lineup/make_lineup_icons.py
Needs Pillow.
"""
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "ART", "Lineup")
SIZE = 256

# Icons made from the overlay pictures in OUT.
FROM_OVERLAY = {"Icon_Cap": "Cap", "Icon_Beanie": "Beanie", "Icon_Bag": "Bag", "Icon_Mask": "Mask", "Icon_Glasses": "Glasses", "Icon_FaceMask": "FaceMask",
                "Icon_Mustache": "Mustache", "Icon_Newspaper": "Newspaper", "Icon_Gloves": "Gloves"}


def fit(picture):
    """Crops to what is drawn and centres it on a square transparent canvas with a margin."""
    picture = picture.crop(picture.getbbox())
    scale = (SIZE * 0.86) / max(picture.size)
    picture = picture.resize((max(1, int(picture.width * scale)), max(1, int(picture.height * scale))), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(picture, ((SIZE - picture.width) // 2, (SIZE - picture.height) // 2))
    return canvas


def badge(good):
    """A round tick (green) or cross (red) badge with a dark rim."""
    scale = 4
    big = SIZE * scale
    picture = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(picture)
    fill = (84, 186, 74, 255) if good else (214, 58, 46, 255)
    draw.ellipse((big * 0.04, big * 0.04, big * 0.96, big * 0.96), fill=(43, 22, 16, 255))
    draw.ellipse((big * 0.11, big * 0.11, big * 0.89, big * 0.89), fill=fill)
    stroke = int(big * 0.11)
    if good:
        draw.line([(big * 0.27, big * 0.52), (big * 0.44, big * 0.69), (big * 0.74, big * 0.33)],
                  fill=(255, 255, 255, 255), width=stroke, joint="curve")
    else:
        draw.line([(big * 0.31, big * 0.31), (big * 0.69, big * 0.69)], fill=(255, 255, 255, 255), width=stroke)
        draw.line([(big * 0.69, big * 0.31), (big * 0.31, big * 0.69)], fill=(255, 255, 255, 255), width=stroke)
    return picture.resize((SIZE, SIZE), Image.LANCZOS)


CARD_SIZE = 128
CARD_BORDER = 40


def card():
    scale = 4
    big = Image.new("RGBA", (CARD_SIZE * scale, CARD_SIZE * scale), (0, 0, 0, 0))
    ImageDraw.Draw(big).rounded_rectangle((0, 0, CARD_SIZE * scale - 1, CARD_SIZE * scale - 1), radius=34 * scale, fill=(255, 255, 255, 255))
    return big.resize((CARD_SIZE, CARD_SIZE), Image.LANCZOS)


def pill():
    scale = 4
    big = Image.new("RGBA", (CARD_SIZE * scale, CARD_SIZE * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(big)
    draw.rounded_rectangle((0, 0, CARD_SIZE * scale - 1, CARD_SIZE * scale - 1), radius=34 * scale, fill=(43, 22, 16, 255))
    rim = 7 * scale
    draw.rounded_rectangle((rim, rim, CARD_SIZE * scale - 1 - rim, CARD_SIZE * scale - 1 - rim), radius=27 * scale, fill=(250, 237, 204, 255))
    return big.resize((CARD_SIZE, CARD_SIZE), Image.LANCZOS)


def spotlight(size=256):
    """White, brightest in the middle and fading smoothly to nothing at the edge."""
    y, x = np.mgrid[0:size, 0:size]
    r = np.hypot(x - (size - 1) / 2, y - (size - 1) / 2) / (size / 2)
    alpha = np.clip(1 - r, 0, 1) ** 1.6
    pixels = np.zeros((size, size, 4), np.uint8)
    pixels[..., :3] = 255
    pixels[..., 3] = (alpha * 255).astype(np.uint8)
    return Image.fromarray(pixels, "RGBA")


def wall_background(width=2048, height=540, blur=26, light=0.55):
    picture = Image.open(os.path.join(ROOT, "Assets", "ART", "BG", "WallPlay", "BG.png")).convert("RGB")
    picture = picture.resize((width, height), Image.LANCZOS).filter(ImageFilter.GaussianBlur(blur))
    return Image.eval(picture, lambda v: int(v * light)).convert("RGBA")


def save(name, picture):
    os.makedirs(OUT, exist_ok=True)
    picture.save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


if __name__ == "__main__":
    for name, overlay in FROM_OVERLAY.items():
        save(name, fit(Image.open(os.path.join(OUT, overlay + ".png")).convert("RGBA")))
    cap = fit(Image.open(os.path.join(OUT, "Cap.png")).convert("RGBA")).resize((SIZE * 6 // 10,) * 2, Image.LANCZOS)
    beanie = fit(Image.open(os.path.join(OUT, "Beanie.png")).convert("RGBA")).resize((SIZE * 6 // 10,) * 2, Image.LANCZOS)
    hats = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    hats.alpha_composite(beanie, (SIZE * 4 // 10, SIZE * 1 // 10))
    hats.alpha_composite(cap, (0, SIZE * 3 // 10))
    save("Icon_Hat", hats)
    save("WallBackground", wall_background())
    save("Card", card())
    save("Pill", pill())
    save("Spotlight", spotlight())
    save("Badge_Yes", badge(True))
    save("Badge_No", badge(False))
