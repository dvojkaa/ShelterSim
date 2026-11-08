import random
from ss_art import Model, finish
m = Model('piece_floor', seed=22)
r = random.Random(2204)

# Tiling contract: the same steel frame on all four edges, flush to +-1.0, so tiles close into a
# continuous 0.14 m beam grid with 0.14 m square plates at every crossing.
E = 0.07        # frame half-width per tile
TOP = 0.19

m.box((1.98, 1.98, 0.13), (0, 0, 0.075), 'gunmetal')   # inside the frame's outer faces: no coplanar z-fight

# Frame
for s in (-1, 1):
    m.box((2.0, E, TOP), (0, s * (1 - E / 2), TOP / 2), 'steel_dark')
    m.box((E, 2.0 - 2 * E, TOP), (s * (1 - E / 2), 0, TOP / 2), 'steel_dark')

# Corner + mid-edge plates: a quarter/half of a full plate, completed by the neighbouring tiles.
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((E, E, 0.006), (sx * (1 - E / 2), sy * (1 - E / 2), TOP + 0.003), 'steel')
    m.box((E, 0.14, 0.006), (sx * (1 - E / 2), 0, TOP + 0.003), 'steel')
    m.box((0.14, E, 0.006), (0, sx * (1 - E / 2), TOP + 0.003), 'steel')
    for k in (-0.5, 0.5):
        m.cyl(0.014, 0.008, (sx * 0.962, k, TOP + 0.002), 'gunmetal', seg=6)
        m.cyl(0.014, 0.008, (k, sx * 0.962, TOP + 0.002), 'gunmetal', seg=6)

# Planks run along X, inside the frame, on a pair of bearers.
INNER = 1 - E
widths = [0.27, 0.22, 0.3, 0.25, 0.29, 0.24, 0.29]
scale = (2 * INNER) / sum(widths)
colors = ['wood', 'wood_grey', 'wood_dark', 'wood_light', 'wood', 'wood_grey', 'wood_dark']
y = -INNER
for i, w in enumerate(widths):
    w *= scale
    cy = y + w / 2
    top = 0.183 + r.uniform(0, 0.004)
    if i == 3:
        # Rotted plank cut short: a tread plate is bolted over the hole.
        x_cut = 0.08
        m.box((x_cut + INNER - 0.006, w - 0.012, top - 0.14), ((x_cut - INNER) / 2, cy, 0.14 + (top - 0.14) / 2), colors[i])
        m.box((INNER - x_cut - 0.006, w - 0.012, top - 0.15), ((INNER + x_cut) / 2, cy, 0.14 + (top - 0.15) / 2), 'wood_dark')
    else:
        m.box((2 * INNER - 0.006, w - 0.012, top - 0.14), (0, cy, 0.14 + (top - 0.14) / 2), colors[i])
    # Nail heads where each plank meets the bearers
    for x in (-0.7, 0.0, 0.7) if i != 3 else (-0.7,):
        m.box((0.016, 0.016, 0.004), (x, cy - w * 0.25, top + 0.001), 'gunmetal')
        m.box((0.016, 0.016, 0.004), (x, cy + w * 0.25, top + 0.001), 'gunmetal')
    y += w

# Worn grain streaks: overlays that sit 1 mm proud so they never z-fight the plank top.
for x, yy, l, c in ((-0.45, -0.73, 0.5, 'wood_dark'), (0.5, -0.46, 0.4, 'wood_dark'), (-0.3, -0.2, 0.45, 'wood_dark'),
                    (0.35, 0.62, 0.55, 'wood_dark'), (-0.55, 0.36, 0.4, 'wood_dark')):
    m.box((l, 0.012, 0.002), (x, yy, 0.1885), c)

# Riveted tread plate over the rotten section
PX, PY = 0.48, 0.015
m.box((0.8, 0.46, 0.006), (PX, PY, 0.192), 'rust')
for i in range(5):
    for j in range(3):
        if (i + j) % 2 == 0:
            m.box((0.05, 0.016, 0.004), (PX - 0.26 + i * 0.13, PY - 0.13 + j * 0.13, 0.196), 'rust_light', rot=(0, 0, 45))
        else:
            m.box((0.05, 0.016, 0.004), (PX - 0.26 + i * 0.13, PY - 0.13 + j * 0.13, 0.196), 'rust_light', rot=(0, 0, -45))
for sx in (-1, 1):
    for sy in (-1, 1):
        m.cyl(0.012, 0.005, (PX + sx * 0.36, PY + sy * 0.19, 0.1965), 'gunmetal', seg=6)

# Underside bearers keep the tile from looking like a hollow shell when seen from a raised edge.
for x in (-0.5, 0.5):
    m.box((0.08, 2.0 - 2 * E, 0.03), (x, 0, 0.015), 'rust_dark')

finish(m, bounds={'x': (-1.0, 1.0), 'y': (-1.0, 1.0), 'z': (0, 0.2)})
