import random
from ss_art import Model, finish
m = Model('piece_foundation_concrete', seed=43)
r = random.Random(4301)
H = 0.6
m.box((2.0, 2.0, H - 0.02), (0, 0, (H - 0.02) / 2), 'concrete')
m.box((2.0, 2.0, 0.02), (0, 0, H - 0.01), 'concrete_light', taper=(0.98, 0.98))
# top: joint lines (shallow dark strips, 2 mm proud), pour seam across the middle
for v in (-0.5, 0.5):
    m.box((1.96, 0.02, 0.003), (0, v, H + 0.0005), 'concrete_dark')
    m.box((0.02, 1.96, 0.003), (v, 0, H + 0.0005), 'concrete_dark')
# lighter trowelled patches and light stains
for x, y, w, d, a, c in [(-0.55, -0.45, 0.35, 0.2, 20, 'concrete_light'), (0.5, 0.55, 0.3, 0.18, -15, 'concrete_light'),
                         (0.55, -0.6, 0.28, 0.12, 35, 'concrete_dark'), (-0.6, 0.4, 0.2, 0.3, -25, 'concrete_dark'), (0.1, 0.2, 0.4, 0.1, 5, 'concrete_dark')]:
    m.box((w, d, 0.002), (x, y, H + 0.001), c, rot=(0, 0, a))
# anchor bolts: washers flush with the surface
for x in (-0.75, 0.75, 0.0):
    for y in (-0.75, 0.75):
        if x == 0.0: continue
        m.cyl(0.05, 0.006, (x, y, H + 0.001), 'steel_dark', seg=8)
        m.cyl(0.025, 0.008, (x, y, H + 0.002), 'rust', seg=6)
# sides: formwork seams, tie holes, stains
for s in (-1, 1):
    m.box((1.96, 0.008, 0.012), (0, s * 0.996, 0.3), 'concrete_dark')
    m.box((0.008, 1.96, 0.012), (s * 0.996, 0, 0.3), 'concrete_dark')
    for k in (-0.6, 0.0, 0.6):
        for z in (0.15, 0.45):
            m.cyl(0.03, 0.01, (k, s * 0.995, z), 'black', rot=(90, 0, 0), seg=6)
            m.cyl(0.03, 0.01, (s * 0.995, k, z), 'black', rot=(0, 90, 0), seg=6)
    m.box((0.18, 0.006, 0.4), (0.3 * s, s * 0.996, 0.38), 'concrete_dark')
    m.box((0.006, 0.14, 0.35), (s * 0.996, -0.3 * s, 0.4), 'concrete_dark')
    m.box((0.3, 0.006, 0.18), (-0.45 * s, s * 0.996, 0.12), 'concrete_dark')
    m.box((0.006, 0.3, 0.18), (s * 0.996, 0.5 * s, 0.12), 'concrete_dark')
finish(m, bounds={'x': (-1, 1), 'y': (-1, 1), 'z': (0, H + 0.005)})
