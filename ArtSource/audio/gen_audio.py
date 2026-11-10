#!/usr/bin/env python3
"""Generates every sound in SheltreSim, so there is nothing to license and nothing to download.

Run with ArtSource/audio/build_audio.sh (needs numpy, which Blender's bundled Python has).
Output is deterministic: one literal seed per clip name. Writes 16-bit WAVs plus .meta files with
GUIDs derived from the path, so a re-run never churns Unity references.

Naming: `key.wav` or `key_01.wav`. GameAudio groups `_NN` files into random variants of `key`.

A section can be generated alone: `build_audio.sh m22` (or `m24`) writes only that section's clips and their metas, leaving
every existing WAV and every .meta Unity has since filled in untouched. Add new clips in their own section.
"""
import hashlib
import os
import sys
import wave
import zlib

import numpy as np

SR = 44100
OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Audio", "Resources", "Audio"))
written = []


# ---------------------------------------------------------------- core helpers

def rng_for(name):
    return np.random.default_rng(zlib.crc32(name.encode()))


def t_axis(duration, sr=SR):
    return np.arange(int(duration * sr)) / sr


def n_of(duration, sr=SR):
    return int(duration * sr)


def fft_band(x, low, high, sr=SR, slope=0.35):
    """Zero-phase, circular band filter. `slope` is the soft skirt width as a fraction of the corner."""
    spectrum = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1.0 / sr)
    gain = np.ones_like(f)
    if low > 0:
        gain *= np.clip((f - low * (1 - slope)) / max(1e-6, low * slope * 2), 0, 1) ** 2
    if high < sr / 2:
        gain *= np.clip((high * (1 + slope) - f) / max(1e-6, high * slope * 2), 0, 1) ** 2
    return np.fft.irfft(spectrum * gain, len(x))


def noise(rng, n):
    return rng.standard_normal(n)


def band_noise(rng, n, low, high, sr=SR, slope=0.35):
    return unit(fft_band(noise(rng, n), low, high, sr, slope))


def unit(x):
    peak = np.max(np.abs(x))
    return x / peak if peak > 1e-9 else x


def decay(n, tau, sr=SR, attack=0.001):
    t = np.arange(n) / sr
    env = np.exp(-t / tau)
    a = max(1, int(attack * sr))
    env[:a] *= np.linspace(0, 1, a)
    return env


def place(buffer, signal, at, sr=SR, gain=1.0):
    start = max(0, int(at * sr))
    if start >= len(buffer):
        return
    end = min(len(buffer), start + len(signal))
    buffer[start:end] += signal[: end - start] * gain


def modal(rng, n, modes, sr=SR, jitter=0.03, tau_scale=1.0):
    """Struck-object synthesis: decaying partials. modes = [(frequency, tau seconds, amplitude)]."""
    t = np.arange(n) / sr
    out = np.zeros(n)
    for frequency, tau, amplitude in modes:
        f = frequency * (1 + rng.uniform(-jitter, jitter))
        out += amplitude * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / (tau * tau_scale))
    return out


def thump(rng, n, start_hz, end_hz, tau, sr=SR):
    t = np.arange(n) / sr
    frequency = end_hz + (start_hz - end_hz) * np.exp(-t / 0.018)
    phase = 2 * np.pi * np.cumsum(frequency) / sr
    return np.sin(phase) * decay(n, tau, sr, 0.0015)


def grains(rng, n, count, span, low, high, tau=0.005, sr=SR, delay=0.0, strength=(0.3, 1.0)):
    """Scattered tiny noise bursts: gravel, crunch, debris, leaves."""
    out = np.zeros(n)
    grain_n = int(tau * 7 * sr)
    for _ in range(count):
        g = band_noise(rng, grain_n, low * rng.uniform(0.8, 1.25), high * rng.uniform(0.8, 1.25), sr) * decay(grain_n, tau * rng.uniform(0.5, 1.5), sr, 0.0005)
        place(out, g, delay + rng.uniform(0, span) ** 1.3, sr, rng.uniform(*strength))
    return out


def room(x, size=0.25, mix=0.25, rng=None, damp=4000.0, sr=SR):
    """Small-room tail by convolving with decaying filtered noise. Extends the buffer."""
    rng = rng or np.random.default_rng(7)
    ir_n = int(size * 3 * sr)
    ir = band_noise(rng, ir_n, 150, damp, sr) * np.exp(-np.arange(ir_n) / (size * sr))
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    size_out = len(x) + ir_n
    wet = np.fft.irfft(np.fft.rfft(x, size_out) * np.fft.rfft(ir, size_out), size_out)
    return np.concatenate([x, np.zeros(ir_n)]) * (1 - mix) + wet * mix


def svf_sweep(x, cutoff, q, sr=SR):
    """Time-varying band-pass (Chamberlin SVF) for swooshes. cutoff is per-sample Hz."""
    low = band = 0.0
    out = np.zeros(len(x))
    damp = 1.0 / q
    coefficient = 2 * np.sin(np.pi * np.minimum(cutoff, sr / 6.5) / sr)
    for i in range(len(x)):
        f = coefficient[i]
        low += f * band
        high = x[i] - low - damp * band
        band += f * high
        out[i] = band
    return out


def resonator(x, frequency, bandwidth, sr=SR):
    """Fixed two-pole resonator (for formants)."""
    r = np.exp(-np.pi * bandwidth / sr)
    a1 = 2 * r * np.cos(2 * np.pi * frequency / sr)
    a2 = -r * r
    y1 = y2 = 0.0
    out = np.zeros(len(x))
    for i in range(len(x)):
        y = x[i] + a1 * y1 + a2 * y2
        out[i] = y
        y2, y1 = y1, y
    return out


def voice(rng, duration, f0_points, formants, rough=0.0, breath=0.15, vibrato=(0.0, 5.0), attack=0.02, release=0.08):
    """Formant voice: glottal pulses through vocal-tract resonances. f0_points = [(time 0..1, hz)]."""
    n = n_of(duration)
    t = np.arange(n) / SR
    times, values = zip(*f0_points)
    f0 = np.interp(t / duration, times, values)
    f0 *= 1 + vibrato[0] * np.sin(2 * np.pi * vibrato[1] * t) + 0.012 * fft_band(noise(rng, n), 1, 30) / 0.02 * 0.02
    if rough > 0:
        f0 *= 1 + rough * unit(fft_band(noise(rng, n), 20, 90))
    phase = np.cumsum(f0) / SR % 1.0
    # Rosenberg-style glottal pulse: smooth opening, sharp closure, rich in harmonics.
    glottal = np.where(phase < 0.65, np.sin(np.pi * phase / 0.65) ** 2, np.clip(1 - (phase - 0.65) / 0.08, 0, 1) ** 2)
    glottal = np.diff(glottal, prepend=0.0)
    source = unit(glottal) + breath * band_noise(rng, n, 400, 7000) * (0.5 + 0.5 * unit(np.abs(glottal)))
    out = np.zeros(n)
    for frequency, bandwidth, amplitude in formants:
        out += amplitude * unit(resonator(source, frequency, bandwidth))
    env = np.ones(n)
    a, r = int(attack * SR), int(release * SR)
    env[:a] = np.linspace(0, 1, a)
    env[-r:] *= np.linspace(1, 0, r) ** 1.5
    return np.tanh(unit(out) * 1.6) * env


def finish(x, peak=0.9, fade=0.003):
    """Remove DC, shape the very ends so nothing clicks, normalise."""
    x = np.asarray(x, dtype=np.float64)
    x = x - np.mean(x, axis=0)
    n = max(8, int(fade * SR))
    ramp = np.linspace(0, 1, n)
    if x.ndim == 1:
        x[-n:] *= ramp[::-1]
        x[:8] *= np.linspace(0, 1, 8)
    else:
        x[-n:] *= ramp[::-1, None]
        x[:8] *= np.linspace(0, 1, 8)[:, None]
    return unit(x) * peak


def trim_tail(x, threshold=0.002):
    """Cut trailing silence left over by reverb so clips stay short."""
    level = np.abs(x)
    indices = np.nonzero(level > threshold * np.max(level))[0]
    if len(indices) == 0:
        return x
    return x[: min(len(x), indices[-1] + int(0.01 * SR))]


def write(name, x, sr=SR, peak=None):
    x = np.asarray(x, dtype=np.float64)
    if peak is not None:
        x = unit(x) * peak
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype(np.int16)
    channels = 1 if data.ndim == 1 else data.shape[1]
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as f:
        f.setnchannels(channels)
        f.setsampwidth(2)
        f.setframerate(sr)
        f.writeframes(data.tobytes())
    write_meta(path, "AudioImporter:\n  externalObjects: {}\n  serializedVersion: 8\n")
    written.append(name)


def write_meta(path, body):
    relative = os.path.relpath(path, os.path.normpath(os.path.join(OUT, "..", "..", "..", ".."))).replace(os.sep, "/")
    guid = hashlib.md5(relative.encode()).hexdigest()
    with open(path + ".meta", "w") as f:
        f.write(f"fileFormatVersion: 2\nguid: {guid}\n{body}")


def clip(name, fn, variants=None, peak=0.9):
    """Runs fn(rng, index) for each variant, with its own literal-derived seed."""
    if variants is None:
        write(name, finish(trim_tail(fn(rng_for(name), 0)), peak))
        return
    for i in range(variants):
        full = f"{name}_{i + 1:02d}"
        write(full, finish(trim_tail(fn(rng_for(full), i)), peak))


# ---------------------------------------------------------------- footsteps

def foot_dirt(rng, _):
    n = n_of(0.24)
    x = 0.8 * thump(rng, n, 150, 65, 0.035)
    x += 0.5 * band_noise(rng, n, 80, 500) * decay(n, 0.03)
    x += 0.55 * grains(rng, n, 14, 0.11, 1400, 5500, 0.004)
    return x


def foot_concrete(rng, _):
    n = n_of(0.2)
    x = 0.6 * thump(rng, n, 210, 95, 0.022)
    x += 0.9 * band_noise(rng, n, 700, 3400) * decay(n, 0.011, attack=0.0004)
    x += 0.25 * grains(rng, n, 5, 0.05, 2500, 7500, 0.003)
    return room(x, 0.05, 0.25, rng)


def foot_wood(rng, _):
    n = n_of(0.22)
    x = 0.8 * modal(rng, n, [(165, 0.04, 1), (390, 0.028, 0.7), (710, 0.018, 0.45), (1250, 0.01, 0.25)], jitter=0.07)
    x += 0.35 * band_noise(rng, n, 500, 3000) * decay(n, 0.008)
    return x


def foot_metal(rng, _):
    n = n_of(0.34)
    x = 0.55 * modal(rng, n, [(310, 0.06, 1), (742, 0.08, 0.7), (1290, 0.11, 0.5), (2260, 0.07, 0.3), (3510, 0.05, 0.2)], jitter=0.05)
    x += 0.5 * thump(rng, n, 180, 90, 0.025)
    return x


def foot_snow(rng, _):
    n = n_of(0.3)
    x = 0.45 * thump(rng, n, 120, 60, 0.04)
    x += 0.8 * grains(rng, n, 46, 0.2, 1800, 7500, 0.0035, strength=(0.2, 0.8))
    return x


def foot_mud(rng, _):
    n = n_of(0.32)
    x = 0.6 * thump(rng, n, 110, 55, 0.05)
    t = np.arange(n) / SR
    sweep = 250 + 900 * np.clip(t / 0.15, 0, 1) ** 2
    x += 0.8 * unit(svf_sweep(noise(rng, n), sweep, 4)) * decay(n, 0.07, attack=0.012)
    x += 0.3 * grains(rng, n, 7, 0.18, 500, 1800, 0.007)
    return x


def land(rng, i):
    n = n_of(0.42)
    x = thump(rng, n, 170, 48, 0.08)
    x += 0.5 * band_noise(rng, n, 60, 700) * decay(n, 0.05)
    x += 0.5 * grains(rng, n, 28, 0.2, 900, 5000, 0.005)
    return x


def jump(rng, _):
    n = n_of(0.22)
    t = np.arange(n) / SR
    x = 0.6 * unit(svf_sweep(noise(rng, n), 500 + 1800 * t / 0.22, 2)) * np.sin(np.pi * np.clip(t / 0.22, 0, 1)) ** 2
    x += 0.5 * thump(rng, n, 120, 70, 0.025)
    return x


# ---------------------------------------------------------------- combat

