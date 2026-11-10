import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_rubble_pile', seed=1105)
r = random.Random(1105)
cols = ['concrete', 'concrete_dark', 'concrete_light', 'brick', 'brick_dark', 'stone', 'plaster_dirty']
# heap core
m.blob((3.0, 2.2, 0.8), (0, 0, 0.3), 'concrete', rough=0.2, colors=['concrete', 'concrete_dark', 'concrete_light'], flat_bottom=0.0)
m.blob((1.8, 1.4, 0.6), (-0.2, 0.05, 0.6), 'concrete_light', rough=0.22, colors=['concrete_light', 'concrete'])
# chunks scattered, heavier toward the middle
for i in range(26):
    d = r.random() ** 0.8
    a = r.uniform(0, 6.283)
    x = 1.45 * d * math.cos(a); y = 1.15 * d * math.sin(a)
    zc = 0.2 + (1 - d) * 0.5
    s = r.uniform(0.3, 0.7) * (1.0 - 0.3 * d)
    m.blob((s * 1.2, s, s * 0.8), (x, y, max(zc, s * 0.45)), r.choice(cols), rough=0.22, rot=(0, 0, r.uniform(0, 90)), flat_bottom=None)
m.box((0.9, 0.7, 0.18), (0.9, -0.75, 0.2), 'concrete_light', rot=(0, 8, 20))   # slab
m.box((1.2, 0.18, 0.2), (-0.5, -0.45, 0.35), 'wood_dark', rot=(0, 15, -15))      # beam
for x, y in ((-0.5, 0.1), (0.2, -0.1), (0.7, 0.2)):
    m.cyl(0.025, 0.7, (x, y, 1.0), 'rust', rot=(r.uniform(-25, 25), r.uniform(-25, 25), 0), seg=5)
finish(m, bounds={'x': (-1.6, 1.6), 'y': (-1.4, 1.3), 'z': (0, 1.5)})
