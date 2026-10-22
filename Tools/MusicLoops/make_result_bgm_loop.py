"""Cuts Result_BGM into an intro and a seamless loop for ResultMusic (Assets/Sound/BGM):

- Result_BGM_Intro.wav   the song from its start up to the loop point (played once)
- Result_BGM_Loop.wav    one 8-bar phrase from there (played over and over right after the intro)

The song (152 eighths a minute, a beat every 0.394 s, the first at 0.16 s) does not end where it
could start again, and the mp3's own start has a gap (encoder padding), so looping the file as it is
clicks and jumps. Instead the loop is the 8 bars (64 beats) from LOOP_START, whose harmony and sound
best match the 8 bars after them (found by comparing chroma and spectrum on the beat grid). The
loop's last FADE seconds cross-fade (equal power) into the FADE seconds just before LOOP_START, so
the loop's end runs straight into its own start, and the intro's end runs into it the same way.
The loop end is nudged by a few samples so the two waveforms line up in phase.

Run again after changing anything below:  python Tools/MusicLoops/make_result_bgm_loop.py
Needs numpy and ffmpeg.
"""
import os
import subprocess
import wave

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FOLDER = os.path.join(ROOT, "Assets", "Sound", "BGM")
SOURCE = os.path.join(FOLDER, "Result_BGM.mp3")
RATE = 48000
BEAT = 0.394
FIRST_BEAT = 0.16
# The loop: 64 beats (8 bars) from beat 51.
LOOP_START = FIRST_BEAT + 51 * BEAT
LOOP_BEATS = 64
FADE = 0.5
# How far (seconds) the loop end may move to line the waveforms up.
ALIGN = 0.01


def load():
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", SOURCE, "-ac", "2", "-ar", str(RATE), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, 2).astype(np.float64)


def aligned_end(song, start, end):
    """The loop end within ALIGN of end whose lead-in best matches the loop start's lead-in."""
    span = int(0.05 * RATE)
    reference = song[start - span:start + span].mean(1)
    best, best_end = -np.inf, end
    for shift in range(-int(ALIGN * RATE), int(ALIGN * RATE) + 1):
        candidate = song[end + shift - span:end + shift + span].mean(1)
        score = np.dot(reference, candidate) / (np.linalg.norm(reference) * np.linalg.norm(candidate) + 1e-9)
        if score > best:
            best, best_end = score, end + shift
    return best_end


def save(name, sound):
    data = (np.clip(sound, -1, 1) * 32767).astype(np.int16)
    with wave.open(os.path.join(FOLDER, name), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(data.tobytes())
    print("wrote", name, f"{len(sound) / RATE:.3f}s")


if __name__ == "__main__":
    song = load()
    start = int(round(LOOP_START * RATE))
    end = aligned_end(song, start, int(round((LOOP_START + LOOP_BEATS * BEAT) * RATE)))
    fade = int(FADE * RATE)

    loop = song[start:end].copy()
    # Equal-power cross-fade: the loop's tail fades out as the music just before the loop start
    # fades in, so the last sample leads straight into the first.
    w = np.linspace(0, 1, fade)[:, None]
    loop[-fade:] = loop[-fade:] * np.cos(w * np.pi / 2) + song[start - fade:start] * np.sin(w * np.pi / 2)

    save("Result_BGM_Intro.wav", song[:start])
    save("Result_BGM_Loop.wav", loop)
