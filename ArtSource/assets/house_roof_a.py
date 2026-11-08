import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import math
from ss_art import Model, finish
from _sub import *
m = Model('house_roof_a', seed=21)
HW, E, R = 5.35, 0.12, 2.95
ang = math.degrees(math.atan2(R - E, HW)); L = math.hypot(HW, R - E) + 0.1
for s in (1, -1):
    cx, cz = s * HW / 2, (E + R) / 2 + 0.05
    m.box((L, 12.9, 0.14), (cx, 0, cz), 'brick', rot=(0, s * ang, 0))
    # tile courses
    n = int(L / 0.42)
    for i in range(n):
        d = -L / 2 + 0.3 + i * 0.42
        px, pz = cx + s * d * math.cos(math.radians(ang)) * 1.0, cz - d * math.sin(math.radians(ang)) * s * s * 1.0
        # local x runs down-slope for s=+1; for s=-1 local x runs up toward... use local-x offset symmetric
        ox = d * math.cos(math.radians(ang)); oz = -d * math.sin(math.radians(ang)) * s
        m.box((0.07, 12.9, 0.05), (cx + ox, 0, cz + oz + 0.07), 'brick_dark', rot=(0, s * ang, 0))
    # fascia along the eave and barge boards at the gables
    m.box((0.1, 12.9, 0.2), (s * (HW + 0.05), 0, E - 0.02), 'paint_white')
    for y in (-6.45, 6.45):
        m.box((L, 0.1, 0.1), (cx, y, cz - 0.03 + 0.02), 'paint_white', rot=(0, s * ang, 0))
m.box((0.5, 12.9, 0.2), (0, 0, R + 0.1), 'brick_dark', rot=(0, 0, 0))
# gable ends
for y in (-5.95, 5.95):
    m.poly([(-5.0, 0.12), (5.0, 0.12), (0.0, 2.65)], 0.2, (0, y, 0), 'plaster')
    m.box((0.9, 0.22, 0.6), (0, y + (-0.02 if y < 0 else 0.02), 1.0), 'paint_white')
    m.box((0.7, 0.24, 0.45), (0, y + (-0.03 if y < 0 else 0.03), 1.0), 'glass')
# soffit
m.box((11.0, 12.9, 0.06), (0, 0, 0.03), 'wood_grey')
# chimney
m.box((0.9, 1.0, 2.2), (2.3, 1.5, 2.4), 'brick', bevel=0.02)
m.box((1.1, 1.2, 0.15), (2.3, 1.5, 3.4), 'concrete_light')
m.box((0.5, 0.6, 0.1), (2.3, 1.5, 3.27), 'black')
for i in range(5):
    m.box((0.92, 1.02, 0.04), (2.3, 1.5, 1.7 + i * 0.38), 'brick_dark')
finish(m, bounds={'x': (-5.5, 5.5), 'y': (-6.5, 6.5), 'z': (0, 3.5)})
