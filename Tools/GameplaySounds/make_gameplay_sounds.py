"""Synthesises the gameplay sounds into Assets/Audio/Gameplay (44.1 kHz, 16-bit mono WAV), in the soft,
playful cartoon-cop mood of the select and countdown sounds: round, low-passed, nothing shrill on the
floor speakers. GameSfx plays them.

A thief running away (only then; every thief, three picked at random so a run does not repeat):
- SFX_Run_0..2           little feet running: a quick left-right pair of light taps (not squeaky)

Hiding spots (one sound per map; in = a thief dives in, out = it comes out):
- SFX_Wheelie_In                  the street's bins: lid flipped up, a hollow plastic thud with a
                                  crinkle of rubbish bags, the lid clapping shut
- SFX_Wheelie_Out                 popping out: the lid shoved open, a rush of air and rubbish, feet
                                  landing, the lid dropping back
- SFX_Bush_In / SFX_Bush_Out      bush: fresh green leaves swished aside (soft and a little roomy,
                                  not dry and crackly); coming out is a single shiver
- SFX_Crate_In / SFX_Crate_Out    crate or barrel: two wooden knocks and a creak
- SFX_Step_On_Bin / _Bush / _Crate   a player stepping onto a hiding spot: the spot giving a little
- SFX_Flush_Build                 the 2 s a player stands on a spot with a thief in it: a soft
                                  trembling speeding up over a quiet rising hum (played in step)
- SFX_Flushed                     a springy boing as a player standing on the spot flushes it out

Catching a thief and grabbing a passer-by by mistake share the same fight cloud, so their sounds
tell them apart from the first moment: a catch is bright and rising (it is won), a mistake is dull
and falling (it went wrong).
- SFX_Scuffle      catching a thief: a quick grab swish, punchy thumps and pops bouncing upward,
                   ending in a rising whoop as the cloud bursts
- SFX_Cuffs        handcuffs ratcheting shut: click-click-click-CLACK
- SFX_Coin         the bounty paid: a mellow rising chime, G C E G (soft attack, pure round
                   tones, low-passed; a pleasant reward, never piercing)
- SFX_Bump         grabbing a passer-by: a hollow cartoon bonk, a clumsy muffled tumble with
                   pops sliding down
- SFX_Wrong        the big X slamming down on the passer-by: a stamp thud and a low "eh-eh" buzzer

Bombs:
- SFX_Bomb_Drop    the bomb landing with a bounce
- SFX_Bomb_Fuse    the fuse fizzing (loops seamlessly)
- SFX_Bomb_Boom    a round cartoon boom
- SFX_Player_Hit   a player caught in the blast: a thump and a falling boing

The game's rhythm:
- SFX_Poster_Pin   a poster swishing up, pinned into the cork with a round "tock", the paper settling
- SFX_Tick         the last ten seconds of a stage, one per second (a wood block)
- SFX_Tick_Final   the last three seconds (higher)
- SFX_Time_Up      the stage's clock running out: a police whistle
- SFX_Map_Whoosh   moving on to the next map

Run again after changing anything below:  python Tools/GameplaySounds/make_gameplay_sounds.py
Only some sounds:                          python Tools/GameplaySounds/make_gameplay_sounds.py SFX_Run_0 SFX_Poster_Pin
Needs numpy and scipy.
"""
import os
import sys
import wave
import zlib

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Gameplay")
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


def glide(length, start, end, curve=3.0):
    """A pitch moving from start to end, fast at first."""
    t = seconds(length) / length
    return start + (end - start) * (1 - (1 - t) ** curve)


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


# Running thieves ------------------------------------------------------------------------------------

def pat(length, start, end, decay, brightness):
    """One footfall: a small low thud and a touch of contact noise."""
    t = seconds(length)
    return thump(length, start, end, decay) + brightness * band(noise(length), 900, 3500) * np.exp(-t * 90)


def tap(pitch):
    """A small foot tapping the pavement: a short hollow tick with a little body under it."""
    t = seconds(0.05)
    tick = band(noise(0.05), 350 * pitch, 1600 * pitch) * np.exp(-t * 110)
    body = 0.6 * thump(0.05, 230 * pitch, 150 * pitch, 80)
    return (tick + body) * attack(t, 0.001)


