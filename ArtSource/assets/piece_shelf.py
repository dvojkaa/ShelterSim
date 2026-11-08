import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_shelf', seed=88)
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.05, 0.05, 2.0), (sx * 0.7, sy * 0.2, 1.0), 'steel_dark')
for z in (0.15, 0.7, 1.25, 1.8):
    m.box((1.46, 0.46, 0.04), (0, 0, z), 'steel')
    m.box((1.46, 0.02, 0.06), (0, -0.23, z + 0.03), 'steel_dark')
for s in (-1, 1): beam(m, (s * 0.72, 0.15), (-s * 0.72, 1.7), 0.025, 0.02, 0.22, 'steel_dark') if False else None
beam(m, (-0.7, 0.15), (0.7, 1.8), 0.025, 0.02, 0.22, 'steel_dark')
# cans
rng = [('steel', 'warning_red'), ('chrome', 'leaf_green'), ('steel', 'hazard_yellow'), ('steel', 'plastic_blue')]
for i in range(8):
    x = -0.6 + i * 0.09
    c = rng[i % 4]
    m.cyl(0.04, 0.1, (x, -0.1, 0.22), c[0], seg=8)
    m.cyl(0.041, 0.05, (x, -0.1, 0.22), c[1], seg=8)
for i in range(6):
    x = -0.55 + i * 0.1
    m.cyl(0.04, 0.1, (x, 0.05, 0.22), 'steel', seg=8)
    m.cyl(0.041, 0.04, (x, 0.05, 0.22), 'rust' if False else 'orange', seg=8)
# boxes
m.box((0.3, 0.3, 0.22), (-0.45, 0.0, 0.83), 'wood_light', bevel=0.01)
m.box((0.28, 0.01, 0.1), (-0.45, -0.155, 0.83), 'paint_white')
m.box((0.35, 0.3, 0.3), (0.0, 0.0, 0.87), 'army_green', bevel=0.01)
m.box((0.34, 0.012, 0.06), (0.0, -0.155, 0.88), 'hazard_yellow')
m.box((0.3, 0.28, 0.2), (0.45, 0.0, 0.82), 'plastic_blue', bevel=0.01)
# jars and bottles
for i in range(4):
    x = -0.55 + i * 0.12
    m.cyl(0.045, 0.13, (x, -0.05, 1.34), 'glass_dirty', seg=8)
    m.cyl(0.035, 0.08, (x, -0.05, 1.32), ['leaf_brown', 'orange', 'leaf_dark', 'warning_red'][i], seg=8)
    m.cyl(0.038, 0.02, (x, -0.05, 1.415), 'steel_dark', seg=8)
for i in range(3):
    x = 0.1 + i * 0.2
    m.box((0.16, 0.3, 0.14), (x + 0.1, 0.0, 1.34), ['cloth_red', 'fabric_blue', 'fabric_grey'][i], bevel=0.01)
# top: crate & toolbox
m.box((0.5, 0.3, 0.14), (-0.3, 0.0, 1.89), 'rust_dark')
m.box((0.5, 0.01, 0.04), (-0.3, -0.155, 1.89), 'rust')
m.cyl(0.08, 0.2, (0.45, 0.0, 1.9), 'steel_dark', seg=8)
m.box((0.25, 0.25, 0.1), (0.05, 0.0, 1.87), 'wood')
finish(m, bounds={'x': (-0.75, 0.75), 'y': (-0.25, 0.25), 'z': (0, 2.0)})
