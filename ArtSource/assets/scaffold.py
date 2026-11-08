import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('scaffold', seed=76)
Wd, D = 2.8, 1.0
xs = (-1.4 + 0.04, 1.4 - 0.04); ys = (-0.5 + 0.04, 0.5 - 0.04)
for x in xs:
    for y in ys:
        m.cyl(0.04, 4.0, (x, y, 2.0), 'steel', seg=6)
# horizontal levels
for z in (0.3, 1.3, 2.3, 3.3):
    for y in ys:
        m.box((2.72, 0.04, 0.04), (0, y, z), 'steel_dark')
    for x in xs:
        m.box((0.04, 0.92, 0.04), (x, 0, z), 'steel_dark')
# diagonal braces on both long sides
for y in ys:
    beam(m, (-1.36, 0.3), (1.36, 2.3), 0.035, 0.035, y - 0.03 * (1 if y < 0 else -1), 'steel_dark') if False else None
    beam(m, (-1.36, 0.3), (1.36, 1.3), 0.035, 0.035, y, 'steel')
    beam(m, (-1.36, 2.3), (1.36, 1.3), 0.035, 0.035, y, 'steel')
    beam(m, (-1.36, 2.3), (1.36, 3.3), 0.035, 0.035, y, 'steel')
# planks at two working levels
for z in (2.3, 3.3):
    for i in range(4):
        m.box((2.8, 0.22, 0.045), (0, -0.4 + i * 0.27, z + 0.05), 'wood_light' if i % 2 else 'wood', bevel=0.005)
# guard rail at the top
for y in ys:
    m.box((2.72, 0.035, 0.035), (0, y, 3.9), 'steel')
    m.box((2.72, 0.035, 0.035), (0, y, 3.6), 'steel')
for x in xs: m.box((0.035, 0.92, 0.035), (x, 0, 3.9), 'steel')
# ladder on the end (x min side)
for y in (-0.15, 0.15): m.box((0.03, 0.03, 3.4), (-1.5 + 0.06, y, 1.7), 'steel') if False else None
# base plates
for x in xs:
    for y in ys: m.box((0.14, 0.14, 0.03), (x, y, 0.015), 'steel_dark')
# a toolbucket and hazard tape on the first plank row
m.cyl(0.1, 0.25, (0.8, 0.2, 3.46), 'plastic_blue', seg=8, r2=0.12)
m.box((0.5, 0.04, 0.2), (0.0, -0.52, 3.74), 'hazard_yellow') if False else None
m.box((0.8, 0.02, 0.2), (0.0, -0.46, 3.74), 'hazard_yellow')
finish(m, bounds={'x': (-1.5, 1.5), 'y': (-0.6, 0.6), 'z': (0, 4.0)})
