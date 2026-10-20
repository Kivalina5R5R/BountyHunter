"""Synthesises the character-select sounds into Assets/Audio/Select (44.1 kHz, 16-bit WAV). They
are kept soft and round (marimba) and low-passed, so nothing is shrill on
the floor speakers, and in the playful cartoon-cop mood of the game:

- SFX_Card_Pop.wav     a card swishing in and landing with a soft pop (its SELECT button comes
                       up with it, without a sound of its own)
- SFX_Step_On.wav      a soft, warm bop on a light thump when a player steps onto a card
- SFX_Load_Climb.wav   the ring filling: soft marimba notes climbing G4 to G5 over the 2 s the ring
                       takes to fill, ringing on a moment after; CharacterCardSounds plays it in step with the ring
- SFX_Selected.wav     a soft rubber-stamp thump and a marimba "ta-da" (G major, rising)

Run again after changing anything below:  python Tools/UISounds/make_select_sounds.py
Needs numpy and scipy.
"""
import os
import wave

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Select")
RATE = 44100
rng = np.random.default_rng(11)


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def tone(frequency):
    """A tone following frequency (an array), with continuous phase."""
    return np.sin(2 * np.pi * np.cumsum(np.asarray(frequency, dtype=np.float64)) / RATE)


def lowpass(sound, cutoff):
    return signal.lfilter(*signal.butter(2, cutoff, "lowpass", fs=RATE), sound)


def band(sound, low, high):
    return signal.lfilter(*signal.butter(2, [low, high], "bandpass", fs=RATE), sound)


def place(out, sound, at):
    i = int(at * RATE)
    n = max(0, min(len(sound), len(out) - i))
    out[i:i + n] += sound[:n]


def marimba(frequency, length=0.9):
    """A soft mallet on a wooden bar: the note, its fourth harmonic dying fast, a muffled knock."""
    t = seconds(length)
    note = tone(np.full(len(t), frequency)) * np.exp(-t * 5.5)
    overtone = 0.3 * tone(np.full(len(t), frequency * 4)) * np.exp(-t * 28)
    knock = 0.15 * lowpass(rng.standard_normal(len(t)), 1800) * np.exp(-t * 120)
    return (note + overtone + knock) * np.minimum(1, t / 0.003)


def card_pop():
    out = np.zeros(int(0.45 * RATE))
    # A soft swish as the card spins in (air, low-passed so it is not hissy).
    t = seconds(0.24)
    air = rng.standard_normal(len(t)) * np.sin(np.pi * t / 0.24) ** 2
    place(out, 0.3 * band(air, 300, 2200), 0.0)
    # Landing: a round, low bloop like a cork, with a marimba note under it.
    t = seconds(0.2)
    bloop = tone(200 + 380 * (1 - (1 - t / 0.2) ** 3)) * np.exp(-t * 18) * np.minimum(1, t / 0.004)
    place(out, 0.7 * bloop, 0.2)
    place(out, 0.35 * marimba(587.3, 0.25), 0.21)
    return lowpass(out, 4500)


def glide(length, start, end, curve=3.0):
    """A pitch moving from start to end, fast at first."""
    t = seconds(length) / length
    return start + (end - start) * (1 - (1 - t) ** curve)


def step_on():
    t = seconds(0.22)
    # A warm bop: a short downward note on a soft thump.
    bop = (tone(glide(0.22, 470, 330, 2)) + 0.3 * tone(glide(0.22, 940, 660, 2))) * np.exp(-t * 16)
    thump = tone(110 * np.exp(-t * 10) + 60) * np.exp(-t * 30)
    return (0.8 * bop + 0.6 * thump) * np.minimum(1, t / 0.003)


def load_climb(fill=2.0, tail=0.4):
    """The ring filling over fill seconds (CharacterSelect.holdSeconds): soft marimba notes climbing
    the G major scale from G4 to G5, the last landing just as the ring completes, then ringing on."""
    out = np.zeros(int((fill + tail) * RATE))
    notes = (392.0, 440.0, 493.9, 523.3, 587.3, 659.3, 740.0, 784.0)
    step = (fill - 0.05) / (len(notes) - 1)
    for k, frequency in enumerate(notes):
        # A little louder as it climbs, so it builds toward the stamp.
        place(out, (0.45 + 0.05 * k) * marimba(frequency, 0.5), k * step)
    return lowpass(out, 4500)


def selected():
    out = np.zeros(int(1.4 * RATE))
    # The rubber stamp: a soft, low thump (no paper slap).
    t = seconds(0.25)
    place(out, 0.9 * tone(95 * np.exp(-t * 10) + 55) * np.exp(-t * 18) * np.minimum(1, t / 0.004), 0.0)
    place(out, 0.25 * lowpass(rng.standard_normal(len(t)), 900) * np.exp(-t * 40), 0.0)
    # "Ta-da": G4, B4, D5 rising, then G5 held with the chord ringing under it.
    for k, note in enumerate((392.0, 493.9, 587.3)):
        place(out, 0.5 * marimba(note, 0.9), 0.12 + k * 0.09)
    place(out, 0.65 * marimba(784.0, 1.1), 0.12 + 3 * 0.09)
    place(out, 0.3 * marimba(392.0, 1.1), 0.12 + 3 * 0.09)
    return lowpass(out, 4500)


def finish(sound, peak, fade_out=0.03):
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
    print("wrote", name, f"{len(sound) / RATE:.2f}s")


if __name__ == "__main__":
    save("SFX_Card_Pop.wav", finish(card_pop(), 0.7))
    save("SFX_Step_On.wav", finish(step_on(), 0.7))
    save("SFX_Load_Climb.wav", finish(load_climb(), 0.65))
    save("SFX_Selected.wav", finish(selected(), 0.8))
