import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('camp_tent', seed=701)
H, HW, L = 2.05, 1.32, 3.5          # ridge height, half base width, canvas length
sl = math.degrees(math.atan2(HW, H)); pl = math.hypot(HW, H)
cols = {1: 'army_green_dark', -1: 'army_green'}
for s in (-1, 1):
    m.box((0.05, L, pl), (s * HW / 2, 0, H / 2 + 0.02), cols[s], rot=(0, s * -sl, 0), bevel=0.01)
    # patches (slightly proud of the outer face)
    nx, nz = math.cos(math.radians(sl)), math.sin(math.radians(sl))
    for (y, t, w, h, c) in [(-0.9, 0.4, 0.8, 0.7, 'leather'), (0.7, 0.6, 0.9, 0.6, 'cloth_dark'), (1.2, 0.3, 0.6, 0.6, 'fabric_stain'), (-0.2, 0.75, 0.5, 0.5, 'cloth_red')]:
        px = s * (HW / 2 + (t - 0.5) * -0.0) 
        cx = s * (HW * (1 - t / pl * 0.0)) 
        # point on panel at fraction t along slope from the ground edge
        gx = s * (HW - HW * t / pl); gz = H * t / pl
        nn = math.hypot(H, HW); m.box((0.02, w, h), (gx + s * 0.034 * H / nn, y, gz + 0.034 * HW / nn + 0.02), c, rot=(0, s * -sl, 0))
    # rope lashings across the slope
    for y in (-1.5, -0.3, 0.9, 1.6):
        a = (s * HW * 0.98, y, 0.08); b = (s * 0.05, y, H - 0.03)
        m.tube([(s * (HW + 0.04), y, 0.06), (s * (HW * 0.5 + 0.04), y + 0.02, H * 0.5), (s * 0.06, y, H + 0.0)], 0.014, 'rope', seg=4)
# back wall (closed), triangle
m.poly([(-HW, 0), (HW, 0), (0, H - 0.02)], 0.05, (0, L / 2, 0.012), 'army_green_dark')
# front: pole frame and rolled-back flaps, keep |x|<0.5, z<1.3 free
m.cyl(0.04, L + 0.02, (0, 0, H - 0.04), 'wood_dark', rot=(90, 0, 0), seg=6)
m.box((0.04, 0.3, 0.2), (0.0, -L / 2 + 0.2, H - 0.1), 'cloth_red', rot=(0, 0, 0), taper=(0.6, 1))
m.box((0.03, 0.12, 0.3), (0.0, -L / 2 + 0.16, H - 0.28), 'cloth_red', rot=(10, 0, 0))
for s in (-1, 1):
    m.poly([(0, 0), (0.5, 0), (0.3, 1.0), (0.0, 1.0)], 0.04, (s * 0.85, -L / 2 + 0.02, 0.1), 'army_green') if False else None
    # tied-back flap bundles at the front edge
    m.box((0.14, 0.14, 1.5), (s * (0.78), -L / 2 + 0.05, 0.9), 'army_green_dark', rot=(0, s * -sl * 0.5, 0), taper=(0.7, 0.7))
    m.tube([(s * 0.7, -L / 2 + 0.05, 1.45), (s * 0.78, -L / 2 + 0.03, 1.0), (s * 0.85, -L / 2 + 0.05, 0.55)], 0.012, 'rope', seg=4)
    # stakes with rope loops
    for y in (-1.3, 0.2, 1.4):
        m.cyl(0.035, 0.34, (s * 1.33, y, 0.2), 'wood', rot=(0, s * 12, 0), seg=5, r2=0.015)
        m.tube([(s * 1.2, y, 0.1), (s * 1.28, y + 0.03, 0.16), (s * 1.33, y, 0.26)], 0.012, 'rope', seg=4)
# frayed rope ends
for (x, y) in [(1.3, -1.3), (-1.3, 1.4)]:
    m.tube([(x * 0.98, y, 0.26), (x * 0.95, y - 0.1, 0.06), (x * 0.9, y - 0.25, 0.03)], 0.01, 'rope', seg=3)
# ground cloth + bedroll inside (deep, away from the entrance)
m.box((1.7, 2.3, 0.02), (0, 0.5, 0.011), 'cloth_dark')
m.box((0.7, 1.9, 0.14), (0.15, 0.55, 0.09), 'fabric_stain', bevel=0.05)
m.box((0.72, 0.5, 0.12), (0.15, 1.35, 0.2), 'fabric_white', bevel=0.04)
m.box((0.5, 1.2, 0.03), (0.15, 0.2, 0.17), 'cloth_red', rot=(0, 0, 3))
m.cyl(0.14, 0.7, (-0.6, 1.4, 0.15), 'army_green_dark', rot=(0, 90, 0), seg=8)   # rolled bag
# junk at the corners, outside the opening
m.box((0.45, 0.35, 0.3), (-0.95, -1.5, 0.15), 'wood_grey', bevel=0.015, rot=(0, 0, 14))
m.box((0.47, 0.37, 0.03), (-0.95, -1.5, 0.31), 'wood_dark', rot=(0, 0, 14))
m.cyl(0.16, 0.3, (0.98, -1.5, 0.15), 'rust', seg=8)
m.cyl(0.14, 0.02, (0.98, -1.5, 0.3), 'black', seg=8)
for i, (x, y) in enumerate([(1.0, -1.15), (1.12, -1.2), (0.92, -1.3)]):
    m.cyl(0.035, 0.1, (x, y, 0.05), ['rust_light', 'steel', 'rust'][i], seg=6)
m.cyl(0.035, 0.1, (-0.45, 1.55, 0.05), 'steel', seg=6)
m.blob((0.3, 0.2, 0.06), (0.1, -1.65, 0.05), 'dirt', colors=['dirt', 'wood_dark'])   # trampled mud mat
finish(m, bounds={'x': (-1.4, 1.4), 'y': (-1.8, 1.8), 'z': (0, 2.1)})
