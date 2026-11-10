import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_farm_barn', seed=1207)
r = random.Random(1207)
WZ = 3.6
T = 0.25
m.box((16.4, 16.4, 0.3), (0, 0, 0.15), 'concrete_dark')                 # foundation ring look
def plank_wall(cx, cy, sx, sy, face):   # face: -1 front/back battens along x walls
    pass
cols = ['wood', 'wood_grey', 'wood_dark', 'wood_light']
# back wall and side walls
m.box((16.0, T, WZ), (0, 8 - T / 2, WZ / 2 + 0.2), 'wood_grey')
for sx in (-1, 1):
    m.box((T, 16.0, WZ), (sx * (8 - T / 2), 0, WZ / 2 + 0.2), 'wood')
# front wall pieces beside 3.4 gap, lintel above 2.8
for sx in (-1, 1):
    m.box((6.3, T, WZ), (sx * (1.7 + 3.15), -8 + T / 2, WZ / 2 + 0.2), 'wood')
m.box((3.4, T, WZ - 2.8 + 0.2), (0, -8 + T / 2, (WZ + 3.0) / 2 + 0.0), 'wood_dark') if False else None
m.box((3.4, T, 1.0), (0, -8 + T / 2, 3.3), 'wood_dark')
# vertical battens on exterior
for i in range(33):
    x = -8 + i * 0.5
    c = r.choice(cols)
    if abs(x) > 1.8 or True:
        if abs(x) >= 1.85:
            m.box((0.12, 0.05, WZ - 0.2), (x, -8.0 - 0.0, WZ / 2 + 0.3), 'wood_dark')
    m.box((0.12, 0.05, WZ - 0.2), (x, 8.0, WZ / 2 + 0.3), 'wood_dark')
for i in range(33):
    y = -8 + i * 0.5
    for sx in (-1, 1):
        m.box((0.05, 0.12, WZ - 0.2), (sx * 8.0, y, WZ / 2 + 0.3), 'wood_dark')
# patches of different planks, missing boards
for i in range(24):
    side = r.choice(['b', 'l', 'r'])
    w, h = r.uniform(0.4, 1.4), r.uniform(0.6, 1.6)
    z = r.uniform(1.0, 3.2)
    c = r.choice(['wood_light', 'wood_grey', 'rust_dark', 'wood_dark'])
    if side == 'b':
        m.box((w, 0.04, h), (r.uniform(-7, 7), 8.04, z), c)
    else:
        s = -1 if side == 'l' else 1
        m.box((0.04, w, h), (s * 8.04, r.uniform(-7, 7), z), c)
for i in range(6):
    m.box((r.uniform(0.4, 1.2), 0.04, r.uniform(0.6, 1.6)), (r.choice([-1, 1]) * r.uniform(2.2, 7), -8.04, r.uniform(1.0, 3.0)), r.choice(['wood_light', 'wood_grey']))
# corner posts
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.35, 0.35, WZ + 0.2), (sx * 7.95, sy * 7.95, (WZ + 0.2) / 2 + 0.2), 'wood_dark')
# gable fills (front above lintel, back)
gab = [(-8, WZ + 0.2), (8, WZ + 0.2), (5.6, 5.8), (0, 7.0), (-5.6, 5.8)]
m.poly(gab, T, (0, 8 - T / 2, 0), 'wood_grey')
m.poly([(-8, WZ + 0.2), (8, WZ + 0.2), (5.6, 5.8), (0, 7.0), (-5.6, 5.8)], T, (0, -8 + T / 2, 0), 'wood')
# front lintel face is covered by front gable poly down to z 3.8; fill under to meet lintel top at 3.8 already
# hayloft door and hoist beam on front gable
m.box((1.6, 0.1, 1.6), (0, -8.2, 5.0), 'wood_dark', bevel=0.02)
m.box((1.3, 0.1, 1.3), (0, -8.26, 5.0), 'black')
m.box((1.4, 0.06, 0.12), (0, -8.3, 5.0), 'wood_light', rot=(0, 40, 0))
m.box((1.4, 0.06, 0.12), (0, -8.3, 5.0), 'wood_light', rot=(0, -40, 0))
m.box((0.2, 0.25, 0.2), (0, -8.5, 6.2), 'steel_dark')
m.box((0.14, 0.9, 0.14), (0, -8.6, 6.3), 'wood_dark')
# hay spilling out
m.blob((1.2, 0.5, 0.35), (0.2, -8.35, 4.3), 'sand', rough=0.2, flat_bottom=None)
# gambrel roof panels (long along y, 17.2 with overhang)
import math
def panel(side, x0, z0, x1, z1, col, thick=0.2):
    dx, dz = abs(x1 - x0), z1 - z0
    L = math.hypot(dx, dz) + 0.1
    ang = math.degrees(math.atan2(dz, dx))
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    # normal offset so top surface passes through points
    m.box((L, 17.2, thick), (side * cx, 0, cz), col, rot=(0, side * ang, 0))
for side in (-1, 1):
    panel(side, 8.25, WZ + 0.1, 5.6, 5.85, 'rust_dark' if side > 0 else 'steel_dark')
    panel(side, 5.6, 5.85, 0.0, 7.1, 'rust' if side > 0 else 'rust_dark')
# missing sheets: dark holes / lighter patches on roof
for i in range(8):
    side = r.choice([-1, 1])
    m.box((r.uniform(0.8, 1.6), r.uniform(1.0, 2.0), 0.05), (side * r.uniform(1.5, 5), r.uniform(-7, 7), 0), 'rust_light') if False else None
m.box((0.5, 17.4, 0.25), (0, 0, 7.05), 'steel_dark')                   # ridge cap
# interior: rafters hint (keep hollow), hay stack
for x in (-6, -3, 3, 6):
    pass
# sliding door slab hung on a track beside the gap (outside, leaves gap open)
m.box((8.4, 0.12, 0.1), (3.6 + 0.0, -8.35, 3.2), 'steel_dark')
m.box((3.0, 0.12, 2.9), (6.4, -8.3, 1.65), 'wood_dark', bevel=0.02)
for k in range(2):
    m.box((3.0, 0.14, 0.12), (6.4, -8.4, 0.6 + k * 1.8), 'wood_light')
m.box((0.12, 0.14, 2.9), (6.4, -8.4, 1.65), 'rust_dark', rot=(0, 0, 0))
m.box((0.12, 0.14, 3.0), (6.4, -8.4, 1.65), 'rust_dark', rot=(0, 45, 0)) if False else None
# door frame at gap
for sx in (-1, 1):
    m.box((0.2, 0.3, 3.0), (sx * 1.8, -8.1, 1.7), 'wood_dark')
m.box((3.8, 0.3, 0.2), (0, -8.1, 3.0), 'wood_dark')
finish(m, bounds={'x': (-8.7, 8.7), 'y': (-9.1, 8.7), 'z': (0, 7.4)})
