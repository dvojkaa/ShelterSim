"""Generate the world-space tiled surface textures for SheltreSim.

Run through Blender's bundled Python (numpy only, bpy just for PNG output):
    /mnt/Apps/blender-5.2.3-linux-x64/blender -b --factory-startup -P ArtSource/assets/gen_surfaces.py

Optional: append `-- SS_Brick SS_Ground` to rebuild only the named textures.

Every texture is 1024x1024 and tiles exactly. Periodicity comes from construction, not
from blending at the edges:
  * noise is FFT-filtered white noise, which is periodic by definition;
  * blurs, drips and emboss lighting are circular (FFT or np.roll);
  * drawn structures (joints, bricks, ribs) use whole divisions of the tile;
  * stamped structures (cracks, rivets, tie holes) wrap their indices modulo the tile.

Every random value comes from a literal seed. All colour work happens directly in sRGB
values, and the images are saved as Non-Color so Blender does not convert them again.
"""
import os
import sys

import numpy as np

N = 1024
HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Art", "Textures"))

_Y, _X = np.mgrid[0:N, 0:N].astype(np.float32)
_FY = np.fft.fftfreq(N)[:, None] * N             # cycles per tile
_FX = np.fft.rfftfreq(N)[None, :] * N


# ---------------------------------------------------------------- noise and filtering

def noise(seed, lo=2.0, hi=32.0, beta=1.0, stretch_y=1.0, stretch_x=1.0):
    """Periodic band-limited noise, zero mean, unit deviation.

    lo/hi are in cycles per tile. stretch_y > 1 elongates features vertically.
    """
    white = np.random.default_rng(seed).standard_normal((N, N))
    r = np.sqrt((_FX * stretch_x) ** 2 + (_FY * stretch_y) ** 2)
    r[0, 0] = 1.0
    gain = r ** -beta * (1.0 - np.exp(-(r / lo) ** 4)) * np.exp(-(r / hi) ** 2)
    gain[0, 0] = 0.0
    out = np.fft.irfft2(np.fft.rfft2(white) * gain, s=(N, N))
    return ((out - out.mean()) / out.std()).astype(np.float32)


def blur(img, sigma):
    """Circular gaussian blur, sigma in pixels."""
    f2 = (_FX / N) ** 2 + (_FY / N) ** 2
    gain = np.exp(-2.0 * np.pi ** 2 * sigma ** 2 * f2)
    return np.fft.irfft2(np.fft.rfft2(img) * gain, s=(N, N)).astype(np.float32)


def drip(mask, length):
    """Smear a mask downward (increasing row) with exponential falloff, circularly."""
    kernel = np.exp(-np.arange(N) / float(length))
    kernel /= kernel.max()
    spec = np.fft.rfft(kernel)[:, None]
    return np.fft.irfft(np.fft.rfft(mask, axis=0) * spec, n=N, axis=0).astype(np.float32)


def water_runs(seed, threshold, length):
    """Streaks that start hard at a seed spot and fade downward, 0..1."""
    seeds = smooth(threshold, threshold + 0.6, noise(seed, lo=16, hi=80, beta=0.4, stretch_y=3.0))
    return np.clip(drip(seeds, length) / (length * 0.16), 0.0, 1.0)


def remove_low_freq(img, cutoff):
    """Strip tile-scale blotches from an RGB image, keeping the mean.

    A blotch one tile wide is what makes repetition readable over a large area.
    """
    r = np.sqrt(_FX ** 2 + _FY ** 2)
    gain = 1.0 - np.exp(-(r / cutoff) ** 4)
    gain[0, 0] = 1.0
    out = np.empty_like(img)
    for c in range(3):
        out[..., c] = np.fft.irfft2(np.fft.rfft2(img[..., c]) * gain, s=(N, N))
    return out


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def terrace(x, steps, softness=0.35):
    """Soft posterise, which gives noise the flat painted look instead of static."""
    v = x * steps
    f = v - np.floor(v)
    return (np.floor(v) + smooth(0.5 - softness, 0.5 + softness, f)) / steps


def emboss(height, light_x=-0.6, light_y=-0.8):
    """Directional slope lighting from a height field, light from the upper left."""
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5
    return dx * light_x + dy * light_y


def rgb(r, g, b):
    return np.array([r, g, b], dtype=np.float32) / 255.0


def layer(img, colour, amount):
    """Mix a colour (or an RGB image) into img by a mask."""
    amount = np.clip(amount, 0.0, 1.0)[..., None]
    return img * (1.0 - amount) + colour * amount


def shade(img, amount):
    """Multiply brightness by 1 + amount."""
    return img * (1.0 + amount)[..., None]


def solid(colour):
    return np.ones((N, N, 3), dtype=np.float32) * colour


# ---------------------------------------------------------------- structures