def swing(rng, _):
    d = rng.uniform(0.26, 0.34)
    n = n_of(d)
    t = np.linspace(0, 1, n)
    shape = np.sin(np.pi * t ** 0.8)
    peak_hz = rng.uniform(1300, 2200)
    x = svf_sweep(noise(rng, n), 260 + peak_hz * shape ** 2, 2.5)
    return unit(x) * shape ** 1.6


def hit_flesh(rng, _):
    n = n_of(0.3)
    x = thump(rng, n, 190, 55, 0.05)
    x += 0.75 * band_noise(rng, n, 450, 2100) * decay(n, 0.018, attack=0.0005)
    x += 0.3 * band_noise(rng, n, 2500, 7000) * decay(n, 0.006, attack=0.0003)
    return np.tanh(x * 1.6)


def hit_block(rng, _):
    n = n_of(0.3)
    x = 0.8 * modal(rng, n, [(210, 0.05, 1), (560, 0.035, 0.7), (1180, 0.02, 0.5), (2300, 0.01, 0.3)], jitter=0.08)
    x += 0.4 * band_noise(rng, n, 800, 4500) * decay(n, 0.007)
    x += 0.35 * thump(rng, n, 130, 70, 0.03)
    return x


def raider_weapon_swing(rng, _):
    n = n_of(0.4)
    t = np.linspace(0, 1, n)
    shape = np.sin(np.pi * t ** 0.7)
    x = svf_sweep(noise(rng, n), 180 + 1000 * shape ** 2, 3)
    return unit(x) * shape ** 1.4


# ---------------------------------------------------------------- voices (deliberately short and sparse)

AH = [(720, 90, 1.0), (1180, 110, 0.55), (2500, 170, 0.25), (3400, 250, 0.1)]
UH = [(600, 80, 1.0), (1050, 100, 0.5), (2400, 170, 0.18), (3300, 260, 0.08)]
OH = [(480, 70, 1.0), (850, 90, 0.5), (2500, 200, 0.12)]
EH = [(560, 80, 1.0), (1750, 130, 0.5), (2500, 170, 0.2)]
RASP = [(650, 180, 1.0), (1250, 240, 0.7), (2600, 340, 0.45)]


def player_hurt(rng, i):
    f = rng.uniform(128, 150)
    return voice(rng, rng.uniform(0.2, 0.32), [(0, f * 1.15), (0.3, f * 1.25), (1, f * 0.8)], UH if i % 2 else AH, rough=0.03, breath=0.35, attack=0.008)


def player_death(rng, _):
    return voice(rng, 1.25, [(0, 150), (0.15, 165), (0.7, 105), (1, 72)], AH, rough=0.06, breath=0.5, vibrato=(0.015, 6), attack=0.02, release=0.55)


def player_jump_breath(rng, _):
    return voice(rng, 0.16, [(0, 130), (1, 120)], UH, breath=0.8, attack=0.02, release=0.06)


def raider_alert(rng, i):
    f = rng.uniform(92, 112)
    d = rng.uniform(0.45, 0.7)
    formant = [EH, AH, OH][i % 3]
    return voice(rng, d, [(0, f * 0.9), (0.3, f * 1.35), (1, f * 0.85)], formant, rough=0.14, breath=0.5, attack=0.025, release=0.14)


def raider_hurt(rng, i):
    f = rng.uniform(95, 118)
    return voice(rng, rng.uniform(0.2, 0.3), [(0, f * 1.3), (1, f * 0.8)], AH if i % 2 else RASP, rough=0.16, breath=0.5, attack=0.006)


def raider_death(rng, i):
    f = rng.uniform(100, 118)
    return voice(rng, rng.uniform(0.8, 1.05), [(0, f * 1.3), (0.2, f * 1.4), (1, f * 0.55)], AH if i else OH, rough=0.15, breath=0.55, vibrato=(0.02, 7), attack=0.015, release=0.4)


def raider_growl(rng, _):
    f = rng.uniform(72, 88)
    return voice(rng, rng.uniform(0.55, 0.85), [(0, f), (0.5, f * 1.1), (1, f * 0.9)], RASP, rough=0.25, breath=0.7, attack=0.05, release=0.2)


# ---------------------------------------------------------------- doors

def creak(rng, duration, hz_low=170, hz_high=420):
    """Stick-slip hinge: an irregular pulse train through metal-ish resonances."""
    n = n_of(duration)
    t = np.arange(n) / SR
    wander = unit(np.cumsum(noise(rng, n)) - np.linspace(0, np.cumsum(noise(rng, n))[-1], n))
    wander = fft_band(wander, 0, 9)
    wander = unit(wander)
    f0 = hz_low + (hz_high - hz_low) * (0.5 + 0.5 * wander)
    phase = np.cumsum(f0) / SR % 1
    pulses = (phase < 0.06).astype(float) * (0.5 + 0.5 * unit(fft_band(noise(rng, n), 2, 40)))
    x = resonator(pulses, 950, 110) + 0.7 * resonator(pulses, 1900, 150) + 0.5 * resonator(pulses, 3100, 220)
    return unit(x)


def door_open(rng, _):
    n = n_of(1.3)
    x = np.zeros(n)
    place(x, 0.8 * modal(rng, n_of(0.25), [(420, 0.03, 1), (1320, 0.025, 0.7), (2900, 0.012, 0.5)]) + 0.6 * thump(rng, n_of(0.25), 180, 90, 0.025), 0)
    c = creak(rng, 0.85) * np.sin(np.pi * np.linspace(0, 1, n_of(0.85))) ** 0.6
    place(x, c, 0.1, gain=0.42)
    place(x, 0.55 * thump(rng, n_of(0.25), 120, 60, 0.05), 0.95)
    return room(x, 0.12, 0.25, rng)


def door_close(rng, _):
    n = n_of(1.2)
    x = np.zeros(n)
    c = creak(rng, 0.42, 220, 480) * np.sin(np.pi * np.linspace(0, 1, n_of(0.42))) ** 0.6
    place(x, c, 0, gain=0.35)
    impact = n_of(0.55)
    hit = thump(rng, impact, 190, 55, 0.08) + 0.6 * modal(rng, impact, [(240, 0.1, 1), (640, 0.08, 0.8), (1460, 0.05, 0.5), (2800, 0.03, 0.3)])
    place(x, hit, 0.38)
    latch = modal(rng, n_of(0.1), [(1800, 0.012, 1), (3300, 0.008, 0.6)])
    place(x, latch, 0.56, gain=0.4)
    return room(x, 0.14, 0.3, rng)


def door_bash(rng, _):
    n = n_of(1.0)
    x = thump(rng, n, 200, 42, 0.12)
    x += 0.8 * modal(rng, n, [(195, 0.16, 1), (480, 0.13, 0.9), (1130, 0.1, 0.7), (2150, 0.07, 0.5), (3700, 0.04, 0.3)])
    x += 0.5 * band_noise(rng, n, 500, 6000) * decay(n, 0.03)
    x += 0.5 * grains(rng, n, 28, 0.5, 900, 5000, 0.006, delay=0.02)
    return room(np.tanh(x * 1.5), 0.15, 0.3, rng)


# ---------------------------------------------------------------- gathering

def gather_wood(rng, _):
    n = n_of(0.5)
    x = 0.9 * modal(rng, n, [(195, 0.05, 1), (430, 0.04, 0.8), (880, 0.025, 0.5), (1650, 0.012, 0.3)], jitter=0.1)
    x += 0.65 * band_noise(rng, n, 1000, 5500) * decay(n, 0.01, attack=0.0004)
    x += 0.4 * thump(rng, n, 140, 70, 0.04)
    x += 0.3 * grains(rng, n, 12, 0.3, 1200, 5000, 0.005, delay=0.03, strength=(0.1, 0.6))
    return x


def gather_stone(rng, _):
    n = n_of(0.55)
    x = 0.8 * modal(rng, n, [(1450, 0.012, 1), (2950, 0.01, 0.8), (4800, 0.007, 0.5)], jitter=0.12)
    x += 0.5 * band_noise(rng, n, 1500, 8000) * decay(n, 0.006, attack=0.0003)
    x += 0.35 * thump(rng, n, 180, 90, 0.02)
    x += 0.6 * grains(rng, n, 30, 0.38, 900, 4500, 0.007, delay=0.02, strength=(0.1, 0.7))
    return x


def gather_ore(rng, _):
    n = n_of(0.7)
    x = 0.7 * modal(rng, n, [(1320, 0.05, 1), (2480, 0.09, 0.85), (3890, 0.06, 0.6), (5620, 0.03, 0.3)], jitter=0.05)
    x += 0.5 * band_noise(rng, n, 1500, 8000) * decay(n, 0.006, attack=0.0003)
    x += 0.5 * grains(rng, n, 22, 0.4, 800, 3800, 0.008, delay=0.03, strength=(0.1, 0.6))
    return x


def gather_scrap(rng, _):
    n = n_of(0.75)
    x = np.zeros(n)
    for j in range(rng.integers(4, 7)):
        base = rng.uniform(280, 900)
        length = n_of(0.3)
        hit = modal(rng, length, [(base, 0.08, 1), (base * 2.37, 0.1, 0.7), (base * 4.1, 0.05, 0.5), (base * 6.3, 0.03, 0.3)], jitter=0.02)
        place(x, hit, rng.uniform(0, 0.42), gain=rng.uniform(0.3, 1.0))
    return x


def gather_leaves(rng, _):
    n = n_of(0.6)
    t = np.arange(n) / SR
    bursts = np.zeros(n)
    for _j in range(rng.integers(5, 8)):
        place(bursts, np.hanning(n_of(rng.uniform(0.05, 0.14))), rng.uniform(0, 0.46), gain=rng.uniform(0.4, 1.0))
    x = band_noise(rng, n, 1800, 9000) * bursts
    x += 0.55 * grains(rng, n, 24, 0.48, 2500, 8000, 0.003, strength=(0.1, 0.6))
    # A small pop: a berry coming off the stem.
    place(x, np.sin(2 * np.pi * 620 * np.arange(n_of(0.03)) / SR) * decay(n_of(0.03), 0.006), rng.uniform(0.25, 0.4), gain=0.4)
    return x


def rummage(rng, _):
    n = n_of(0.8)
    x = 0.45 * grains(rng, n, 24, 0.68, 400, 3200, 0.012, strength=(0.1, 0.8))
    for _j in range(3):
        place(x, modal(rng, n_of(0.12), [(rng.uniform(300, 800), 0.03, 1), (rng.uniform(1200, 2400), 0.02, 0.5)]), rng.uniform(0.05, 0.6), gain=rng.uniform(0.2, 0.5))
    return x


def crate_open(rng, _):
    n = n_of(1.05)
    x = np.zeros(n)
    place(x, modal(rng, n_of(0.18), [(310, 0.03, 1), (720, 0.02, 0.6), (1500, 0.01, 0.3)]), 0, gain=0.8)
    c = creak(rng, 0.42, 300, 700) * np.sin(np.pi * np.linspace(0, 1, n_of(0.42)))
    place(x, c, 0.04, gain=0.3)
    place(x, rummage(rng, 0), 0.32, gain=0.75)
    return x


def tones(rng, notes, tau=0.14, length=0.5, gap=0.09, gain=1.0):
    """Soft marimba-like note sequence for UI and reward cues."""
    n = n_of(length + gap * len(notes))
    x = np.zeros(n)
    for j, hz in enumerate(notes):
        m = n_of(length)
        tone = modal(rng, m, [(hz, tau, 1), (hz * 3.9, tau * 0.22, 0.18), (hz * 2, tau * 0.5, 0.2)], jitter=0)
        place(x, tone, j * gap, gain=gain)
    return x


def loot_found(rng, _):
    return tones(rng, [523, 784])


def loot_empty(rng, _):
    return tones(rng, [262], tau=0.08, length=0.25) + 0


# ---------------------------------------------------------------- items, building and UI

def eat(rng, _):
    n = n_of(0.95)
    x = np.zeros(n)
    for j in range(3):
        at = 0.04 + j * 0.3 + rng.uniform(-0.03, 0.03)
        bite = grains(rng, n_of(0.2), 22, 0.14, 700, 4200, 0.006, strength=(0.2, 1.0))
        bite += 0.5 * thump(rng, n_of(0.2), 130, 80, 0.025)
        place(x, bite, at)
    return x


def drink(rng, _):
    n = n_of(1.0)
    x = np.zeros(n)
    for j in range(3):
        m = n_of(0.2)
        t = np.arange(m) / SR
        start = rng.uniform(170, 220)
        hz = start + 520 * (t / 0.2) ** 1.5
        gulp = np.sin(2 * np.pi * np.cumsum(hz) / SR) * np.sin(np.pi * t / 0.2) ** 2
        gulp += 0.3 * band_noise(rng, m, 300, 1500) * np.sin(np.pi * t / 0.2) ** 3
        place(x, gulp, 0.03 + j * 0.3 + rng.uniform(-0.02, 0.02))
    return x