def run(variant):
    # Little feet running: a quick left-right pair of light taps, clear but not squeaky.
    out = np.zeros(int(0.13 * RATE))
    pitch = 1 + 0.06 * (variant - 1)
    place(out, 0.9 * tap(pitch), 0.0)
    place(out, 0.7 * tap(pitch * 0.94), 0.058)
    return lowpass(out, 2200)


# Hiding spots ---------------------------------------------------------------------------------------

def crinkle(length, density=60):
    """Rubbish bags crumpling: a crackle of tiny, bright, uneven clicks dying away."""
    out = np.zeros(int(length * RATE))
    for _ in range(int(density * length)):
        click = band(noise(0.012), 2000, 7000) * np.exp(-seconds(0.012) * 400)
        place(out, click * rng.uniform(0.2, 1), rng.uniform(0, length * 0.9))
    return out * np.exp(-seconds(length) * 3)[:len(out)]


def plastic_clap(frequency=520):
    """A plastic lid clapping onto the rim: a sharp, short, boxy knock."""
    t = seconds(0.12)
    return (tone(np.full(len(t), frequency)) * np.exp(-t * 45)
            + 0.6 * tone(np.full(len(t), frequency * 1.9)) * np.exp(-t * 70)
            + 0.5 * band(noise(0.12), 1000, 4000) * np.exp(-t * 120)) * attack(t, 0.001)


def hollow_plastic(frequency=105):
    """The bin's hollow body taking a weight: a low boxy boom with a short resonance."""
    t = seconds(0.4)
    return (thump(0.4, 200, frequency, 11)
            + 0.45 * tone(np.full(len(t), frequency * 2.2)) * np.exp(-t * 16)
            + 0.2 * tone(np.full(len(t), frequency * 3.5)) * np.exp(-t * 24)) * attack(t)


def whoop(length, start, end):
    """A quick rush of air sweeping up: something springing out."""
    t = seconds(length)
    centre = start + (end - start) * (t / length) ** 0.7
    sweep = np.zeros(len(t))
    for low, high in ((300, 800), (800, 1800), (1800, 3500)):
        sweep += band(noise(length), low, high) * np.exp(-((centre - (low + high) / 2) / 700) ** 2)
    return sweep * np.sin(np.pi * t / length) ** 2


def wheelie(coming_out):
    out = np.zeros(int(0.75 * RATE))
    if coming_out:
        # Pops out in one beat: the lid shoved open from inside, with a short rush of air.
        place(out, 0.9 * plastic_clap(440), 0.0)
        place(out, 0.5 * thump(0.15, 170, 90, 25), 0.0)
        place(out, 0.4 * whoop(0.16, 500, 2200), 0.0)
    else:
        place(out, 0.35 * creak(0.1, 140, 220), 0.0)
        place(out, 0.9 * hollow_plastic(), 0.1)
        place(out, 0.55 * crinkle(0.4), 0.12)
        place(out, 1.0 * plastic_clap(520), 0.42)
    return lowpass(out, 6000)


def rustle(length, swells):
    """Leaves: band-passed noise in quick swells."""
    t = seconds(length)
    envelope = np.zeros(len(t))
    for k in range(swells):
        centre = length * (0.12 + 0.75 * k / max(1, swells - 1))
        envelope += np.exp(-((t - centre) / (0.05 + 0.02 * rng.random())) ** 2) * rng.uniform(0.6, 1)
    leaves = band(noise(length), 1200, 6000) * envelope
    body = 0.4 * band(noise(length), 300, 1200) * envelope
    return lowpass(leaves + body, 6500)


def leaf_ticks(length, count, start_density=1.0):
    """Leaves brushing and snapping back: many tiny crisp ticks, thick at first and thinning out."""
    out = np.zeros(int(length * RATE))
    for _ in range(count):
        at = length * rng.random() ** (2.2 * start_density)
        tick = band(noise(0.01), 1800, 6500) * np.exp(-seconds(0.01) * 450)
        place(out, tick * rng.uniform(0.2, 1) * (1 - at / length), at)
    return out


def wet(sound, seconds_=0.25, mix=0.3):
    """A little room around a sound (a short decaying-noise reverb), so it is not bone dry."""
    impulse = noise(seconds_) * np.exp(-seconds(seconds_) * (6.9 / seconds_))
    tail = signal.fftconvolve(sound, impulse)[:len(sound)]
    tail /= np.abs(tail).max() + 1e-9
    return sound + mix * tail * np.abs(sound).max()