def worley(seed, cells, px=None, py=None, jitter=0.9):
    """Periodic Worley noise on a jittered grid.

    Returns F1, F2 (pixels) and the wrapped id of the nearest cell.
    """
    px = _X if px is None else px
    py = _Y if py is None else py
    rng = np.random.default_rng(seed)
    pts = (1.0 - jitter) * 0.5 + jitter * rng.random((cells, cells, 2)).astype(np.float32)
    size = N / float(cells)
    cx = np.floor(px / size).astype(np.int32)
    cy = np.floor(py / size).astype(np.int32)
    f1 = np.full((N, N), 1e9, dtype=np.float32)
    f2 = np.full((N, N), 1e9, dtype=np.float32)
    ids = np.zeros((N, N), dtype=np.int32)
    for dj in (-1, 0, 1):
        for di in (-1, 0, 1):
            nx = cx + di
            ny = cy + dj
            wx = np.mod(nx, cells)
            wy = np.mod(ny, cells)
            ddx = (nx + pts[wy, wx, 0]) * size - px
            ddy = (ny + pts[wy, wx, 1]) * size - py
            d = np.sqrt(ddx * ddx + ddy * ddy)
            nearer = d < f1
            f2 = np.where(nearer, f1, np.minimum(f2, d))
            f1 = np.where(nearer, d, f1)
            ids = np.where(nearer, wy * cells + wx, ids)
    return f1, f2, ids


def stamp_disc(mask, x, y, radius, value=1.0):
    """Stamp a disc with wrapped indices."""
    r = int(np.ceil(radius))
    oy, ox = np.mgrid[-r:r + 1, -r:r + 1]
    keep = ox * ox + oy * oy <= radius * radius
    mask[(int(round(y)) + oy[keep]) % N, (int(round(x)) + ox[keep]) % N] = value


def cracks(seed, count, length=(180, 420), width=1.6, jag=0.65, branch=0.18, angle=None):
    """Chunky jagged cracks drawn as wrapped polylines. Returns a 0..1 mask."""
    rng = np.random.default_rng(seed)
    mask = np.zeros((N, N), dtype=np.float32)

    def walk(x, y, a, remaining, w):
        total = float(remaining)
        heading = a
        while remaining > 0:
            seg = rng.uniform(10.0, 34.0)
            heading += rng.normal(0.0, 0.14)
            a = heading + rng.normal(0.0, jag)
            for _ in range(int(seg / 1.2)):
                taper = 0.45 + 0.55 * min(1.0, remaining / total * 2.0)
                stamp_disc(mask, x, y, max(0.7, w * taper))
                x += np.cos(a) * 1.2
                y += np.sin(a) * 1.2
                remaining -= 1.2
            if rng.random() < branch and w > 0.9:
                side = rng.choice((-1.0, 1.0))
                walk(x, y, a + side * rng.uniform(0.5, 1.2),
                     remaining * rng.uniform(0.25, 0.6), w * 0.65)

    for _ in range(count):
        a = rng.uniform(0.0, 2.0 * np.pi) if angle is None else angle + rng.normal(0.0, 0.35)
        walk(rng.uniform(0, N), rng.uniform(0, N), a, rng.uniform(*length), width)
    return blur(mask, 0.7)


def pebbles(seed, cells, keep, r_min, r_max):
    """Sparse wrapped pebbles. Returns coverage mask, dome height and per-pebble tint."""
    rng = np.random.default_rng(seed)
    warp_x = noise(seed + 1, lo=14, hi=44, beta=0.8) * 1.6
    warp_y = noise(seed + 2, lo=14, hi=44, beta=0.8) * 1.6
    f1, _, ids = worley(seed + 3, cells, _X + warp_x, _Y + warp_y, jitter=0.7)
    alive = rng.random(cells * cells) < keep
    radius = rng.uniform(r_min, r_max, cells * cells).astype(np.float32)
    tint = rng.random(cells * cells).astype(np.float32)
    r = radius[ids]
    mask = smooth(r, r - 1.5, f1) * alive[ids]
    dome = np.clip(1.0 - (f1 / r) ** 2, 0.0, 1.0) * r * 0.8 * mask
    return mask, dome, tint[ids]


def brick_wall(seed, courses, per_course, mortar=2.4, chew=3.0):
    """Running-bond bricks. `courses` must be even so the half-brick offset wraps.

    Returns a dict with the brick coverage, per-brick randoms and a bevel height field.
    """
    rng = np.random.default_rng(seed)
    px = _X + noise(seed + 1, lo=3, hi=14, beta=1.0) * 2.0
    py = _Y + noise(seed + 2, lo=3, hi=14, beta=1.0) * 1.6
    bh = N / float(courses)
    bw = N / float(per_course)
    row = np.floor(py / bh).astype(np.int32)
    u = px + np.mod(row, 2) * bw * 0.5
    col = np.floor(u / bw).astype(np.int32)
    ex = (u / bw - col) * bw
    ey = (py / bh - row) * bh
    edge_x = np.minimum(ex, bw - ex)
    edge_y = np.minimum(ey, bh - ey)
    # Rounded box distance, so brick corners are soft and chunky rather than razor sharp.
    corner = 3.0
    ix = np.maximum(corner - edge_x, 0.0)
    iy = np.maximum(corner - edge_y, 0.0)
    edge = np.where((ix > 0) & (iy > 0), corner - np.sqrt(ix * ix + iy * iy),
                    np.minimum(edge_x, edge_y))

    damage = np.maximum(noise(seed + 3, lo=18, hi=60, beta=0.7) - 0.4, 0.0) * chew
    width = mortar + damage
    cover = smooth(width, width + 1.3, edge)

    rows_w = np.mod(row, courses)
    cols_w = np.mod(col, per_course)
    index = rows_w * per_course + cols_w
    count = courses * per_course
    shape = {
        "cover": cover,
        "pick": rng.random(count).astype(np.float32)[index],
        "value": rng.normal(0.0, 1.0, count).astype(np.float32)[index],
        "weather": rng.random(count).astype(np.float32)[index],
    }
    proud = rng.normal(0.0, 1.0, count).astype(np.float32)[index]
    shape["height"] = smooth(width, width + 5.0, edge) * (3.2 + proud * 0.7)
    return shape


