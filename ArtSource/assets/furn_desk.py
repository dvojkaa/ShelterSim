import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_desk', seed=311)
# top
m.box((1.4, 0.66, 0.05), (0, 0.02, 0.745), 'wood', bevel=0.012)
m.box((1.38, 0.02, 0.03), (0, -0.30, 0.72), 'wood_dark')
m.box((0.5, 0.3, 0.012), (-0.35, 0.02, 0.774), 'leather_dark')   # blotter
# left legs + modesty panel
for y in (-0.27, 0.31):
    m.box((0.06, 0.06, 0.72), (-0.66, y, 0.36), 'wood_dark', bevel=0.005)
m.box((0.04, 0.6, 0.3), (-0.66, 0, 0.5), 'wood_dark')
m.box((0.8, 0.03, 0.4), (-0.25, 0.31, 0.5), 'wood_dark')
m.box((0.8, 0.5, 0.03), (-0.25, 0, 0.14), 'wood_dark')  # low shelf
# pedestal
m.box((0.42, 0.62, 0.72), (0.48, 0.04, 0.36), 'wood_dark', bevel=0.008)
m.box((0.44, 0.04, 0.06), (0.48, -0.28, 0.03), 'black')
# static lower drawer fronts
for i, z in enumerate((0.1, 0.27)):
    m.box((0.38, 0.02, 0.15), (0.48, -0.30, z), 'wood', bevel=0.006)
    m.box((0.1, 0.02, 0.02), (0.48, -0.32, z + 0.02), 'steel_dark')
# damage: broken corner, stains
m.box((0.12, 0.66, 0.01), (-0.64, 0.02, 0.774), 'wood_dark', rot=(0, 0, 0))
m.box((0.2, 0.02, 0.1), (0.2, 0.33, 0.69), 'wood_dark', rot=(0, 18, 0))
# Drawer_1 (top drawer, pivot at front-face centre)
pv = (0.48, -0.305, 0.55)
d = m.part('Drawer_1', pivot=pv)
d.box((0.38, 0.03, 0.15), pv, 'wood', bevel=0.006)
d.box((0.12, 0.025, 0.025), (0.48, -0.33, 0.57), 'steel')
d.box((0.01, 0.01, 0.01), (0.48, -0.345, 0.57), 'rust')
d.box((0.34, 0.5, 0.01), (0.48, -0.06, 0.49), 'wood_dark')
d.box((0.01, 0.5, 0.1), (0.31, -0.06, 0.54), 'wood_dark')
d.box((0.01, 0.5, 0.1), (0.65, -0.06, 0.54), 'wood_dark')
d.box((0.34, 0.01, 0.1), (0.48, 0.19, 0.54), 'wood_dark')
# CRT terminal
m.box((0.4, 0.36, 0.03), (-0.35, 0.1, 0.795), 'steel_dark')
m.box((0.34, 0.34, 0.2), (-0.35, 0.1, 0.9), 'steel', bevel=0.02, taper=(0.9, 0.9))
m.box((0.26, 0.02, 0.17), (-0.35, -0.075, 0.9), 'glass', bevel=0.01)
m.box((0.2, 0.02, 0.005), (-0.35, -0.09, 0.965), 'glass_dirty', rot=(0, 0, 0))
m.cyl(0.1, 0.12, (-0.35, 0.35, 0.89), 'steel_dark', rot=(90, 0, 0), seg=8, r2=0.14) if False else None
m.box((0.2, 0.16, 0.16), (-0.35, 0.27, 0.9), 'steel_dark', taper=(0.7, 0.7), bevel=0.01)
m.box((0.38, 0.12, 0.02), (-0.35, -0.15, 0.79), 'gunmetal')   # keyboard
for i in range(5):
    m.box((0.06, 0.02, 0.008), (-0.5 + i * 0.075, -0.15, 0.805), 'bone')
# lamp
m.cyl(0.05, 0.015, (0.3, 0.2, 0.78), 'black', seg=8)
m.tube([(0.3, 0.2, 0.78), (0.3, 0.2, 0.9), (0.37, 0.12, 0.97)], 0.008, 'steel_dark', seg=5)
m.cyl(0.04, 0.06, (0.4, 0.1, 0.96), 'mustard_dark', rot=(60, 0, 0), seg=7, r2=0.02)
# papers
for i, (x, y, r, c) in enumerate([(0.1, -0.1, 12, 'paint_white'), (0.15, -0.12, -20, 'fabric_white'), (0.0, -0.15, 40, 'plaster'), (-0.55, -0.15, 5, 'fabric_white'), (0.45, -0.05, 30, 'paint_white')]):
    m.box((0.21, 0.29, 0.004), (x, y, 0.7825 + 0.004 * i), c, rot=(0, 0, r))
m.box((0.2, 0.28, 0.02), (0.12, 0.15, 0.79), 'paint_white', rot=(0, 0, -8))
m.box((0.14, 0.1, 0.05), (0.6, 0.15, 0.805), 'brick_dark', rot=(0, 0, 15))  # book? mug-ish
m.cyl(0.035, 0.08, (-0.1, -0.25, 0.82), 'rust_light', seg=7)  # old mug
finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.35, 0.35), 'z': (0, 1.0)})
