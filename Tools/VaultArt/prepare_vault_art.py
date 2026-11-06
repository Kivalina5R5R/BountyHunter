# Prepares the boss vault's props Codex drew (the diamond on its pedestal, the light switch) for
# Unity: crops each to its outline and scales it down.
# Usage: python prepare_vault_art.py <folder with DiamondPedestal.png, LightSwitch.png> <Assets/ART/Vault>
import sys
from PIL import Image

source, out = sys.argv[1], sys.argv[2]


def crop_scale(name, width):
    picture = Image.open(f"{source}/{name}.png").convert("RGBA")
    picture = picture.crop(picture.getbbox())
    height = round(picture.height * width / picture.width)
    picture.resize((width, height), Image.LANCZOS).save(f"{out}/{name}.png")


crop_scale("DiamondPedestal", 512)
crop_scale("LightSwitch", 384)