def brick_colour(wall, seed, palette):
    """Paint each brick from a palette, with mottling so they are not flat rectangles."""
    pick = wall["pick"]
    idx = np.minimum((pick * len(palette)).astype(np.int32), len(palette) - 1)
    img = np.asarray(palette, dtype=np.float32)[idx]
    img = shade(img, wall["value"] * 0.07)
    img = shade(img, noise(seed, lo=20, hi=70, beta=0.6) * 0.055)
    return img


# ---------------------------------------------------------------- textures

def make_ground():
    # Large-scale variation stays low-contrast and well above one cycle per tile, so an
    # 8 m repeat has no single landmark to give it away from a distance.
    img = solid(rgb(114, 97, 71))
    img = shade(img, noise(100, lo=4, hi=9, beta=0.5) * 0.028)

    warm = noise(101, lo=5, hi=16, beta=0.8)
    img = layer(img, rgb(122, 96, 64), smooth(0.2, 1.4, warm) * 0.45)
    img = layer(img, rgb(96, 86, 68), smooth(0.3, 1.5, -warm) * 0.45)

    # Chunky clods: flat-shaded cells, the main stylised read up close.
    clod_warp_x = noise(102, lo=10, hi=50, beta=0.8) * 8.0
    clod_warp_y = noise(103, lo=10, hi=50, beta=0.8) * 8.0
    f1, f2, ids = worley(104, 58, _X + clod_warp_x, _Y + clod_warp_y)
    clod = np.random.default_rng(105).normal(0.0, 1.0, 58 * 58).astype(np.float32)[ids]
    img = shade(img, clod * 0.035)
    img = shade(img, -smooth(5.0, 0.0, f2 - f1) * 0.05)

    # Dry patches: paler dust with mud cracks. Many small ones, none of them dominant.
    dry = noise(106, lo=4, hi=11, beta=0.6)
    dry_mask = smooth(-0.25, 0.55, dry + noise(107, lo=30, hi=90, beta=0.5) * 0.22)
    img = layer(img, rgb(132, 115, 86), dry_mask * 0.34)

    warp_x = noise(108, lo=5, hi=22, beta=1.0) * 5.0
    warp_y = noise(109, lo=5, hi=22, beta=1.0) * 5.0
    f1, f2, plate_ids = worley(110, 17, _X + warp_x, _Y + warp_y)
    plate_gap = f2 - f1
    line = smooth(4.2, 1.8, plate_gap)
    halo = smooth(13.0, 2.0, plate_gap)
    plate_tint = np.random.default_rng(111).normal(0.0, 1.0, 17 * 17).astype(np.float32)[plate_ids]
    img = shade(img, plate_tint * 0.03 * dry_mask)
    crack_strength = smooth(0.05, 0.6, dry_mask)
    img = layer(img, rgb(62, 51, 38), line * crack_strength * 0.72)
    img = shade(img, -halo * 0.09 * crack_strength)
    # The lip of each plate catches light on its upper-left side.
    img = shade(img, emboss(smooth(0.0, 9.0, plate_gap) * 6.0) * 0.1 * crack_strength)

    # Damp darker hollows opposite the dry patches.
    img = shade(img, -smooth(0.5, 1.5, -dry) * 0.07)

    # Grain: terraced soft bumps rather than per-pixel static.
    grain = noise(112, lo=40, hi=130, beta=0.6)
    img = shade(img, grain * 0.022)
    img = shade(img, emboss(noise(113, lo=16, hi=70, beta=0.9) * 3.0) * 0.06)

    # Sparse pebbles with contact shadows.
    for seed, cells, keep, r0, r1 in ((120, 30, 0.13, 6.0, 11.0), (130, 72, 0.10, 2.4, 4.8)):
        mask, dome, tint = pebbles(seed, cells, keep, r0, r1)
        contact = np.roll(np.roll(blur(mask, 1.8), 2, 0), 2, 1)
        img = shade(img, -contact * 0.32)
        stone = np.stack([
            0.40 + tint * 0.20,
            0.37 + tint * 0.18,
            0.32 + tint * 0.16], axis=-1).astype(np.float32)
        stone = shade(stone, emboss(dome) * 0.26)
        img = layer(img, stone, mask)

    return remove_low_freq(img, 2.6)


