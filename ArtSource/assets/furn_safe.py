import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('furn_safe', seed=313)
# carcass behind the door (door occupies y -0.27..-0.21)
m.box((0.6, 0.51, 0.66), (0, 0.045, 0.41), 'gunmetal', bevel=0.02)
m.box((0.5, 0.46, 0.06), (0, 0.05, 0.03), 'black')                 # plinth
for x in (-0.24, 0.24):
    for y in (-0.15, 0.25):
        m.box((0.08, 0.08, 0.03), (x, y, 0.015), 'rust_dark')
m.box((0.6, 0.6, 0.05), (0, 0.0, 0.77), 'steel_dark', bevel=0.012)  # top cap
# wear patches
m.box((0.006, 0.12, 0.08), (0.296, 0.05, 0.3), 'rust_dark')
m.box((0.006, 0.1, 0.12), (-0.296, 0.12, 0.2), 'rust')
m.box((0.12, 0.1, 0.004), (0.12, 0.05, 0.797), 'rust_dark', rot=(0, 0, 20))
m.box((0.2, 0.006, 0.2), (-0.1, 0.296, 0.45), 'rust_dark', rot=(0, 15, 0))
# door: x -0.3..0.3, z 0.06..0.76, y -0.27..-0.21
pv = (-0.3, -0.24, 0.41)
d = m.part('Door', pivot=pv)
d.box((0.6, 0.06, 0.7), (0, -0.24, 0.41), 'steel_dark', bevel=0.012)
d.box((0.5, 0.008, 0.6), (0, -0.274, 0.41), 'steel', bevel=0.01)       # raised panel
d.box((0.56, 0.008, 0.035), (0, -0.274, 0.69), 'steel_dark')
for sx in (-0.26, 0.26):
    for z in (0.1, 0.72):
        d.cyl(0.015, 0.01, (sx, -0.272, z), 'rust_light', rot=(90, 0, 0), seg=6)
# dial
d.cyl(0.095, 0.015, (0.0, -0.278, 0.5), 'gunmetal', rot=(90, 0, 0), seg=12)
d.cyl(0.065, 0.02, (0.0, -0.285, 0.5), 'chrome', rot=(90, 0, 0), seg=12)
d.cyl(0.02, 0.02, (0.0, -0.29, 0.5), 'black', rot=(90, 0, 0), seg=6)
d.box((0.012, 0.008, 0.045), (0.0, -0.294, 0.525), 'warning_red')
for k in range(8):
    a = k * math.pi / 4
    d.box((0.01, 0.006, 0.02), (0.08 * math.cos(a), -0.28, 0.5 + 0.08 * math.sin(a)), 'paint_white', rot=(0, -math.degrees(a) + 90, 0))
# handle T-bar
d.cyl(0.025, 0.02, (0.0, -0.283, 0.3), 'steel', rot=(90, 0, 0), seg=8)
d.box((0.22, 0.02, 0.035), (0.0, -0.288, 0.3), 'chrome', bevel=0.005)
d.box((0.035, 0.02, 0.22), (0.0, -0.288, 0.3), 'chrome', bevel=0.005)
# hinges (left edge)
for z in (0.2, 0.65):
    d.cyl(0.022, 0.1, (-0.275, -0.27, z), 'black', seg=6)
# scratches and dent
for (x, z, w, r) in [(0.1, 0.62, 0.18, 30), (-0.15, 0.4, 0.14, -20), (0.15, 0.18, 0.2, 70), (-0.1, 0.65, 0.1, 10)]:
    d.box((w, 0.004, 0.008), (x, -0.279, z), 'chrome', rot=(0, r, 0))
d.box((0.12, 0.008, 0.1), (0.2, -0.277, 0.38), 'gunmetal', rot=(0, 25, 0), taper=(0.7, 0.7))   # dent
d.box((0.1, 0.006, 0.08), (-0.2, -0.278, 0.12), 'rust_dark')
finish(m, bounds={'x': (-0.3, 0.3), 'y': (-0.3, 0.3), 'z': (0, 0.8)})
