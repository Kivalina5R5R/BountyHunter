"""Synthesises the suspect lineup's sounds (mini game 1) into Assets/Audio/Lineup (44.1 kHz, 16-bit mono
WAV), in the soft, playful cartoon-cop mood of the gameplay sounds: round, low-passed, nothing shrill
on the wall and floor speakers. LineupSfx plays them; the ring step and the bounty's coin are the
select screen's and the gameplay's own.

Suspects coming in (pair by pair):
- SFX_Lineup_Drop       a pair of suspects dropping into the lineup: a plucked double-bass note with
                        a finger snap, in the night-time detective mood of a jazz club; LineupSfx
                        plays each pair a step higher, so the three pairs walk up like a bass line

Clues and answers:
- SFX_Lineup_Clue       a clue card going up: a paper flick and a mellow two-note "da-dum"
- SFX_Lineup_Locked     a player locking in their answer: a latch clicking shut and a rising chime

The clock:
- SFX_Lineup_Hurry       each of the last seconds: a muted wood "tock" over a soft low pulse, like a
                         heartbeat
- SFX_Lineup_Hurry_Final the last three: the same, a step higher and a little firmer
- SFX_Lineup_TimeUp      answers closed: a mellow vibraphone "ding-dong" falling, with a soft low knock

The reveal (silent until the stamp, straight after the time-up chime):
- SFX_Lineup_Stamp      CAUGHT stamped on it: a firm "ka-CHUNK", the stamp's handle clacking down and
                        the rubber slapping the paper, a clear thwack rather than a dull thud
- SFX_Lineup_Cheer      someone got it right: a confetti popper and a little rising marimba run

Run again after changing anything below:  python Tools/LineupSounds/make_lineup_sounds.py
Only some sounds:                          python Tools/LineupSounds/make_lineup_sounds.py SFX_Lineup_Clue
Needs numpy and scipy.
"""
import os
import sys
import wave
import zlib

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Lineup")
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


def marimba(frequency, length=0.6):
    t = seconds(length)
    note = tone(np.full(len(t), frequency)) * np.exp(-t * 6)
    overtone = 0.3 * tone(np.full(len(t), frequency * 4)) * np.exp(-t * 28)
    knock = 0.15 * lowpass(rng.standard_normal(len(t)), 1800) * np.exp(-t * 120)
    return (note + overtone + knock) * attack(t)


def room(sound, seconds_=0.35, mix=0.25):
    """A little soft space round a sound, so it does not sound dry and boxed in."""
    t = seconds(seconds_)
    tail = lowpass(noise(seconds_), 2500) * np.exp(-t * 9)
    tail /= np.abs(tail).sum() + 1e-9
    return sound + mix * 40 * signal.fftconvolve(sound, tail)[:len(sound)]


def fade_end(sound, seconds_):
    n = int(seconds_ * RATE)
    sound[-n:] *= np.linspace(1, 0, n) ** 2
    return sound


# Suspects coming in ----------------------------------------------------------------------------------

def snap():
    """A finger snap: a short, dry, mid crack, its top rolled off."""
    t = seconds(0.06)
    return lowpass(band(noise(0.06), 1200, 3500), 3800) * np.exp(-t * 110) * attack(t, 0.0005)


def bass_pluck(frequency, length=0.8):
    """A plucked upright bass: a round, woody low note with a soft thump of the string."""
    t = seconds(length)
    note = sum(tone(np.full(len(t), frequency * k)) * np.exp(-t * (3 + 4 * k)) / k ** 1.2 for k in range(1, 6))
    finger = 0.4 * thump(0.08, frequency * 3, frequency * 2, 40)
    out = note * attack(t, 0.004)
    place(out, finger, 0)
    return lowpass(out, 1400)


def drop():
    # A walking-bass note on A1 (LineupSfx steps it up for each pair) with a finger snap on top,
    # landing as the pair drops onto their plates (0.18 s in).
    length = 0.9
    out = np.zeros(int(length * RATE))
    place(out, bass_pluck(110.0, length - 0.15), 0.15)
    place(out, 0.45 * snap(), 0.15)
    return fade_end(room(out, mix=0.25), 0.15)


# The clock --------------------------------------------------------------------------------------------

def hurry(pitch, firm):
    length = 0.45
    out = np.zeros(int(length * RATE))
    # A soft low pulse, like a heartbeat, under a muted wood "tock".
    place(out, 0.9 * thump(0.35, 90 * pitch, 60 * pitch, 11), 0)
    t = seconds(0.08)
    tock = tone(np.full(len(t), 700 * pitch)) * np.exp(-t * 60) + 0.4 * band(noise(0.08), 600, 2000) * np.exp(-t * 110)
    place(out, firm * tock * attack(t, 0.001), 0.005)
    return fade_end(room(lowpass(out, 2400), mix=0.15), 0.1)