def make_concrete():
    # Formwork panels, 2 m x 1 m, in a staggered bond so the wall never reads as a grid.
    rows, cols = 4, 2
    px = _X + noise(200, lo=3, hi=12, beta=1.0) * 1.3
    py = _Y + noise(201, lo=3, hi=12, beta=1.0) * 1.3
    ph = N / float(rows)
    pw = N / float(cols)
    row = np.floor(py / ph).astype(np.int32)
    u = px + np.mod(row, 2) * pw * 0.5
    col = np.floor(u / pw).astype(np.int32)
    ex = (u / pw - col) * pw
    ey = (py / ph - row) * ph
    edge = np.minimum(np.minimum(ex, pw - ex), np.minimum(ey, ph - ey))
    panel = np.mod(row, rows) * cols + np.mod(col, cols)

    img = solid(rgb(128, 125, 117))
    rng = np.random.default_rng(202)
    panel_tint = rng.normal(0.0, 1.0, rows * cols).astype(np.float32)
    img = shade(img, panel_tint[panel] * 0.045)
    warmth = rng.uniform(-1.0, 1.0, rows * cols).astype(np.float32)[panel]
    img = img + warmth[..., None] * np.array([0.006, 0.002, -0.006], dtype=np.float32)

    # Blotchy pour marks, painted rather than noisy.
    pour = terrace(smooth(-2.2, 2.2, noise(203, lo=4, hi=22, beta=1.1)), 6)
    img = shade(img, (pour - 0.5) * 0.15)
    # Faint horizontal lift lines inside each panel.
    lift = noise(204, lo=6, hi=40, beta=0.7, stretch_x=14.0)
    img = shade(img, lift * 0.020)

    # Water streaks: tall dark runs, the strongest shape on the wall.
    breakup = smooth(-0.9, 0.6, noise(206, lo=3, hi=9, beta=0.9))
    img = layer(img, rgb(70, 71, 68), water_runs(205, 1.8, 130) * breakup * 0.60)
    pale = noise(207, lo=6, hi=60, beta=0.6, stretch_y=12.0)
    img = layer(img, rgb(158, 155, 145), smooth(0.9, 2.2, pale) * 0.35)

    # Panel lines: dark groove with a bright lip below, plus a run of dirt from the seam.
    groove = smooth(2.8, 1.0, edge)
    seam_runs = drip(smooth(3.0, 1.0, ey) * smooth(0.0, 1.2, noise(208, lo=14, hi=70, beta=0.5,
                                                                  stretch_y=30.0)), 46) * 0.025
    img = shade(img, -np.clip(seam_runs, 0.0, 0.34))
    lip = smooth(7.0, 2.5, edge) - groove
    img = shade(img, emboss(smooth(0.0, 4.0, edge) * 3.0) * 0.20)
    img = shade(img, lip * 0.03)
    img = layer(img, rgb(44, 43, 41), groove * 0.86)

    # Tie holes: chunky dots, each with its own rust-brown drip.
    holes = np.zeros((N, N), dtype=np.float32)
    jit = np.random.default_rng(209)
    for r in range(rows):
        for c in range(cols):
            off = (r % 2) * pw * 0.5
            for hx in (0.25, 0.75):
                x = (c + hx) * pw - off + jit.uniform(-3, 3)
                y = (r + 0.5) * ph + jit.uniform(-3, 3)
                stamp_disc(holes, x, y, 6.5)
    holes = blur(holes, 0.8)
    runs = np.clip(drip(holes, 42) * 0.018, 0.0, 1.0)
    img = layer(img, rgb(84, 70, 58), runs * 0.55)
    img = shade(img, emboss(holes * -4.5) * 0.25)
    img = layer(img, rgb(38, 37, 35), holes * 0.92)

    # Air pockets.
    f1, _, ids = worley(210, 64, jitter=0.85)
    alive = np.random.default_rng(211).random(64 * 64) < 0.16
    radius = np.random.default_rng(212).uniform(1.3, 3.4, 64 * 64).astype(np.float32)
    bug = smooth(radius[ids], radius[ids] - 1.0, f1) * alive[ids]
    img = shade(img, -bug * 0.30)

    # A few cracks with a soft stain around them.
    c = cracks(213, 4, length=(130, 300), width=1.6, jag=0.6, branch=0.12)
    img = shade(img, -blur(c, 5.0) * 0.40)
    img = layer(img, rgb(38, 37, 35), c * 0.9)

    img = shade(img, noise(214, lo=50, hi=150, beta=0.5) * 0.016)
    img = shade(img, emboss(noise(215, lo=20, hi=90, beta=0.9) * 2.2) * 0.07)
    # Keep every panel line off the tile edge.
    return np.roll(img, (64, 64), (0, 1))


