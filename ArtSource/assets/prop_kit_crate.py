import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('prop_kit_crate', seed=72)
X, Y, Z = 1.08, 0.77, 0.7   # body; rope handles add 0.06 each end -> 1.2 total
hx, hy = X / 2, Y / 2
# carcass, flat bottom and top
m.box((X - 0.04, Y - 0.04, Z - 0.02), (0, 0, Z / 2), 'wood_dark')
# bottom skids (flush with the floor, 0.04 high) and top rails keep both faces flat
for y in (-0.3, 0, 0.3):
    m.box((X, 0.1, 0.04), (0, y, 0.02), 'wood_grey')
# planked sides: front (-Y) and back (+Y) boards
cols = ['army_green', 'army_green_dark', 'army_green', 'army_green_dark']
for i, c in enumerate(cols):
    z = 0.12 + i * 0.145
    m.box((X, 0.02, 0.14), (0, -hy + 0.01, z + 0.07 - 0.07 + 0.07), c)
    m.box((X, 0.02, 0.14), (0, hy - 0.01, z + 0.07), cols[(i + 1) % 4])
for i, c in enumerate(cols):
    z = 0.12 + i * 0.145
    m.box((0.02, Y - 0.04, 0.14), (-hx + 0.01, 0, z + 0.07), cols[(i + 2) % 4])
    m.box((0.02, Y - 0.04, 0.14), (hx - 0.01, 0, z + 0.07), cols[(i + 3) % 4])
# top lid: planks and perimeter frame, flat
m.box((X, 0.04, 0.04), (0, -hy + 0.02, Z - 0.02), 'wood')
m.box((X, 0.04, 0.04), (0, hy - 0.02, Z - 0.02), 'wood')
for i in range(5):
    m.box((0.2, Y - 0.08, 0.04), (-0.43 + i * 0.215, 0, Z - 0.02), 'wood_light' if i % 2 else 'wood')
# corner steel brackets (flush, within bounds)
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.07, 0.07, 0.6), (sx * (hx - 0.035), sy * (hy - 0.035), 0.37), 'steel_dark')
# stencils on front (-Y): blank bars, arrows, hazard stripe
m.box((0.5, 0.006, 0.2), (-0.2, -hy - 0.003, 0.36), 'paint_white')
for i, w in enumerate((0.38, 0.3, 0.2)):
    m.box((w, 0.007, 0.025), (-0.2 - (0.38 - w) / 2 + 0.0, -hy - 0.0065, 0.42 - i * 0.055), 'black')
m.box((0.12, 0.007, 0.05), (0.2, -hy - 0.0065, 0.46), 'black')
m.box((0.26, 0.006, 0.1), (0.3, -hy - 0.003, 0.2), 'hazard_yellow')
for i in range(4):
    m.box((0.035, 0.008, 0.11), (0.2 + i * 0.07, -hy - 0.0065, 0.2), 'black', rot=(0, 30, 0))
# end-face arrows (up arrow)
for sx in (-1, 1):
    m.box((0.05, 0.1, 0.18) if False else (0.006, 0.05, 0.14), (sx * (hx + 0.003), 0, 0.19), 'paint_white')
    m.box((0.006, 0.14, 0.04), (sx * (hx + 0.003), 0, 0.28), 'paint_white', rot=(0, 0, 0))
# rock-simple wear patches
m.box((0.2, 0.006, 0.07), (0.45, hy + 0.003, 0.3), 'rust')
m.box((0.006, 0.18, 0.06), (-hx - 0.003, -0.2, 0.15), 'rust_dark')
# rope handles on end faces (recessed grips), on steel eyelets
for sx in (-1, 1):
    for y in (-0.15, 0.15):
        m.box((0.02, 0.05, 0.04), (sx * (hx + 0.005), y, 0.42), 'steel')
    m.tube([(sx * (hx + 0.008), -0.15, 0.42), (sx * (hx + 0.045), -0.1, 0.36), (sx * (hx + 0.045), 0.1, 0.36), (sx * (hx + 0.008), 0.15, 0.42)], 0.012, 'rope', seg=4)
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.4, 0.4), 'z': (0, 0.7)})
