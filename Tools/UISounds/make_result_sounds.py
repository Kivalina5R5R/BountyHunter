"""Synthesises the result screen's sounds into Assets/Audio/Result (44.1 kHz, 16-bit WAV), soft and
low-passed so nothing is shrill on the speakers. ResultSounds plays it; the applause is a recording
(Assets/Sound/BGM/Clapping.mp3) and Result_BGM is the music.

- SFX_Result_Tick.wav      a score counting up: a tiny wooden tick (ResultSounds raises its pitch
                           as the count goes on)

Run again after changing anything below:  python Tools/UISounds/make_result_sounds.py
Needs numpy and scipy.
"""
import os
import wave

import numpy as np
from scipy import signal

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Audio", "Result")
RATE = 44100


def seconds(length):
    return np.arange(int(length * RATE)) / RATE


def tone(frequency):
    """A tone following frequency (an array), with continuous phase."""
    return np.sin(2 * np.pi * np.cumsum(np.asarray(frequency, dtype=np.float64)) / RATE)


def lowpass(sound, cutoff):
    return signal.lfilter(*signal.butter(2, cutoff, "lowpass", fs=RATE), sound)


def tick():
    t = seconds(0.06)
    wood = tone(np.full(len(t), 700.0)) * np.exp(-t * 70) + 0.3 * tone(np.full(len(t), 1610.0)) * np.exp(-t * 110)
    return lowpass(wood * np.minimum(1, t / 0.002), 2500)


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
    save("SFX_Result_Tick.wav", finish(tick(), 0.5, 0.01))
