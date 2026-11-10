import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_nightstand', seed=102)
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.04, 0.04, 0.12), (sx * 0.2, sy * 0.17, 0.06), 'wood_dark', taper=(0.8, 0.8))
m.box((0.46, 0.4, 0.32), (0, 0.01, 0.28), 'wood', bevel=0.01)
m.box((0.5, 0.45, 0.03), (0, 0, 0.455), 'wood_light', bevel=0.01)
m.box((0.38, 0.015, 0.1), (0, -0.185, 0.17), 'wood_dark')   # open lower shelf shadow
d = m.part('Drawer_1', pivot=(0, -0.225, 0.37))
d.box((0.4, 0.025, 0.12), (0, -0.2, 0.37), 'wood_light', bevel=0.006)
d.box((0.34, 0.3, 0.08), (0, -0.04, 0.36), 'wood_dark')
d.cyl(0.018, 0.03, (0, -0.21, 0.37), 'chrome', rot=(90, 0, 0), seg=6)
# lamp: base, stem, bulb, tilted shade
m.cyl(0.07, 0.02, (0.1, 0.08, 0.48), 'steel_dark', seg=8)
m.cyl(0.015, 0.1, (0.1, 0.08, 0.53), 'steel_dark', seg=6)
m.cyl(0.035, 0.05, (0.1, 0.08, 0.56), 'lamp_warm', seg=6)
m.cyl(0.08, 0.07, (0.1, 0.08, 0.555), 'fabric_stain', seg=7, r2=0.05, rot=(4, 0, 0))
m.box((0.08, 0.01, 0.06), (-0.12, 0.0, 0.485), 'paper') if False else m.box((0.1, 0.08, 0.012), (-0.12, 0.02, 0.47), 'fabric_white', rot=(0, 0, 15))
finish(m, bounds={'x': (-0.25, 0.25), 'y': (-0.225, 0.225), 'z': (0, 0.6)})
