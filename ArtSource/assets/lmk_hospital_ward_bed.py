import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lmk_hospital_ward_bed', seed=1107)
# legs with castors
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.06, 0.06, 0.4), (sx * 0.7, sy * 1.1, 0.3), 'steel')
        m.cyl(0.07, 0.05, (sx * 0.7, sy * 1.1, 0.07), 'rubber', rot=(0, 90, 0), seg=8)
m.box((1.5, 2.3, 0.08), (0, 0, 0.5), 'steel_dark', bevel=0.01)           # frame
m.box((1.4, 2.2, 0.16), (0, 0, 0.62), 'fabric_white', bevel=0.04)         # mattress
m.box((1.0, 1.2, 0.05), (0.1, 0.25, 0.71), 'fabric_stain', bevel=0.02, rot=(0, 0, 8))   # sheet
m.box((0.8, 0.5, 0.08), (0.0, 0.1, 0.74), 'fabric_stain', rot=(0, 0, -6)) if False else None
m.box((0.5, 0.35, 0.12), (-0.15, 0.95, 0.76), 'fabric_white', bevel=0.04)  # pillow (head at +Y)
m.box((0.3, 0.4, 0.02), (0.35, -0.1, 0.735), 'rust_dark', rot=(0, 0, 20))  # blood-ish stain
m.box((1.45, 1.2, 0.07), (0, -0.35, 0.72), 'fabric_blue', bevel=0.02)      # blanket folded at foot
# head/foot boards
m.box((1.5, 0.06, 0.6), (0, 1.17, 0.8), 'steel_dark', bevel=0.01)
m.box((1.3, 0.04, 0.4), (0, 1.2, 0.85), 'paint_white')
m.box((1.5, 0.06, 0.5), (0, -1.17, 0.7), 'steel_dark', bevel=0.01)
m.box((1.3, 0.04, 0.3), (0, -1.2, 0.7), 'paint_white')
# side rails with posts
for sx in (-1, 1):
    for y in (-0.9, 0.0, 0.9):
        m.box((0.04, 0.04, 0.4), (sx * 0.78, y, 0.88), 'steel')
    m.box((0.04, 2.0, 0.04), (sx * 0.78, 0.0, 1.05), 'chrome')
    m.box((0.04, 2.0, 0.04), (sx * 0.78, 0.0, 0.86), 'steel')
# IV stand at the head, +x side
m.cyl(0.02, 0.9, (0.8, 1.05, 0.5), 'chrome', seg=6)
m.box((0.5, 0.04, 0.04), (0.85, 1.05, 1.08), 'steel') if False else None
m.cyl(0.07, 0.2, (0.8, 1.05, 0.88), 'glass_dirty', seg=8)
finish(m, bounds={'x': (-0.85, 0.9), 'y': (-1.25, 1.25), 'z': (0, 1.1)})
