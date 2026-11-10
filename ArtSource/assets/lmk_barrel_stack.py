import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_barrel_stack', seed=1103)
cols = ['rust', 'warning_red', 'steel_dark', 'plastic_blue', 'rust_dark', 'army_green']
def barrel(x, y, z, c, tilt=0):
    m.cyl(0.3, 0.9, (x, y, z + 0.45), c, seg=10, rot=(0, 0, tilt))
    for dz in (0.2, 0.7):
        m.cyl(0.32, 0.05, (x, y, z + dz), 'steel_dark', seg=10)
    m.cyl(0.27, 0.03, (x, y, z + 0.91), 'black', seg=10)
for i, (x, y) in enumerate([(-0.6, 0.0), (0.0, 0.05), (0.6, 0.0)]):
    barrel(x, y, 0, cols[i])
barrel(-0.3, 0.03, 0.9, cols[3])
barrel(0.3, 0.03, 0.9, cols[4])
m.cyl(0.3, 0.9, (0.1, -0.7, 0.3), 'rust', rot=(0, 90, 20), seg=10)   # fallen barrel
m.cyl(0.05, 0.02, (0.1, 0.0, 1.83), 'steel', seg=6)
m.box((0.5, 0.02, 0.06), (-0.6, -0.31, 0.5), 'hazard_yellow')
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-1.2, 0.5), 'z': (0, 1.9)})