def make_concrete_floor():
    img = solid(rgb(98, 96, 91))

    # Four slabs per tile, joints on the quarter lines so none sits on the tile edge.
    half = N // 2
    wob_x = noise(300, lo=6, hi=30, beta=0.9) * 1.1
    wob_y = noise(301, lo=6, hi=30, beta=0.9) * 1.1
    dist_x = np.abs(np.mod(_X + wob_x - N * 0.25 + half * 0.5, half) - half * 0.5)
    dist_y = np.abs(np.mod(_Y + wob_y - N * 0.25 + half * 0.5, half) - half * 0.5)
    joint_dist = np.minimum(dist_x, dist_y)

    slab_x = np.mod(np.floor((_X + wob_x - N * 0.25) / half), 2).astype(np.int32)
    slab_y = np.mod(np.floor((_Y + wob_y - N * 0.25) / half), 2).astype(np.int32)
    tints = np.random.default_rng(302).normal(0.0, 1.0, 4).astype(np.float32)
    img = shade(img, tints[slab_y * 2 + slab_x] * 0.03)

    # Worn paler traffic areas against dark grime, in soft painted steps.
    wear = terrace(smooth(-2.0, 2.0, noise(303, lo=3, hi=14, beta=1.2)), 5)
    img = layer(img, rgb(120, 118, 112), wear * 0.40)
    grime = smooth(-0.6, 2.0, noise(304, lo=3, hi=30, beta=1.3))
    img = layer(img, rgb(58, 57, 54), grime * 0.50)

    # Long scuffs dragged along one direction.
    scuff = noise(305, lo=8, hi=90, beta=0.6, stretch_x=18.0)
    patchy = smooth(-0.3, 1.0, noise(306, lo=3, hi=9, beta=0.9))
    img = layer(img, rgb(125, 123, 116), smooth(0.9, 2.2, scuff) * patchy * 0.34)
    img = layer(img, rgb(56, 55, 52), smooth(1.0, 2.4, -scuff) * patchy * 0.34)

    # Oil stains: a few dark soft-edged pools.
    oil = noise(307, lo=3, hi=9, beta=0.8) + noise(308, lo=12, hi=50, beta=0.8) * 0.30
    img = layer(img, rgb(46, 45, 44), smooth(1.7, 2.3, oil) * 0.38)

    # Exposed aggregate on the worn parts.
    px = _X + noise(309, lo=20, hi=70, beta=0.7) * 2.0
    py = _Y + noise(310, lo=20, hi=70, beta=0.7) * 2.0
    f1, _, ids = worley(311, 104, px, py, jitter=0.85)
    rng = np.random.default_rng(312)
    alive = rng.random(104 * 104) < 0.30
    radius = rng.uniform(1.6, 3.8, 104 * 104).astype(np.float32)
    value = rng.uniform(-1.0, 1.0, 104 * 104).astype(np.float32)
    speck = smooth(radius[ids], radius[ids] - 1.2, f1) * alive[ids]
    img = shade(img, speck * value[ids] * (0.08 + wear * 0.16))

    # Hairline cracks that start from the joints' direction.
    c = cracks(313, 5, length=(150, 360), width=1.6, jag=0.65, branch=0.2)
    img = shade(img, -blur(c, 4.0) * 0.35)
    img = layer(img, rgb(30, 30, 29), c * 0.85)

    # Expansion joints with chipped edges, which are the dominant lines of the texture.
    chip = np.maximum(noise(314, lo=20, hi=70, beta=0.5) - 0.9, 0.0) * 3.2
    w = 3.0 + chip
    joint = smooth(w + 1.2, w, joint_dist)
    img = shade(img, -smooth(22.0, 2.0, joint_dist) * 0.13)
    img = shade(img, emboss(smooth(w, w + 3.5, joint_dist) * 3.2) * 0.22)
    img = layer(img, rgb(24, 24, 23), joint * 0.95)

    img = shade(img, noise(315, lo=50, hi=150, beta=0.5) * 0.018)
    img = shade(img, emboss(noise(316, lo=20, hi=90, beta=0.9) * 2.0) * 0.07)
    return img


BRICK_PALETTE = (
    rgb(136, 86, 66), rgb(122, 76, 60), rgb(148, 98, 72),
    rgb(112, 74, 62), rgb(128, 90, 74), rgb(96, 66, 58),
)


def make_brick():
    wall = brick_wall(400, 24, 12, mortar=2.2, chew=3.2)
    bricks = brick_colour(wall, 401, BRICK_PALETTE)
    # A handful of soot-burnt bricks for chunky value contrast.
    burnt = (wall["weather"] > 0.92).astype(np.float32)
    bricks = layer(bricks, rgb(72, 54, 50), burnt * 0.75)
    pale = (wall["weather"] < 0.07).astype(np.float32)
    bricks = layer(bricks, rgb(160, 122, 96), pale * 0.55)

    mortar = solid(rgb(116, 108, 94))
    mortar = shade(mortar, noise(402, lo=14, hi=70, beta=0.6) * 0.09)
    img = bricks * wall["cover"][..., None] + mortar * (1.0 - wall["cover"][..., None])
    img = shade(img, emboss(wall["height"]) * 0.27)
    img = shade(img, -(1.0 - wall["cover"]) * 0.22)

    # Weather: grime in big soft areas, streaks running down the face.
    dirt = smooth(-0.2, 1.4, noise(403, lo=3, hi=16, beta=1.1))
    img = layer(img, rgb(60, 50, 44), dirt * 0.32)
    streak = noise(404, lo=7, hi=60, beta=0.6, stretch_y=14.0)
    img = layer(img, rgb(50, 44, 40), smooth(0.8, 2.2, streak) * 0.42)
    salts = noise(405, lo=5, hi=36, beta=0.9)
    img = layer(img, rgb(158, 142, 122), smooth(1.2, 2.2, salts) * 0.26)

    # Remains of the old plaster coat, clinging on in a few rough-edged islands.
    field = (noise(406, lo=3, hi=9, beta=1.0)
             + noise(407, lo=14, hi=50, beta=0.7) * 0.28
             + noise(408, lo=50, hi=120, beta=0.4) * 0.10)
    coat = smooth(1.86, 1.94, field)
    rim = smooth(1.70, 1.86, field) * (1.0 - coat)
    img = shade(img, -rim * 0.30)
    plaster = solid(rgb(138, 126, 106))
    plaster = shade(plaster, noise(409, lo=7, hi=50, beta=0.9) * 0.08)
    plaster = shade(plaster, -drip(smooth(0.0, 2.0, streak), 60) * 0.0006)
    plaster = shade(plaster, emboss(noise(410, lo=20, hi=90, beta=0.8) * 2.5) * 0.07)
    img = layer(img, plaster, coat)
    img = shade(img, emboss(blur(coat, 1.2) * 3.0) * 0.28)

    c = cracks(411, 3, length=(150, 320), width=1.5, jag=0.6, branch=0.2)
    img = layer(img, rgb(30, 25, 23), c * 0.80)

    img = shade(img, noise(412, lo=50, hi=150, beta=0.5) * 0.02)
    return img


