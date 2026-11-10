import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('furn_tv_unit', seed=302)
# sideboard body y -0.2..0.25, top slab z 0.60..0.64
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.06, 0.06, 0.12), (sx * 0.74, sy * 0.16 + 0.02, 0.06), 'wood_dark')
m.box((1.6, 0.5, 0.04), (0, 0, 0.62), 'wood_light', bevel=0.01)
m.box((1.56, 0.44, 0.04), (0, 0.03, 0.14), 'wood_dark')
for x in (-0.76, 0, 0.76):
    m.box((0.04, 0.44, 0.48), (x, 0.03, 0.36), 'wood')
m.box((1.52, 0.02, 0.48), (0, 0.24, 0.36), 'wood_dark')
m.box((0.2, 0.14, 0.05), (-0.4, 0.05, 0.19), 'steel_dark')
m.box((0.12, 0.2, 0.08), (0.4, 0.05, 0.2), 'cloth_dark')
# doors hinged at outer vertical edges, front face y=-0.2
zc, yf = 0.36, -0.215
pL = m.part('Door_L', pivot=(-0.78, yf, zc))
pL.box((0.76, 0.03, 0.46), (-0.39, yf, zc), 'wood', bevel=0.008)
pL.box((0.6, 0.012, 0.34), (-0.39, yf - 0.02, zc), 'wood_light')
pL.box((0.03, 0.03, 0.12), (-0.06, yf - 0.02, zc), 'lamp_warm')
pL.box((0.2, 0.02, 0.1), (-0.55, yf - 0.022, zc + 0.1), 'rust_dark')
pR = m.part('Door_R', pivot=(0.78, yf, zc))
pR.box((0.76, 0.03, 0.46), (0.39, yf, zc), 'wood', bevel=0.008)
pR.box((0.6, 0.012, 0.34), (0.39, yf - 0.02, zc), 'wood_light')
pR.box((0.03, 0.03, 0.12), (0.06, yf - 0.02, zc), 'lamp_warm')
pR.box((0.2, 0.02, 0.1), (0.5, yf - 0.022, zc - 0.1), 'steel_dark')
# CRT TV
tz = 0.64
m.box((0.66, 0.34, 0.42), (-0.15, -0.02, tz + 0.21), 'gunmetal', bevel=0.03)
m.box((0.44, 0.22, 0.3), (-0.15, 0.14, tz + 0.2), 'steel_dark', bevel=0.02, taper=(0.7, 0.7))
m.box((0.5, 0.02, 0.32), (-0.15, -0.19, tz + 0.22), 'black')
m.box((0.46, 0.015, 0.28), (-0.15, -0.2, tz + 0.22), 'glass')
m.box((0.01, 0.01, 0.26), (-0.1, -0.21, tz + 0.22), 'glass_dirty', rot=(0, 20, 0))
m.box((0.01, 0.01, 0.16), (-0.22, -0.21, tz + 0.17), 'glass_dirty', rot=(0, -35, 0))
m.box((0.18, 0.01, 0.01), (-0.17, -0.21, tz + 0.26), 'glass_dirty')
m.box((0.07, 0.015, 0.3), (0.12, -0.185, tz + 0.21), 'cloth_dark')
m.cyl(0.02, 0.02, (0.12, -0.2, tz + 0.3), 'chrome', rot=(90, 0, 0), seg=8)
m.cyl(0.02, 0.02, (0.12, -0.2, tz + 0.23), 'chrome', rot=(90, 0, 0), seg=8)
m.box((0.05, 0.01, 0.07), (0.12, -0.2, tz + 0.12), 'rust')
# antenna
ax, az = -0.15, tz + 0.43
m.cyl(0.05, 0.04, (ax, 0.1, az + 0.02), 'steel_dark', seg=8)
m.tube([(ax, 0.1, az + 0.04), (ax - 0.3, 0.05, az + 0.22)], 0.01, 'chrome', seg=4)
m.tube([(ax, 0.1, az + 0.04), (ax + 0.26, 0.12, az + 0.2)], 0.01, 'chrome', seg=4)
m.tube([(ax + 0.26, 0.12, az + 0.2), (ax + 0.33, 0.13, az + 0.13)], 0.01, 'rust', seg=4)
# cable and dead plant
m.tube([(0.1, 0.2, tz + 0.1), (0.3, 0.22, tz + 0.02), (0.4, 0.0, tz + 0.01)], 0.01, 'black', seg=4)
m.cyl(0.07, 0.12, (0.58, 0.0, tz + 0.06), 'rust_dark', seg=8, r2=0.09)
m.tube([(0.58, 0, tz + 0.1), (0.55, 0.0, tz + 0.25), (0.5, 0, tz + 0.35)], 0.01, 'bark_dead', seg=4)
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.25, 0.25), 'z': (0, 1.3)})
