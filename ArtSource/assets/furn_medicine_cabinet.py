import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_medicine_cabinet', seed=103)
# wall at y=0, protrudes -Y. cabinet z 1.4..1.8, basin below z 1.1..1.4
m.box((0.56, 0.14, 0.4), (0, -0.07, 1.6), 'paint_white', bevel=0.008)          # carcass
m.box((0.5, 0.02, 0.34), (0, -0.01, 1.6), 'steel_dark')                          # inner back (visible when open)
for z in (1.52, 1.64):
    m.box((0.48, 0.1, 0.012), (0, -0.07, z), 'steel')                            # shelves
m.box((0.6, 0.03, 0.05), (0, -0.035, 1.775), 'paint_white', bevel=0.006)         # top cap
# rust streaks
# door: hinged at left edge x=-0.28, swings about Z
d = m.part('Door', pivot=(-0.28, -0.13, 1.6))
d.box((0.56, 0.025, 0.38), (0.0, -0.1325, 1.6), 'steel_dark', bevel=0.006)        # frame
d.box((0.5, 0.01, 0.32), (0.0, -0.1465, 1.6), 'steel')                      # mirror
Y = -0.1475
# stepped crack lines (no rotation) and a missing chip
for i in range(6):
    d.box((0.012, 0.012, 0.06), (0.02 + i * 0.03, Y, 1.73 - i * 0.05), 'concrete_light')
    d.box((0.05, 0.012, 0.01), (0.04 + i * 0.03, Y, 1.7 - i * 0.05), 'concrete_light') if i % 2 == 0 else None
for i in range(3):
    d.box((0.012, 0.012, 0.05), (-0.12 - i * 0.025, Y, 1.7 - i * 0.04), 'concrete_light')
d.box((0.07, 0.012, 0.07), (0.2, Y, 1.72), 'steel_dark')
d.box((0.025, 0.03, 0.08), (0.24, -0.155, 1.6), 'chrome')                         # handle
d.box((0.04, 0.03, 0.05), (-0.27, -0.135, 1.6), 'rust_dark')                      # hinge
d.box((0.2, 0.012, 0.12), (-0.14, -0.1475, 1.5), 'steel_dark')   # grime patch
d.box((0.05, 0.012, 0.1), (0.15, -0.1475, 1.5), 'steel_dark')

m.box((0.03, 0.14, 0.03), (-0.2, -0.07, 1.78), 'rust_dark')
# basin
m.box((0.5, 0.14, 0.05), (0, -0.07, 1.3), 'paint_white', bevel=0.01)             # rim slab
m.box((0.44, 0.12, 0.12), (0, -0.07, 1.23), 'paint_white', taper=(0.7, 0.7))       # bowl
m.box((0.38, 0.1, 0.012), (0, -0.075, 1.327), 'water')                             # water stain
m.box((0.5, 0.01, 0.1), (0, -0.005, 1.35), 'paint_white')                         # splashback
m.cyl(0.015, 0.1, (0, -0.03, 1.36), 'chrome', seg=6)                             # tap riser
m.tube([(0, -0.03, 1.40), (0, -0.07, 1.40), (0, -0.09, 1.37)], 0.012, 'chrome', seg=5)
m.tube([(0, -0.07, 1.17), (0, -0.04, 1.13), (0, -0.01, 1.12)], 0.018, 'steel_dark', seg=5)  # drain
finish(m, bounds={'x': (-0.3, 0.3), 'y': (-0.18, 0.0), 'z': (1.1, 1.8)})