def make_plaster():
    img = solid(rgb(170, 158, 134))

    # Big dirty washes, painted in soft steps.
    wash = terrace(smooth(-2.2, 2.2, noise(500, lo=3, hi=14, beta=1.2)), 5)
    img = layer(img, rgb(122, 110, 88), (1.0 - wash) * 0.48)
    warm = smooth(0.1, 1.6, noise(501, lo=4, hi=18, beta=1.0))
    img = layer(img, rgb(150, 124, 88), warm * 0.28)

    # Rough hand-applied coat: broad low relief, no direction.
    img = shade(img, emboss(noise(502, lo=10, hi=44, beta=1.0) * 3.0) * 0.10)
    img = shade(img, noise(503, lo=50, hi=150, beta=0.5) * 0.018)

    # Run-down dirt.
    breakup = smooth(-0.9, 0.6, noise(505, lo=3, hi=9, beta=0.9))
    img = layer(img, rgb(88, 78, 62), water_runs(504, 2.2, 140) * breakup * 0.50)

    # Cracks with stained halos.
    c = cracks(506, 7, length=(200, 470), width=1.9, jag=0.55, branch=0.12)
    img = shade(img, -blur(c, 5.0) * 0.42)
    img = layer(img, rgb(46, 40, 34), c * 0.92)
    fine = cracks(507, 10, length=(60, 160), width=1.05, jag=0.7, branch=0.05)
    img = layer(img, rgb(70, 62, 52), fine * 0.60)

    # Exposed brick. Same brick size as SS_Brick in world space: 8 courses per metre.
    wall = brick_wall(508, 32, 16, mortar=2.0, chew=2.0)
    bricks = brick_colour(wall, 509, BRICK_PALETTE)
    mortar = solid(rgb(104, 96, 84))
    under = bricks * wall["cover"][..., None] + mortar * (1.0 - wall["cover"][..., None])
    under = shade(under, emboss(wall["height"]) * 0.25)
    under = shade(under, -(1.0 - wall["cover"]) * 0.20)
    under = shade(under, -smooth(-0.2, 1.4, noise(510, lo=4, hi=20, beta=1.0)) * 0.20)

    field = (noise(511, lo=3, hi=8, beta=1.0)
             + noise(512, lo=12, hi=40, beta=0.8) * 0.30
             + noise(513, lo=40, hi=120, beta=0.5) * 0.11)
    hole = smooth(1.34, 1.41, field)
    plaster_edge = smooth(1.18, 1.34, field) * (1.0 - hole)
    img = shade(img, -plaster_edge * 0.22)
    # Shadow thrown by the thick plaster onto the bricks below its broken edge.
    drop = np.roll(np.roll(1.0 - hole, 4, 0), 3, 1)
    under = shade(under, -drop * 0.46)
    img = layer(img, under, hole)
    # Bright broken lip of plaster on the lower side of each hole.
    lip = smooth(0.0, 1.0, np.roll(np.roll(hole, -3, 0), -2, 1) - hole)
    img = layer(img, rgb(198, 188, 164), lip * 0.55)
    return img