def time_up():
    length = 1.2
    out = np.zeros(int(length * RATE))
    # A mellow vibraphone "ding-dong" falling a fourth, with a soft low knock under the first note.
    for at, frequency in ((0.0, 784.0), (0.22, 587.3)):
        t = seconds(length - at)
        tremolo = 1 + 0.15 * np.sin(2 * np.pi * 5 * t)
        note = (tone(np.full(len(t), frequency)) + 0.2 * tone(np.full(len(t), frequency * 3.98)) * np.exp(-t * 12))
        place(out, 0.7 * note * np.exp(-t * 3.5) * tremolo * attack(t, 0.004), at)
    place(out, 0.6 * thump(0.3, 160, 100, 14), 0)
    return fade_end(room(lowpass(out, 3000), seconds_=0.5, mix=0.3), 0.3)


# Clues and answers ----------------------------------------------------------------------------------

def clue():
    out = np.zeros(int(0.9 * RATE))
    # The card flicked up onto the board.
    place(out, 0.35 * lowpass(band(noise(0.06), 1200, 4500), 4000) * np.sin(np.pi * seconds(0.06) / 0.06), 0)
    place(out, 0.3 * thump(0.08, 260, 180, 50), 0.045)
    # A mellow "da-dum": a fourth up, like a detective's "aha".
    place(out, 0.8 * marimba(392.0, 0.5), 0.06)
    place(out, 0.9 * marimba(523.3, 0.8), 0.19)
    return room(lowpass(out, 3000), mix=0.2)


def locked():
    out = np.zeros(int(0.7 * RATE))
    # A latch clicking shut: two firm plastic clicks.
    for at, pitch in ((0.0, 1.0), (0.05, 1.3)):
        t = seconds(0.04)
        click = (tone(np.full(len(t), 900 * pitch)) * np.exp(-t * 120) + 0.5 * band(noise(0.04), 1500, 4000) * np.exp(-t * 200))
        place(out, 0.45 * click, at)
    # A short rising chime: it is in.
    place(out, 0.7 * marimba(659.3, 0.45), 0.09)
    place(out, 0.8 * marimba(987.8, 0.55), 0.16)
    return room(lowpass(out, 3500), mix=0.2)


# The reveal ---------------------------------------------------------------------------------------------

def stamp():
    length = 0.6
    t = seconds(length)
    out = np.zeros(len(t))
    # "ka-": the stamp's wooden handle clacking down.
    clack = seconds(0.05)
    place(out, 0.5 * (tone(np.full(len(clack), 620)) * np.exp(-clack * 90) + 0.5 * band(noise(0.05), 800, 2500) * np.exp(-clack * 150)), 0)
    # "-CHUNK": the rubber slapping the paper (a clear mid thwack) over the desk taking the weight.
    hit = seconds(length - 0.06)
    slap = band(noise(length - 0.06), 400, 2200) * np.exp(-hit * 38)
    body = thump(length - 0.06, 200, 90, 14)
    place(out, 0.9 * slap + 0.9 * body, 0.06)
    return fade_end(room(lowpass(out, 3000), mix=0.2), 0.12)


def cheer():
    length = 1.3
    out = np.zeros(int(length * RATE))
    # The confetti popper: a round pop and a puff.
    place(out, 0.9 * thump(0.15, 260, 120, 30) + 0.4 * lowpass(noise(0.15), 2000) * np.exp(-seconds(0.15) * 45), 0)
    # Paper pieces fluttering down.
    for _ in range(40):
        at = rng.uniform(0.05, 1.0)
        flick = lowpass(band(noise(0.02), 1500, 4500), 4000) * np.exp(-seconds(0.02) * 150)
        place(out, rng.uniform(0.03, 0.1) * flick, at)
    # A happy little run up: C E G C.
    for i, frequency in enumerate((523.3, 659.3, 784.0, 1046.5)):
        place(out, 0.55 * marimba(frequency, 0.5), 0.08 + i * 0.07)
    return fade_end(room(lowpass(out, 3500), mix=0.2), 0.2)


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
    ("SFX_Lineup_Drop", drop, 0.7, 0.02),
    ("SFX_Lineup_Clue", clue, 0.65, 0.02),
    ("SFX_Lineup_Locked", locked, 0.65, 0.02),
    ("SFX_Lineup_Hurry", lambda: hurry(1.0, 0.75), 0.8, 0.02),
    ("SFX_Lineup_Hurry_Final", lambda: hurry(1.19, 1.0), 0.9, 0.02),
    ("SFX_Lineup_TimeUp", time_up, 0.6, 0.02),
    ("SFX_Lineup_Stamp", stamp, 0.9, 0.02),
    ("SFX_Lineup_Cheer", cheer, 0.7, 0.02),
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
