import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('prop_vault_door_leaf', seed=75)
# Leaf only, origin at the floor centre like blast_door_leaf: spans x -0.9..0.9, z 0..2.2, hinge side at x=-0.9, front -Y.
# closed door leaf, fills |x|<0.9, z 0..2.2, front face y=-0.2
m.box((1.8, 0.24, 2.2), (0, 0.0, 1.1), 'steel_dark', bevel=0.03)
m.box((1.64, 0.04, 2.04), (0, -0.13, 1.1), 'steel', bevel=0.02)
# raised ring bands
for z in (0.3, 1.9): m.box((1.7, 0.03, 0.08), (0, -0.15, z), 'steel_dark')
for x in (-0.65, 0.65): m.box((0.08, 0.03, 1.5), (x, -0.15, 1.1), 'steel_dark')
# locking bolts around the rim (bolt heads) into frame
for i in range(5):
    z = 0.35 + i * 0.4
    m.box((0.16, 0.12, 0.12), (-0.9, -0.1, z), 'gunmetal') if False else None
    m.box((0.2, 0.14, 0.14), (0.86, -0.12, z), 'gunmetal')
# hinge barrels, left x=-0.9
for z in (0.35, 1.1, 1.85):
    m.cyl(0.1, 0.4, (-0.92, -0.14, z), 'gunmetal', seg=8)
    m.box((0.28, 0.06, 0.3), (-0.82, -0.14, z), 'gunmetal')
# central hub and wheel
cx, cz, y0 = 0.0, 1.1, -0.16
m.cyl(0.5, 0.04, (cx, y0, cz), 'steel_dark', rot=(90, 0, 0), seg=16)
m.cyl(0.42, 0.04, (cx, y0 - 0.03, cz), 'gunmetal', rot=(90, 0, 0), seg=16)
# wheel rim as ring of segments
N = 12
R = 0.36
for i in range(N):
    a = 2 * math.pi * i / N
    m.box((2 * R * math.tan(math.pi / N) + 0.02, 0.05, 0.07), (cx + R * math.cos(a), y0 - 0.055, cz + R * math.sin(a)), 'rust_light' if False else 'steel',
          rot=(0, -math.degrees(a) - 90 + 180, 0) if False else (0, -(math.degrees(a) + 90), 0))
# spokes
for i in range(3):
    m.box((0.76, 0.04, 0.07), (cx, y0 - 0.04, cz), 'steel', rot=(0, i * 60, 0))
m.cyl(0.11, 0.06, (cx, y0 - 0.04, cz), 'gunmetal', rot=(90, 0, 0), seg=8)
# grips on rim
for a in (0, 120, 240):
    r = math.radians(a)
    m.cyl(0.04, 0.08, (cx + R * math.cos(r), y0 - 0.04, cz + R * math.sin(r)), 'warning_red', rot=(90, 0, 0), seg=6)
# status plate
m.box((0.3, 0.03, 0.14), (0.0, -0.15, 1.95), 'black')
m.box((0.08, 0.035, 0.08), (-0.08, -0.155, 1.95), 'warning_red')
m.box((0.08, 0.035, 0.08), (0.08, -0.155, 1.95), 'hazard_yellow')
finish(m, bounds={'x': (-1.1, 1.0), 'y': (-0.3, 0.15), 'z': (0, 2.25)})