def heal(rng, _):
    n = n_of(1.0)
    t = np.arange(n) / SR
    rip = band_noise(rng, n, 1500, 8000) * np.clip(1 - np.abs(t - 0.14) / 0.14, 0, 1) ** 0.7 * (0.5 + 0.5 * np.sin(2 * np.pi * 75 * t) > 0.5)
    x = 0.6 * rip
    place(x, tones(rng, [392, 587], tau=0.2, length=0.6, gap=0.13), 0.32, gain=0.55)
    return x


def equip(rng, _):
    n = n_of(0.65)
    t = np.arange(n) / SR
    x = 0.7 * unit(svf_sweep(noise(rng, n), 1200 + 5000 * np.clip(t / 0.22, 0, 1), 5)) * np.sin(np.pi * np.clip(t / 0.22, 0, 1)) ** 2
    place(x, modal(rng, n_of(0.45), [(1680, 0.15, 1), (3370, 0.1, 0.5), (5150, 0.06, 0.25)]), 0.18, gain=0.5)
    return x


def craft(rng, _):
    n = n_of(0.95)
    x = np.zeros(n)
    for at in (0, 0.27, 0.55):
        hit = modal(rng, n_of(0.3), [(980, 0.04, 1), (2260, 0.05, 0.7), (3940, 0.03, 0.4)], jitter=0.04) + 0.5 * thump(rng, n_of(0.3), 180, 90, 0.025)
        place(x, hit, at + rng.uniform(-0.015, 0.015), gain=rng.uniform(0.7, 1))
    return x


def place_piece(rng, _):
    n = n_of(0.6)
    x = 0.9 * thump(rng, n, 150, 58, 0.08)
    x += 0.6 * band_noise(rng, n, 200, 1400) * decay(n, 0.04)
    x += 0.5 * modal(rng, n, [(260, 0.05, 1), (620, 0.04, 0.6), (1500, 0.02, 0.3)])
    place(x, grains(rng, n_of(0.35), 14, 0.28, 900, 4200, 0.006, strength=(0.1, 0.5)), 0.03)
    return x


def ui_deny(rng, _):
    n = n_of(0.3)
    t = np.arange(n) / SR
    wave_ = np.tanh(2.5 * np.sin(2 * np.pi * 138 * t))
    gate = ((t < 0.09) | ((t > 0.14) & (t < 0.25))).astype(float)
    return fft_band(wave_ * gate, 0, 1800) * 0.8


def ui_click(rng, _):
    n = n_of(0.06)
    return modal(rng, n, [(1750, 0.006, 1), (3300, 0.003, 0.5)], jitter=0) + 0.4 * thump(rng, n, 400, 200, 0.005)


def ui_open(rng, _):
    n = n_of(0.18)
    x = 0.5 * band_noise(rng, n, 500, 3500) * np.sin(np.pi * np.arange(n) / n) ** 2
    place(x, modal(rng, n_of(0.1), [(520, 0.02, 1), (1300, 0.012, 0.4)], jitter=0), 0.08, gain=0.8)
    return x


def ui_close(rng, _):
    n = n_of(0.16)
    x = 0.4 * band_noise(rng, n, 400, 2600) * np.sin(np.pi * np.arange(n) / n) ** 2
    place(x, modal(rng, n_of(0.08), [(380, 0.016, 1), (950, 0.01, 0.4)], jitter=0), 0.05, gain=0.8)
    return x


def ui_tick(rng, _):
    n = n_of(0.045)
    return modal(rng, n, [(2400, 0.004, 1), (4200, 0.002, 0.4)], jitter=0)


def game_saved(rng, _):
    return tones(rng, [440, 659], tau=0.12, length=0.42, gap=0.08)


def game_loaded(rng, _):
    return tones(rng, [659, 440], tau=0.12, length=0.42, gap=0.08)


def sleep(rng, _):
    n = n_of(2.6)
    t = np.arange(n) / SR
    breath = band_noise(rng, n, 300, 1800) * np.sin(np.pi * np.clip(t / 1.1, 0, 1)) ** 2
    exhale = band_noise(rng, n, 180, 1100) * np.sin(np.pi * np.clip((t - 1.0) / 1.5, 0, 1)) ** 2
    x = 0.42 * breath + 0.52 * exhale
    place(x, tones(rng, [196, 294], tau=0.6, length=1.3, gap=0.3), 0.8, gain=0.45)
    return x


# ---------------------------------------------------------------- apocalypse and dread

def button_press(rng, _):
    n = n_of(0.75)
    x = 0.9 * modal(rng, n, [(680, 0.02, 1), (1720, 0.014, 0.8), (3300, 0.008, 0.4)])
    x += 0.6 * thump(rng, n, 240, 90, 0.02)
    hum_at = n_of(0.08)
    t = np.arange(n - hum_at) / SR
    hum = (np.sin(2 * np.pi * 100 * t) + 0.5 * np.sin(2 * np.pi * 300 * t) + 0.25 * np.sin(2 * np.pi * 500 * t)) * np.exp(-t / 0.22)
    x[hum_at:] += 0.22 * hum
    return room(x, 0.1, 0.2, rng)


def siren(rng, _):
    """Hand-cranked air-raid siren: slow rise, hold, fall. Far away, with slapback off the valley."""
    d = 10.0
    n = n_of(d)
    t = np.arange(n) / SR
    shape = np.interp(t / d, [0, 0.22, 0.42, 0.55, 0.72, 0.84, 1], [0.0, 1, 1, 0.72, 1, 1, 0.0])
    hz = 170 + 400 * shape
    phase = 2 * np.pi * np.cumsum(hz) / SR
    x = sum(a * np.sin(phase * h) for h, a in [(1, 1.0), (2, 0.5), (3, 0.32), (4, 0.16), (5, 0.08)])
    x *= np.clip(shape * 1.7, 0, 1) ** 0.8
    x *= 1 + 0.06 * np.sin(2 * np.pi * 5.1 * t)
    echo = np.zeros(n + n_of(0.55))
    echo[:n] += x
    echo[n_of(0.55):] += 0.42 * fft_band(x, 200, 1600)
    return room(fft_band(echo, 120, 3200), 0.5, 0.3, rng)


def quake(rng, _):
    d = 6.5
    n = n_of(d)
    t = np.arange(n) / SR
    env = np.interp(t, [0, 0.25, 1.2, 3.2, 6.5], [0, 0.75, 1, 0.55, 0]) ** 1.2
    rumble = band_noise(rng, n, 18, 120) * (0.72 + 0.28 * unit(fft_band(noise(rng, n), 1, 8)))
    x = rumble * env
    place(x, 1.1 * thump(rng, n_of(2.2), 95, 26, 0.75), 0.05)
    debris = grains(rng, n, 150, 4.6, 700, 4000, 0.008, delay=0.35, strength=(0.02, 0.22))
    x += debris * np.interp(t, [0, 1, 6.5], [0.4, 1, 0.15])
    for at in (0.9, 1.9, 3.1):
        place(x, modal(rng, n_of(0.5), [(330, 0.09, 1), (780, 0.07, 0.8), (1800, 0.04, 0.4)]), at + rng.uniform(-0.1, 0.1), gain=0.09)
    return np.tanh(x * 1.5)


def heartbeat(rng, _):
    n = n_of(0.85)
    x = np.zeros(n)
    place(x, thump(rng, n_of(0.3), 72, 42, 0.07), 0.0, gain=1.0)
    place(x, thump(rng, n_of(0.3), 62, 38, 0.08), 0.24, gain=0.7)
    return fft_band(x, 20, 300)


def warn_vital(rng, _):
    """Low stomach-and-throat unease, not a UI beep: this plays out in the world."""
    n = n_of(1.1)
    t = np.arange(n) / SR
    hz = 95 + 40 * np.sin(2 * np.pi * 2.1 * t) + 18 * unit(fft_band(noise(rng, n), 3, 20))
    x = resonator(np.sin(2 * np.pi * np.cumsum(hz) / SR), 240, 80)
    return unit(x) * np.sin(np.pi * t / 1.1) ** 1.2


def stinger_death(rng, _):
    d = 5.5
    n = n_of(d)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for hz, a in [(55, 1.0), (58.3, 0.8), (110, 0.6), (155.6, 0.42), (164.8, 0.36), (233, 0.2)]:
        x += a * saw(hz, t, rng)
    x = fft_band(x, 25, 950)
    x *= np.interp(t, [0, 0.03, 0.7, 5.5], [0, 1, 0.55, 0]) ** 1.2
    place(x, thump(rng, n_of(2.0), 90, 30, 0.45), 0, gain=0.9)
    return room(x, 0.9, 0.35, rng, 2200)


def saw(hz, t, rng, partials=14):
    x = np.zeros_like(t)
    for k in range(1, partials + 1):
        if hz * k > SR / 2.5:
            break
        x += np.sin(2 * np.pi * hz * k * t + rng.uniform(0, 6.28)) / k
    return x


# ---------------------------------------------------------------- ambient one-shots

def bird(rng, i):
    """Thin FM chirps. Each variant is a different little phrase."""
    n = n_of(1.4)
    x = np.zeros(n)
    at = 0.0
    for _j in range(rng.integers(3, 7)):
        d = rng.uniform(0.05, 0.14)
        m = n_of(d)
        tt = np.linspace(0, 1, m)
        base = rng.uniform(2700, 4800)
        sweep = base * (1 + rng.uniform(-0.25, 0.25) * tt + 0.08 * np.sin(2 * np.pi * rng.uniform(1, 4) * tt))
        place(x, np.sin(2 * np.pi * np.cumsum(sweep) / SR) * np.sin(np.pi * tt) ** 2, at, gain=rng.uniform(0.5, 1))
        at += d + rng.uniform(0.02, 0.14)
    return room(x, 0.3, 0.3, rng, 7000)


def crow(rng, _):
    n = n_of(1.5)
    x = np.zeros(n)
    for j in range(rng.integers(2, 4)):
        f = rng.uniform(420, 560)
        caw = voice(rng, rng.uniform(0.22, 0.3), [(0, f * 1.1), (1, f * 0.8)], [(1300, 300, 1), (2200, 300, 0.7), (3400, 400, 0.4)], rough=0.2, breath=0.5, attack=0.012, release=0.06)
        place(x, caw, j * rng.uniform(0.34, 0.42))
    return room(x, 0.35, 0.3, rng)


def metal_creak(rng, _):
    d = rng.uniform(0.9, 1.7)
    return room(creak(rng, d, rng.uniform(70, 110), rng.uniform(150, 260)) * np.sin(np.pi * np.linspace(0, 1, n_of(d))) ** 1.2, 0.3, 0.35, rng, 3000)


def distant_boom(rng, _):
    n = n_of(3.5)
    x = thump(rng, n, 60, 26, 0.75) + 0.5 * band_noise(rng, n, 20, 200) * decay(n, 0.7, attack=0.04)
    return fft_band(x, 15, 260)


def wolf(rng, _):
    d = rng.uniform(2.4, 3.2)
    f = rng.uniform(300, 370)
    x = voice(rng, d, [(0, f * 0.72), (0.2, f), (0.75, f * 1.04), (1, f * 0.72)], [(f * 1.0, 80, 1), (f * 2, 120, 0.35), (1400, 300, 0.1)], rough=0.004, breath=0.05, vibrato=(0.007, 4.2), attack=0.25, release=0.8)
    return room(x, 0.9, 0.45, rng, 3500)


def frog(rng, _):
    n = n_of(0.9)
    x = np.zeros(n)
    count = rng.integers(2, 5)
    hz = rng.uniform(280, 450)
    for j in range(count):
        d = 0.11
        m = n_of(d)
        tt = np.arange(m) / SR
        pulse = (0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(50, 70) * tt)) ** 2 * np.sin(np.pi * tt / d)
        place(x, np.sin(2 * np.pi * hz * tt) * pulse + 0.4 * np.sin(4 * np.pi * hz * tt) * pulse, j * 0.18)
    return x


def drip(rng, _):
    n = n_of(0.5)
    t = np.arange(n) / SR
    hz = rng.uniform(900, 1400) * (1 + 0.55 * np.clip(t / 0.03, 0, 1))
    x = np.sin(2 * np.pi * np.cumsum(hz) / SR) * np.exp(-t / 0.022)
    return room(x, 0.28, 0.45, rng, 4500)


