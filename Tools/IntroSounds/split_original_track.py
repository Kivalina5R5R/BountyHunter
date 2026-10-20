"""Splits the intro video's own sound track into speech, music and sound effects with Bandit v2
(the cinematic separation model by K. Watcharasupat, trained on Divide and Remaster v3), so the
game can play the original car, siren and jumping sounds without the background music.

On this track the model hears much of the car, siren and jumping as music, so the effects are
taken as its sound-effects stem plus the sharp, short parts of its music stem (a harmonic /
percussive split): the hits, swooshes, rolls and sweeps stay, the held notes of the music go.
Writes Assets/Audio/Intro/Intro_SFX.wav.

Setup (once): clone https://github.com/kwatcharasupat/bandit-v2 and download checkpoint-multi.ckpt
from https://zenodo.org/records/12701995; pip install torch torchaudio pytorch_lightning librosa
soundfile numpy.

usage: python split_original_track.py <bandit-v2 folder> <checkpoint-multi.ckpt>
"""
import os
import subprocess
import sys
import tempfile

import librosa
import numpy as np
import soundfile
import torch

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VIDEO = os.path.join(ROOT, "Assets", "ART", "Intro", "BountyHunter_Intro_NoLogo.mp4")
OUT = os.path.join(ROOT, "Assets", "Audio", "Intro")
FS = 48000

BANDIT, CHECKPOINT = sys.argv[1], sys.argv[2]
sys.path.insert(0, BANDIT)
from src.models.bandit.bandit import Bandit  # noqa: E402

# As in the repository's configs/models/bandit-mus64.yaml, with its three stems.
model = Bandit(
    in_channels=1, stems=["speech", "music", "sfx"], band_type="musical", n_bands=64,
    normalize_channel_independently=False, treat_channel_as_feature=True, n_sqm_modules=8,
    emb_dim=128, rnn_dim=256, bidirectional=True, rnn_type="GRU", mlp_dim=512,
    hidden_activation="Tanh", hidden_activation_kwargs=None, complex_mask=True,
    use_freq_weights=True, n_fft=2048, win_length=2048, hop_length=512, window_fn="hann_window",
    wkwargs=None, power=None, center=True, normalized=True, pad_mode="reflect", onesided=True, fs=FS,
)
state = torch.load(CHECKPOINT, map_location="cpu", weights_only=False)["state_dict"]
weights = {key[len("model."):]: value for key, value in state.items() if key.startswith("model.")}
missing, unexpected = model.load_state_dict(weights, strict=False)
print("weights loaded; missing", len(missing), "unexpected", len(unexpected))
model.eval()

with tempfile.TemporaryDirectory() as temp:
    wav = os.path.join(temp, "intro.wav")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", VIDEO, "-vn", "-ac", "2", "-ar", str(FS), wav], check=True)
    audio, _ = soundfile.read(wav, dtype="float32")

# Each stereo channel is separated on its own (the model takes one channel at a time).
mixture = torch.from_numpy(audio.T.copy())[:, None, :]
with torch.inference_mode():
    estimates = model({"mixture": {"audio": mixture}})["estimates"]

sfx = estimates["sfx"]["audio"][:, 0, :].numpy()
music = estimates["music"]["audio"][:, 0, :].numpy()

# The sharp parts of the music stem: wide in frequency, short in time (margin keeps held notes out).
sharp = []
for channel in music:
    spectrum = librosa.stft(channel, n_fft=4096, hop_length=512)
    _, percussive = librosa.decompose.hpss(spectrum, kernel_size=(31, 31), margin=(3.0, 1.0))
    sharp.append(librosa.istft(percussive, hop_length=512, length=len(channel)))

out = (sfx + np.stack(sharp)).T
os.makedirs(OUT, exist_ok=True)
soundfile.write(os.path.join(OUT, "Intro_SFX.wav"), out, FS, subtype="PCM_16")
print(f"wrote Intro_SFX.wav: {20 * np.log10(np.sqrt((out ** 2).mean()) + 1e-9):.1f} dB RMS")