def fresh_leaves(length, attack_seconds=0.03, decay=7.0):
    """Green, springy foliage moving: a soft mid "shhh" fluttering as many leaves shiver at once."""
    t = seconds(length)
    flutter = 0.6 + 0.4 * lowpass(noise(length), 18) / (np.abs(lowpass(noise(length), 18)).max() + 1e-9)
    body = band(noise(length), 450, 2600) + 0.35 * band(noise(length), 2600, 5000)
    envelope = np.minimum(1, t / attack_seconds) * np.exp(-t * decay)
    return body * flutter * envelope


def bush_dive():
    # Diving into a fresh green bush: leaves swished aside, a soft thud of the body landing in the
    # foliage, then the branches springing back with a smaller shiver.
    length = 0.7
    out = np.zeros(int(length * RATE))
    place(out, fresh_leaves(0.45, 0.03, 6.5), 0.0)
    place(out, 0.15 * leaf_ticks(0.35, 25), 0.0)
    place(out, 0.45 * thump(0.2, 140, 75, 16), 0.05)
    place(out, 0.45 * fresh_leaves(0.25, 0.02, 12), 0.32)
    return lowpass(wet(out), 6000)


def bush(coming_out):
    # Coming out is one beat: a single shiver of fresh leaves.
    return lowpass(wet(fresh_leaves(0.32, 0.015, 11)), 6000) if coming_out else bush_dive()


def flush_build(length=2.0):
    """A player standing on a hiding spot with a thief inside, for the 2 s it takes to flush it out:
    the spot trembles softly, slowly at first, then faster and a little higher, over a quiet hum
    rising underneath. Gentle on purpose (soft, low-passed, no hard repeats): GameSfx plays it in
    step with the flush, so it may sound a while."""
    out = np.zeros(int(length * RATE))
    t = seconds(length)
    progress = t / length
    # The trembling: soft knocks, their rate rising from 5 to 14 a second, each a touch higher.
    at = 0.0
    while at < length - 0.02:
        u = at / length
        pitch = 160 + 140 * u
        knock = thump(0.07, pitch * 1.6, pitch, 45) + 0.25 * band(noise(0.07), 400, 1800) * np.exp(-seconds(0.07) * 80)
        place(out, (0.35 + 0.45 * u) * knock, at)
        at += 1 / (5 + 9 * u ** 1.3)
    # The hum: a soft rising tone, swelling with the progress.
    hum = tone(196 + 196 * progress ** 1.5) + 0.3 * tone(2 * (196 + 196 * progress ** 1.5))
    out += 0.12 * hum * (0.2 + 0.8 * progress ** 1.2) * np.minimum(1, t / 0.15)
    return lowpass(out, 2800)


def step_on(spot):
    """A player stepping onto a hiding spot: one short sound of that spot giving a little."""
    if spot == "bin":
        out = np.zeros(int(0.4 * RATE))
        place(out, 0.8 * hollow_plastic(130), 0.0)
        place(out, 0.35 * plastic_clap(620), 0.03)
        return lowpass(out, 5000)
    if spot == "bush":
        out = np.zeros(int(0.4 * RATE))
        place(out, fresh_leaves(0.25, 0.01, 13), 0.0)
        place(out, 0.3 * thump(0.12, 130, 80, 25), 0.0)
        return lowpass(wet(out), 6000)
    out = np.zeros(int(0.45 * RATE))
    place(out, 0.3 * creak(0.18, 70, 50), 0.0)
    place(out, 0.8 * wood_knock(230), 0.05)
    return lowpass(out, 4500)


def wood_knock(frequency):
    t = seconds(0.18)
    return (tone(np.full(len(t), frequency)) * np.exp(-t * 30)
            + 0.5 * tone(np.full(len(t), frequency * 2.7)) * np.exp(-t * 55)
            + 0.3 * band(noise(0.18), 800, 3000) * np.exp(-t * 90)) * attack(t)


def creak(length, start, end):
    t = seconds(length)
    # A stick-slip creak: a buzzy low tone made of fast little pulses.
    pulses = np.sign(tone(glide(length, start, end, 1.2))) * (0.5 + 0.5 * np.sin(2 * np.pi * 7 * t))
    return band(pulses, 300, 2200) * np.sin(np.pi * t / length) ** 2


