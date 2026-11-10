import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('camp_locked_chest', seed=703)
W, D = 1.34, 0.78            # x, y
# base box y -0.45..0.45, z 0..0.62
m.box((W, D, 0.08), (0, 0, 0.04), 'black')
m.box((W - 0.04, D - 0.04, 0.54), (0, 0, 0.35), 'wood_dark', bevel=0.015)
for i in range(4):
    m.box((W - 0.03, 0.005, 0.02), (0, -D / 2 + 0.01 * 0, 0.18 + i * 0.12), 'wood_grey') if False else None
for x in (-0.5, 0.5):
    m.box((0.1, D + 0.03, 0.56), (x, 0, 0.35), 'steel_dark', bevel=0.01)           # iron bands
    for y in (-D / 2 - 0.02, D / 2 + 0.02):
        for z in (0.15, 0.55):
            m.cyl(0.02, 0.02, (x, y, z), 'rust_light', rot=(90, 0, 0), seg=6)
for sx in (-W / 2 + 0.03, W / 2 - 0.03):
    m.box((0.1, D, 0.1), (sx, 0, 0.09), 'steel_dark')                           # corner feet
    m.box((0.08, D + 0.02, 0.08), (sx, 0, 0.61), 'steel_dark')
m.box((W, 0.05, 0.1), (0, -D / 2 + 0.0, 0.1), 'steel_dark')
m.box((0.2, 0.01, 0.2), (-0.7 + 0.2, -D / 2 - 0.025, 0.3), 'rust_dark', rot=(0, 15, 0)) if False else None
m.box((0.3, 0.012, 0.2), (-0.2, -D / 2 - 0.012, 0.3), 'rust_dark', rot=(0, 10, 0))
m.box((0.012, 0.3, 0.2), (W / 2 - 0.02 + 0.01, 0.1, 0.35), 'rust', rot=(0, 0, 0))
# hasp plate on base (front), lid hasp tongue
m.box((0.22, 0.03, 0.2), (0, -D / 2 - 0.015, 0.52), 'steel', bevel=0.008)
# lid: pivot at back hinge edge (y=+D/2, z=0.62)
lid = m.part('Lid', pivot=(0, D / 2, 0.62))
lid.box((W, D, 0.07), (0, 0, 0.655), 'wood', bevel=0.015)
for i in range(5):    # arched look: stepped planks on top
    pass
lid.box((W - 0.12, D - 0.12, 0.07), (0, 0, 0.7), 'wood', bevel=0.02, taper=(0.92, 0.88)) if False else None
lid.box((W - 0.1, D - 0.14, 0.06), (0, 0, 0.71), 'wood_dark', bevel=0.02)
lid.box((W - 0.3, D - 0.34, 0.06), (0, 0, 0.77), 'wood', bevel=0.02)
for x in (-0.5, 0.5):
    lid.box((0.1, D + 0.03, 0.075), (x, 0, 0.67), 'steel_dark', bevel=0.01)
    lid.box((0.1, D - 0.2, 0.07), (x, 0, 0.745), 'steel_dark', bevel=0.01)
    lid.box((0.1, 0.05, 0.14), (x, -D / 2 + 0.03, 0.65), 'steel_dark')
    lid.box((0.1, 0.05, 0.14), (x, D / 2 - 0.03, 0.65), 'steel_dark')
for x in (-0.65, 0.0, 0.65):
    lid.cyl(0.025, 0.02, (x, -D / 2 - 0.01, 0.68), 'rust_light', rot=(90, 0, 0), seg=6)
lid.box((W - 0.2, 0.02, 0.05), (0, -D / 2 - 0.005, 0.67), 'rust_dark')
# hinge barrels at the back
for x in (-0.55, 0.55):
    lid.cyl(0.04, 0.16, (x, D / 2 + 0.03, 0.62), 'black', rot=(0, 90, 0), seg=8)
# hasp tongue and big padlock at -Y
lid.box((0.16, 0.025, 0.28), (0, -D / 2 - 0.015, 0.55), 'steel_dark', bevel=0.006)
lid.box((0.16, 0.03, 0.13), (0, -D / 2 - 0.015, 0.69), 'steel_dark', bevel=0.006)
m.box((0.26, 0.08, 0.24), (0, -D / 2 - 0.035, 0.42), 'hazard_yellow', bevel=0.03)    # padlock body
m.box((0.26, 0.012, 0.24), (0, -D / 2 - 0.15, 0.42), 'mustard_dark', bevel=0.02) if False else None
m.cyl(0.015, 0.01, (0, -D / 2 - 0.074, 0.42), 'black', rot=(90, 0, 0), seg=6)         # keyhole
m.box((0.012, 0.02, 0.04), (0, -D / 2 - 0.072, 0.39), 'black')
m.tube([(-0.075, -D / 2 - 0.035, 0.52), (-0.075, -D / 2 - 0.035, 0.62), (-0.045, -D / 2 - 0.035, 0.67), (0.045, -D / 2 - 0.035, 0.67), (0.075, -D / 2 - 0.035, 0.62), (0.075, -D / 2 - 0.035, 0.52)], 0.027, 'chrome', seg=6)
m.box((0.05, 0.012, 0.06), (0.09, -D / 2 - 0.078, 0.5), 'mustard_dark')
finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.475, 0.475), 'z': (0, 0.95)})
