import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_wardrobe', seed=103)
FY = -0.25   # carcass front; doors -0.29..-0.26; knobs/panels to -0.30
# plinth, carcass, cornice
m.box((1.38, 0.52, 0.1), (0, 0.04, 0.05), 'wood_dark')
m.box((1.4, 0.56, 1.9), (0, 0.02, 1.05), 'wood_dark', bevel=0.01)
m.box((1.4, 0.6, 0.1), (0, 0.0, 2.05), 'wood', bevel=0.012)
m.box((1.3, 0.5, 0.02), (0, 0.0, 2.1), 'wood_dark') if False else None
m.box((1.0, 0.03, 0.04), (0, 0.29, 2.0), 'wood_dark') if False else None
# centre stile + inside gloom behind the gap
m.box((0.03, 0.02, 1.9), (0, FY - 0.001, 1.05), 'black')
# worn feet
for x in (-0.62, 0.62):
    m.box((0.12, 0.12, 0.04), (x, -0.2, 0.02), 'gunmetal')
    m.box((0.12, 0.12, 0.04), (x, 0.2, 0.02), 'gunmetal')

def door(name, hinge_x, sign, sag_deg, panel_col, hz=1.05):
    p = m.part(name, pivot=(hinge_x, FY - 0.015, hz))
    a = math.radians(sag_deg)
    def B(size, loc, col, bevel=0.0):
        # rotate loc about the hinge in the XZ plane
        dx, dz = loc[0] - hinge_x, loc[2] - hz
        nx = hinge_x + dx * math.cos(a) - dz * math.sin(a)
        nz = hz + dx * math.sin(a) + dz * math.cos(a)
        p.box(size, (nx, loc[1], nz), col, rot=(0, sag_deg, 0), bevel=bevel)
    w = 0.69
    cx = hinge_x + sign * (w / 2 + 0.003)
    y = FY - 0.015
    B((w, 0.03, 1.84), (cx, y, 1.05), 'wood', 0.008)
    # raised panels upper/lower
    B((w - 0.16, 0.012, 0.9), (cx, FY - 0.03, 1.4), panel_col, 0.006)
    B((w - 0.16, 0.012, 0.5), (cx, FY - 0.03, 0.55), panel_col, 0.006)
    # knob + backplate on the inner edge
    kx = hinge_x + sign * (w - 0.07)
    B((0.05, 0.012, 0.16), (kx, FY - 0.033, 1.05), 'steel_dark')
    B((0.035, 0.03, 0.12), (kx, FY - 0.05 + 0.02, 1.05), 'chrome') if False else None
    B((0.03, 0.025, 0.12), (kx, FY - 0.0375, 1.05), 'chrome', 0.005)
    return p, B
# Door_L straight, Door_R sagging ~2 deg (free edge drops)
dl, BL = door('Door_L', -0.7, +1, 0.0, 'wood_light')
dr, BR = door('Door_R', 0.7, -1, -1.8, 'wood_light', hz=1.95)
# warped damage: split lower corner of right door, glass-like mirror strip on left
BL((0.3, 0.008, 0.5), (-0.5, FY - 0.0375, 1.5), 'glass_dirty')
BL((0.18, 0.006, 0.18), (-0.25, FY - 0.0345, 0.25), 'wood_dark')
BR((0.2, 0.006, 0.3), (0.35, FY - 0.0355, 0.4), 'fabric_stain')
BR((0.07, 0.006, 0.4), (0.12, FY - 0.0345, 0.35), 'wood_dark')
dl.box((0.3, 0.007, 0.04), (-0.55, FY - 0.0345, 0.14), 'rust_dark') if False else None
finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.3, 0.3), 'z': (0, 2.1)})
