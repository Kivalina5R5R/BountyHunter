"""Synthesises the boss chase's sounds into Assets/Audio/Boss (44.1 kHz, 16-bit mono WAV), in the
soft, playful cartoon-cop mood of the gameplay sounds: round, low-passed, nothing shrill on the wall
and floor speakers. BossSfx plays them; its footfalls, the hiding spots, the scuffle, the handcuffs and
the bounty's chime are the gameplay's own.

The entrance:
- SFX_Boss_Gust         the feathers blowing in: a soft swell of wind, rising and settling
- SFX_Boss_Appear       the boss puffing up in its smoke: a cloud swelling into a round "fwump" and
                        a little three-note sparkle, a magician's "ta-da"

Its moves:
- SFX_Boss_Smoke_Lit    the smoke bomb lit as the boss takes it out: two lighter clicks, a soft flame
                        "fwoof" and the fuse fizzing low while a note creeps up to the throw
- SFX_Boss_Smoke        the smoke bomb thrown down: a little toss, a clay "pok" on the floor and the
                        smoke billowing out in a thick, rolling "pffffshhh"
- SFX_Boss_Gun_Pump     the water gun raised: two plastic "chk-shk" pumps
- SFX_Boss_Gun_Fire     the water shot into the air: a "thup", a watery "bloop" springing up and the
                        spray whooshing away
- SFX_Boss_Rain_Warn    the warning circles swirling (1.6 s, as long as they last): a soft, pulsing
                        swirl of water rising towards the strike
- SFX_Boss_Rain_Strike  the water coming down: a splash with a short electric crackle and droplets
- SFX_Boss_Soaked       a player hit by the water: a wet "splat" and a dribble
- SFX_Boss_Cape         the boss starting its disguise: the cape swirled round twice and snapped
                        shut, two sly low plucks sneaking down under it
- SFX_Boss_Disguised    the passer-by fading in: a soft, sly shimmer
- SFX_Boss_Reveal       the cape thrown off: a cloth "fwap" and a two-note pop up

The chase's end:
- SFX_Boss_Hit          a touch that counts: a round "bonk" and a springy "bwip" (the chime follows)
- SFX_Boss_Caught       the boss caught: a deep thump and a bright mallet run up into a held chord
- SFX_Boss_Escape       the boss getting away: a whoosh off and three sly notes stepping down

The rooftop:
- SFX_Boss_Heli_Loop    the police helicopter far off behind the buildings: a muffled rotor "whup"
                        over a low engine hum, a seamless 2 s loop faded in and out with each pass

Run again after changing anything below:  python Tools/BossSounds/make_boss_sounds.py
Only some sounds:                          python Tools/BossSounds/make_boss_sounds.py SFX_Boss_Hit
Needs numpy and scipy.
"""
import os
import sys
import wave
import zlib

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Boss")
RATE = 44100
rng = np.random.default_rng(29)


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def tone(frequency):
    """A tone following frequency (an array), with continuous phase."""
    return np.sin(2 * np.pi * np.cumsum(np.asarray(frequency, dtype=np.float64)) / RATE)


def steady(length, frequency):
    return tone(np.full(int(length * RATE), float(frequency)))


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


def mallet(length, frequency, decay=6.0):
    """A soft wooden-mallet note: round, with a quiet octave that dies first."""
    t = seconds(length)
    return (steady(length, frequency) + 0.3 * steady(length, frequency * 2) * np.exp(-t * 12)) * np.exp(-t * decay) * attack(t, 0.004)


def plip(length, frequency):
    """A tiny water droplet: a short note bending up."""
    t = seconds(length)
    return tone(frequency * (1 + 0.5 * np.minimum(1, t / 0.03))) * np.exp(-t * 70) * attack(t, 0.002)


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


# The entrance -----------------------------------------------------------------------------------------

def gust():
    length = 2.6
    t = seconds(length)
    # A soft swell of wind as the feathers blow in: breathy, low, its colour drifting up and back down.
    wind = sweep(length, 280, 650, 0.8)
    shape = np.sin(np.pi * np.minimum(1, t / length)) ** 1.5
    drift = 1 + 0.25 * np.sin(2 * np.pi * 1.3 * t)
    return fade_end(lowpass(wind * shape * drift, 1400), 0.3)


