import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_mil_tent', seed=1207)
r = random.Random(1207)
# ridge tent: walls to 1.6, roof to 2.8 ridge along X; front gable at -Y open flap
W, D = 2.5, 2.0
def panel(p0, p1, p2, p3, col):
    pass
# roof slopes (two big tilted boxes)
ang = math.degrees(math.atan2(2.8 - 1.5, D))
L = math.hypot(D, 1.3) + 0.1
for sy in (-1, 1):
    m.box((5.0, L, 0.06), (0, sy * (D / 2 + 0.0), 2.15), 'army_green', rot=(sy * ang * -1, 0, 0), bevel=0.01)
# walls
for sy in (-1, 1):
    m.box((5.0, 0.06, 1.55), (0, sy * (D - 0.07), 0.8), 'army_green_dark', bevel=0.01)
# gable ends: triangle above the wall
for sx in (-1, 1):
    m.poly([(-1.95, 1.5), (1.95, 1.5), (0, 2.78)], 0.06, (sx * (W - 0.06), 0, 0), 'army_green', rot=(0, 0, 90))
    m.box((0.06, 3.9, 1.55), (sx * (W - 0.06), 0, 0.78), 'army_green_dark')
# door flap rolled on -X end
m.box((0.08, 1.0, 1.7), (-W + 0.0, 0, 0.85), 'cloth_dark') if False else None
m.box((0.12, 1.1, 1.8), (W - 0.05, 0, 0.92), 'gunmetal')
m.box((0.1, 0.9, 0.12), (W - 0.03, 0, 1.75), 'army_green_dark', rot=(0, 20, 0))
# ridge pole and poles
m.cyl(0.04, 5.1, (0, 0, 2.8), 'wood', rot=(0, 90, 0), seg=6)
for sx in (-1, 1):
    m.cyl(0.05, 2.8, (sx * W, 0, 1.4), 'wood_dark', seg=6)
# guy ropes and pegs
for sx in (-1, 1):
    for sy in (-1, 1):
        m.tube([(sx * 2.45, sy * 1.7, 1.8), (sx * 2.45, sy * 2.0 , 0.1)], 0.015, 'rope', seg=4) if False else None
        m.tube([(sx * 2.4, sy * 1.0, 2.3), (sx * 2.45, sy * 2.0, 0.08)], 0.015, 'rope', seg=4)
        m.box((0.05, 0.05, 0.25), (sx * 2.45, sy * 2.0, 0.15), 'steel_dark', rot=(sy * 30, 0, 0))
# patches, stencil, stain
m.box((0.8, 0.04, 0.6), (-1.0, -2.04, 1.0), 'fabric_stain')
m.box((0.5, 0.04, 0.5), (1.2, -2.04, 1.1), 'fabric_white') if False else None
m.box((0.7, 0.04, 0.2), (1.2, -2.04, 1.1), 'fabric_white')
m.box((0.4, 0.04, 0.4), (0.0, -1.6, 2.35), 'cloth_red', rot=(-30, 0, 0)) if False else None
finish(m, bounds={'x': (-2.6, 2.6), 'y': (-2.1, 2.1), 'z': (0, 2.9)})
