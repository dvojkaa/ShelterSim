import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_wall_steel', seed=86)
m.box((2.0, 0.14, 3.0), (0, 0, 1.5), 'steel')
# plates in a grid with weld seams and rivets
cw, chh = 0.66, 1.0
for i in range(3):
    for j in range(3):
        x0 = -1.0 + i * 2.0 / 3 + 1.0 / 3
        z = 0.5 + j * 1.0
        col = ['steel', 'steel_dark', 'steel', 'steel_dark', 'steel', 'steel'][(i * 3 + j) % 6]
        m.box((0.62, 0.2, 0.94), (x0 - 1.0 + 0.0, 0, z), col if False else col, bevel=0.01) if False else None
for i in range(3):
    x = -0.667 + i * 0.667
    for j in range(3):
        z = 0.5 + j * 1.0
        col = ['steel', 'steel_dark', 'gunmetal'][(i + j * 2) % 3] if False else ['steel', 'steel_dark', 'steel'][(i + j) % 3]
        m.box((0.64, 0.2, 0.96), (x, 0, z), col, bevel=0.012)
        for dx in (-0.26, 0.26):
            for dz in (-0.4, 0.4):
                m.cyl(0.022, 0.03, (x + dx, -0.085, z + dz), 'steel_dark', rot=(90, 0, 0), seg=5)
                m.cyl(0.022, 0.03, (x + dx, 0.085, z + dz), 'steel_dark', rot=(-90, 0, 0), seg=5)
# welded vertical and horizontal seams
for x in (-0.333, 0.333):
    m.box((0.04, 0.2, 3.0), (x, 0, 1.5), 'rust_dark')
for z in (1.0, 2.0):
    m.box((2.0, 0.2, 0.04), (0, 0, z), 'rust_dark')
# a patched sheet and rust
m.box((0.5, 0.01, 0.4), (-0.35, -0.105, 1.55), 'rust_dark', rot=(0, 0, 4)) if False else None
m.box((0.4, 0.006, 0.3), (0.35, -0.097, 0.55), 'rust')
m.box((0.5, 0.006, 0.12), (-0.5, -0.097, 2.55), 'rust_dark')
# frame edge posts
m.box((0.08, 0.2, 3.0), (-0.96, 0, 1.5), 'gunmetal', bevel=0.01) if False else None
m.box((2.0, 0.2, 0.08), (0, 0, 0.04), 'gunmetal')
m.box((2.0, 0.2, 0.08), (0, 0, 2.96), 'gunmetal')
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.1, 0.1), 'z': (0, 3.0)})