def pipe_groan(rng, _):
    d = rng.uniform(1.4, 2.2)
    n = n_of(d)
    t = np.arange(n) / SR
    hz = rng.uniform(62, 88) * (1 + 0.08 * unit(fft_band(noise(rng, n), 0.5, 5)))
    x = sum(a * np.sin(2 * np.pi * np.cumsum(hz * h) / SR) for h, a in [(1, 1), (2, 0.6), (3.02, 0.4), (5.03, 0.2)])
    return room(x * np.sin(np.pi * t / d) ** 1.5, 0.35, 0.4, rng, 2000)


# ---------------------------------------------------------------- seamless ambience loops

def periodic_lfo(rng, n, cycles, count=4):
    """Random slow modulation in 0..1 that returns exactly to its start over the loop."""
    t = np.arange(n) / n
    x = sum(rng.uniform(0.4, 1) * np.sin(2 * np.pi * rng.integers(1, cycles + 1) * t + rng.uniform(0, 6.28)) for _ in range(count))
    return 0.5 + 0.5 * x / np.max(np.abs(x))


def wind_layer(rng, n, sr, bands, cycles=8):
    """Wind as filtered noise bands with independent gust envelopes. All circular, so it loops exactly."""
    x = np.zeros(n)
    for low, high, level, depth in bands:
        band = band_noise(rng, n, low, high, sr)
        gust = periodic_lfo(rng, n, cycles)
        x += level * band * ((1 - depth) + depth * gust ** 2)
    return x


def write_loop(name, x, sr=32000, peak=0.8):
    x = x - np.mean(x)
    write(name, unit(x) * peak, sr)


def ambience():
    sr = 32000
    n = 28 * sr
    r = rng_for("amb_wasteland")
    write_loop("amb_wasteland", wind_layer(r, n, sr, [(40, 240, 1.0, 0.65), (200, 700, 0.5, 0.85), (700, 2200, 0.13, 0.95), (2500, 6000, 0.03, 0.9)]))

    r = rng_for("amb_forest")
    x = wind_layer(r, n, sr, [(60, 260, 0.45, 0.6), (300, 900, 0.22, 0.8)])
    x += wind_layer(r, n, sr, [(2200, 9000, 0.3, 1.0), (1000, 2500, 0.14, 0.95)], cycles=12)
    write_loop("amb_forest", x)

    r = rng_for("amb_desert")
    x = wind_layer(r, n, sr, [(60, 300, 0.7, 0.7), (2500, 9000, 0.34, 0.95), (900, 2500, 0.16, 0.9)], cycles=10)
    write_loop("amb_desert", x)

    r = rng_for("amb_snow")
    x = wind_layer(r, n, sr, [(50, 300, 0.75, 0.7), (1500, 6000, 0.1, 0.9)])
    for low, high, level in [(330, 390, 0.42), (470, 540, 0.34), (680, 760, 0.22)]:
        x += level * band_noise(r, n, low, high, sr, 0.08) * periodic_lfo(r, n, 6) ** 3
    write_loop("amb_snow", x)

    r = rng_for("amb_marsh")
    x = wind_layer(r, n, sr, [(40, 200, 0.38, 0.6), (400, 1200, 0.1, 0.8)])
    # Insect bed: two chirring voices, integer cycles over the loop so they wrap cleanly.
    t = np.arange(n) / sr
    for hz, rate, level in [(4200, 27, 0.05), (5300, 19, 0.035)]:
        carrier = np.sin(2 * np.pi * round(hz * 28) / 28 * t)
        trill = (0.5 + 0.5 * np.sin(2 * np.pi * round(rate * 28) / 28 * t)) ** 3
        phrase = (0.5 + 0.5 * np.sin(2 * np.pi * r.integers(9, 15) / 28 * t + r.uniform(0, 6))) ** 2
        x += level * carrier * trill * phrase
    write_loop("amb_marsh", x)

    r = rng_for("amb_highlands")
    x = wind_layer(r, n, sr, [(40, 200, 1.0, 0.5), (180, 620, 0.65, 0.8), (600, 1800, 0.22, 0.95), (2500, 7000, 0.05, 1.0)], cycles=11)
    write_loop("amb_highlands", x)

    r = rng_for("amb_bunker")
    x = np.zeros(n)
    t = np.arange(n) / sr
    for hz, level in [(50, 1.0), (100, 0.75), (150, 0.32), (200, 0.14), (250, 0.09), (350, 0.04)]:
        drift = 0.75 + 0.25 * periodic_lfo(r, n, 4)
        x += level * np.sin(2 * np.pi * hz * t + r.uniform(0, 6.28)) * drift
    x = unit(x) * 0.7
    x += 0.52 * band_noise(r, n, 90, 900, sr) * (0.8 + 0.2 * periodic_lfo(r, n, 5))  # ventilation
    x += 0.06 * band_noise(r, n, 2200, 6000, sr)
    write_loop("amb_bunker", x, peak=0.7)


# ---------------------------------------------------------------- weather, wildlife, discovery

def foot_water(rng, _):
    n = n_of(0.38)
    x = 0.5 * thump(rng, n, 130, 70, 0.03)
    t = np.arange(n) / SR
    x += 0.7 * unit(svf_sweep(noise(rng, n), 500 + 2600 * np.clip(t / 0.09, 0, 1), 2.5)) * decay(n, 0.07, attack=0.006)
    for _j in range(rng.integers(6, 11)):  # Droplets falling back.
        m = n_of(0.05)
        tt = np.arange(m) / SR
        hz = rng.uniform(700, 2100) * (1 + 0.6 * tt / 0.05)
        place(x, np.sin(2 * np.pi * np.cumsum(hz) / SR) * np.exp(-tt / 0.011), rng.uniform(0.04, 0.3), gain=rng.uniform(0.05, 0.3))
    return x


def thunder(rng, _):
    d = rng.uniform(6.5, 8.5)
    n = n_of(d)
    t = np.arange(n) / SR
    x = band_noise(rng, n, 22, 260) * np.interp(t, [0, 0.12, 1.2, d], [0, 1, 0.5, 0]) ** 1.5
    for _j in range(rng.integers(4, 8)):  # Rolling claps, each further off than the last.
        at = rng.uniform(0.05, d * 0.5)
        place(x, band_noise(rng, n_of(1.4), 40, 900) * decay(n_of(1.4), rng.uniform(0.12, 0.35), attack=0.012), at, gain=rng.uniform(0.35, 0.85) * np.exp(-at / 2.5))
    return np.tanh(room(x, 0.9, 0.35, rng, 1400) * 1.4)


def discovery(rng, _):
    """A held, wary interval, not a reward fanfare: finding a place here is uneasy."""
    d = 3.2
    n = n_of(d)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for hz, at, level in [(146.83, 0.0, 0.8), (220.0, 0.28, 0.65), (293.66, 0.58, 0.55), (311.13, 1.0, 0.16)]:
        partial = sum(a * np.sin(2 * np.pi * hz * k * t) for k, a in [(1, 1), (2, 0.35), (3, 0.14)])
        x += level * partial * np.clip((t - at) / 0.35, 0, 1) * np.exp(-np.maximum(0, t - at) / 1.35)
    return room(x, 0.8, 0.45, rng, 2600)


def upgrade(rng, _):
    n = n_of(1.7)
    x = np.zeros(n)
    place(x, craft(rng, 0), 0, gain=0.7)
    t = np.arange(n_of(0.8)) / SR
    power = sum(a * np.sin(2 * np.pi * np.cumsum(70 * h * (1 + 0.5 * t / 0.8)) / SR) for h, a in [(1, 1), (2, 0.5), (3, 0.3)]) * np.sin(np.pi * t / 0.8) ** 2
    place(x, power, 0.72, gain=0.35)
    place(x, tones(rng, [392, 523, 659], tau=0.16, length=0.5, gap=0.1), 1.0, gain=0.5)
    return room(x, 0.1, 0.2, rng)


def deer_call(rng, _):
    f = rng.uniform(420, 520)
    x = voice(rng, rng.uniform(0.3, 0.42), [(0, f), (0.3, f * 1.12), (1, f * 0.72)], [(1000, 260, 1), (2300, 340, 0.45)], rough=0.05, breath=0.5, attack=0.02, release=0.14)
    return room(x, 0.28, 0.3, rng)


def boar_grunt(rng, _):
    n = n_of(0.9)
    x = np.zeros(n)
    for j in range(rng.integers(2, 4)):
        f = rng.uniform(70, 96)
        place(x, voice(rng, rng.uniform(0.13, 0.24), [(0, f * 1.15), (1, f * 0.85)], [(420, 160, 1), (980, 220, 0.55), (2100, 360, 0.2)], rough=0.3, breath=0.7, attack=0.012, release=0.06), j * rng.uniform(0.22, 0.3))
    return x


def weather_loops():
    sr = 32000
    n = 28 * sr
    t = np.arange(n) / sr

    r = rng_for("amb_rain")
    x = wind_layer(r, n, sr, [(2200, 11000, 0.8, 0.22), (700, 2400, 0.42, 0.28), (90, 450, 0.22, 0.4)], cycles=6)
    # Individual nearby drops, wrapped round the buffer so the loop stays seamless.
    for _j in range(1400):
        length = int(0.012 * sr)
        tt = np.arange(length) / sr
        drop = np.sin(2 * np.pi * r.uniform(1400, 5200) * tt) * np.exp(-tt / r.uniform(0.0012, 0.004))
        idx = (r.integers(0, n) + np.arange(length)) % n
        x[idx] += drop * r.uniform(0.03, 0.2)
    write_loop("amb_rain", x, peak=0.75)

    r = rng_for("amb_night")
    x = 0.1 * band_noise(r, n, 40, 240, sr) * (0.7 + 0.3 * periodic_lfo(r, n, 5))
    # Crickets: integer cycles over the loop for each voice, in slow-beating groups.
    for hz, rate, level in [(3900, 16, 0.11), (4350, 13, 0.08), (4820, 19, 0.06), (3350, 11, 0.05)]:
        carrier = np.sin(2 * np.pi * round(hz * 28) / 28 * t + r.uniform(0, 6))
        chirp = (0.5 + 0.5 * np.sin(2 * np.pi * round(rate * 28) / 28 * t + r.uniform(0, 6))) ** 4
        group = (0.5 + 0.5 * np.sin(2 * np.pi * r.integers(20, 34) / 28 * t + r.uniform(0, 6))) ** 3
        x += level * carrier * chirp * (0.25 + 0.75 * group)
    write_loop("amb_night", x, peak=0.6)

# ---------------------------------------------------------------- music

MUSIC_SR = 22050


def pad_note(rng, hz, duration, sr=MUSIC_SR):
    n = int(duration * sr)
    t = np.arange(n) / sr
    x = np.zeros(n)
    for detune in (0.997, 1.0, 1.0031):
        for k, a in [(1, 1.0), (2, 0.42), (3, 0.2), (4, 0.09), (5, 0.04)]:
            drift = 0.0009 * np.sin(2 * np.pi * rng.uniform(0.04, 0.16) * t + rng.uniform(0, 6))
            phase = 2 * np.pi * hz * detune * k * (t + np.cumsum(drift) / sr)
            x += a * np.sin(phase + rng.uniform(0, 6.28)) * (0.75 + 0.25 * np.sin(2 * np.pi * rng.uniform(0.03, 0.09) * t + rng.uniform(0, 6)))
    swell = np.sin(np.pi * np.clip(t / duration, 0, 1)) ** 1.1
    return x * swell / 9


def bell_note(rng, hz, sr=MUSIC_SR):
    n = int(5.5 * sr)
    return modal(rng, n, [(hz, 1.3, 1), (hz * 2.01, 0.8, 0.28), (hz * 3.02, 0.35, 0.14), (hz * 5.4, 0.12, 0.05)], sr=sr, jitter=0) * 0.35