def make_corrugated():
    # 32 ribs per 4 m is a chunky 12.5 cm pitch. Four sheets across, two tall, staggered
    # by half a sheet so seams do not form a cross.
    ribs = 32
    bend = noise(600, lo=2, hi=6, beta=1.0, stretch_y=2.0) * 0.9
    dent = noise(601, lo=6, hi=16, beta=1.0) * 0.45
    phase = 2.0 * np.pi * ribs * (_X + bend + dent) / N
    crest = np.cos(phase)

    # Faded paint under the rust, so the sheet is not one flat orange.
    img = solid(rgb(106, 96, 86))

    sheet_h = N // 2
    row = np.floor(_Y / sheet_h).astype(np.int32) % 2
    sheet_w = N // 4
    u = _X + row * sheet_w * 0.5
    col = np.floor(u / sheet_w).astype(np.int32) % 4
    sheet = row * 4 + col
    rng = np.random.default_rng(602)
    tint = rng.normal(0.0, 1.0, 8).astype(np.float32)[sheet]
    rust_bias = rng.uniform(-0.45, 0.45, 8).astype(np.float32)[sheet]
    img = shade(img, tint * 0.05)

    # Rust eats the sheets in big chunky blotches and runs down the troughs.
    tall = noise(603, lo=4, hi=36, beta=0.8, stretch_y=7.0)
    blotch = noise(604, lo=4, hi=30, beta=1.0) + noise(605, lo=30, hi=110, beta=0.5) * 0.26
    rust_field = blotch * 0.7 + tall * 0.5 + rust_bias
    img = layer(img, rgb(124, 82, 54), smooth(-1.55, -0.95, rust_field) * 0.95)
    img = layer(img, rgb(138, 84, 48), smooth(0.2, 0.9, rust_field) * 0.7)
    img = layer(img, rgb(84, 50, 36), smooth(1.25, 1.8, rust_field) * 0.8)
    # Dirt collects in the troughs, the crests are scrubbed clean.
    img = shade(img, -smooth(0.0, 1.0, -crest) * 0.10)

    # Vertical drips from the horizontal seam and from rivets are added below.
    # Baked corrugation lighting: bright flank on one side, deep shadow on the other.
    flank = -np.sin(phase)
    img = shade(img, flank * 0.33)
    spec = np.maximum(np.cos(phase - 0.8), 0.0) ** 7
    img = img + (spec * 0.075)[..., None]
    img = shade(img, -np.maximum(np.cos(phase + 2.5), 0.0) ** 5 * 0.24)

    # Sheet seams.
    edge_x = np.minimum(np.mod(u, sheet_w), sheet_w - np.mod(u, sheet_w))
    edge_x_dir = np.mod(u, sheet_w)
    overlap = smooth(12.0, 0.0, edge_x_dir) * 0.42
    img = shade(img, -overlap)
    img = shade(img, smooth(4.0, 0.0, sheet_w - edge_x_dir) * 0.30)
    img = layer(img, rgb(30, 23, 20), smooth(2.6, 1.0, edge_x) * 0.92)

    ey = np.mod(_Y, sheet_h)
    edge_y = np.minimum(ey, sheet_h - ey)
    img = shade(img, -smooth(15.0, 0.0, ey) * 0.46)
    img = shade(img, smooth(5.0, 0.0, sheet_h - ey) * 0.26)
    img = layer(img, rgb(26, 20, 18), smooth(2.6, 1.0, edge_y) * 0.92)

    # Rivets on the crests just below each horizontal seam, each weeping rust.
    rivets = np.zeros((N, N), dtype=np.float32)
    jit = np.random.default_rng(606)
    pitch = N / float(ribs)
    for r in range(2):
        for k in range(ribs):
            if k % 2:
                continue
            # cos(phase) = 1 at x = k * pitch, which is the crest of rib k.
            stamp_disc(rivets, k * pitch + jit.uniform(-1, 1),
                       r * sheet_h + 15 + jit.uniform(-1, 1), 5.0)
    rivets = blur(rivets, 0.8)
    weep = np.clip(drip(rivets, 70) * 0.017, 0.0, 1.0)
    weep = weep * smooth(-0.4, 0.5, noise(607, lo=3, hi=9, beta=1.0))
    img = layer(img, rgb(72, 40, 28), weep * 0.72)
    img = shade(img, emboss(blur(rivets, 1.2) * 7.0) * 0.40)
    img = layer(img, rgb(92, 74, 62), rivets * 0.55)

    img = shade(img, noise(608, lo=50, hi=150, beta=0.5) * 0.032)
    # Keep every sheet seam off the tile edge.
    return np.roll(img, (100, 64), (0, 1))


