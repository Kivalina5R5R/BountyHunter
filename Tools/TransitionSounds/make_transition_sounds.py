"""Synthesises the wall transitions' sounds into Assets/Audio/Transition (44.1 kHz, 16-bit mono WAV),
in the soft, playful cartoon-cop mood of the gameplay sounds: round, low-passed, nothing shrill on the
wall speakers. CageTransition, PoliceCarTransition and BossTransition play them (TransitionSound);
each is made to the length of the moment it goes with.

The jail cage (between the intro and the first map):
- SFX_Cage_Slide     the heavy cell doors rolling in on their track, speeding up (0.45 s, to the slam)
- SFX_Cage_Slam      the steel doors clashing shut: a heavy thud and a short KLANG of the bars (dying
                     fast, not ringing like a bell), a smaller knock as they bounce, a soft rattle
- SFX_Cage_Open      the latch letting go with a soft clunk, the doors rolling back out (0.5 s)

The police car (the way out of a mini game):
- SFX_Police_Pass    the car going by in 2.1 s: a gentle two-tone siren and an engine hum swelling as
                     it passes, the pitch dropping as it goes (no wail, nothing piercing)

The boss's cut-in:
- SFX_Boss_Feathers  feathers fluttering down: a few soft wing-like rustles
- SFX_Boss_Strike    the streak cracking across like a bolt: a quick airy sweep and a soft, low
                     rumble of thunder (the crack rounded off)
- SFX_Boss_Reveal    the boss popping up: a timpani boom under a low, swelling villain brass chord
- SFX_Boss_Pigeons   the pigeons flying in: a flurry of wing beats and a soft coo
- SFX_Boss_Out       everything sweeping back off the wall: a soft falling whoosh

Run again after changing anything below:  python Tools/TransitionSounds/make_transition_sounds.py
Only some sounds:                          python Tools/TransitionSounds/make_transition_sounds.py SFX_Cage_Slam
Needs numpy and scipy.
"""
import os
import sys
import wave
import zlib

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Transition")
RATE = 44100
rng = np.random.default_rng(23)


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def tone(frequency):
    """A tone following frequency (an array), with continuous phase."""
    return np.sin(2 * np.pi * np.cumsum(np.asarray(frequency, dtype=np.float64)) / RATE)


def lowpass(sound, cutoff):
    return signal.lfilter(*signal.butter(2, cutoff, "lowpass", fs=RATE), sound)


def highpass(sound, cutoff):
    return signal.lfilter(*signal.butter(2, cutoff, "highpass", fs=RATE), sound)


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
    """Noise swept through a band from start to end Hz (its middle), swelling and dying away."""
    t = seconds(length)
    out = np.zeros(len(t))
    centre = start * (end / start) ** (t / length)
    for low in (200, 400, 800, 1600, 3200):
        high = low * 2
        weight = np.exp(-(np.log(centre / (low * 1.41)) / width) ** 2)
        out += band(noise(length), low, high) * weight
    return out


def room(sound, seconds_=0.35, mix=0.25):
    """A little soft space round a sound, so it does not sound dry and boxed in."""
    t = seconds(seconds_)
    tail = lowpass(noise(seconds_), 2500) * np.exp(-t * 9)
    tail /= np.abs(tail).sum() + 1e-9
    return sound + mix * 40 * signal.fftconvolve(sound, tail)[:len(sound)]


# The jail cage -------------------------------------------------------------------------------------

def rolling(length, speed_from, speed_to, swell):
    """A heavy door rolling on its track: a low rumble and the steel humming along the rail, with the
    wheels giving soft, dull knocks over the joints (no clicks)."""
    t = seconds(length)
    rumble = lowpass(band(noise(length), 50, 400), 300)
    hum = band(noise(length), 250, 800) * (0.6 + 0.4 * np.sin(2 * np.pi * 9 * t))
    out = 0.8 * rumble + 0.25 * hum
    # Knocks over the joints, closer together as it speeds up.
    rate = speed_from + (speed_to - speed_from) * (t / length)
    phase = np.cumsum(rate) / RATE
    for i in range(1, int(phase[-1]) + 1):
        at = np.searchsorted(phase, i) / RATE
        knock = 0.5 * thump(0.07, 170, 110, 55) + 0.12 * lowpass(band(noise(0.07), 300, 1200), 900) * np.exp(-seconds(0.07) * 80)
        place(out, knock, at)
    return lowpass(out * swell(t / length), 1400)