def convolve_stereo(x, seconds, rng, sr=MUSIC_SR, mix=0.55, circular=False, damp=2400.0):
    """Large hall from stereo decaying noise. Circular wraps the tail round so a loop has no seam."""
    n_ir = int(seconds * 3 * sr)
    ir_l = band_noise(rng, n_ir, 90, damp, sr) * np.exp(-np.arange(n_ir) / (seconds * sr))
    ir_r = band_noise(rng, n_ir, 90, damp, sr) * np.exp(-np.arange(n_ir) / (seconds * sr))
    ir_l /= np.sqrt(np.sum(ir_l ** 2))
    ir_r /= np.sqrt(np.sum(ir_r ** 2))
    if circular:
        size = x.shape[0]
        wet_l = np.fft.irfft(np.fft.rfft(x[:, 0]) * np.fft.rfft(ir_l, size), size)
        wet_r = np.fft.irfft(np.fft.rfft(x[:, 1]) * np.fft.rfft(ir_r, size), size)
        return x * (1 - mix) + np.stack([wet_l, wet_r], 1) * mix
    size = x.shape[0] + n_ir
    wet_l = np.fft.irfft(np.fft.rfft(x[:, 0], size) * np.fft.rfft(ir_l, size), size)
    wet_r = np.fft.irfft(np.fft.rfft(x[:, 1], size) * np.fft.rfft(ir_r, size), size)
    dry = np.vstack([x, np.zeros((n_ir, 2))])
    return dry * (1 - mix) + np.stack([wet_l, wet_r], 1) * mix


def music_explore():
    """Sparse, cold, unhurried: drones in D minor with a few far-off notes. Ends in silence on its own."""
    sr = MUSIC_SR
    r = rng_for("music_explore")
    d = 92.0
    n = int(d * sr)
    x = np.zeros((n, 2))
    chords = [
        (0, 22, [73.42, 110.0, 174.61]),
        (17, 22, [58.27, 116.54, 174.61]),
        (34, 22, [65.41, 98.0, 155.56]),
        (51, 24, [73.42, 110.0, 164.81]),
        (70, 22, [73.42, 87.31, 146.83]),
    ]
    for at, length, notes in chords:
        for hz in notes:
            voice_ = pad_note(r, hz, length, sr)
            pan = r.uniform(0.3, 0.7)
            start = int(at * sr)
            end = min(n, start + len(voice_))
            x[start:end, 0] += voice_[: end - start] * np.sqrt(1 - pan)
            x[start:end, 1] += voice_[: end - start] * np.sqrt(pan)
    melody = [(9, 293.66), (13.5, 349.23), (25, 261.63), (31, 233.08), (43, 311.13), (47, 293.66), (59, 220.0), (62.5, 261.63), (65, 293.66), (77, 349.23), (82, 293.66)]
    for at, hz in melody:
        bell = bell_note(r, hz, sr)
        pan = r.uniform(0.25, 0.75)
        start = int(at * sr)
        end = min(n, start + len(bell))
        x[start:end, 0] += bell[: end - start] * np.sqrt(1 - pan)
        x[start:end, 1] += bell[: end - start] * np.sqrt(pan)
    wet = convolve_stereo(x, 2.4, r, sr, 0.5)
    wet = wet[: n + int(5 * sr)]
    wet[-int(5 * sr):] *= np.linspace(1, 0, int(5 * sr))[:, None]
    wet[: int(0.5 * sr)] *= np.linspace(0, 1, int(0.5 * sr))[:, None]
    write("music_explore", np.tanh(unit(wet) * 1.15) * 0.82, sr)


def music_tension():
    """Exact-length loop for the chase layer: pulse, low ostinato and a nagging semitone. Circular reverb."""
    sr = MUSIC_SR
    r = rng_for("music_tension")
    bpm = 96
    beat = 60.0 / bpm
    bars = 12
    d = bars * 4 * beat
    n = int(round(d * sr))
    x = np.zeros((n, 2))

    def add(sig, at, pan=0.5, gain=1.0):
        start = int(at * sr) % n
        idx = (start + np.arange(len(sig))) % n
        x[idx, 0] += sig * gain * np.sqrt(1 - pan)
        x[idx, 1] += sig * gain * np.sqrt(pan)

    def drum(rng, hz=62, tau=0.16):
        m = int(0.7 * sr)
        return thump(rng, m, hz * 2.4, hz, tau, sr) + 0.14 * band_noise(rng, m, 200, 1500, sr) * decay(m, 0.03, sr)

    def low_string(rng, hz, length):
        m = int(length * sr)
        t = np.arange(m) / sr
        s = sum(np.sin(2 * np.pi * hz * k * t + rng.uniform(0, 6)) / k for k in range(1, 9))
        return s * np.minimum(1, t / 0.03) * np.exp(-t / (length * 0.55)) * 0.35

    pattern = [0, 1.5, 2.0, 3.5]
    for bar in range(bars):
        for hit in pattern:
            add(drum(r), (bar * 4 + hit) * beat, r.uniform(0.42, 0.58), 1.0 if hit == 0 else 0.68)
        root = 73.42 if bar % 4 != 3 else 77.78
        for sixteenth in range(0, 16, 2):
            accent = 1.0 if sixteenth % 8 == 0 else 0.62
            add(low_string(r, root, beat * 0.5), (bar * 4 + sixteenth / 4) * beat, 0.5, 0.5 * accent)
        if bar % 2 == 1:
            add(pad_note(r, 293.66, beat * 4, sr) * 0.5, (bar * 4) * beat, 0.3, 0.8)
            add(pad_note(r, 311.13, beat * 4, sr) * 0.5, (bar * 4 + 0.5) * beat, 0.7, 0.8)
    x = convolve_stereo(x, 1.0, r, sr, 0.3, circular=True)
    write("music_tension", np.tanh(unit(x) * 1.2) * 0.82, sr)


# ---------------------------------------------------------------- firearms

def gunshot(rng, d, crack, body, low, ring, tail, drive=1.6):
    """crack and body = (low hz, high hz, tau, level); low = (start hz, end hz, tau, level); ring = (hz, tau, level); tail = (size, mix)."""
    n = n_of(d)
    snap = crack[3] * band_noise(rng, n, crack[0], crack[1]) * decay(n, crack[2], attack=0.0002)
    x = body[3] * band_noise(rng, n, body[0], body[1]) * decay(n, body[2], attack=0.0004)
    x += low[3] * thump(rng, n, low[0], low[1], low[2])
    x += ring[2] * modal(rng, n, [(ring[0], ring[1], 1), (ring[0] * 1.73, ring[1] * 0.7, 0.7), (ring[0] * 2.91, ring[1] * 0.45, 0.4)], jitter=0.05)
    # The crack rides over the saturated body, or the thump's first swing would crush it. The last tanh is the
    # punch: it holds the sum's peak down, so normalising leaves the body as loud as door_bash's.
    return room(np.tanh(1.5 * (np.tanh(x * drive) + np.tanh(snap))), tail[0], tail[1], rng)


def gun_pistol(rng, i):
    return gunshot(rng, 0.3, (1800, 9000, 0.004, 1.25), (350, 2400, 0.018, 0.85), (rng.uniform(220, 260), 85, 0.04, 0.8), (1900 + 150 * i, 0.012, 0.16), (0.08, 0.3))


def gun_smg(rng, i):
    """620 rounds a minute is one shot every 97 ms: the body has to be gone by then, or the burst is a smear."""
    return gunshot(rng, 0.09, (2200, 10000, 0.0025, 1.0), (500, 3000, 0.009, 0.8), (rng.uniform(250, 290), 110, 0.015, 0.65), (2300 + 180 * i, 0.007, 0.14), (0.025, 0.18))


def gun_shotgun(rng, i):
    x = gunshot(rng, 0.7, (900, 7000, 0.007, 1.0), (180, 1800, 0.05, 1.0), (rng.uniform(160, 185), 46, 0.11, 1.0), (1250 + 90 * i, 0.02, 0.12), (0.2, 0.36), drive=1.8)
    return x


def gun_rifle(rng, i):
    x = gunshot(rng, 0.5, (2500, 11000, 0.0035, 1.5), (300, 2500, 0.025, 0.85), (rng.uniform(190, 215), 64, 0.06, 0.72), (2100 + 170 * i, 0.012, 0.14), (0.11, 0.3), drive=1.7)
    # A hard surface a little way off answers the shot.
    out = np.concatenate([x, np.zeros(n_of(0.25))])
    place(out, fft_band(x, 250, 2000), rng.uniform(0.15, 0.19), gain=0.16)
    return out


def metal_click(rng, hz, tau=0.008, body=0.5):
    n = n_of(0.08)
    return modal(rng, n, [(hz, tau, 1), (hz * 1.83, tau * 0.7, 0.7), (hz * 3.1, tau * 0.4, 0.35)], jitter=0.04) + body * thump(rng, n, 320, 150, 0.009)


def gun_reload(rng, _):
    n = n_of(1.25)
    x = np.zeros(n)
    place(x, metal_click(rng, 1900), 0.05, gain=0.7)  # Magazine catch.
    m = n_of(0.17)
    tt = np.linspace(0, 1, m)
    place(x, unit(svf_sweep(noise(rng, m), 900 + 1500 * tt, 3)) * np.sin(np.pi * tt) ** 1.5, 0.08, gain=0.3)  # Magazine out.
    place(x, grains(rng, n_of(0.25), 6, 0.2, 800, 3000, 0.008, strength=(0.05, 0.2)), 0.3)
    place(x, metal_click(rng, 1250, 0.014, 0.9), 0.58, gain=0.95)  # Magazine seats.
    place(x, metal_click(rng, 2500, 0.005, 0.2), 0.62, gain=0.5)
    m = n_of(0.13)
    tt = np.linspace(0, 1, m)
    place(x, metal_click(rng, 2200, 0.006), 0.83, gain=0.6)  # Slide back.
    place(x, unit(svf_sweep(noise(rng, m), 1500 + 2500 * tt, 4)) * np.sin(np.pi * tt), 0.84, gain=0.42)
    m = n_of(0.05)
    tt = np.linspace(0, 1, m)
    place(x, unit(svf_sweep(noise(rng, m), 4000 - 2000 * tt, 4)) * np.sin(np.pi * tt), 0.98, gain=0.4)  # Slide home.
    place(x, metal_click(rng, 1550, 0.016, 1.0), 1.02, gain=1.0)
    return room(x, 0.035, 0.2, rng)


def gun_empty(rng, _):
    n = n_of(0.1)
    x = modal(rng, n, [(2900, 0.004, 1), (4800, 0.002, 0.6)], jitter=0.02) + 0.35 * thump(rng, n, 520, 260, 0.004)
    place(x, modal(rng, n_of(0.04), [(2100, 0.003, 1)], jitter=0.02), 0.03, gain=0.25)
    return x


def bullet_impact(rng, i):
    n = n_of(0.3)
    dirt = 0.7 * thump(rng, n, 190, 70, 0.03) + 0.7 * band_noise(rng, n, 300, 2400) * decay(n, 0.016, attack=0.0003)
    dirt += 0.55 * grains(rng, n, 14, 0.14, 900, 5000, 0.004, delay=0.005)
    metal = modal(rng, n, [(rng.uniform(2100, 2700), 0.022, 1), (rng.uniform(3600, 4300), 0.03, 0.75), (rng.uniform(5700, 6600), 0.012, 0.5)], jitter=0.02)
    metal += 0.7 * band_noise(rng, n, 3000, 10000) * decay(n, 0.003, attack=0.0002)
    return dirt * (1.0, 0.45, 0.8)[i] + metal * (0.2, 0.9, 0.5)[i]


def raider_gunshot(rng, _):
    """Rougher and further away than the player's own guns: clipped, dull, and answered by the valley."""
    n = n_of(0.35)
    x = band_noise(rng, n, 900, 5000) * decay(n, 0.006, attack=0.0004)
    x += 0.9 * band_noise(rng, n, 250, 1800) * decay(n, 0.03, attack=0.0006)
    x += 0.8 * thump(rng, n, 170, 62, 0.05)
    x = fft_band(np.clip(x * 2.6, -0.8, 1.0), 150, 4200)
    out = np.concatenate([x, np.zeros(n_of(0.5))])
    dull = fft_band(x, 200, 1500)
    place(out, dull, 0.17, gain=0.3)
    place(out, dull, 0.36, gain=0.13)
    return room(out, 0.2, 0.36, rng, 2400)


# ---------------------------------------------------------------- energy weapons

def arcs(rng, n, count, span, delay=0.0, strength=(0.3, 1.0)):
    """Electric crackle: scattered short buzzing discharges, each with its own pitch."""
    out = np.zeros(n)
    for _ in range(count):
        m = n_of(rng.uniform(0.012, 0.05))
        tt = np.arange(m) / SR
        buzz = np.tanh(4 * np.sin(2 * np.pi * rng.uniform(700, 3400) * tt)) * (band_noise(rng, m, 200, 3000) > -0.1)
        sig = (0.6 * buzz + 0.7 * band_noise(rng, m, 2500, 11000)) * decay(m, m / SR * 0.35, attack=0.0003)
        place(out, sig, delay + span * rng.uniform(0, 1) ** 1.6, gain=rng.uniform(*strength))
    return out


