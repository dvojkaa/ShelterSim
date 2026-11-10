import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bld_roof_water_tank', seed=901)
r = random.Random(901)
# stand: four legs, top frame, cross braces
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.1, 0.1, 1.2), (sx * 0.7, sy * 0.7, 0.6), 'steel_dark', bevel=0.01)
        m.box((0.24, 0.24, 0.04), (sx * 0.7, sy * 0.7, 0.02), 'concrete_dark')
for sy in (-1, 1):
    beam(m, (-0.7, 0.15), (0.7, 1.1), 0.05, 0.06, sy * 0.7, 'rust_dark')
    beam(m, (-0.7, 1.1), (0.7, 0.15), 0.05, 0.06, sy * 0.7, 'rust')
for sx in (-1, 1):
    beam_yz(m, (-0.7, 0.15), (0.7, 1.1), 0.05, 0.06, sx * 0.7, 'rust')
    beam_yz(m, (-0.7, 1.1), (0.7, 0.15), 0.05, 0.06, sx * 0.7, 'rust_dark')
m.box((1.7, 1.7, 0.08), (0, 0, 1.16), 'steel_dark')
m.box((1.5, 1.5, 0.04), (0, 0, 1.2), 'rust_dark')
# tank body, bands, lid
m.cyl(0.85, 1.6, (0, 0, 2.0), 'rust', seg=14)
for z in (1.35, 2.0, 2.65):
    m.cyl(0.885, 0.07, (0, 0, z), 'steel_dark', seg=14)
m.cyl(0.88, 0.2, (0, 0, 2.9), 'steel', seg=14, r2=0.12)
m.cyl(0.14, 0.05, (0, 0, 2.965), 'rust_dark', seg=8)
# rust / grime streaks
for i in range(7):
    a = r.uniform(0, 6.28)
    m.box((0.12, 0.03, r.uniform(0.3, 0.8)), (0.84 * math.cos(a), 0.84 * math.sin(a), r.uniform(1.6, 2.5)), r.choice(['rust_dark', 'rust_light', 'black']), rot=(0, 0, math.degrees(a) + 90))
# dented panel on +Y: bent plate with dark dent
m.box((0.5, 0.05, 0.5), (0.0, 0.86, 1.9), 'steel', rot=(0, 0, 0))
m.box((0.3, 0.06, 0.28), (0.02, 0.84, 1.88), 'rust_dark', rot=(8, 0, 10))
m.box((0.5, 0.04, 0.08), (0.0, 0.8, 1.72), 'steel_dark', rot=(25, 0, 0))
# pipe stub on +X side with elbow and valve
m.tube([(0.8, 0.0, 1.5), (0.88, 0.0, 1.5), (0.88, 0.0, 1.2)], 0.06, 'steel_dark', seg=6)
m.cyl(0.1, 0.05, (0.84, 0, 1.5), 'steel', rot=(0, 90, 0), seg=8)
m.cyl(0.08, 0.05, (0.88, 0.0, 1.38), 'warning_red', seg=8)
# ladder on -Y
for sx in (-1, 1):
    m.box((0.05, 0.05, 2.7), (sx * 0.22, -0.95, 1.35), 'steel_dark')
    m.box((0.05, 0.12, 0.05), (sx * 0.22, -0.89, 2.7), 'steel_dark')
for i in range(10):
    m.box((0.44, 0.04, 0.04), (0, -0.95, 0.2 + i * 0.27), 'rust')
for z in (0.8, 2.0):
    m.box((0.06, 0.12, 0.06), (0.22, -0.9, z), 'steel')
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-1.0, 1.0), 'z': (0, 3.0)})
