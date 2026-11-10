import os, sys, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_mail_lockers', seed=93)
r = random.Random(9301)
Z0, RH, CW = 0.42, 0.32, 0.72      # cabinet bottom, row height, column width
FY = -0.12                          # front face y; back at +0.2
# floor stand with feet, legs and a cross brace
m.box((1.6, 0.36, 0.05), (0, 0.02, Z0 - 0.025), 'steel_dark')
for sx in (-0.72, 0.72):
    for y in (-0.12, 0.14):
        m.box((0.06, 0.06, Z0 - 0.05), (sx, y, (Z0 - 0.05) / 2), 'gunmetal')
    m.box((0.06, 0.3, 0.03), (sx, 0.01, 0.02), 'gunmetal')
m.box((1.4, 0.03, 0.05), (0, 0.14, 0.25), 'rust_dark')
# carcass: back panel, sides, top, dividers
m.box((1.56, 0.04, RH * 4 + 0.08), (0, 0.18, Z0 + RH * 2), 'steel_dark')
m.box((0.04, 0.32, RH * 4 + 0.08), (-0.78, 0.02, Z0 + RH * 2), 'army_green_dark')
m.box((0.04, 0.32, RH * 4 + 0.08), (0.78, 0.02, Z0 + RH * 2), 'army_green_dark')
m.box((1.6, 0.36, 0.05), (0, 0.02, Z0 + RH * 4 + 0.045), 'army_green_dark', bevel=0.01)
m.box((0.03, 0.3, RH * 4), (0, 0.04, Z0 + RH * 2), 'army_green_dark')
for k in range(1, 4):
    m.box((1.52, 0.3, 0.025), (0, 0.04, Z0 + k * RH), 'army_green_dark')
cols = ['army_green', 'steel', 'army_green', 'mustard_dark']
MOV = {(3, 0): 'Door_1', (1, 1): 'Door_2'}   # (row from bottom, col): top row left, second-from-bottom right
for row in range(4):
    for c in range(2):
        cx = -0.37 + c * 0.74
        zb = Z0 + row * RH + 0.012
        dh, dw = RH - 0.024, CW - 0.06
        zc = zb + dh / 2
        col = r.choice(('army_green', 'army_green', 'steel', 'army_green_dark', 'mustard_dark'))
        parts = MOV.get((row, c))
        tgt = m.part(parts, pivot=(cx, FY, zb)) if parts else m
        if parts:
            # dark recess behind the movable door so the opening reads
            m.box((dw - 0.04, 0.22, dh - 0.04), (cx, 0.04, zc), 'black')
        tgt.box((dw, 0.02, dh), (cx, FY - 0.01, zc), col, bevel=0.005)
        tgt.box((0.2, 0.008, 0.05), (cx - 0.1 * (1 if (row + c) % 2 else -1), FY - 0.024, zc + dh / 2 - 0.06), 'paint_white')  # blank number plate
        tgt.box((0.14, 0.008, 0.015), (cx, FY - 0.024, zc - 0.02), 'black')     # slot
        tgt.box((0.03, 0.02, 0.03), (cx + dw / 2 - 0.05, FY - 0.03, zc - dh / 2 + 0.06), 'chrome')  # lock
        if r.random() < 0.6:   # dent: slightly angled darker plate
            tgt.box((0.12, 0.006, 0.08), (cx + r.uniform(-0.15, 0.15), FY - 0.023, zc + r.uniform(-0.02, 0.0)), 'rust_dark', rot=(0, 15, 0))
        if r.random() < 0.5:
            tgt.box((0.2, 0.006, 0.03), (cx + r.uniform(-0.1, 0.1), FY - 0.023, zb + 0.04), 'rust')
        if r.random() < 0.3:
            tgt.box((0.2, 0.006, 0.04), (cx + 0.05, FY - 0.023, zc), 'concrete_dark')
finish(m, bounds={'x': (-0.8, 0.8), 'y': (-0.2, 0.2), 'z': (0, 1.8)})
