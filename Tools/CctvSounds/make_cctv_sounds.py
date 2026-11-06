"""Synthesises the security camera's own sounds (mini game 2) into Assets/Audio/Cctv (44.1 kHz, 16-bit
mono WAV), in the soft, playful cartoon-cop mood of the gameplay sounds: round, low-passed, nothing
shrill on the wall and floor speakers. CctvSfx plays them; picking, the clock, the CAUGHT stamp and
the cheer are the lineup's own, the moment being the same.

The clip on the camera:
- SFX_Cctv_Jump        a thief leaping (running in, or out of a bin): a light, springy little "doink"
- SFX_Cctv_Land        a thief dropping into a bin: a hollow plastic "bonk" with the lid clapping;
                       CctvSfx plays each bin a note of its own

Banana peels:
- SFX_Cctv_Peel_Throw  a peel flapping towards the camera: a few soft, muffled "flup"s swelling
                       into the splat, low and quiet under everything else
- SFX_Cctv_Peel_Splat  the peel slapping onto the screen: a wet, squishy "splat"

The reveal:
- SFX_Cctv_Peek        a thief popping up out of its bin: the lid flipping open and a bouncy "bloop"

Run again after changing anything below:  python Tools/CctvSounds/make_cctv_sounds.py
Only some sounds:                          python Tools/CctvSounds/make_cctv_sounds.py SFX_Cctv_Land
Needs numpy and scipy.
"""
import os
import sys
import wave
import zlib

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Cctv")
RATE = 44100
rng = np.random.default_rng(23)


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def tone(frequency):
    """A tone following frequency (an array), with continuous phase."""
    return np.sin(2 * np.pi * np.cumsum(np.asarray(frequency, dtype=np.float64)) / RATE)


def lowpass(sound, cutoff):
    return signal.lfilter(*signal.butter(2, cutoff, "lowpass", fs=RATE), sound)


def band(sound, low, high):
    return signal.lfilter(*signal.butter(2, [low, high], "bandpass", fs=RATE), sound)


def noise(length):
    return rng.standard_normal(int(length * RATE))


def place(out, sound, at):
    i = int(at * RATE)
    n = max(0, min(len(sound), len(out) - i))
    out[i:i + n] += sound[:n]


def attack(t, seconds_=0.003):
    return np.minimum(1, t / seconds_)


def thump(length, start, end, decay):
    """A low drum-like hit: a sine falling from start to end Hz."""
    t = seconds(length)
    return tone(end + (start - end) * np.exp(-t * 30)) * np.exp(-t * decay) * attack(t)


def sweep(length, start, end, width=0.6):
    """Noise swept through a band from start to end Hz (its middle)."""
    t = seconds(length)
    centre = start * (end / start) ** (t / length)
    out = np.zeros(len(t))
    for low in (150, 300, 600, 1200, 2400):
        out += band(noise(length), low, low * 2) * np.exp(-(np.log(centre / (low * 1.41)) / width) ** 2)
    return out


def room(sound, seconds_=0.3, mix=0.2):
    """A little soft space round a sound, so it does not sound dry and boxed in."""
    t = seconds(seconds_)
    tail = lowpass(noise(seconds_), 2500) * np.exp(-t * 10)
    tail /= np.abs(tail).sum() + 1e-9
    return sound + mix * 40 * signal.fftconvolve(sound, tail)[:len(sound)]


def fade_end(sound, seconds_):
    n = int(seconds_ * RATE)
    sound[-n:] *= np.linspace(1, 0, n) ** 2
    return sound


# The clip on the camera ---------------------------------------------------------------------------

def jump():
    length = 0.2
    t = seconds(length)
    # A light, springy little "doink" as the thief pushes off: a soft round note springing up, a
    # quick wobble on it like a spring, no air or rustle (noise there sounded like paper tearing).
    rise = 330 * 2 ** (0.7 * np.minimum(1, t / 0.06))
    wobble = 1 + 0.06 * np.sin(2 * np.pi * 28 * t) * np.exp(-t * 18)
    pitch = rise * wobble
    note = tone(pitch) + 0.25 * tone(pitch * 2)
    shape = attack(t, 0.004) * np.exp(-t * 16)
    return lowpass(note * shape, 1800)


