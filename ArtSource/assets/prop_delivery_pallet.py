import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('prop_delivery_pallet', seed=71)
D = 0.15      # pallet deck top
# --- wooden pallet: 3 stringers, bottom and top deck boards
for x in (-0.54, 0, 0.54):
    m.box((0.12, 1.16, 0.09), (x, 0, 0.065), 'wood_dark')
for y, c in ((-0.5, 'wood_grey'), (0, 'wood'), (0.5, 'wood_grey')):
    m.box((1.2, 0.14, 0.025), (0, y, 0.0125), c)
for i in range(5):
    m.box((1.2, 0.2, 0.03), (0, -0.5 + i * 0.25, 0.135), 'wood_light' if i % 2 else 'wood')
# --- cardboard stack (left half): two boxes at the bottom, a third on top
CX = -0.3
for cy, c in ((-0.28, 'wood_light'), (0.28, 'wood')):
    m.box((0.54, 0.52, 0.42), (CX, cy, D + 0.21), c, bevel=0.015)
    m.box((0.07, 0.53, 0.01), (CX, cy, D + 0.424), 'sand')
    m.box((0.5, 0.5, 0.38), (CX, cy, D + 0.61), 'wood' if c == 'wood_light' else 'wood_light', bevel=0.015)
    m.box((0.07, 0.51, 0.01), (CX, cy, D + 0.804), 'sand')
m.box((0.46, 0.8, 0.34), (CX, 0, D + 0.97), 'wood_light', bevel=0.015)
m.box((0.07, 0.81, 0.01), (CX, 0, D + 1.144), 'sand')
# blank stencil marks on front (-Y) faces: bars, arrow block, blank plate
def stencil(x, y, z):
    m.box((0.2, 0.006, 0.03), (x, y, z + 0.05), 'black')
    m.box((0.12, 0.006, 0.03), (x - 0.04, y, z), 'black')
    m.box((0.05, 0.006, 0.05), (x + 0.1, y, z), 'hazard_yellow')
stencil(CX, -0.543, D + 0.2)
stencil(CX, -0.531, D + 0.6)
m.box((0.12, 0.006, 0.12), (CX - 0.18, -0.543, D + 0.33), 'paint_white')
m.box((0.05, 0.007, 0.05), (CX - 0.18, -0.544, D + 0.33), 'black')
# --- steel drum (right half)
dx, dy = 0.3, 0.0
m.cyl(0.27, 0.9, (dx, dy, D + 0.45), 'army_green', seg=10)
for z in (0.3, 0.6):
    m.cyl(0.285, 0.04, (dx, dy, D + z), 'army_green_dark', seg=10)
m.cyl(0.285, 0.03, (dx, dy, D + 0.015), 'steel_dark', seg=10)
m.cyl(0.285, 0.03, (dx, dy, D + 0.885), 'steel_dark', seg=10)
m.cyl(0.045, 0.04, (dx + 0.12, dy + 0.08, D + 0.92), 'steel', seg=6)
m.cyl(0.035, 0.03, (dx - 0.12, dy - 0.05, D + 0.915), 'rust', seg=6)
m.box((0.12, 0.02, 0.16), (dx, -0.265, D + 0.5), 'hazard_yellow')
m.box((0.06, 0.022, 0.06), (dx, -0.268, D + 0.5), 'black')
m.box((0.1, 0.03, 0.05), (dx + 0.06, -0.26, D + 0.18), 'rust')
# --- shrink-wrap bands (pale) around the stack and straps
for z in (D + 0.3, D + 0.7):
    m.box((0.57, 0.02, 0.05), (CX, -0.572, z), 'fabric_white')
    m.box((0.57, 0.02, 0.05), (CX, 0.572, z), 'fabric_white')
    m.box((0.02, 1.14, 0.05), (-0.588, 0, z), 'fabric_white')
# black straps over the top and down the front/back of the stack
for x in (CX - 0.15, CX + 0.12):
    m.box((0.05, 0.84, 0.015), (x, 0, D + 1.148), 'black')
m.cyl(0.288, 0.02, (dx, dy, D + 0.75), 'black', seg=10)   # drum strap
m.box((0.06, 0.03, 0.06), (dx, -0.285, D + 0.75), 'steel')
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.6, 0.6), 'z': (0, 1.4)})
