import math
from pathlib import Path
import bpy
from ss_art import Model, finish

# Scrap bicycle. Rolls along Y, front = +Y. Wheels are discs in the Y-Z plane (axle along X).
m = Model('piece_bicycle', seed=2511)
R = 0.33
RY, FY = -0.52, 0.52          # rear / front axle y
HUB_Z = R

def wheel(name, yc, rim_col):
    w = m.part(name, pivot=(0, yc, HUB_Z))
    n = 20
    ring = [(0, yc + 0.30 * math.sin(i * 2 * math.pi / n), HUB_Z + 0.30 * math.cos(i * 2 * math.pi / n)) for i in range(n + 1)]
    w.tube(ring, 0.03, 'rubber', seg=5)
    #ring2 = [(0, yc + 0.255 * math.sin(i * 2 * math.pi / 12), HUB_Z + 0.255 * math.cos(i * 2 * math.pi / 12)) for i in range(13)]
    for i in range(6):
        a = i * math.pi / 3 + 0.2
        w.tube([(0, yc, HUB_Z), (0, yc + 0.285 * math.sin(a), HUB_Z + 0.285 * math.cos(a))], 0.008, rim_col, seg=3)
    w.cyl(0.035, 0.09, (0, yc, HUB_Z), 'steel_dark', seg=6, rot=(0, 90, 0))

wheel('Wheel_B', RY, 'steel')
wheel('Wheel_F', FY, 'rust_light')

def t(a, b, r=0.016, c='rust', seg=5):
    m.tube([a, b], r, c, seg=seg)

bb = (0, -0.06, 0.34)
seat_top = (0, -0.20, 0.92)
head_top = (0, 0.40, 0.90)
head_bot = (0, 0.43, 0.68)
rear = (0, RY, HUB_Z)
front = (0, FY, HUB_Z)
# main diamond
t(seat_top, head_top, 0.019)          # top tube
t(bb, head_bot, 0.021, 'rust_dark')   # down tube
t(bb, seat_top, 0.019)                # seat tube
t(head_top, head_bot, 0.026, 'steel_dark')  # head tube
for s in (-0.05, 0.05):
    t(bb, (s, RY, HUB_Z), 0.013, 'rust_dark')       # chain stay
    t((0, -0.22, 0.88), (s, RY, HUB_Z), 0.011)       # seat stay
    t((0, 0.43, 0.68), (s, FY, HUB_Z), 0.015, 'steel_dark')  # fork leg
t(seat_top, (0, -0.22, 1.0), 0.016, 'steel')         # seat post
m.box((0.12, 0.26, 0.05), (0, -0.24, 1.03), 'leather_dark', bevel=0.015, taper=(0.55, 0.5))  # saddle
# stem + handlebars
t(head_top, (0, 0.40, 1.06), 0.014, 'steel')
t((-0.32, 0.38, 1.06), (0.32, 0.38, 1.06), 0.013, 'steel_dark')
t((-0.32, 0.38, 1.06), (-0.33, 0.30, 1.03), 0.014, 'black')
t((0.32, 0.38, 1.06), (0.33, 0.30, 1.03), 0.014, 'black')
# crank, chain ring, pedals, chain
cr = m.part('Crank', pivot=(0, -0.06, 0.34))
cr.cyl(0.09, 0.012, (0.05, -0.06, 0.34), 'steel_dark', seg=10, rot=(0, 90, 0))
cr.cyl(0.03, 0.09, (0, -0.06, 0.34), 'steel', seg=6, rot=(0, 90, 0))
cr.tube([(0.07, -0.06, 0.34), (0.07, -0.06 + 0.02, 0.34 + 0.17)], 0.011, 'steel_dark', seg=5)
cr.tube([(-0.07, -0.06, 0.34), (-0.07, -0.06 - 0.02, 0.34 - 0.17)], 0.011, 'steel_dark', seg=5)
cr.box((0.09, 0.07, 0.02), (0.11, -0.04, 0.51), 'black')
cr.box((0.09, 0.07, 0.02), (-0.11, -0.08, 0.17), 'black')
t((0.045, -0.06, 0.43), (0.045, RY, 0.36), 0.005, 'black', seg=3)
t((0.045, -0.06, 0.25), (0.045, RY, 0.30), 0.005, 'black', seg=3)
# rear rack / salvage basket + panniers
m.box((0.34, 0.42, 0.025), (0, RY - 0.03, 0.68), 'steel_dark', bevel=0.005)
for s in (-1, 1):
    t((s * 0.17, RY + 0.18, 0.68), (s * 0.17, RY - 0.24, 0.68), 0.008, 'steel', seg=4)
    t((s * 0.05, RY, HUB_Z), (s * 0.15, RY - 0.1, 0.67), 0.008, 'steel', seg=4)
    m.box((0.1, 0.30, 0.28), (s * 0.21, RY - 0.03, 0.52), 'cloth_red' if s > 0 else 'army_green', bevel=0.02, taper=(0.9, 0.9))
    m.box((0.11, 0.30, 0.05), (s * 0.21, RY - 0.03, 0.665), 'leather_dark', bevel=0.01)
# front salvage plate + light
m.box((0.1, 0.03, 0.08), (0, 0.43, 1.0), 'rust_dark', bevel=0.008)
for n, p in (('Seat', (0, -0.05, 1.3)), ('Exit_L', (-0.38, -0.05, 0)), ('Exit_R', (0.38, -0.05, 0))):
    m.part(n, pivot=p).box((0.01, 0.01, 0.01), (p[0], p[1], p[2] + (0.02 if p[2] == 0 else -0.02)), 'black')
finish(m, bounds={'x': (-0.4, 0.4), 'y': (-0.95, 0.95), 'z': (0, 1.3)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'piece_bicycle.blend'))
