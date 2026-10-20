"""Makes Assets/ART/UI/Kiosk/KioskBG_Rim.png: the light of the police car's light bar catching the
dogs' outlines on the kiosk picture, only where an edge faces the light bar, and weaker the further
it is from the car. KioskPoliceLight tints it red or blue with the lamps.

The dogs are cut out of the picture with rembg (isnet-general-use); below the logo only, and not the
car. Each edge's facing comes from the cut-out's slope; LIGHT is where the light bar is in the picture.

usage: python make_kiosk_rim.py
Needs rembg, numpy and opencv-python.
"""
import os

import cv2
import numpy as np
from PIL import Image
from rembg import new_session, remove

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PICTURE = os.path.join(ROOT, "Assets", "ART", "UI", "Kiosk", "KioskBG.png")
OUT = os.path.join(ROOT, "Assets", "ART", "UI", "Kiosk", "KioskBG_Rim.png")
# The light bar, in picture pixels (y down).
LIGHT = (90.0, 640.0)
# Where the dogs are (rows), and the car's box left out of the cut-out (x up to, rows).
DOG_ROWS = (480, 1360)
CAR = (200, 600, 930)
# How deep the lit edge is, how tightly it must face the light, how fast it fades with distance.
EDGE = 9
FACING_POWER = 2.2
FADE = 480.0
STRENGTH = 2.3

picture = Image.open(PICTURE).convert("RGB")
mask = np.asarray(remove(picture, session=new_session("isnet-general-use"), only_mask=True), np.float32) / 255
h, w = mask.shape

keep = np.zeros_like(mask)
keep[DOG_ROWS[0]:DOG_ROWS[1], :] = 1
keep[CAR[1]:CAR[2], :CAR[0]] = 0
keep = cv2.GaussianBlur(keep, (0, 0), 18)
dogs = np.clip((mask - 0.35) / 0.4, 0, 1) * keep

# The outline band, just inside the edge.
band = np.clip(dogs - cv2.erode(dogs, np.ones((EDGE, EDGE), np.uint8)), 0, 1)
band = cv2.GaussianBlur(band, (0, 0), 1.2)

# Which way each edge faces: away from the inside of the cut-out.
soft = cv2.GaussianBlur(dogs, (0, 0), 3)
nx = -cv2.Sobel(soft, cv2.CV_32F, 1, 0, ksize=5)
ny = -cv2.Sobel(soft, cv2.CV_32F, 0, 1, ksize=5)
length = np.sqrt(nx ** 2 + ny ** 2) + 1e-6
nx, ny = nx / length, ny / length

# Toward the light bar, and how far it is.
yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
lx, ly = LIGHT[0] - xx, LIGHT[1] - yy
distance = np.sqrt(lx ** 2 + ly ** 2) + 1e-6
facing = np.clip(nx * lx / distance + ny * ly / distance, 0, 1) ** FACING_POWER
fade = np.exp(-np.maximum(distance - 150, 0) / FADE)

rim = cv2.GaussianBlur(np.clip(band * facing * fade * STRENGTH, 0, 1), (0, 0), 0.8)
out = np.zeros((h, w, 4), np.uint8)
out[..., :3] = 255
out[..., 3] = (rim * 255).astype(np.uint8)
cv2.imwrite(OUT, out)
print("wrote", OUT)
