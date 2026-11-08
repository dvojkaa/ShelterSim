import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_filter', seed=90)
for sx in (-1, 1):
    for sy in (-1, 1): m.box((0.05, 0.05, 0.4), (sx * 0.3, sy * 0.3, 0.2), 'steel_dark')
m.box((0.75, 0.75, 0.05), (0, 0, 0.4), 'wood_dark')
# gravel/sand column (glass tube with layers) under the barrel
m.cyl(0.14, 0.34, (0, 0, 0.6), 'glass_dirty', seg=10)
for i, (c, h) in enumerate((('stone_dark', 0.07), ('stone', 0.07), ('sand', 0.08), ('bark', 0.05))):
    m.cyl(0.12, h, (0, 0, 0.46 + i * 0.075 + 0.02), c, seg=10)
m.cyl(0.15, 0.03, (0, 0, 0.44), 'steel_dark', seg=10)
m.cyl(0.15, 0.03, (0, 0, 0.77), 'steel_dark', seg=10)
# upper water barrel
m.cyl(0.3, 0.5, (0, 0, 1.03), 'plastic_blue', seg=12)
for z in (0.9, 1.15): m.cyl(0.31, 0.04, (0, 0, z), 'steel_dark', seg=12)
m.cyl(0.3, 0.04, (0, 0, 1.3), 'steel_dark', seg=12)
m.cyl(0.3, 0.08, (0, 0, 1.33), 'steel', seg=12, r2=0.1)
m.cyl(0.05, 0.06, (0, 0, 1.365), 'steel_dark', seg=8)
# pipe from barrel into column
m.tube([(0, 0, 0.78), (0, 0, 0.78)], 0.001, 'steel') if False else None
m.cyl(0.04, 0.1, (0, 0, 0.82), 'steel', seg=8)
# tap at the front (-Y)
m.cyl(0.03, 0.2, (0, -0.2, 0.52), 'steel', rot=(90, 0, 0), seg=6)
m.cyl(0.025, 0.1, (0, -0.3, 0.48), 'steel', seg=6)
m.box((0.12, 0.03, 0.03), (0, -0.2, 0.57), 'warning_red')
# clean-water jug on the floor, under the tap
m.cyl(0.1, 0.22, (0, -0.3, 0.11), 'glass_dirty', seg=10, r2=0.07)
m.cyl(0.085, 0.1, (0, -0.3, 0.07), 'water', seg=10)
m.cyl(0.05, 0.04, (0, -0.3, 0.24), 'steel_dark', seg=8)
finish(m, bounds={'x': (-0.4, 0.4), 'y': (-0.4, 0.4), 'z': (0, 1.4)})
