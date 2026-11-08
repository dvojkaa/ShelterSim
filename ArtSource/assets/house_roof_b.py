import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import math
from ss_art import Model, finish
from _sub import *
m = Model('house_roof_b', seed=22)
E, Rz = 0.12, 3.0
V = [(-5.5, -6.5, E), (5.5, -6.5, E), (5.5, 6.5, E), (-5.5, 6.5, E), (0, -2.5, Rz), (0, 2.5, Rz)]
solid(m, V, [(0, 1, 4), (1, 2, 5, 4), (2, 3, 5), (3, 0, 4, 5), (0, 1, 2, 3)],
      ['car_blue', 'plastic_blue', 'car_blue', 'plastic_blue', 'wood_grey'], centre=(0, 0, 1.0))
# shingle courses on the long sides (thin darker bars following the slope)
sl = (Rz - E) / 5.5
for s in (1, -1):
    for i in range(1, 9):
        t = i / 9.0
        x = s * 5.5 * (1 - t); z = E + (Rz - E) * t
        y0 = 6.5 - 4.0 * t
        m.box((0.06, 2 * y0 - 0.1, 0.05), (x * 1.0, 0, z + 0.045), 'steel_dark', rot=(0, s * math.degrees(math.atan(sl)), 0))
    for y in (-1, 1):
        for i in range(1, 8):
            t = i / 8.0
            yy = y * (6.5 - 4.0 * t)
            m.box((0.5, 0.06, 0.05), (s * 5.5 * (1 - t) * 0.0 + s * 5.5 * (1 - t) - s * 0.0, yy, E + (Rz - E) * t + 0.04), 'steel_dark', rot=(0, s * math.degrees(math.atan(sl)), 0)) if False else None
# fascia
for s in (1, -1):
    m.box((0.1, 12.9, 0.16), (s * 5.45, 0, E - 0.02), 'paint_white')
for y in (-1, 1):
    m.box((10.8, 0.1, 0.16), (0, y * 6.45, E - 0.02), 'paint_white')
# ridge cap
m.box((0.3, 5.1, 0.12), (0, 0, Rz + 0.04), 'steel_dark')
# dormer on the front hip face (-Y)
m.box((1.8, 1.5, 1.3), (0, -4.65, 1.25), 'paint_white', bevel=0.02)
m.box((1.1, 0.08, 0.75), (0, -5.42, 1.3), 'glass')
m.box((1.3, 0.05, 0.95), (0, -5.405, 1.3), 'paint_white')
m.box((0.05, 0.1, 0.75), (0, -5.43, 1.3), 'paint_white')
m.box((1.3, 0.12, 0.06), (0, -5.45, 0.8), 'paint_white')
m.poly([(-0.9, 0), (0.9, 0), (0.0, 0.7)], 1.5, (0, -4.65, 1.9), 'paint_white', rot=(0, 0, 90) if False else (0, 0, 0))
for s in (1, -1):
    m.box((1.2, 1.7, 0.07), (s * 0.5, -4.7, 2.25), 'car_blue', rot=(0, s * 38, 0))
# chimney
m.box((0.8, 0.9, 1.8), (-2.2, 1.0, 2.4), 'brick', bevel=0.02)
m.box((1.0, 1.1, 0.12), (-2.2, 1.0, 3.34), 'concrete_light')
m.box((0.45, 0.5, 0.1), (-2.2, 1.0, 3.28), 'black')
finish(m, bounds={'x': (-5.5, 5.5), 'y': (-6.5, 6.5), 'z': (0, 3.5)})
