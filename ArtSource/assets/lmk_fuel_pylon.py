import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_fuel_pylon', seed=1205)
m.box((1.0, 0.8, 0.4), (0, 0.1, 0.2), 'concrete_dark')
m.box((0.5, 0.4, 8.2), (0, 0.2, 4.5), 'steel_dark', bevel=0.02)
for sx in (-1, 1):
    m.box((0.1, 0.1, 7.6), (sx * 0.15, -0.05, 4.2), 'rust_dark')
# top sign: 4 x 2.2 frame with blank face and dead light bar
m.box((4.0, 0.5, 2.2), (0, 0, 8.9), 'rust_dark', bevel=0.03)
m.box((3.7, 0.1, 1.9), (0, -0.28, 8.9), 'plaster_dirty')
m.poly([(0, 0.7), (0.4, 0.1), (0.5, -0.2), (0.3, -0.55), (0, -0.7), (-0.3, -0.55), (-0.5, -0.2), (-0.4, 0.1)], 0.05, (-1.0, -0.34, 8.9), 'warning_red')
m.box((1.4, 0.06, 0.3), (0.8, -0.34, 9.2), 'steel_dark')
m.box((1.4, 0.06, 0.3), (0.8, -0.34, 8.6), 'steel_dark')
m.cyl(0.25, 0.3, (0, -0.1, 10.15), 'steel_dark', seg=8)
for sx in (-1, 1):
    m.cyl(0.1, 0.2, (sx * 1.7, -0.3, 10.0), 'glass_dirty', rot=(90, 0, 0), seg=8)
# price board 3 x 2.6 with dead digits
m.box((3.0, 0.4, 3.0), (0, 0, 5.3), 'gunmetal', bevel=0.03)
for i in range(3):
    z = 6.1 - i * 0.9
    m.box((2.6, 0.06, 0.7), (0, -0.23, z), 'black')
    m.box((0.6, 0.04, 0.5), (-0.9, -0.27, z), 'steel_dark')
    for j in range(3):
        m.box((0.4, 0.04, 0.5), (-0.1 + j * 0.5, -0.27, z), 'steel_dark' if (i + j) % 2 else 'glass')
    m.box((0.08, 0.04, 0.08), (1.1, -0.27, z - 0.25), 'hazard_yellow')
for sx in (-1, 1):
    m.box((0.12, 0.5, 3.1), (sx * 1.55, 0, 5.3), 'rust')
m.box((0.5, 0.05, 1.2), (1.2, -0.25, 7.0), 'rust_dark')
finish(m, bounds={'x': (-2.1, 2.1), 'y': (-0.5, 0.6), 'z': (0, 10.3)})