def appear():
    length = 0.9
    t = seconds(length)
    out = np.zeros(len(t))
    # The cloud swelling up (its noise opening as it grows) into a round "fwump" as the boss shows.
    swell = lowpass(noise(length), 900) * np.minimum(1, t / 0.32) ** 2 * np.exp(-np.maximum(0, t - 0.32) * 12)
    out += 0.8 * swell
    place(out, thump(0.45, 210, 90, 10), 0.3)
    # A little magician's sparkle on top, three soft notes up.
    for at, note in ((0.36, 784), (0.43, 988), (0.5, 1175)):
        place(out, 0.18 * mallet(0.4, note, 9), at)
    return room(lowpass(out, 2600), 0.3, 0.15)


# Its moves ---------------------------------------------------------------------------------------------

def smoke_lit():
    length = 0.9
    t = seconds(length)
    out = np.zeros(len(t))
    # The boss flicking a lighter at the fuse: two clicks of the wheel, a little flame catching with a
    # soft "fwoof", then the fuse fizzing warm and low (a few tiny pops in it) while a low note creeps
    # up under it, the tension rising to the throw.
    for at in (0.0, 0.11):
        c = seconds(0.04)
        place(out, 0.4 * (steady(0.04, 1300) * 0.4 + band(noise(0.04), 900, 2200)) * np.exp(-c * 140), at)
    f = seconds(0.25)
    place(out, 0.55 * lowpass(noise(0.25), 500) * attack(f, 0.03) * np.exp(-f * 9), 0.14)
    fizz_from = 0.2
    fizz = band(noise(length), 350, 1300) * (0.8 + 0.2 * np.sin(2 * np.pi * 17 * t))
    fizz *= np.minimum(1, np.maximum(0, t - fizz_from) / 0.15) * (0.6 + 0.4 * t / length)
    out += 0.3 * fizz
    for _ in range(6):
        p = seconds(0.03)
        place(out, 0.18 * band(noise(0.03), 500, 1500) * np.exp(-p * 150), rng.uniform(0.25, 0.85))
    creep = seconds(length - fizz_from)
    place(out, 0.08 * tone(130 * 2 ** (creep / (length - fizz_from) * 5 / 12)) * np.minimum(1, creep / 0.2), fizz_from)
    return lowpass(out, 1800)


def smoke():
    length = 1.3
    t = seconds(length)
    out = np.zeros(len(t))
    # The bomb tossed down (a quick little whoosh) and cracking on the floor with a clay "pok", then
    # the smoke billowing out: a thick, rolling "pffffshhh" that spreads and thins, churning as it
    # goes, with a low puff under it.
    toss = seconds(0.1)
    place(out, 0.3 * sweep(0.1, 500, 1000, 0.6) * (toss / 0.1) ** 2, 0)
    place(out, 0.5 * thump(0.1, 700, 420, 45) + 0.25 * band(noise(0.1), 900, 2500) * np.exp(-toss * 70), 0.1)
    b = seconds(length - 0.12)
    churn = 0.7 + 0.3 * lowpass(rng.standard_normal(len(b)), 12) / 0.15
    billow = sweep(length - 0.12, 1300, 350, 0.9) * churn * attack(b, 0.04) * np.exp(-b * 2.6)
    place(out, billow + 0.6 * thump(length - 0.12, 140, 70, 7), 0.12)
    return fade_end(room(lowpass(out, 2400), 0.35, 0.2), 0.3)


def gun_pump():
    length = 0.36
    t = seconds(length)
    out = np.zeros(len(t))

    def click(pitch):
        c = seconds(0.06)
        return band(noise(0.06), 800, 2800) * np.exp(-c * 90) + 0.3 * steady(0.06, pitch) * np.exp(-c * 120)

    # Two plastic pumps, "chk" back and "shk" home, with the slide rubbing between them.
    place(out, click(1100), 0)
    slide = seconds(0.1)
    place(out, 0.15 * band(noise(0.1), 400, 1400) * np.sin(np.pi * slide / 0.1), 0.03)
    place(out, 0.9 * click(900), 0.14)
    return lowpass(out, 3200)


def gun_fire():
    length = 0.7
    t = seconds(length)
    out = np.zeros(len(t))
    # The water gun fired into the air, cartoon-style: a pressure "thup", a round watery "bloop"
    # springing up (a glug of water leaving the barrel), and the spray whooshing up and away.
    out += 0.6 * thump(length, 220, 110, 30)
    b = seconds(0.22)
    glug = 220 * 2.6 ** np.minimum(1, b / 0.12) * (1 + 0.05 * np.sin(2 * np.pi * 30 * b))
    place(out, 0.55 * tone(glug) * np.exp(-b * 14) * attack(b, 0.005), 0.01)
    w = seconds(0.55)
    place(out, 0.35 * sweep(0.55, 450, 1300, 0.6) * attack(w, 0.02) * np.exp(-w * 5), 0.05)
    return fade_end(lowpass(out, 2200), 0.12)