def laser_shot(rng, i):
    """A hot, torn zap: two inharmonic FM voices falling fast through a mains-like buzz."""
    d = rng.uniform(0.26, 0.32)
    n = n_of(d)
    t = np.arange(n) / SR
    start = (2300, 2700, 2000)[i] * rng.uniform(0.95, 1.05)
    end = start * 0.12
    hz = end + (start - end) * np.exp(-t / 0.055)
    phase = 2 * np.pi * np.cumsum(hz) / SR
    voices = np.sin(phase + 1.6 * np.sin(phase * 1.5)) + 0.6 * np.sin(phase * 1.012 + 1.1 * np.sin(phase * 0.51))
    buzz = 0.75 + 0.25 * np.sign(np.sin(2 * np.pi * 140 * t))
    x = np.tanh(2.2 * voices) * buzz * decay(n, 0.085, attack=0.001)
    x += 0.35 * band_noise(rng, n, 3000, 11000) * decay(n, 0.012, attack=0.0002)
    x += 0.35 * thump(rng, n, 320, 120, 0.02)
    return room(fft_band(x, 90, 12000), 0.05, 0.18, rng)


def plasma_shot(rng, _):
    n = n_of(0.75)
    x = np.zeros(n)
    m = n_of(0.11)
    tt = np.arange(m) / SR
    place(x, np.sin(2 * np.pi * np.cumsum(300 + 1100 * (tt / 0.11) ** 2) / SR) * (tt / 0.11) ** 2, 0, gain=0.3)  # The charge.
    m = n_of(0.62)
    tt = np.arange(m) / SR
    low = np.tanh(3 * np.sin(2 * np.pi * np.cumsum(48 + 330 * np.exp(-tt / 0.07)) / SR)) * decay(m, 0.16, attack=0.004)
    air = unit(svf_sweep(noise(rng, m), 180 + 2400 * np.exp(-tt / 0.09), 2.2)) * decay(m, 0.13, attack=0.003)
    sizzle = band_noise(rng, m, 2500, 8000) * decay(m, 0.05) * (band_noise(rng, m, 30, 400) > -0.1)
    place(x, low + 0.65 * air + 0.25 * sizzle, 0.1)
    return room(x, 0.09, 0.22, rng)


def plasma_burst(rng, _):
    n = n_of(1.5)
    x = thump(rng, n, 170, 34, 0.24)
    x += 0.8 * band_noise(rng, n, 60, 1200) * decay(n, 0.16, attack=0.002)
    x += 0.5 * band_noise(rng, n, 1500, 9000) * decay(n, 0.02, attack=0.0003)
    x += 0.7 * arcs(rng, n, 46, 1.0, 0.01) * np.exp(-np.arange(n) / SR / 0.55)
    return room(np.tanh(x * 1.5), 0.2, 0.3, rng)


def energy_overheat(rng, _):
    """A capacitor whine climbing to a hard cut-out, then the vents opening."""
    rise = 0.95
    n = n_of(2.4)
    x = np.zeros(n)
    m = n_of(rise)
    tt = np.arange(m) / SR
    phase = 2 * np.pi * np.cumsum(500 * (3400 / 500) ** (tt / rise) + 40 * np.sin(2 * np.pi * 7 * tt)) / SR
    whine = np.sin(phase) + 0.4 * np.sin(2 * phase) + 0.5 * np.sin(1.012 * phase)
    edge = np.minimum(1, (rise - tt) / 0.02)
    place(x, whine * (tt / rise) ** 1.5 * edge, 0, gain=0.42)
    place(x, modal(rng, n_of(0.25), [(300, 0.04, 1), (720, 0.03, 0.7), (1500, 0.015, 0.4)]) + 0.8 * thump(rng, n_of(0.25), 180, 80, 0.025), rise - 0.005, gain=0.8)
    m = n_of(2.4 - rise)
    hiss = band_noise(rng, m, 2000, 10000) + 0.4 * band_noise(rng, m, 600, 2500)
    place(x, hiss * decay(m, 0.5, attack=0.02) * np.minimum(1, np.linspace(5, 0, m)), rise, gain=0.75)
    return x


# ---------------------------------------------------------------- base defence

def turret_fire(rng, i):
    n = n_of(0.16)
    x = gunshot(rng, 0.16, (1800, 8000, 0.003, 0.9), (500, 2600, 0.009, 0.8), (rng.uniform(220, 260), 110, 0.014, 0.6), (1100 + 90 * i, 0.02, 0.32), (0.035, 0.15))
    # The mount rattling under the recoil.
    for at in rng.uniform(0.018, 0.075, 3):
        place(x, modal(rng, n_of(0.03), [(rng.uniform(1500, 3400), 0.006, 1), (rng.uniform(4000, 6000), 0.003, 0.5)]), at, gain=rng.uniform(0.15, 0.35))
    return x


def trap_snap(rng, _):
    n = n_of(0.55)
    t = np.arange(n) / SR
    x = thump(rng, n, 230, 80, 0.03)
    x += 0.7 * band_noise(rng, n, 1500, 9000) * decay(n, 0.004, attack=0.0002)
    x += 0.8 * modal(rng, n, [(1700, 0.02, 1), (3100, 0.015, 0.7), (5200, 0.01, 0.4)])
    spring = modal(rng, n, [(410, 0.14, 1), (1130, 0.11, 0.7), (2050, 0.07, 0.45)], jitter=0.02) * (0.6 + 0.4 * np.sin(2 * np.pi * 38 * t))
    place(x, spring, 0.008, gain=0.45)
    return x


def trap_shock(rng, _):
    n = n_of(1.0)
    t = np.arange(n) / SR
    env = np.interp(t, [0, 0.03, 0.35, 0.95, 1.0], [0, 1, 0.6, 0.12, 0])
    buzz = np.tanh(3 * sum(np.sin(2 * np.pi * 120 * k * t) / k for k in range(1, 12))) * (band_noise(rng, n, 8, 60) > -0.2)
    x = 0.4 * fft_band(buzz, 100, 6000) * env + arcs(rng, n, 46, 0.9) * (0.35 + 0.65 * env)
    x += 0.6 * band_noise(rng, n, 2000, 10000) * decay(n, 0.008, attack=0.0002)
    return x


def structure_hit(rng, _):
    n = n_of(0.6)
    x = thump(rng, n, rng.uniform(150, 185), 50, 0.075)
    x += 0.8 * modal(rng, n, [(185, 0.05, 1), (410, 0.04, 0.8), (860, 0.025, 0.5)], jitter=0.1)
    x += 0.3 * modal(rng, n, [(rng.uniform(460, 620), 0.09, 1), (rng.uniform(1150, 1400), 0.07, 0.7), (rng.uniform(2500, 3000), 0.04, 0.4)], jitter=0.02)
    x += 0.5 * band_noise(rng, n, 500, 4000) * decay(n, 0.012, attack=0.0003)
    x += 0.35 * grains(rng, n, 16, 0.25, 700, 4000, 0.006, delay=0.015)
    return room(np.tanh(x * 1.4), 0.07, 0.22, rng)


def structure_break(rng, _):
    n = n_of(2.6)
    t = np.arange(n) / SR
    x = thump(rng, n, 190, 38, 0.18)
    x += 0.8 * modal(rng, n, [(195, 0.14, 1), (480, 0.11, 0.9), (1130, 0.08, 0.7), (2150, 0.05, 0.5)])
    x += 0.7 * band_noise(rng, n, 500, 6000) * decay(n, 0.03)
    x += 0.5 * grains(rng, n, 60, 0.8, 1200, 6000, 0.004, delay=0.01)  # Splintering.
    x += 0.5 * band_noise(rng, n, 30, 300) * np.interp(t, [0, 0.1, 1.0, 2.2, 2.6], [0, 1, 0.55, 0.08, 0])
    x += 0.1 * band_noise(rng, n, 1500, 6000) * np.interp(t, [0, 0.5, 2.5, 2.6], [0, 1, 0.1, 0])  # Dust settling.
    for at in np.sort(rng.uniform(0.15, 1.75, 14)):  # Chunks landing, fewer and quieter as it finishes.
        m = n_of(0.3)
        chunk = thump(rng, m, rng.uniform(110, 220), rng.uniform(45, 80), rng.uniform(0.03, 0.07))
        chunk += 0.7 * modal(rng, m, [(rng.uniform(170, 300), 0.04, 1), (rng.uniform(400, 700), 0.03, 0.7), (rng.uniform(900, 1500), 0.015, 0.4)])
        place(x, chunk, at, gain=0.65 * np.exp(-at / 1.1) * rng.uniform(0.6, 1))
    for at in rng.uniform(0.2, 1.3, 4):
        base = rng.uniform(320, 800)
        place(x, modal(rng, n_of(0.35), [(base, 0.09, 1), (base * 2.37, 0.1, 0.7), (base * 4.1, 0.05, 0.4)], jitter=0.02), at, gain=rng.uniform(0.12, 0.3))
    return room(np.tanh(x * 1.4), 0.2, 0.3, rng)


def raid_horn(rng, _):
    """A long horn a long way off: a wide brass stack, dulled by distance, the valley answering late."""
    d = 3.4
    n = n_of(d)
    t = np.arange(n) / SR
    f0 = np.interp(t, [0, 0.35, 0.9, 2.7, d], [74, 94, 98, 97, 84])
    f0 = f0 * (1 + 0.004 * np.sin(2 * np.pi * 4.3 * t) + 0.003 * unit(fft_band(noise(rng, n), 0.3, 3)))

    def brass(hz):
        phase = 2 * np.pi * np.cumsum(hz) / SR
        return sum(np.sin(k * phase) / k ** 0.9 for k in range(1, 23))

    x = brass(f0) + 0.7 * brass(f0 * 1.006) + 0.45 * brass(f0 * 0.7492) + 0.3 * brass(f0 * 0.5)
    x = unit(x) * np.interp(t, [0, 0.5, 2.6, d], [0, 1, 0.9, 0]) ** 1.2
    x = fft_band(x, 70, 1300, slope=0.5) + 0.6 * fft_band(x, 300, 800)
    x += 0.03 * band_noise(rng, n, 300, 1600)
    out = np.concatenate([x, np.zeros(n_of(0.7))])
    place(out, fft_band(x, 200, 900), 0.7, gain=0.32)
    out = room(out, 0.55, 0.4, rng, 1800)[:n_of(4.3)]
    out[-n_of(0.5):] *= np.linspace(1, 0, n_of(0.5)) ** 1.5
    return out


def alarm_klaxon(rng, _):
    n = n_of(2.0)
    x = np.zeros(n)
    length = 0.56
    for at in (0.0, 0.68, 1.36):
        m = n_of(length)
        tt = np.arange(m) / SR
        hz = 400 - 110 * np.exp(-tt / 0.045)
        phase = 2 * np.pi * np.cumsum(hz) / SR
        honk = np.tanh(6 * np.sin(phase)) + 0.7 * np.tanh(6 * np.sin(phase * 1.26))
        honk *= 0.82 + 0.18 * np.sign(np.sin(2 * np.pi * 60 * tt))
        honk *= np.minimum(1, tt / 0.008) * np.minimum(1, (length - tt) / 0.025)
        place(x, honk, at)
    return room(fft_band(x, 200, 5000), 0.1, 0.2, rng)


# ---------------------------------------------------------------- power

def engine_pop(rng, sr=SR):
    n = int(0.09 * sr)
    return thump(rng, n, 170, 75, 0.024, sr) + 0.55 * band_noise(rng, n, 250, 1600, sr) * decay(n, 0.012, sr, 0.0005)


def engine_run(rng, rates, skip=None, gains=None):
    """Fires one combustion pop per cycle of `rates` (per-sample Hz). skip is a per-sample misfire chance."""
    x = np.zeros(len(rates))
    cycles = np.cumsum(rates) / SR
    fired = np.nonzero(np.diff(np.floor(cycles)) > 0)[0]
    for index in fired:
        if skip is not None and rng.uniform() < skip[index]:
            continue
        gain = rng.uniform(0.75, 1.0) * (gains[index] if gains is not None else 1.0)
        place(x, engine_pop(rng), index / SR, gain=gain)
    return x