def crate(coming_out):
    out = np.zeros(int(0.6 * RATE))
    if coming_out:
        # One beat: a single wooden knock as the lid is pushed up.
        place(out, 0.9 * wood_knock(300), 0.0)
    else:
        place(out, 0.9 * wood_knock(260), 0.0)
        place(out, 0.6 * wood_knock(210), 0.09)
        place(out, 0.3 * creak(0.3, 90, 55), 0.15)
    return lowpass(out, 4500)


def flushed():
    t = seconds(0.55)
    # A cartoon spring: a fast wobble that climbs, then settles.
    pitch = glide(0.55, 180, 520, 1.5) * (1 + 0.18 * np.sin(2 * np.pi * 18 * t) * np.exp(-t * 4))
    spring = tone(pitch) * np.exp(-t * 4.5) * attack(t)
    return lowpass(0.9 * spring + 0.3 * rustle(0.55, 3), 4000)


# Catching ---------------------------------------------------------------------------------------------

def whoosh(length, low, high):
    t = seconds(length)
    return band(noise(length), low, high) * np.sin(np.pi * t / length) ** 2


def pop(start, end, length=0.06):
    """A small cartoon pop sliding from start to end Hz."""
    t = seconds(length)
    return tone(glide(length, start, end, 2)) * np.exp(-t * 60) * attack(t)


def scuffle():
    # Catching a thief: bright and winning. Punchy thumps, whooshes and pops that slide upward,
    # climbing toward a whoop as the cloud bursts (0.95 s).
    length = 1.2
    out = np.zeros(int(length * RATE))
    place(out, 0.5 * whoop(0.14, 600, 2600), 0.0)
    for k in range(8):
        at = 0.06 + k * 0.105 + rng.uniform(-0.02, 0.02)
        place(out, rng.uniform(0.6, 0.95) * thump(0.12, rng.uniform(190, 260), 85, 36), at)
    for k in range(3):
        place(out, 0.3 * whoosh(0.14, 700, 2800), 0.12 + k * 0.25 + rng.uniform(0, 0.04))
    for k in range(6):
        low = 500 + 90 * k
        place(out, 0.3 * pop(low, low * 1.8), 0.12 + k * 0.13 + rng.uniform(-0.02, 0.02))
    place(out, 0.55 * whoop(0.3, 500, 3200), 0.7)
    out *= np.minimum(1, np.linspace(length, 0, len(out)) / 0.2)
    return lowpass(out, 5000)


def metal_click(frequency):
    t = seconds(0.12)
    return (tone(np.full(len(t), frequency)) * np.exp(-t * 45)
            + 0.6 * tone(np.full(len(t), frequency * 1.47)) * np.exp(-t * 60)
            + 0.4 * band(noise(0.12), 3000, 8000) * np.exp(-t * 200)) * attack(t, 0.001)


def cuffs():
    # The ratchet zipping round (little clicks) and the cuff snapping shut (a fuller clack).
    out = np.zeros(int(0.45 * RATE))
    for k in range(4):
        place(out, 0.35 * metal_click(2600 + 120 * k), k * 0.035)
    place(out, 0.7 * thump(0.08, 400, 220, 50), 0.17)
    place(out, 1.0 * metal_click(1800), 0.17)
    place(out, 0.6 * metal_click(2700), 0.175)
    return lowpass(out, 7000)


def chime(frequency, length):
    """A mellow chime (a felt mallet on a soft bar): an almost pure tone with a slow-ish attack and
    a faint octave, nothing bright on top, so it rings without stinging."""
    t = seconds(length)
    return (tone(np.full(len(t), frequency)) * np.exp(-t * 6)
            + 0.08 * tone(np.full(len(t), frequency * 2)) * np.exp(-t * 12)) * np.minimum(1, t / 0.012)


def coin():
    # The bounty paid: four mellow notes climbing G C E G, spaced so they never pile into a
    # loud chord, the last left to ring. Mid-range and low-passed, a reward and not a sting.
    out = np.zeros(int(1.0 * RATE))
    for k, note in enumerate((392.0, 523.3, 659.3, 784.0)):
        place(out, (0.7 + 0.1 * k) * chime(note, 0.7 if k == 3 else 0.3), k * 0.08)
    return lowpass(out, 2200)