def land():
    length = 0.45
    t = seconds(length)
    # A hollow plastic bonk (the bin's body ringing round, a little nasal), the lid clapping shut on it.
    body = thump(length, 260, 170, 14) + 0.45 * tone(np.full(len(t), 340)) * np.exp(-t * 22) * attack(t)
    out = body + 0.25 * lowpass(noise(length), 1500) * np.exp(-t * 70)
    clap = seconds(0.08)
    place(out, 0.45 * (tone(np.full(len(clap), 520)) * np.exp(-clap * 60) + 0.4 * band(noise(0.08), 900, 2500) * np.exp(-clap * 120)), 0.07)
    return fade_end(room(lowpass(out, 2600), mix=0.15), 0.08)


# Banana peels -----------------------------------------------------------------------------------------

def peel_throw():
    length = 0.35
    t = seconds(length)
    # A floppy peel flapping towards the camera: a few soft, muffled "flup"s, closer and quicker each
    # time, swelling into the splat (it lasts the peel's flight). Low and quiet, no note and no rush
    # of air, so it sits under the other sounds.
    rate = 9 + 9 * (t / length)
    flap = np.sin(np.pi * np.mod(np.cumsum(rate) / RATE, 1.0)) ** 3
    soft = lowpass(band(noise(length), 150, 700), 500)
    shape = 0.3 + 0.7 * (t / length) ** 1.5
    return fade_end(soft * flap * shape, 0.03)


def peel_splat():
    length = 0.4
    t = seconds(length)
    # A wet slap onto the glass: a soft, squishy burst over a dull thud, with a little suck after it.
    slap = lowpass(band(noise(length), 250, 2000), 1800) * np.exp(-t * 28)
    body = 0.7 * thump(length, 180, 100, 20)
    suck = np.zeros(len(t))
    place(suck, 0.25 * band(noise(0.1), 400, 1200) * np.sin(np.pi * seconds(0.1) / 0.1), 0.09)
    return fade_end(room(slap + body + suck, mix=0.15), 0.08)


# The reveal ---------------------------------------------------------------------------------------------

def peek():
    length = 0.45
    t = seconds(length)
    out = np.zeros(len(t))
    # The lid flipped open: a light plastic clack.
    clack = seconds(0.06)
    place(out, 0.45 * (tone(np.full(len(clack), 480)) * np.exp(-clack * 70) + 0.4 * band(noise(0.06), 800, 2200) * np.exp(-clack * 130)), 0)
    # A bouncy "bloop" as the thief pops up: a round tone sliding up.
    up = seconds(0.22)
    bloop = tone(300 * 2 ** (np.minimum(1, up / 0.12))) * np.sin(np.pi * np.minimum(1, up / 0.22)) ** 1.2
    place(out, 0.7 * (bloop + 0.2 * tone(600 * 2 ** (np.minimum(1, up / 0.12))) * np.sin(np.pi * np.minimum(1, up / 0.22))), 0.03)
    return fade_end(room(lowpass(out, 2500), mix=0.15), 0.1)


# Output -------------------------------------------------------------------------------------------------

def finish(sound, peak, fade_out=0.02):
    sound = sound / (np.abs(sound).max() + 1e-9) * peak
    if fade_out > 0:
        n = int(fade_out * RATE)
        sound[-n:] *= np.linspace(1, 0, n)
    return sound


def save(name, sound):
    os.makedirs(OUT, exist_ok=True)
    data = (np.clip(sound, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print(f"  {name}  {len(sound) / RATE:.2f}s")


# Every sound: its file, how it is made and its peak (and end fade).
SOUNDS = [
    ("SFX_Cctv_Jump", jump, 0.45, 0.01),
    ("SFX_Cctv_Land", land, 0.65, 0.02),
    ("SFX_Cctv_Peel_Throw", peel_throw, 0.4, 0.02),
    ("SFX_Cctv_Peel_Splat", peel_splat, 0.7, 0.02),
    ("SFX_Cctv_Peek", peek, 0.6, 0.02),
]


if __name__ == "__main__":
    # Each sound has its own random seed (from its name), so remaking or changing one leaves the
    # others exactly as they were. Names on the command line make only those sounds.
    wanted = set(sys.argv[1:])
    for name, make, peak, fade in SOUNDS:
        if wanted and name not in wanted:
            continue
        rng = np.random.default_rng(zlib.crc32(name.encode()))
        save(name + ".wav", finish(make(), peak, fade))