def rain_warn():
    length = 1.6
    t = seconds(length)
    # The circles swirling as the water comes: a soft swirl rising towards the strike, pulsing with the
    # rings (2.5 a second), with a low hum climbing under it.
    swirl = sweep(length, 300, 1100, 0.7)
    pulse = 0.75 + 0.25 * np.sin(2 * np.pi * 2.5 * t)
    hum = 0.2 * tone(110 * 1.5 ** (t / length))
    shape = attack(t, 0.15) * (0.3 + 0.7 * (t / length) ** 1.5)
    return lowpass((swirl * pulse + hum) * shape, 1800)


def rain_strike():
    length = 1.0
    t = seconds(length)
    out = np.zeros(len(t))
    # The water slapping down: a wide, soft splash on a low thud.
    out += lowpass(noise(length), 2200) * np.exp(-t * 9) * attack(t, 0.004)
    out += 0.8 * thump(length, 180, 80, 12)
    # A short electric crackle with the flicker on the spot, kept low.
    crackle = np.zeros(int(0.25 * RATE))
    for _ in range(26):
        crackle[rng.integers(0, len(crackle))] = rng.uniform(0.5, 1)
    c = seconds(0.25)
    place(out, 0.9 * band(crackle, 900, 2600) * np.exp(-c * 10), 0.0)
    # Droplets landing after it.
    for _ in range(7):
        place(out, 0.1 * plip(0.08, rng.uniform(550, 950)), rng.uniform(0.08, 0.6))
    return fade_end(room(lowpass(out, 2800), 0.3, 0.2), 0.25)


def soaked():
    length = 0.6
    t = seconds(length)
    out = np.zeros(len(t))
    # A wet "splat" on the player, squishy, then a little dribble running off.
    squish = 0.75 + 0.25 * np.sin(2 * np.pi * 40 * t)
    out += band(noise(length), 300, 1700) * np.exp(-t * 14) * attack(t, 0.005) * squish
    out += 0.8 * thump(length, 220, 110, 18)
    for at, note in ((0.2, 820), (0.29, 700), (0.37, 600)):
        place(out, 0.14 * plip(0.08, note), at)
    return fade_end(lowpass(out, 2400), 0.1)


def cape():
    length = 1.0
    t = seconds(length)
    out = np.zeros(len(t))
    # The boss swirling its cape round itself to start the disguise: the cloth sweeping round twice
    # ("fwoo-fwoo", the second faster), snapped shut with a "fwap", and two sly, low tiptoe plucks
    # sneaking down under it (up to something).
    for at, span, low, high in ((0, 0.3, 300, 700), (0.24, 0.24, 400, 900)):
        s = seconds(span)
        place(out, 0.7 * sweep(span, low, high, 0.6) * np.sin(np.pi * s / span) ** 2, at)
    snap = seconds(0.15)
    place(out, 0.8 * band(noise(0.15), 300, 1800) * np.exp(-snap * 35) * attack(snap, 0.002), 0.46)
    for at, note in ((0.5, 196), (0.68, 165)):
        p = seconds(0.3)
        place(out, 0.45 * (steady(0.3, note) + 0.4 * steady(0.3, note * 2) * np.exp(-p * 20)) * np.exp(-p * 12) * attack(p, 0.004), at)
    return fade_end(lowpass(out, 2000), 0.1)


def disguised():
    length = 1.0
    t = seconds(length)
    out = np.zeros(len(t))
    # The passer-by fading in: a soft, sly shimmer of quiet notes, wobbling a little, on a breath.
    for i, note in enumerate((587, 740, 880, 1109)):
        n = seconds(length - i * 0.07)
        wobble = 1 + 0.004 * np.sin(2 * np.pi * 6 * n)
        place(out, 0.22 * tone(note * wobble) * np.exp(-n * 3.5) * attack(n, 0.03), i * 0.07)
    out += 0.12 * lowpass(noise(length), 900) * np.sin(np.pi * t / length)
    return fade_end(room(lowpass(out, 2500), 0.3, 0.2), 0.2)


def reveal():
    length = 0.75
    t = seconds(length)
    out = np.zeros(len(t))
    # A short swish up and the cape snapped off with a "fwap", then a two-note pop up: ta-da.
    lead = seconds(0.12)
    place(out, 0.5 * sweep(0.12, 400, 900, 0.6) * (lead / 0.12) ** 2, 0)
    f = seconds(0.2)
    place(out, band(noise(0.2), 300, 2000) * np.exp(-f * 25) * attack(f, 0.002), 0.12)
    place(out, 0.4 * mallet(0.4, 523, 8), 0.16)
    place(out, 0.45 * mallet(0.5, 784, 6), 0.26)
    return fade_end(lowpass(out, 2600), 0.1)