def bump():
    # Grabbing a passer-by: dull and clumsy. A hollow cartoon bonk on the head first, so it is
    # clear at once this one went wrong, then a muffled tumble with pops sliding down.
    length = 0.95
    out = np.zeros(int(length * RATE))
    t = seconds(0.25)
    bonk = (tone(glide(0.25, 520, 300, 2)) * np.exp(-t * 18)
            + 0.5 * tone(glide(0.25, 780, 450, 2)) * np.exp(-t * 30)) * attack(t, 0.001)
    place(out, 1.0 * bonk, 0.0)
    for k in range(6):
        at = 0.2 + k * 0.11 + rng.uniform(-0.02, 0.02)
        place(out, rng.uniform(0.4, 0.65) * thump(0.14, rng.uniform(120, 160), 60, 28), at)
    for k in range(4):
        high = 650 - 70 * k
        place(out, 0.22 * pop(high, high * 0.55, 0.08), 0.25 + k * 0.15 + rng.uniform(-0.02, 0.02))
    out *= np.minimum(1, np.linspace(length, 0, len(out)) / 0.2)
    return lowpass(out, 2400)


def wrong():
    # The big X slamming down: a heavy stamp thud with a low game-show buzzer, "eh-eh".
    out = np.zeros(int(0.6 * RATE))
    place(out, 1.0 * thump(0.25, 180, 55, 14), 0.0)
    for k in range(2):
        length = 0.16 if k == 0 else 0.24
        t = seconds(length)
        square = np.sign(tone(np.full(len(t), 110.0))) + 0.6 * np.sign(tone(np.full(len(t), 116.5)))
        gate = np.minimum(1, t / 0.008) * np.minimum(1, (length - t) / 0.03)
        place(out, 0.45 * band(square, 120, 1400) * gate, 0.02 + k * 0.2)
    return lowpass(out, 1800)


# Bombs ------------------------------------------------------------------------------------------------

def bomb_drop():
    out = np.zeros(int(0.45 * RATE))
    place(out, thump(0.2, 140, 60, 22), 0.0)
    place(out, 0.45 * thump(0.15, 170, 80, 30), 0.17)
    place(out, 0.2 * thump(0.1, 190, 90, 40), 0.28)
    t = seconds(0.12)
    place(out, 0.25 * tone(glide(0.12, 600, 380, 2)) * np.exp(-t * 30), 0.0)
    return lowpass(out, 2500)


def bomb_fuse(length=1.0):
    t = seconds(length)
    crackle = band(noise(length), 2500, 9000) * (0.6 + 0.4 * (rng.random(len(t)) > 0.97))
    hiss = 0.5 * band(noise(length), 1200, 4000)
    sound = lowpass(crackle + hiss, 9500)
    # Loops seamlessly: cross-fade the end into the start.
    n = int(0.1 * RATE)
    fade = np.linspace(0, 1, n)
    sound[:n] = sound[:n] * fade + sound[-n:] * (1 - fade)
    return sound[:-n]


def bomb_boom():
    length = 1.4
    out = np.zeros(int(length * RATE))
    t = seconds(length)
    low = tone(35 + 90 * np.exp(-t * 7)) * np.exp(-t * 3.2) * attack(t, 0.002)
    rumble = lowpass(noise(length), 300) * np.exp(-t * 2.8)
    crack = band(noise(0.2), 400, 3000) * np.exp(-seconds(0.2) * 25)
    place(out, 1.0 * low, 0.0)
    place(out, 1.6 * rumble, 0.0)
    place(out, 0.6 * crack, 0.0)
    return lowpass(out, 3500)


def player_hit():
    out = np.zeros(int(0.6 * RATE))
    place(out, thump(0.2, 200, 90, 20), 0.0)
    t = seconds(0.45)
    boing = tone(glide(0.45, 520, 160, 1.4) * (1 + 0.05 * np.sin(2 * np.pi * 14 * t))) * np.exp(-t * 5) * attack(t)
    place(out, 0.6 * boing, 0.05)
    return lowpass(out, 3500)


# The game's rhythm --------------------------------------------------------------------------------------

def poster_pin():
    # The poster swishing up onto the board, the pin going into the cork with a round "tock", and
    # the paper settling with a soft flutter. No metal.
    out = np.zeros(int(0.45 * RATE))
    t = seconds(0.14)
    place(out, 0.5 * band(noise(0.14), 700, 3000) * np.sin(np.pi * t / 0.14) ** 2, 0.0)
    t = seconds(0.12)
    tock = (thump(0.12, 320, 170, 38) + 0.35 * tone(np.full(len(t), 520)) * np.exp(-t * 50)
            + 0.3 * band(noise(0.12), 500, 1500) * np.exp(-t * 90))
    place(out, 0.9 * tock, 0.12)
    t = seconds(0.18)
    flutter = band(noise(0.18), 1200, 4000) * (0.5 + 0.5 * np.sin(2 * np.pi * 28 * t)) * np.exp(-t * 18)
    place(out, 0.25 * flutter, 0.15)
    return lowpass(out, 5000)


