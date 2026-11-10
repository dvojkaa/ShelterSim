import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bld_roof_hvac', seed=902)
r = random.Random(902)
# base rails / feet
for sx in (-1, 1):
    m.box((0.12, 0.88, 0.12), (sx * 0.55, 0, 0.06), 'concrete_dark')
m.box((1.3, 0.88, 0.04), (0, 0, 0.14), 'steel_dark')
# main casing
m.box((1.3, 0.88, 0.78), (0, 0, 0.57), 'paint_white', bevel=0.02)
m.box((1.34, 0.9, 0.05), (0, 0, 0.98), 'steel', bevel=0.01)
m.box((1.32, 0.9, 0.04), (0, 0, 0.19), 'steel_dark')
# grime
m.box((1.0, 0.02, 0.12), (-0.1, -0.455, 0.9), 'plaster_dirty')
for x in (-0.5, 0.2, 0.6):
    m.box((0.1, 0.02, r.uniform(0.2, 0.4)), (x, -0.456, 0.75), 'rust_light')
m.box((0.02, 0.88, 0.2), (0.655, 0, 0.3), 'rust_dark')
# round fan grille on top with blades
m.cyl(0.36, 0.1, (-0.1, 0, 1.05), 'steel_dark', seg=14)
m.cyl(0.31, 0.12, (-0.1, 0, 1.05), 'black', seg=14)
for i in range(5):
    m.box((0.28, 0.07, 0.015), (-0.1 + 0.14 * math.cos(i * 1.2566), 0.14 * math.sin(i * 1.2566), 1.09), 'steel', rot=(0, 0, math.degrees(i * 1.2566)))
m.cyl(0.05, 0.05, (-0.1, 0, 1.1), 'rust', seg=8)
for k in (0.12, 0.24, 0.34):
    m.cyl(k + 0.01, 0.015, (-0.1, 0, 1.125), 'gunmetal', seg=14, r2=k) if False else None
m.box((0.7, 0.03, 0.03), (-0.1, 0, 1.13), 'gunmetal')
m.box((0.03, 0.7, 0.03), (-0.1, 0, 1.13), 'gunmetal')
m.box((0.55, 0.03, 0.03), (-0.1, 0, 1.13), 'gunmetal', rot=(0, 0, 45))
m.box((0.55, 0.03, 0.03), (-0.1, 0, 1.13), 'gunmetal', rot=(0, 0, -45))
# louvred side vents on front (-Y): dark recess + slats
m.box((0.6, 0.02, 0.5), (-0.2, -0.445, 0.55), 'black')
for i in range(7):
    m.box((0.58, 0.05, 0.03), (-0.2, -0.465, 0.34 + i * 0.07), 'steel', rot=(-25, 0, 0))
# louvres on +X end
m.box((0.02, 0.5, 0.4), (0.66, 0.0, 0.62), 'black')
for i in range(5):
    m.box((0.05, 0.48, 0.03), (0.68, 0.0, 0.46 + i * 0.07), 'steel', rot=(0, 25, 0))
# corroded panel with screws on +Y/back
m.box((0.5, 0.03, 0.4), (0.25, 0.455, 0.6), 'rust', rot=(0, 0, 3))
m.box((0.2, 0.035, 0.2), (0.35, 0.458, 0.55), 'rust_dark')
for sx in (-1, 1):
    for sz in (-1, 1):
        m.cyl(0.015, 0.02, (0.25 + sx * 0.22, 0.47, 0.6 + sz * 0.17), 'chrome', rot=(90, 0, 0), seg=6)
# duct stub on -X end
m.box((0.2, 0.5, 0.4), (-0.7, 0, 0.5), 'steel', bevel=0.01)
m.box((0.04, 0.56, 0.46), (-0.62, 0, 0.5), 'steel_dark')
m.box((0.16, 0.4, 0.02), (-0.7, 0, 0.71), 'rust_dark')
# cables
m.tube([(0.66, 0.3, 0.3), (0.74, 0.35, 0.2), (0.74, 0.43, 0.05), (0.5, 0.45, 0.03)], 0.02, 'black', seg=5)
m.tube([(0.66, 0.3, 0.38), (0.77, 0.3, 0.3), (0.78, 0.1, 0.1), (0.6, -0.3, 0.03)], 0.015, 'warning_red', seg=5)
m.tube([(0.4, 0.44, 0.98), (0.45, 0.47, 0.8), (0.5, 0.47, 0.3)], 0.015, 'black', seg=5)
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.5, 0.5), 'z': (0, 1.2)})
