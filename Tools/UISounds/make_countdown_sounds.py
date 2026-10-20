"""Synthesises the countdown sounds into Assets/Audio/Countdown (44.1 kHz, 16-bit WAV). Each sounds
as its number lands, LAND seconds into its clip (FloorCountdown's fall: fallPart x stepSeconds).

The set in use is the arcade race start: a short, warm square-wave bip on A4 for 3, 2 and 1 and a
longer bip a fourth higher, gliding up a touch, for GO! The square wave is built only from its
harmonics below 1.8 kHz and starts gently, so it is round and never shrill, and a light vibrato
and a soft reverb tail make it float:

- SFX_Count_3.wav, SFX_Count_2.wav, SFX_Count_1.wav, SFX_Count_Go.wav

A second set to try instead, a xylophone (a woody tick climbing 3, 2, 1 and a chord for GO!), is
written to Assets/Audio/Countdown/Xylophone with the same names: drag them onto
FloorCountdownSounds to compare.

Run again after changing anything below:  python Tools/UISounds/make_countdown_sounds.py
Needs numpy and scipy.
"""
import os
import wave

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Countdown")
RATE = 44100
# When the number hits the floor, seconds into its clip.
LAND = 0.25
# The highest harmonic kept, so nothing is shrill.
CEILING = 1800.0
# How much of the floating reverb tail is mixed in, and how long it rings.
REVERB_MIX = 0.35
REVERB_SECONDS = 0.8
rng = np.random.default_rng(31)


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def lowpass(sound, cutoff):
    return signal.lfilter(*signal.butter(2, cutoff, "lowpass", fs=RATE), sound)


def soft_square(pitch):
    """A square wave following pitch (an array), from its odd harmonics below CEILING only."""
    phase = 2 * np.pi * np.cumsum(pitch) / RATE
    out = np.zeros_like(phase)
    k = 1
    while k * pitch.max() < CEILING:
        out += np.sin(k * phase) / k
        k += 2
    return out


def bip(frequency, length, release, rise=0.0):
    """An arcade bip: a soft square note, optionally gliding up by rise (a fraction) at the start."""
    t = seconds(length)
    pitch = frequency * (1 + rise * (1 - np.exp(-t * 25))) * (1 + 0.006 * np.sin(2 * np.pi * 5.5 * t))
    envelope = np.minimum(1, t / 0.015) * np.clip((length - t) / release, 0, 1)
    return soft_square(pitch) * envelope


def reverb(sound):
    """A soft room around the sound: a decaying, darkened noise tail convolved in."""
    t = seconds(REVERB_SECONDS)
    tail = lowpass(rng.standard_normal(len(t)), 2500) * np.exp(-t * 6)
    tail[: int(0.02 * RATE)] = 0
    wet = signal.fftconvolve(sound, tail)[: len(sound)]
    wet = wet / (np.abs(wet).max() + 1e-9) * np.abs(sound).max()
    return (1 - REVERB_MIX) * sound + REVERB_MIX * wet


def xylophone(frequency, length=0.6):
    """A wooden bar struck with a hard mallet: the note, a bright overtone dying fast, a click."""
    t = seconds(length)
    note = np.sin(2 * np.pi * frequency * t) * np.exp(-t * 9)
    overtone = 0.25 * np.sin(2 * np.pi * frequency * 3 * t) * np.exp(-t * 40)
    click = 0.2 * lowpass(rng.standard_normal(len(t)), 2500) * np.exp(-t * 200)
    return (note + overtone + click) * np.minimum(1, t / 0.002)


def at_landing(sound, length=1.0):
    out = np.zeros(int(length * RATE))
    i = int(LAND * RATE)
    n = min(len(sound), len(out) - i)
    out[i:i + n] = sound[:n]
    return reverb(lowpass(out, CEILING))


def finish(sound, peak):
    return sound / (np.abs(sound).max() + 1e-9) * peak


def save(folder, name, sound):
    os.makedirs(folder, exist_ok=True)
    data = (np.clip(sound, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(folder, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print("wrote", os.path.relpath(os.path.join(folder, name), ROOT), f"{len(sound) / RATE:.2f}s")


if __name__ == "__main__":
    # Arcade, in use.
    short = finish(at_landing(bip(440.0, 0.15, 0.06)), 0.42)
    for n in (3, 2, 1):
        save(OUT, f"SFX_Count_{n}.wav", short)
    save(OUT, "SFX_Count_Go.wav", finish(at_landing(bip(587.3, 0.6, 0.35, rise=0.03), 1.4), 0.46))

    # Xylophone, to try.
    folder = os.path.join(OUT, "Xylophone")
    for n, note in ((3, 523.3), (2, 587.3), (1, 659.3)):
        save(folder, f"SFX_Count_{n}.wav", finish(at_landing(xylophone(note)), 0.6))
    chord = sum(xylophone(note, 1.0) for note in (523.3, 659.3, 784.0))
    save(folder, "SFX_Count_Go.wav", finish(at_landing(chord, 1.3), 0.65))