# The chase's end ------------------------------------------------------------------------------------

def hit():
    length = 0.45
    t = seconds(length)
    out = np.zeros(len(t))
    # A round "bonk" as the player gets the boss, and a springy "bwip" up as it is knocked away.
    out += thump(length, 400, 180, 14) + 0.35 * band(noise(length), 500, 2000) * np.exp(-t * 60)
    b = seconds(0.18)
    place(out, 0.4 * tone(500 * 1.8 ** np.minimum(1, b / 0.1)) * np.exp(-b * 18) * attack(b), 0.06)
    return lowpass(out, 2400)


def caught():
    length = 2.2
    t = seconds(length)
    out = np.zeros(len(t))
    # Got him! A deep thump, a bright mallet run up and a warm chord held under a gentle shimmer.
    out += 0.9 * thump(length, 140, 55, 6)
    for i, note in enumerate((523, 659, 784, 1047)):
        place(out, 0.35 * mallet(0.6, note, 7), 0.05 + i * 0.09)
    chord_at = 0.45
    c = seconds(length - chord_at)
    shimmer = 1 + 0.08 * np.sin(2 * np.pi * 5 * c)
    chord = sum(mallet(length - chord_at, note, 2.2) for note in (523, 659, 784, 1047)) * shimmer
    place(out, 0.3 * chord, chord_at)
    return fade_end(room(lowpass(out, 3000), 0.4, 0.25), 0.4)


def escape():
    length = 1.2
    t = seconds(length)
    out = np.zeros(len(t))
    # The boss zipping off, then three sly notes stepping down: it got away this time.
    w = seconds(0.6)
    place(out, 0.7 * sweep(0.6, 900, 300, 0.6) * np.sin(np.pi * w / 0.6) ** 2, 0)
    for at, note, decay in ((0.32, 392, 7), (0.46, 330, 7), (0.6, 262, 3.5)):
        place(out, 0.35 * mallet(length - at, note, decay), at)
    return fade_end(room(lowpass(out, 2200), 0.3, 0.15), 0.2)


# The rooftop ------------------------------------------------------------------------------------------

def heli_loop():
    length = 2.0
    t = seconds(length)
    # Made round (every part repeats a whole number of times in the loop, the noise filtered in a
    # circle) so it loops without a seam: far-off rotor beats (12 a second) on muffled noise, over a
    # low engine hum.
    spectrum = np.fft.rfft(noise(length))
    frequencies = np.fft.rfftfreq(int(length * RATE), 1 / RATE)
    spectrum *= 1 / (1 + (frequencies / 450) ** 4)
    muffled = np.fft.irfft(spectrum, int(length * RATE))
    muffled /= np.abs(muffled).max() + 1e-9
    beat = np.abs(np.sin(np.pi * 12 * t)) ** 4
    hum = 0.3 * np.sin(2 * np.pi * 80 * t) + 0.15 * np.sin(2 * np.pi * 160 * t)
    return muffled * (0.25 + 0.75 * beat) + hum * (0.8 + 0.2 * beat)


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


# Every sound: its file, how it is made and its peak (and end fade; none for the loop).
SOUNDS = [
    ("SFX_Boss_Gust", gust, 0.5, 0.05),
    ("SFX_Boss_Appear", appear, 0.65, 0.05),
    ("SFX_Boss_Smoke_Lit", smoke_lit, 0.5, 0.03),
    ("SFX_Boss_Smoke", smoke, 0.65, 0.05),
    ("SFX_Boss_Gun_Pump", gun_pump, 0.55, 0.01),
    ("SFX_Boss_Gun_Fire", gun_fire, 0.55, 0.03),
    ("SFX_Boss_Rain_Warn", rain_warn, 0.45, 0.03),
    ("SFX_Boss_Rain_Strike", rain_strike, 0.7, 0.05),
    ("SFX_Boss_Soaked", soaked, 0.65, 0.03),
    ("SFX_Boss_Cape", cape, 0.5, 0.02),
    ("SFX_Boss_Disguised", disguised, 0.45, 0.05),
    ("SFX_Boss_Reveal", reveal, 0.6, 0.03),
    ("SFX_Boss_Hit", hit, 0.7, 0.02),
    ("SFX_Boss_Caught", caught, 0.75, 0.1),
    ("SFX_Boss_Escape", escape, 0.6, 0.05),
    ("SFX_Boss_Heli_Loop", heli_loop, 0.5, 0),
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