def make_roof_tar():
    # Tar base: smooth, with a soft bitumen sheen in the bald spots.
    tar = solid(rgb(40, 38, 37))
    sheen = smooth(0.2, 1.6, noise(700, lo=5, hi=26, beta=1.1))
    tar = layer(tar, rgb(60, 58, 58), sheen * 0.55)

    # Blisters in the tar.
    blister = noise(701, lo=10, hi=36, beta=1.1)
    tar = shade(tar, emboss(blister * 6.5) * 0.45)

    # Gravel: tightly packed chunky stones, each with its own colour and dome.
    px = _X + noise(702, lo=20, hi=70, beta=0.6) * 2.2
    py = _Y + noise(703, lo=20, hi=70, beta=0.6) * 2.2
    cells = 74
    f1, f2, ids = worley(704, cells, px, py, jitter=0.82)
    rng = np.random.default_rng(705)
    count = cells * cells
    tone = rng.random(count).astype(np.float32)[ids]
    hue = rng.random(count).astype(np.float32)[ids]
    gap = f2 - f1
    # Separate rounded stones sitting in the tar, not a crazed mosaic.
    stone_r = rng.uniform(6.2, 9.4, count).astype(np.float32)[ids]
    gravel_cover = smooth(0.3, 1.8, gap) * smooth(stone_r, stone_r - 1.6, f1)
    dome = np.clip(1.0 - (f1 / stone_r) ** 2, 0.0, 1.0) * stone_r * 0.7
    gravel = np.stack([
        0.27 + tone * 0.18 + hue * 0.035,
        0.26 + tone * 0.17 + hue * 0.012,
        0.25 + tone * 0.16 - hue * 0.018], axis=-1).astype(np.float32)
    # A few rust-brown and a few pale stones for readable colour variety.
    odd = rng.random(count).astype(np.float32)[ids]
    gravel = layer(gravel, rgb(104, 76, 58), (odd > 0.93).astype(np.float32) * 0.75)
    gravel = layer(gravel, rgb(142, 137, 126), (odd < 0.05).astype(np.float32) * 0.70)
    gravel = shade(gravel, emboss(dome) * 0.30)
    gravel = shade(gravel, smooth(-1.5, 1.5, noise(706, lo=5, hi=24, beta=1.1)) * 0.22 - 0.11)

    # Gravel blankets most of the roof, with bald patches and wind-swept drifts.
    field = (noise(707, lo=4, hi=13, beta=1.0)
             + noise(708, lo=14, hi=44, beta=0.7) * 0.30)
    bald = smooth(1.42, 1.62, field)
    # Stones thin out toward a bald patch: cell-wise, so the edge is made of stones.
    cell_rand = rng.random(count).astype(np.float32)[ids]
    thinning = smooth(0.75, 1.62, field)
    keep = (cell_rand > thinning).astype(np.float32)
    cover = gravel_cover * keep * (1.0 - bald)
    contact = np.roll(np.roll(blur(cover, 1.6), 2, 0), 1, 1)
    tar = shade(tar, -contact * 0.22)
    img = tar * (1.0 - cover[..., None]) + gravel * cover[..., None]

    # Membrane laps, two per tile: a tar ridge running across.
    lap_wob = noise(709, lo=5, hi=24, beta=1.0) * 1.6
    lap_y = np.mod(_Y + lap_wob, N // 2)
    ridge = smooth(9.0, 2.0, np.minimum(lap_y, N // 2 - lap_y))
    ridge_h = ridge * 5.0
    lap_tar = solid(rgb(46, 44, 43))
    lap_tar = shade(lap_tar, emboss(ridge_h + blister * 1.5) * 0.45)
    img = layer(img, lap_tar, smooth(0.25, 0.65, ridge) * 0.92)
    img = shade(img, -smooth(16.0, 8.0, np.minimum(lap_y, N // 2 - lap_y)) * 0.12)
    img = layer(img, rgb(20, 19, 19), smooth(1.6, 0.6, np.minimum(lap_y, N // 2 - lap_y)) * 0.7)

    # Cracks through the tar show only where no gravel hides them.
    c = cracks(710, 8, length=(170, 380), width=1.6, jag=0.65, branch=0.2)
    img = layer(img, rgb(14, 13, 13), c * (1.0 - cover * 0.9) * 0.92)
    # Keep the laps off the tile edge.
    return np.roll(img, 128, 0)


TEXTURES = (
    ("SS_Ground", make_ground, (110, 94, 68)),
    ("SS_Concrete", make_concrete, (118, 115, 108)),
    ("SS_ConcreteFloor", make_concrete_floor, (92, 90, 86)),
    ("SS_Brick", make_brick, (118, 84, 68)),
    ("SS_Plaster", make_plaster, (146, 134, 112)),
    ("SS_CorrugatedRust", make_corrugated, (110, 76, 54)),
    ("SS_RoofTar", make_roof_tar, (66, 63, 60)),
)


# ---------------------------------------------------------------- output

def match_mean(img, target):
    """Offset each channel until the clipped mean lands on the target sRGB value."""
    goal = np.array(target, dtype=np.float32) / 255.0
    before = img.mean(axis=(0, 1)) * 255.0
    for _ in range(8):
        img = img + (goal - np.clip(img, 0.0, 1.0).mean(axis=(0, 1)))
    return np.clip(img, 0.0, 1.0), before


def seam_ratio(img):
    """Edge-wrap step relative to the typical neighbouring-pixel step. About 1 is seamless."""
    grey = img.mean(axis=2)
    inner_y = np.abs(grey[1:, :] - grey[:-1, :]).mean()
    inner_x = np.abs(grey[:, 1:] - grey[:, :-1]).mean()
    wrap_y = np.abs(grey[0, :] - grey[-1, :]).mean()
    wrap_x = np.abs(grey[:, 0] - grey[:, -1]).mean()
    return wrap_y / inner_y, wrap_x / inner_x


def save_png(name, img):
    import bpy

    path = os.path.join(OUT_DIR, name + ".png")
    image = bpy.data.images.new(name, width=N, height=N, alpha=False, float_buffer=False)
    image.colorspace_settings.name = "Non-Color"
    rgba = np.ones((N, N, 4), dtype=np.float32)
    # Blender stores rows bottom-up. Flipping keeps row 0 of the array as the image's top,
    # so "downward" drips and streaks point down in the saved PNG.
    rgba[..., :3] = img[::-1]
    image.pixels.foreach_set(rgba.ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)
    return path


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, build, target in TEXTURES:
        if argv and name not in argv:
            continue
        img, before = match_mean(build(), target)
        sy, sx = seam_ratio(img)
        path = save_png(name, img)
        print("%-18s raw mean (%3.0f,%3.0f,%3.0f) target %s  seam y %.2f x %.2f  -> %s" % (
            name, before[0], before[1], before[2], target, sy, sx, path))


main()