def generator_start(rng, _):
    d = 2.15
    n = n_of(d)
    t = np.arange(n) / SR
    x = np.zeros(n)
    # The cord: a rising rasp and an accelerating ratchet.
    m = n_of(0.58)
    tt = np.linspace(0, 1, m)
    place(x, unit(svf_sweep(noise(rng, m), 300 + 1500 * np.sin(np.pi * tt) ** 2, 3)) * np.sin(np.pi * tt) ** 1.2, 0.03, gain=0.4)
    at = 0.05
    gap = 0.075
    while at < 0.58:
        place(x, modal(rng, n_of(0.03), [(1500, 0.005, 1), (2800, 0.003, 0.5)]), at, gain=0.3)
        at += gap
        gap = max(0.028, gap * 0.87)
    # Two coughs.
    place(x, engine_pop(rng), 0.6, gain=0.7)
    place(x, engine_pop(rng), 0.79, gain=0.55)
    # Catches, overshoots, settles at the rate generator_hum runs at.
    catch = 0.94
    rates = np.interp(t, [0, catch, 1.45, 1.75, d], [0, 7, 29, 25, 25]) * (t >= catch)
    gains = np.interp(t, [catch, 1.3, d], [0.7, 1.0, 0.9])
    x += engine_run(rng, rates, gains=gains)
    # Generator winding spinning up underneath.
    hz = np.interp(t, [0, catch, 1.6, d], [0, 0, 400, 400])
    whine = np.sin(2 * np.pi * np.cumsum(hz) / SR) + 0.4 * np.sin(4 * np.pi * np.cumsum(hz) / SR)
    x += 0.05 * whine * np.clip((t - catch) / 0.5, 0, 1)
    x += 0.2 * band_noise(rng, n, 80, 700) * np.clip((t - catch) / 0.3, 0, 1)
    # generator_hum takes over from here.
    x *= np.interp(t, [0, d - 0.25, d], [1, 1, 0])
    return room(x, 0.05, 0.15, rng)


def generator_stop(rng, _):
    d = 1.7
    n = n_of(d)
    t = np.arange(n) / SR
    rates = np.interp(t, [0, 0.35, 0.9, 1.25, 1.3], [25, 16, 8, 4, 0])
    skip = np.interp(t, [0, 0.3, 1.2], [0, 0.18, 0.4])
    x = engine_run(rng, rates, skip=skip, gains=np.interp(t, [0, 0.6, 1.3], [0.9, 1.0, 0.6]))
    x += 0.25 * band_noise(rng, n, 80, 700) * np.interp(t, [0, 0.6, 1.3, d], [1, 0.5, 0.05, 0])
    hz = np.interp(t, [0, 1.2], [400, 40])
    x += 0.05 * np.sin(2 * np.pi * np.cumsum(hz) / SR) * np.interp(t, [0, 1.2], [1, 0])
    place(x, modal(rng, n_of(0.15), [(2600, 0.02, 1), (4300, 0.012, 0.5)]), 1.48, gain=0.09)  # Metal ticking as it cools.
    return room(x, 0.05, 0.15, rng)


def repair_tick(rng, _):
    n = n_of(0.22)
    x = 0.8 * modal(rng, n, [(3200, 0.006, 1), (5400, 0.004, 0.6)], jitter=0.04)
    x += 0.8 * arcs(rng, n, 8, 0.14)
    x += 0.45 * grains(rng, n, 16, 0.18, 3000, 9000, 0.0025, strength=(0.1, 0.7))
    return x


def power_down(rng, _):
    n = n_of(1.6)
    t = np.arange(n) / SR
    hz = 28 + 392 * np.exp(-t / 0.33)
    phase = 2 * np.pi * np.cumsum(hz) / SR
    x = sum(a * np.sin(k * phase) for k, a in [(1, 1.0), (2, 0.5), (3, 0.3), (5, 0.15)])
    x *= np.interp(t, [0, 0.04, 0.9, 1.5, 1.6], [0, 1, 0.5, 0.05, 0])
    x += 0.5 * (np.sin(2 * np.pi * 100 * t) + 0.5 * np.sin(2 * np.pi * 300 * t)) * np.exp(-t / 0.2)
    x += 0.8 * modal(rng, n, [(680, 0.02, 1), (1720, 0.014, 0.8), (3300, 0.008, 0.4)]) + 0.8 * thump(rng, n, 240, 90, 0.02)  # The relay.
    x += 0.5 * arcs(rng, n, 10, 0.4, 0.01, (0.2, 0.7))
    return room(x, 0.1, 0.2, rng)


def write_device_loop(name, x, sr=32000, peak=0.6):
    """Seamless loop for a device's own AudioSource. Rotated to start on a zero crossing, so starting it doesn't click either."""
    x = x - np.mean(x)
    rising = np.nonzero((x[:-1] <= 0) & (x[1:] > 0))[0]
    gentlest = rising[np.argmin(x[rising + 1] - x[rising])]
    write(name, unit(np.roll(x, -int(gentlest + 1))) * peak, sr)


def device_loops():
    """Every partial has an integer cycle count over the loop, and all noise and pulses wrap, so these repeat exactly."""
    sr = 32000
    seconds = 4
    n = seconds * sr
    t = np.arange(n) / sr

    r = rng_for("generator_hum")
    firing = 25  # Matches where generator_start settles.
    x = np.zeros(n)
    for k in range(firing * seconds):
        pop = engine_pop(r, sr)
        at = int(k * n / (firing * seconds) + r.uniform(-0.0006, 0.0006) * sr)
        x[(at + np.arange(len(pop))) % n] += pop * r.uniform(0.75, 1.0) * (1.0 if k % 2 == 0 else 0.82)
    beat = np.sin(np.pi * firing * t) ** 2
    x += 0.32 * band_noise(r, n, 80, 700, sr) * (0.55 + 0.45 * beat) * (0.85 + 0.15 * periodic_lfo(r, n, 3))
    x += 0.07 * band_noise(r, n, 1800, 5000, sr) * beat ** 4
    for hz, level in [(50, 0.2), (100, 0.14), (400, 0.05), (800, 0.02)]:
        x += level * np.sin(2 * np.pi * hz * t + r.uniform(0, 6.28))
    write_device_loop("generator_hum", x, sr, 0.7)

    r = rng_for("machine_hum")
    x = np.zeros(n)
    for hz, level in [(50, 0.5), (100, 1.0), (150, 0.35), (200, 0.3), (300, 0.14), (350, 0.06), (500, 0.03)]:
        x += level * np.sin(2 * np.pi * hz * t + r.uniform(0, 6.28)) * (0.88 + 0.12 * periodic_lfo(r, n, 4))
    x = unit(x)
    x += 0.18 * band_noise(r, n, 200, 1200, sr) * (0.8 + 0.2 * periodic_lfo(r, n, 5))
    x += 0.025 * band_noise(r, n, 2500, 6000, sr)
    write_device_loop("machine_hum", x, sr, 0.45)

    r = rng_for("fan_loop")
    rotation, blades = 13, 7
    blade = rotation * blades
    x = band_noise(r, n, 150, 2400, sr) * (0.55 + 0.45 * np.cos(np.pi * blade * t) ** 2) * (0.9 + 0.1 * np.sin(2 * np.pi * rotation * t))
    for hz, level in [(blade, 0.3), (blade * 2, 0.16), (blade * 3, 0.07), (rotation, 0.08)]:
        x += level * np.sin(2 * np.pi * hz * t + r.uniform(0, 6.28))
    x += 0.012 * np.sin(2 * np.pi * 2600 * t) * periodic_lfo(r, n, 3)  # Bearing.
    write_device_loop("fan_loop", x, sr, 0.55)

    r = rng_for("turret_laser")
    d = 0.5
    m = int(d * SR)
    t = np.arange(m) / SR
    buzz = sum(np.sin(2 * np.pi * 120 * k * t) / k ** 0.8 for k in range(1, 25))
    x = np.tanh(0.8 * buzz) * (0.8 + 0.2 * np.sin(2 * np.pi * 30 * t))
    x += 0.3 * (np.sin(2 * np.pi * 1440 * t) + 0.7 * np.sin(2 * np.pi * 1924 * t)) * (0.5 + 0.5 * np.sin(2 * np.pi * 34 * t))
    x += 0.2 * band_noise(r, m, 2000, 9000) * (0.5 + 0.5 * np.sin(2 * np.pi * 240 * t))
    write_device_loop("turret_laser", x, SR, 0.7)


# ---------------------------------------------------------------- radio and objectives

def radio_static(rng, _):
    """Static with someone almost talking under it. The syllables are formants only: there are no words to find."""
    d = 2.1
    n = n_of(d)
    t = np.arange(n) / SR
    flutter = 0.55 + 0.45 * unit(fft_band(noise(rng, n), 2, 30))
    x = 0.55 * band_noise(rng, n, 400, 6500) * flutter
    x += 0.35 * grains(rng, n, 40, d, 1500, 7000, 0.002, strength=(0.2, 1.0))
    speech = np.zeros(n)
    at = 0.22
    while at < 1.7:
        length = rng.uniform(0.11, 0.26)
        f = rng.uniform(105, 150)
        formant = [AH, UH, OH, EH][rng.integers(0, 4)]
        place(speech, voice(rng, length, [(0, f), (1, f * rng.uniform(0.82, 1.18))], formant, rough=0.08, breath=0.5, attack=0.015, release=0.05), at)
        at += length + rng.uniform(0.03, 0.14)
    x += 0.65 * fft_band(speech, 500, 2600) * np.sin(np.pi * t / d) ** 0.7
    x *= np.interp(t, [0, 0.03, d - 0.12, d], [0, 1, 0.9, 0])
    for at in (0.0, d - 0.05):  # Squelch opening and closing.
        place(x, modal(rng, n_of(0.04), [(2400, 0.004, 1), (4100, 0.002, 0.5)]), at, gain=0.5)
    return x


def radio_ping(rng, _):
    n = n_of(0.42)
    x = np.zeros(n)
    for hz, at, length in [(880, 0.0, 0.11), (1320, 0.15, 0.17)]:
        m = n_of(length)
        tt = np.arange(m) / SR
        tone = (np.sin(2 * np.pi * hz * tt) + 0.25 * np.sin(6 * np.pi * hz * tt)) * np.minimum(1, tt / 0.004) * np.minimum(1, (length - tt) / 0.012)
        place(x, tone, at)
    return room(fft_band(x, 300, 4000), 0.03, 0.1, rng)


def objective_done(rng, _):
    """Same family as upgrade and discovery: marimba notes over a held open interval, no fanfare."""
    n = n_of(1.45)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for hz, level in [(146.83, 0.8), (220.0, 0.65)]:
        partial = sum(a * np.sin(2 * np.pi * hz * k * t) for k, a in [(1, 1), (2, 0.35), (3, 0.14)])
        x += 0.3 * level * partial * np.clip(t / 0.05, 0, 1) * np.exp(-t / 0.55)
    place(x, tones(rng, [293.66, 440, 587.33], tau=0.2, length=0.6, gap=0.11), 0)
    return room(x, 0.25, 0.3, rng, 2600)


def m22_clips():
    clip("gun_pistol", gun_pistol, 3, 0.92)
    clip("gun_smg", gun_smg, 3, 0.88)
    clip("gun_shotgun", gun_shotgun, 2, 0.95)
    clip("gun_rifle", gun_rifle, 3, 0.95)
    clip("gun_reload", gun_reload, None, 0.75)
    clip("gun_empty", gun_empty, None, 0.6)
    clip("laser_shot", laser_shot, 3, 0.8)
    clip("plasma_shot", plasma_shot, None, 0.9)
    clip("plasma_burst", plasma_burst, None, 0.95)
    clip("energy_overheat", energy_overheat, None, 0.75)
    clip("bullet_impact", bullet_impact, 3, 0.8)
    clip("raider_gunshot", raider_gunshot, None, 0.85)
    clip("turret_fire", turret_fire, 3, 0.85)
    clip("trap_snap", trap_snap, None, 0.9)
    clip("trap_shock", trap_shock, None, 0.8)
    clip("structure_hit", structure_hit, 3, 0.9)
    clip("structure_break", structure_break, None, 0.95)
    clip("raid_horn", raid_horn, None, 0.85)
    clip("alarm_klaxon", alarm_klaxon, None, 0.6)
    clip("generator_start", generator_start, None, 0.8)
    clip("generator_stop", generator_stop, None, 0.8)
    clip("repair_tick", repair_tick, None, 0.6)
    clip("power_down", power_down, None, 0.8)
    clip("radio_static", radio_static, None, 0.65)
    clip("radio_ping", radio_ping, None, 0.55)
    clip("objective_done", objective_done, None, 0.7)
    device_loops()


def geiger_click(rng, i):
    """One Geiger-Muller tube discharge through a tiny speaker: a sharp broadband tick with a short metallic ring."""
    n = n_of(0.035)
    x = band_noise(rng, n, 1500, 12000) * decay(n, 0.0011 * (1 + 0.3 * i), attack=0.0002)
    x += 0.55 * modal(rng, n, [(3100 + 450 * i, 0.003, 1), (5300, 0.0018, 0.5)], jitter=0.05)
    x += 0.3 * thump(rng, n, 900, 300, 0.004)
    return x