def cage_slide():
    return rolling(0.45, 8, 18, lambda u: 0.35 + 0.65 * u ** 1.5)


def bar_clang(length, decay=1.0):
    """Steel bars struck hard: a short, bright, inharmonic KLANG that dies away fast, not a bell."""
    t = seconds(length)
    partials = sum(weight * tone(np.full(len(t), frequency)) * np.exp(-t * rate * decay)
                   for frequency, weight, rate in ((330, 0.7, 14), (587, 0.6, 18), (893, 0.5, 22), (1340, 0.35, 28),
                                                   (1910, 0.2, 36), (2650, 0.1, 46)))
    clank = band(noise(length), 700, 3500) * np.exp(-t * 60)
    return (partials + 0.7 * clank) * attack(t, 0.001)


def cage_slam():
    length = 0.8
    t = seconds(length)
    # The cell doors clashing shut: a heavy thud and a short steel KLANG, the bars knocking against
    # each other once more as the doors bounce, then a soft rattle settling.
    out = 1.1 * thump(length, 150, 58, 9)
    out += 0.9 * bar_clang(length)
    place(out, 0.35 * bar_clang(0.4, 1.4), 0.13)
    place(out, 0.4 * thump(0.25, 140, 80, 18), 0.13)
    for at in (0.2, 0.26, 0.31, 0.37):
        place(out, rng.uniform(0.08, 0.15) * lowpass(band(noise(0.04), 600, 2200), 2000) * np.exp(-seconds(0.04) * 110), at)
    # Dies away to nothing well before the end, so nothing is cut off.
    out *= np.minimum(1, (length - t) / 0.25)
    return room(lowpass(out, 4000), mix=0.2)


def cage_open():
    out = np.zeros(int(0.5 * RATE))
    # The latch: a soft, low clunk.
    place(out, 0.7 * thump(0.12, 260, 140, 40) + 0.2 * band(noise(0.12), 500, 1500) * np.exp(-seconds(0.12) * 70), 0)
    place(out, rolling(0.46, 24, 10, lambda u: 0.9 * (1 - u) ** 1.5), 0.04)
    return lowpass(out, 2000)


# The police car ------------------------------------------------------------------------------------

def police_pass(length=2.1):
    t = seconds(length)
    u = t / length
    # Coming closer then going away: louder in the middle, the pitch a touch high then a touch low.
    near = np.exp(-((u - 0.5) / 0.28) ** 2)
    doppler = 1 + 0.05 * np.tanh((0.5 - u) * 6)
    # A gentle hi-lo siren, two soft round notes swapping, not a wail.
    high = (np.floor(t / 0.36) % 2) == 0
    glide = lowpass(np.where(high, 620.0, 470.0), 40)
    pitch = glide * doppler
    siren = tone(pitch) + 0.25 * tone(pitch * 2) + 0.08 * tone(pitch * 3)
    siren = lowpass(siren, 1800) * (0.25 + 0.75 * near)
    # The engine's hum and the tyres' hiss swelling as it passes.
    engine = lowpass(signal.sawtooth(2 * np.pi * np.cumsum(55 * doppler) / RATE), 240) * (0.2 + 0.8 * near)
    tyres = lowpass(band(noise(length), 200, 1500), 1200) * near ** 1.5
    out = 0.55 * siren + 0.5 * engine + 0.45 * tyres
    edge = np.minimum(1, np.minimum(t / 0.15, (length - t) / 0.25))
    # Squeezed a little, so it carries over the game all the way across rather than only at its peak.
    return np.tanh(1.6 * out / (np.abs(out).max() + 1e-9)) * edge


# The boss's cut-in --------------------------------------------------------------------------------

def flap(length=0.09, brightness=1.0):
    """One soft wing beat: a puff of air with a feathery rustle."""
    t = seconds(length)
    shape = np.sin(np.pi * np.minimum(1, t / length)) ** 2
    return (lowpass(band(noise(length), 250, 1800), 1600) + 0.25 * brightness * band(noise(length), 1800, 4000)) * shape


