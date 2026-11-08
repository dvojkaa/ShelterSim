import math, random
from ss_art import Model, finish
m = Model('ruin_debris_b', seed=30)
r = random.Random(3005)

# Mostly planks: a collapsed floor or roof. Low heap, a shallow criss-cross of boards and a broken beam.
R, H = 0.5, 0.2
m.blob((2 * R, 2 * R, 2 * H), (0.02, 0.0, 0), 'dirt', rough=0.12, subdiv=2, colors=['dirt', 'concrete_dark', 'stone_dark'], flat_bottom=0.0)


def mound(x, y):
    d = min(1.0, math.hypot(x, y) / R)
    return H * math.sqrt(max(0.0, 1 - d * d)) * 0.93


# Heavy broken beam across the pile
m.box((1.02, 0.14, 0.11), (0.02, 0.2, 0.12), 'wood_dark', rot=(0, 3, -9))
m.poly([(0.42, -0.055), (0.62, -0.05), (0.54, -0.01), (0.59, 0.02), (0.52, 0.055), (0.42, 0.055)], 0.14, (-0.05, 0.14, 0.135), 'wood_light', rot=(0, 3, -9))

# Boards, each laid over the previous one (Z strictly stacks up so nothing floats or passes through)
for cx, cy, cz, l, w, rz, ry, c in (
    (-0.12, -0.14, 0.14, 0.94, 0.14, 12, 2, 'wood'),
    (0.08, -0.05, 0.195, 0.86, 0.12, -31, -3, 'wood_grey'),
    (-0.1, 0.04, 0.245, 0.9, 0.13, 58, 7, 'wood_light'),
    (0.14, -0.1, 0.28, 0.78, 0.11, 104, -5, 'wood_dark'),
    (-0.04, 0.0, 0.32, 0.66, 0.1, -14, 4, 'wood_grey'),
):
    m.box((l, w, 0.032), (cx, cy, cz), c, rot=(0, ry, rz))

# A nailed batten still on the top plank, and a rusty sheet underneath it
m.box((0.24, 0.05, 0.02), (-0.04, 0.0, 0.345), 'wood', rot=(0, 4, 72))
m.box((0.46, 0.38, 0.012), (0.24, -0.26, 0.075), 'rust', rot=(9, -6, 32))
m.box((0.12, 0.12, 0.004), (0.26, -0.26, 0.088), 'rust_dark', rot=(9, -6, 32))
for dx, dy in ((-0.2, 0.03), (0.2, -0.04)):
    m.cyl(0.008, 0.01, (-0.04 + dx, dy, 0.343), 'gunmetal', seg=5)

# Bricks fallen around the outside of the heap, so it stays part of the same ruin family
for x, y in ((-0.4, 0.23), (-0.36, -0.33), (0.38, 0.33), (-0.46, -0.04), (0.08, 0.42), (0.4, 0.08), (-0.2, -0.45), (0.24, -0.46)):
    z = max(mound(x, y) + 0.035, 0.075)
    m.box((0.22, 0.105, 0.065), (x, y, z), r.choice(('brick', 'brick', 'brick_dark')),
          rot=(r.uniform(-10, 10), r.uniform(-14, 14), r.uniform(0, 360)))

# A chunk of plaster
m.blob((0.26, 0.22, 0.12), (-0.28, 0.32, 0.08), 'plaster_dirty', rough=0.2, subdiv=1, colors=['plaster', 'plaster_dirty', 'concrete'])

finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 0.5)})