def m24_clips():
    clip("geiger_click", geiger_click, 3, 0.7)


# ---------------------------------------------------------------- vehicles (m29)

def vehicle_loops():
    """Engine and chain loops. Every tone has an integer cycle count over the loop and the noise wraps, so they repeat exactly.
    The game plays them on the vehicle with the pitch following its speed."""
    sr = 32000
    seconds = 2
    n = seconds * sr
    t = np.arange(n) / sr

    def engine(name, firing, harmonics, rasp, rumble, wobble):
        r = rng_for(name)
        x = np.zeros(n)
        for k in range(1, harmonics + 1):
            # A firing pulse train: harmonics of the firing rate, each with its own phase so it is not a pure buzz.
            x += np.sin(2 * np.pi * firing * k * t + r.uniform(0, 2 * np.pi)) / k ** 0.85
        # Uneven combustion: a slow half-rate wobble that wraps.
        x *= 1.0 + wobble * np.sin(2 * np.pi * (firing / 2) * t) + 0.5 * wobble * np.sin(2 * np.pi * (firing / 4) * t)
        x = np.tanh(1.4 * x)
        # Mechanical rasp and exhaust rumble, both pulsed at the firing rate.
        pulse = 0.5 + 0.5 * np.sin(2 * np.pi * firing * t)
        x += rasp * band_noise(r, n, 600, 3500, sr) * pulse
        x += rumble * band_noise(r, n, 40, 180, sr) * (0.6 + 0.4 * pulse)
        write_device_loop(name, x, sr, 0.7)

    engine("motor_buggy", 22, 18, 0.22, 0.45, 0.10)  # light, buzzy buggy engine
    engine("motor_van", 14, 22, 0.14, 0.75, 0.06)  # heavy, low van diesel

    # Bicycle: a freewheel ratchet and a soft chain whirr.
    r = rng_for("bike_chain")
    x = 0.18 * band_noise(r, n, 1200, 5500, sr) * (0.7 + 0.3 * np.sin(2 * np.pi * 3 * t))
    clicks = np.zeros(n)
    for i in range(24):  # 12 clicks per second
        at = int(i * n / 24)
        width = int(0.004 * sr)
        clicks[at:at + width] += np.exp(-np.arange(width) / (0.0012 * sr)) * (1.0 if i % 2 == 0 else 0.7) * r.uniform(0.8, 1.0)
    x += 0.5 * fft_band(clicks, 900, 7000, sr)
    write_device_loop("bike_chain", x, sr, 0.55)



# ---------------------------------------------------------------- furniture, engine start/stop, interiors (m30)

def drawer_open(rng, _):
    """A wooden drawer dragged open: a rough slide, a rattle of whatever is inside, and the stop at the end."""
    n = n_of(0.7)
    x = np.zeros(n)
    slide_n = n_of(0.34)
    slide = band_noise(rng, slide_n, 180, 1600) * (0.55 + 0.45 * unit(fft_band(noise(rng, slide_n), 4, 40)))
    slide *= np.sin(np.pi * np.linspace(0, 1, slide_n)) ** 0.7
    place(x, slide, 0.02, gain=0.55)
    place(x, grains(rng, n_of(0.3), 9, 0.3, 1200, 5200, 0.004, strength=(0.15, 0.5)), 0.12, gain=0.5)
    stop = n_of(0.3)
    place(x, 0.8 * thump(rng, stop, 150, 70, 0.05) + 0.4 * modal(rng, stop, [(310, 0.05, 1), (780, 0.035, 0.6), (1900, 0.02, 0.3)]), 0.34)
    return room(x, 0.1, 0.2, rng)


def cabinet_open(rng, _):
    """A cupboard door on a dry hinge: a latch click, then a squeaking swing."""
    n = n_of(0.9)
    x = np.zeros(n)
    place(x, modal(rng, n_of(0.08), [(2100, 0.01, 1), (3600, 0.006, 0.6)]), 0.0, gain=0.55)
    c = creak(rng, 0.55, 520, 1250) * np.sin(np.pi * np.linspace(0, 1, n_of(0.55))) ** 0.6
    place(x, c, 0.06, gain=0.5)
    place(x, 0.5 * thump(rng, n_of(0.2), 130, 65, 0.05), 0.55)
    return room(x, 0.1, 0.2, rng)


def engine_start(rng, _):
    """A starter cranking, a cough, and the engine catching and settling to its idle."""
    n = n_of(2.2)
    t = np.arange(n) / SR
    x = np.zeros(n)
    crank_end = 1.05
    rate = 5.5 + 2.5 * np.clip(t / crank_end, 0, 1)
    pulses = np.where(t < crank_end, 0.5 + 0.5 * np.sin(2 * np.pi * np.cumsum(rate) / SR), 0.0)
    x += 0.5 * band_noise(rng, n, 80, 900) * pulses
    x += 0.3 * np.sin(2 * np.pi * 95 * t) * pulses
    # The engine catches at the end of the crank and runs up to idle (22 Hz firing, same family as the motor loop).
    catch = np.clip((t - crank_end) / 0.35, 0, 1)
    firing = 11 + 11 * catch
    phase = 2 * np.pi * np.cumsum(firing) / SR
    engine = sum(np.sin(phase * k + rng.uniform(0, 6.28)) / k ** 0.85 for k in range(1, 14))
    engine = np.tanh(1.3 * engine) * (t > crank_end) * (0.2 + 0.8 * catch) * np.exp(-np.maximum(0, t - 1.9) / 0.6)
    x += 0.7 * engine
    x += 0.2 * band_noise(rng, n, 600, 3000) * (t > crank_end) * catch * (0.5 + 0.5 * np.sin(phase))
    return x


def engine_stop(rng, _):
    """The engine winding down with a couple of sputters, then a settling tick."""
    n = n_of(1.3)
    t = np.arange(n) / SR
    firing = 22 * np.exp(-t / 0.35) + 3
    phase = 2 * np.pi * np.cumsum(firing) / SR
    engine = np.tanh(1.2 * sum(np.sin(phase * k + rng.uniform(0, 6.28)) / k ** 0.85 for k in range(1, 12)))
    engine *= np.exp(-t / 0.4)
    stutter = 0.6 + 0.4 * np.sin(2 * np.pi * (firing * 0.5) * t)
    x = 0.75 * engine * stutter
    place(x, modal(rng, n_of(0.12), [(900, 0.02, 1), (2400, 0.01, 0.5)]), 1.0, gain=0.25)
    return x


def interior_loops():
    """Quiet room tone for the inside of a ruined building: low air movement, a faint mains hum, the odd distant drip.
    The noise is circular and every tone has an integer cycle count, so it loops exactly."""
    sr = 32000
    seconds = 8
    n = seconds * sr
    t = np.arange(n) / sr
    r = rng_for("amb_interior")
    x = 0.55 * band_noise(r, n, 40, 260, sr) * (0.7 + 0.3 * np.sin(2 * np.pi * 0.25 * t))
    x += 0.12 * band_noise(r, n, 500, 2200, sr) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.125 * t + 1.0)) ** 2
    x += 0.05 * np.sin(2 * np.pi * 50 * t) + 0.025 * np.sin(2 * np.pi * 100 * t)
    for i in range(3):
        at = int(r.uniform(0.5, seconds - 0.8) * sr)
        d = int(0.25 * sr)
        k = np.arange(d) / sr
        drip = np.sin(2 * np.pi * (900 + 300 * i) * k) * np.exp(-k / 0.03)
        end = min(n, at + d)
        x[at:end] += 0.1 * drip[: end - at]
    write_device_loop("amb_interior", x, sr, 0.35)


def m30_clips():
    clip("drawer_open", drawer_open, 3, 0.8)
    clip("cabinet_open", cabinet_open, 3, 0.8)
    clip("engine_start", engine_start, None, 0.85)
    clip("engine_stop", engine_stop, None, 0.8)
    interior_loops()

# ---------------------------------------------------------------- catalogue

def main():
    os.makedirs(OUT, exist_ok=True)
    only = sys.argv[1] if len(sys.argv) > 1 else None

    def want(section):
        return only is None or only == section

    if want("sfx"):
        clip("foot_dirt", foot_dirt, 5, 0.8)
        clip("foot_concrete", foot_concrete, 5, 0.8)
        clip("foot_wood", foot_wood, 4, 0.8)
        clip("foot_metal", foot_metal, 4, 0.7)
        clip("foot_snow", foot_snow, 4, 0.8)
        clip("foot_mud", foot_mud, 4, 0.8)
        clip("land", land, 2)
        clip("jump", jump, 2, 0.6)
        clip("swing", swing, 3, 0.75)
        clip("hit_flesh", hit_flesh, 4)
        clip("hit_block", hit_block, 3)
        clip("raider_swing", raider_weapon_swing, 2, 0.75)
        clip("player_hurt", player_hurt, 4, 0.85)
        clip("player_death", player_death, None, 0.9)
        clip("player_breath", player_jump_breath, 2, 0.4)
        clip("raider_alert", raider_alert, 3, 0.9)
        clip("raider_hurt", raider_hurt, 4, 0.85)
        clip("raider_death", raider_death, 3, 0.9)
        clip("raider_growl", raider_growl, 3, 0.75)
        clip("door_open", door_open)
        clip("door_close", door_close)
        clip("door_bash", door_bash, None, 0.95)
        clip("gather_wood", gather_wood, 4)
        clip("gather_stone", gather_stone, 4)
        clip("gather_ore", gather_ore, 3)
        clip("gather_scrap", gather_scrap, 4, 0.8)
        clip("gather_leaves", gather_leaves, 3, 0.75)
        clip("rummage", rummage, 2, 0.75)
        clip("crate_open", crate_open, 2, 0.8)
        clip("loot_found", loot_found, None, 0.55)
        clip("loot_empty", loot_empty, None, 0.4)
        clip("eat", eat, 2, 0.75)
        clip("drink", drink, 2, 0.8)
        clip("heal", heal, None, 0.7)
        clip("equip", equip, None, 0.7)
        clip("craft", craft, 2, 0.8)
        clip("place_piece", place_piece, 3)
        clip("ui_deny", ui_deny, None, 0.5)
        clip("ui_click", ui_click, None, 0.5)
        clip("ui_open", ui_open, None, 0.5)
        clip("ui_close", ui_close, None, 0.5)
        clip("ui_tick", ui_tick, None, 0.35)
        clip("game_saved", game_saved, None, 0.55)
        clip("game_loaded", game_loaded, None, 0.55)
        clip("sleep", sleep, None, 0.7)
        clip("button_press", button_press, None, 0.85)
        clip("siren", siren, None, 0.8)
        clip("quake", quake, None, 0.95)
        clip("heartbeat", heartbeat, None, 0.9)
        clip("warn_vital", warn_vital, None, 0.6)
        clip("stinger_death", stinger_death, None, 0.85)
        clip("amb_bird", bird, 5, 0.7)
        clip("amb_crow", crow, 2, 0.7)
        clip("amb_creak", metal_creak, 3, 0.7)
        clip("amb_boom", distant_boom, 2, 0.85)
        clip("amb_wolf", wolf, 2, 0.7)
        clip("amb_frog", frog, 3, 0.7)
        clip("amb_drip", drip, 3, 0.6)
        clip("amb_pipe", pipe_groan, 2, 0.7)
        clip("foot_water", foot_water, 4, 0.75)
        clip("thunder", thunder, 3, 0.9)
        clip("discovery", discovery, None, 0.65)
        clip("upgrade", upgrade, None, 0.75)
        clip("deer_call", deer_call, 2, 0.7)
        clip("boar_grunt", boar_grunt, 3, 0.75)
    if want("ambience"):
        ambience()
        weather_loops()
    if want("music"):
        music_explore()
        music_tension()
    if want("m22"):
        m22_clips()
    if want("m24"):
        m24_clips()
    if want("m29"):
        vehicle_loops()
    if want("m30"):
        m30_clips()

    # Folder metas with stable GUIDs too, written bottom-up from the generated tree.
    folder = OUT
    assets = os.path.normpath(os.path.join(OUT, "..", "..", ".."))
    while folder != assets and only not in ("m22", "m24", "m29", "m30"):
        write_meta(folder, "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
        folder = os.path.dirname(folder)
    print(f"Wrote {len(written)} clips to {OUT}")


if __name__ == "__main__":
    main()