def boss_feathers():
    length = 1.2
    out = np.zeros(int(length * RATE))
    for at in (0.0, 0.18, 0.42, 0.6, 0.85):
        place(out, rng.uniform(0.4, 0.8) * flap(rng.uniform(0.12, 0.2), 0.4), at)
    # A faint airy drift under them.
    t = seconds(length)
    out += 0.15 * lowpass(band(noise(length), 300, 1200), 900) * np.sin(np.pi * t / length)
    return room(lowpass(out, 2400), mix=0.3)


def boss_strike():
    length = 1.1
    t = seconds(length)
    out = np.zeros(len(t))
    # The streak flying across: a quick airy sweep from high to low.
    whoosh = sweep(0.25, 3000, 500) * np.sin(np.pi * np.minimum(1, seconds(0.25) / 0.25)) ** 1.5
    place(out, 1.6 * whoosh, 0)
    # The crack, rounded off: a short low burst rather than a snap.
    place(out, 0.7 * lowpass(noise(0.05), 1400) * np.exp(-seconds(0.05) * 70), 0.12)
    # A low roll of thunder after it.
    roll = lowpass(noise(length - 0.12), 160) * np.exp(-seconds(length - 0.12) * 3.2)
    roll *= 1 + 0.5 * np.sin(2 * np.pi * 7 * seconds(length - 0.12))
    place(out, 2.5 * roll, 0.12)
    place(out, 0.6 * thump(0.5, 90, 45, 6), 0.12)
    return room(lowpass(out, 3500), mix=0.25)


def brass(frequency, length, swell=0.08):
    """A low brass note: a soft sawtooth with a little vibrato, its top rolled off."""
    t = seconds(length)
    vibrato = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.minimum(1, t / 0.3)
    note = signal.sawtooth(2 * np.pi * np.cumsum(frequency * vibrato) / RATE)
    shape = np.minimum(1, t / swell) * np.exp(-t * 1.6)
    return lowpass(lowpass(note, 900), 1400) * shape


def boss_reveal():
    length = 1.8
    out = np.zeros(int(length * RATE))
    # A dark minor chord (D, A, D, F) under a timpani boom.
    for frequency, weight in ((73.4, 0.9), (110.0, 0.6), (146.8, 0.55), (174.6, 0.45)):
        place(out, weight * brass(frequency, length - 0.02), 0.02)
    place(out, 1.6 * thump(1.2, 120, 58, 3.5), 0)
    place(out, 0.3 * lowpass(noise(0.08), 900) * np.exp(-seconds(0.08) * 50), 0)
    return room(out, seconds_=0.6, mix=0.3)


def coo(length=0.5):
    """A pigeon's soft coo: a low hooting note rolling up and down."""
    t = seconds(length)
    pitch = 330 + 40 * np.sin(np.pi * t / length) - 30 * (t / length)
    shape = np.sin(np.pi * np.minimum(1, t / length)) * (1 + 0.35 * np.sin(2 * np.pi * 12 * t))
    return lowpass(tone(pitch) + 0.3 * tone(pitch * 2), 1200) * shape


def boss_pigeons():
    length = 1.1
    out = np.zeros(int(length * RATE))
    # Three birds' wings beating as they come in, each a quick run of flaps.
    for start in (0.0, 0.15, 0.3):
        for i in range(5):
            place(out, rng.uniform(0.5, 0.9) * flap(0.07), start + i * 0.075 + rng.uniform(0, 0.01))
    place(out, 0.35 * coo(0.45), 0.55)
    return room(lowpass(out, 2600), mix=0.25)


def boss_out():
    length = 0.7
    t = seconds(length)
    out = sweep(length, 2200, 300) * np.sin(np.pi * t / length) ** 2
    return room(lowpass(out, 3000), mix=0.2)


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
    ("SFX_Cage_Slide", cage_slide, 0.6, 0.01),
    ("SFX_Cage_Slam", cage_slam, 0.95, 0.08),
    ("SFX_Cage_Open", cage_open, 0.6, 0.05),
    ("SFX_Police_Pass", police_pass, 0.85, 0.05),
    ("SFX_Boss_Feathers", boss_feathers, 0.55, 0.05),
    ("SFX_Boss_Strike", boss_strike, 0.8, 0.05),
    ("SFX_Boss_Reveal", boss_reveal, 0.8, 0.1),
    ("SFX_Boss_Pigeons", boss_pigeons, 0.6, 0.05),
    ("SFX_Boss_Out", boss_out, 0.55, 0.05),
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
