import os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
# Intact concrete wall: clean 4x6 slab grid (0.5 m cells) like _m27 concrete, cast bolt holes, slight stains.
m = Model('piece_wall_concrete', seed=2670)
rng = random.Random(2670)
cw = ch = .5
m.box((2.0, .14, 3.0), (0, 0, 1.5), 'concrete_dark')
for i in range(4):
    for j in range(6):
        x, z = -1 + cw * (i + .5), ch * (j + .5)
        col = rng.choice(['concrete_light', 'concrete', 'concrete', 'concrete', 'concrete'])
        m.box((cw - .015, .16, ch - .015), (x, 0, z), col)
        for s in (-1, 1):
            for dx in ((-.17, .17) if j % 2 == 0 else ()):
                m.cyl(.018, .02, (x + dx, s * .082, z + .17), 'black', rot=(90, 0, 0), seg=5)
for x, z, h in ((-.6, 2.2, .5), (.3, 1.2, .35), (.75, 2.6, .3)):
    for s in (-1, 1):
        m.box((.07, .008, h), (x, s * .084, z), 'concrete_dark')
finish(m, bounds={'x': (-1, 1), 'y': (-.1, .1), 'z': (0, 3)})
