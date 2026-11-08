import math, random
from ss_art import Model, finish
m = Model('ruin_debris_a', seed=29)
r = random.Random(2904)

# Mostly bricks: a slumped heap of a fallen wall, with two planks thrown over it.
R, H = 0.52, 0.3
m.blob((2 * R, 2 * R, 2 * H), (0, 0, 0), 'dirt', rough=0.1, subdiv=2, colors=['dirt', 'dirt', 'stone_dark', 'concrete_dark'], flat_bottom=0.0)


def mound(x, y):
    d = min(1.0, math.hypot(x, y) / R)
    return H * math.sqrt(max(0.0, 1 - d * d)) * 0.93


def brick(x, y, stacked=0.0):
    z = mound(x, y) + 0.016 + stacked
    c = r.choice(('brick', 'brick', 'brick', 'brick_dark', 'rust_dark'))
    m.box((0.22, 0.105, 0.065), (x, y, z), c, rot=(r.uniform(-16, 16), r.uniform(-20, 20), r.uniform(0, 360)))


# Rings of bricks over the heap; never closer to the box edge than a brick half-diagonal.
for ring, n in ((0.12, 5), (0.28, 8), (0.42, 9)):
    start = r.uniform(0, 6.28)
    for i in range(n):
        a = start + i * 2 * math.pi / n + r.uniform(-0.12, 0.12)
        brick(ring * math.cos(a), ring * math.sin(a))

# Second layer on the crown
for x, y in ((-0.05, 0.08), (0.1, -0.04), (-0.12, -0.12)):
    brick(x, y, stacked=0.055)

# Cemented wall fragment: bricks still mortared together, reads as "this was a wall"
m.box((0.46, 0.14, 0.24), (0.14, 0.26, 0.22), 'brick_dark', rot=(14, -22, 24))
for dx, dz, c in ((-0.12, 0.06, 'brick'), (0.12, 0.06, 'brick'), (0, -0.05, 'brick'), (-0.2, -0.05, 'rust_dark')):
    m.box((0.2, 0.012, 0.08), (0.14 + dx * 0.92, 0.19 - dx * 0.1, 0.23 + dz), c, rot=(14, -22, 24))

# Two weathered planks, one with a snapped end, resting on top
m.box((0.92, 0.13, 0.03), (-0.08, -0.26, 0.2), 'wood_grey', rot=(14, 9, 18))
m.box((0.76, 0.11, 0.03), (-0.06, 0.03, 0.335), 'wood', rot=(4, -6, -42))
m.box((0.34, 0.08, 0.024), (0.18, -0.08, 0.38), 'wood_light', rot=(-4, 8, 66))

finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 0.5)})