def wood_block(frequency):
    t = seconds(0.15)
    return (tone(np.full(len(t), frequency)) * np.exp(-t * 40)
            + 0.4 * tone(np.full(len(t), frequency * 2.3)) * np.exp(-t * 70)) * attack(t, 0.001)


def time_up():
    length = 1.0
    t = seconds(length)
    # A police whistle: two close pitches warbling (the pea), breathy, two blasts.
    warble = 1 + 0.03 * np.sign(np.sin(2 * np.pi * 26 * t))
    whistle = tone(2300 * warble) + 0.5 * tone(2450 * warble)
    breath = 0.3 * band(noise(length), 1800, 4500)
    gate = ((t > 0.0) & (t < 0.28)) | ((t > 0.36) & (t < 0.95))
    envelope = np.convolve(gate.astype(float), np.ones(400) / 400, mode="same")
    return lowpass((whistle + breath) * envelope, 5000)


def map_whoosh():
    length = 0.9
    t = seconds(length)
    out = np.zeros(len(t))
    # Noise swept up and back down through a band, swelling in the middle.
    centre = 400 + 2600 * np.sin(np.pi * t / length)
    for low, high in ((300, 900), (900, 2200), (2200, 4500)):
        weight = np.exp(-((centre - (low + high) / 2) / 900) ** 2)
        out += band(noise(length), low, high) * weight
    return lowpass(out * np.sin(np.pi * t / length) ** 2, 5000)


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
    print("wrote", name, f"{len(sound) / RATE:.2f}s")


# Every sound: its file, how it is made and its peak (and end fade).
SOUNDS = [
    ("SFX_Run_0", lambda: run(0), 0.45, 0.01),
    ("SFX_Run_1", lambda: run(1), 0.45, 0.01),
    ("SFX_Run_2", lambda: run(2), 0.45, 0.01),
    ("SFX_Wheelie_In", lambda: wheelie(False), 0.75, 0.02),
    ("SFX_Wheelie_Out", lambda: wheelie(True), 0.75, 0.02),
    ("SFX_Bush_In", lambda: bush(False), 0.7, 0.02),
    ("SFX_Bush_Out", lambda: bush(True), 0.7, 0.02),
    ("SFX_Crate_In", lambda: crate(False), 0.75, 0.02),
    ("SFX_Crate_Out", lambda: crate(True), 0.75, 0.02),
    ("SFX_Flush_Build", flush_build, 0.5, 0.05),
    ("SFX_Flushed", flushed, 0.7, 0.02),
    ("SFX_Step_On_Bin", lambda: step_on("bin"), 0.33, 0.02),
    ("SFX_Step_On_Bush", lambda: step_on("bush"), 0.55, 0.02),
    ("SFX_Step_On_Crate", lambda: step_on("crate"), 0.6, 0.02),
    ("SFX_Scuffle", scuffle, 0.75, 0.02),
    ("SFX_Cuffs", cuffs, 0.95, 0.02),
    ("SFX_Coin", coin, 0.6, 0.02),
    ("SFX_Bump", bump, 0.7, 0.02),
    ("SFX_Wrong", wrong, 0.7, 0.02),
    ("SFX_Bomb_Drop", bomb_drop, 0.7, 0.02),
    ("SFX_Bomb_Fuse", bomb_fuse, 0.45, 0),
    ("SFX_Bomb_Boom", bomb_boom, 0.9, 0.2),
    ("SFX_Player_Hit", player_hit, 0.75, 0.02),
    ("SFX_Poster_Pin", poster_pin, 0.6, 0.02),
    ("SFX_Tick", lambda: wood_block(880), 0.55, 0.02),
    ("SFX_Tick_Final", lambda: wood_block(1320), 0.65, 0.02),
    ("SFX_Time_Up", time_up, 0.6, 0.02),
    ("SFX_Map_Whoosh", map_whoosh, 0.6, 0.05),
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
