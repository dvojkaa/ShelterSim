#!/usr/bin/env python3
"""Objective checks on the generated WAVs, since an agent cannot listen to them.

Fails on clipping, DC offset, silence, a click at either end, or a loop seam. Writes a spectrogram
sheet to ArtSource/previews/audio_spectrograms.ppm for a visual look at the shape of each sound.
"""
import glob
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
FOLDER = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Audio", "Resources", "Audio"))
PREVIEW = os.path.normpath(os.path.join(HERE, "..", "previews", "audio_spectrograms.ppm"))
LOOPS = ("amb_wasteland", "amb_forest", "amb_desert", "amb_snow", "amb_marsh", "amb_highlands", "amb_bunker", "amb_rain", "amb_night", "music_tension")


def read(path):
    with wave.open(path) as f:
        data = np.frombuffer(f.readframes(f.getnframes()), dtype=np.int16).astype(np.float64) / 32768
        channels, rate = f.getnchannels(), f.getframerate()
    return (data.reshape(-1, channels) if channels > 1 else data), rate


def spectrogram(x, rate, width=150, height=64):
    mono = x if x.ndim == 1 else x.mean(axis=1)
    size = 512
    hops = np.linspace(0, max(0, len(mono) - size), width).astype(int)
    window = np.hanning(size)
    image = np.zeros((height, width))
    for column, hop in enumerate(hops):
        frame = mono[hop:hop + size]
        if len(frame) < size:
            frame = np.pad(frame, (0, size - len(frame)))
        magnitude = np.abs(np.fft.rfft(frame * window))
        edges = np.geomspace(1, len(magnitude) - 1, height + 1).astype(int)
        for row in range(height):
            image[height - 1 - row, column] = magnitude[edges[row]:max(edges[row] + 1, edges[row + 1])].max()
    decibels = 20 * np.log10(image + 1e-6)
    return np.clip((decibels + 70) / 70, 0, 1)


def main():
    problems = []
    rows = []
    tiles = []
    total = 0
    for path in sorted(glob.glob(os.path.join(FOLDER, "*.wav"))):
        name = os.path.basename(path)[:-4]
        x, rate = read(path)
        total += os.path.getsize(path)
        peak = float(np.max(np.abs(x)))
        rms = float(np.sqrt(np.mean(x ** 2)))
        dc = float(abs(np.mean(x)))
        seconds = len(x) / rate
        mono = x if x.ndim == 1 else x.mean(axis=1)
        start_jump = abs(mono[0])
        end_jump = abs(mono[-1])
        seam = float(np.max(np.abs(x[0] - x[-1])))
        typical_step = float(np.percentile(np.abs(np.diff(mono)), 99.9)) + 1e-4

        if peak >= 0.999:
            problems.append(f"{name}: clipped (peak {peak:.3f})")
        if peak < 0.05:
            problems.append(f"{name}: nearly silent (peak {peak:.3f})")
        if dc > 0.01:
            problems.append(f"{name}: DC offset {dc:.4f}")
        if name in LOOPS:
            if seam > max(0.02, typical_step * 3):
                problems.append(f"{name}: loop seam jump {seam:.4f} vs typical step {typical_step:.4f}")
        else:
            if start_jump > 0.02 or end_jump > 0.02:
                problems.append(f"{name}: click at an end ({start_jump:.3f} / {end_jump:.3f})")
        rows.append(f"{name:22s} {seconds:7.2f}s {rate:6d}Hz peak {peak:.2f} rms {rms:.3f}")
        tiles.append((name, spectrogram(x, rate)))

    print("\n".join(rows))
    print(f"\n{len(rows)} clips, {total / 1048576:.1f} MB")

    columns = 8
    tile_h, tile_w = tiles[0][1].shape
    lines = (len(tiles) + columns - 1) // columns
    sheet = np.zeros((lines * (tile_h + 3), columns * (tile_w + 3)))
    for index, (_, image) in enumerate(tiles):
        y, x = index // columns * (tile_h + 3), index % columns * (tile_w + 3)
        sheet[y:y + tile_h, x:x + tile_w] = image
    rgb = np.stack([sheet ** 0.7, sheet ** 1.6, 0.25 + 0.5 * (1 - sheet) * (sheet > 0)], 2)
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    with open(PREVIEW, "wb") as f:
        f.write(f"P6 {sheet.shape[1]} {sheet.shape[0]} 255\n".encode())
        f.write((np.clip(rgb, 0, 1) * 255).astype(np.uint8).tobytes())

    if problems:
        print("\nPROBLEMS:\n" + "\n".join(problems))
        sys.exit(1)
    print("\nAll audio checks passed.")


if __name__ == "__main__":
    main()
