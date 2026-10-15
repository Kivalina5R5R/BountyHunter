# Build Rat's idle loop by mirroring only its head, keeping the body fixed.
# Usage: python Tools/make_head_check_frames.py <idle folder> Rat
from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageOps


def flip_head(image, vertical_offset=0):
    # Blend only at the neck to avoid a cut through the scarf when the head mirrors.
    top, bottom = 580 + vertical_offset, 620 + vertical_offset
    mask = Image.new('L', image.size)
    draw = ImageDraw.Draw(mask)
    for y in range(bottom):
        weight = max(0, min(1, (bottom - y) / (bottom - top)))
        draw.line((0, y, image.width, y), fill=round(255 * weight))
    premultiplied = image.convert('RGBa')
    result = Image.composite(ImageOps.mirror(premultiplied), premultiplied, mask).convert('RGBA')
    result.paste(image.crop((0, bottom, image.width, image.height)), (0, bottom))
    assert result.crop((0, bottom, image.width, image.height)).tobytes() == image.crop((0, bottom, image.width, image.height)).tobytes()
    return result


def turn_pose(image, width, dip):
    # One opaque face per frame: compress and dip before flipping, then settle back.
    # The deformation fades out above the scarf, leaving the body untouched.
    mesh = []
    for top in range(0, image.height, 4):
        bottom = min(top + 4, image.height)

        def source(x, y):
            weight = max(0.0, min(1.0, (620 - y) / 60))
            scale = 1 - (1 - width) * weight
            return (x - image.width / 2) / scale + image.width / 2, y - dip * weight

        corners = [source(0, top), source(0, bottom),
                   source(image.width, bottom), source(image.width, top)]
        mesh.append(((0, top, image.width, bottom), tuple(v for point in corners for v in point)))
    result = image.convert('RGBa').transform(image.size, Image.Transform.MESH, mesh,
                                            Image.Resampling.BICUBIC).convert('RGBA')
    result.paste(image.crop((0, 620, image.width, image.height)), (0, 620))
    return result


if __name__ == '__main__':
    folder, name = Path(sys.argv[1]), sys.argv[2]
    assert name == 'Rat'
    still = Image.open(folder / f'{name}Idle_00.png').convert('RGBA')
    turned = flip_head(still)
    # Anticipation, flip at the compressed pose, then settle. Never dissolve the faces.
    widths = (1.0, 0.96, 0.88, 0.88, 0.96, 1.0)
    dips = (0, 2, 5, 5, 2, 0)
    for index in range(22):
        pose = turned if 7 <= index < 17 else still
        phase = index - 4 if 4 <= index <= 9 else index - 14 if 14 <= index <= 19 else 0
        frame = turn_pose(pose, widths[phase], dips[phase]) if widths[phase] < 1 else pose
        assert frame.size == still.size
        assert frame.crop((0, 620, still.width, still.height)).tobytes() == still.crop((0, 620, still.width, still.height)).tobytes()
        frame.save(folder / f'{name}Idle_{index:02d}.png')
    print('Verified 22 idle frames: single face, anticipation and settle, fixed body.')
