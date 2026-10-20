"""Synthesises the intro's bin crash into Assets/Audio/Intro/SFX_Bin_Crash.wav (44.1 kHz, 16-bit):
a metal bin knocked flying: a dull clanking boom, trash crackling out, cans knocking, and the lid
landing and rattling round on its rim until it lies flat. Everything is struck noise ringing through
resonators, so it clanks like metal instead of humming pure tones.
The bin is added over the video (IntroBinCrash), so the video's own sound has nothing for it. Also
copies the video's whole sound track as Intro_Original.wav. The car, siren and jumps come from the
video itself, without its music (split_original_track.py).
Run again after changing anything below:  python Tools/IntroSounds/make_intro_sounds.py
Needs numpy and scipy, and ffmpeg on the PATH for the original track.
"""
import os
import subprocess
import wave

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Intro")
VIDEO = os.path.join(ROOT, "Assets", "ART", "Intro", "BountyHunter_Intro_NoLogo.mp4")
RATE = 44100
rng = np.random.default_rng(7)

# The bin is a hollow metal can: a dull body with a few clanky modes (frequency, gain, Q).
# The lid is thinner and rings higher.
BIN_MODES = [(205, 1.0, 9), (430, 0.8, 11), (810, 0.6, 14), (1320, 0.45, 16), (2050, 0.3, 18), (3300, 0.2, 20)]
LID_MODES = [(1150, 1.0, 22), (2480, 0.7, 26), (3950, 0.45, 30)]


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def burst(length, decay):
    """A struck noise: sharp attack, quick fade."""
    t = seconds(length)
    return rng.standard_normal(len(t)) * np.exp(-t * decay)


def ring(excitation, modes):
    """The excitation through a band-pass resonator per mode, slightly detuned each time."""
    out = np.zeros_like(excitation)
    for frequency, gain, q in modes:
        b, a = signal.iirpeak(frequency * rng.uniform(0.97, 1.03), q, fs=RATE)
        out += gain * signal.lfilter(b, a, excitation)
    return out


def band(sound, low, high):
    return signal.lfilter(*signal.butter(2, [low, high], "bandpass", fs=RATE), sound)


def place(out, sound, at):
    i = int(at * RATE)
    n = max(0, min(len(sound), len(out) - i))
    out[i:i + n] += sound[:n]


def bin_crash(length=1.4):
    out = np.zeros(int(length * RATE))
    # The hit: the car's bumper slams the bin, which crumples and booms.
    place(out, 1.4 * ring(burst(0.35, 30), BIN_MODES), 0.0)
    place(out, 0.7 * band(burst(0.12, 40), 900, 7000), 0.0)
    t = seconds(0.18)
    place(out, 0.9 * np.sin(2 * np.pi * np.cumsum(80 * np.exp(-t * 9) + 45) / RATE) * np.exp(-t * 22), 0.0)
    # Trash spilling out: a scatter of short crackles (paper, plastic) over the first half second.
    for _ in range(26):
        place(out, rng.uniform(0.05, 0.22) * band(burst(0.03, 160), 2500, 12000), rng.uniform(0.04, 0.6))
    # A couple of cans knocking about.
    for at in (0.2, 0.34, 0.5):
        can = [(rng.uniform(1700, 2400), 1.0, 30), (rng.uniform(3600, 4600), 0.5, 34)]
        place(out, 0.35 * ring(burst(0.15, 90), can), at)
    # The lid lands twice, then rattles round on its rim, faster and fainter, until it lies flat.
    for at, gain in ((0.16, 0.8), (0.3, 0.55)):
        place(out, gain * ring(burst(0.2, 70), LID_MODES), at)
    at, gap, gain = 0.42, 0.075, 0.4
    while gap > 0.012 and at < length - 0.1:
        place(out, gain * ring(burst(0.05, 140), LID_MODES), at)
        at += gap
        gap *= 0.86
        gain *= 0.9
    return out


def finish(sound, peak=0.9, fade_out=0.03):
    sound = sound / (np.abs(sound).max() + 1e-9) * peak
    n = int(fade_out * RATE)
    sound[-n:] *= np.linspace(1, 0, n)
    sound[: int(0.002 * RATE)] *= np.linspace(0, 1, int(0.002 * RATE))
    return sound


def save(name, sound):
    os.makedirs(OUT, exist_ok=True)
    data = (np.clip(sound, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print("wrote", name, f"{len(sound) / RATE:.2f}s")


if __name__ == "__main__":
    save("SFX_Bin_Crash.wav", finish(bin_crash(), peak=0.95))
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", VIDEO, "-vn", "-ac", "2", "-ar", str(RATE),
                    os.path.join(OUT, "Intro_Original.wav")], check=True)
    print("wrote Intro_Original.wav")
