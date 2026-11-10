import os, sys, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
# Intact reinforced wall: gunmetal frame, six clean riveted plates (2 cols x 3 rows) like _m27 reinforced.
m = Model('piece_wall_reinforced', seed=2680)
rng = random.Random(2680)
m.box((2.0, .12, .12), (0, 0, .06), 'gunmetal')
m.box((2.0, .12, .10), (0, 0, 2.95), 'gunmetal')
for x in (-.96, .96):
    m.box((.08, .14, 3.0), (x, 0, 1.5), 'gunmetal')
m.box((1.9, .08, 2.9), (0, 0, 1.5), 'steel_dark')
for c in range(2):
    for r in range(3):
        x, z = -.46 + c * .92, .5 + r * 1.0
        col = ['steel', 'steel', 'steel_dark'][(c + r) % 3]
        for s in (-1, 1):
            m.box((.86, .03, .92), (x, s * .08, z), col)
            for dx in (-.36, .36):
                for dz in (-.4, .4):
                    m.cyl(.022, .02, (x + dx, s * .088, z + dz), 'steel', rot=(90, 0, 0), seg=5)
for x, z, h in ((-.7, 1.4, .5), (.5, 2.3, .4)):
    for s in (-1, 1):
        m.box((.06, .008, h), (x, s * .092, z), 'rust')
finish(m, bounds={'x': (-1, 1), 'y': (-.1, .1), 'z': (0, 3)})
