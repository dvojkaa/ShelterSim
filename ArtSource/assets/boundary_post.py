import math
from ss_art import Model, finish
m = Model('boundary_post', seed=31)

# Concrete footing with a broken corner
m.box((0.5, 0.5, 0.18), (0, 0, 0.09), 'concrete', bevel=0.02)
m.box((0.34, 0.34, 0.14), (0, 0, 0.25), 'concrete_light', taper=(0.72, 0.72))
m.box((0.2, 0.02, 0.09), (0.12, -0.255, 0.05), 'concrete_dark')
m.box((0.14, 0.02, 0.08), (-0.14, 0.255, 0.06), 'concrete_dark')

# I-beam post: real profile rather than a stick, so it catches light as it should
m.box((0.17, 0.03, 2.86), (0, -0.065, 1.72), 'rust')
m.box((0.17, 0.03, 2.86), (0, 0.065, 1.72), 'rust')
m.box((0.04, 0.12, 2.86), (0, 0, 1.72), 'rust_dark')
# Rust banding and old paint scraps
for z, c in ((0.58, 'rust_dark'), (1.1, 'rust_light'), (2.3, 'rust_dark'), (2.76, 'rust_light')):
    m.box((0.176, 0.008, 0.14 + (z * 7 % 3) * 0.04), (0, -0.082, z), c)
m.box((0.176, 0.008, 0.2), (0, 0.082, 0.92), 'rust_light')
m.box((0.176, 0.008, 0.12), (0, 0.082, 2.12), 'army_green_dark')
# Base collar
m.box((0.24, 0.24, 0.05), (0, 0, 0.34), 'steel_dark')
for x, y in ((-0.09, -0.09), (0.09, -0.09), (-0.09, 0.09), (0.09, 0.09)):
    m.cyl(0.014, 0.03, (x, y, 0.37), 'gunmetal', seg=6)

# Y-arm at the top: arms slant out over both faces
m.box((0.2, 0.2, 0.06), (0, 0, 3.17), 'steel_dark')
m.tube([(-0.03, 0.0, 3.17), (-0.03, -0.258, 3.5)], 0.03, 'rust_dark', seg=6)
m.tube([(0.03, 0.0, 3.17), (0.03, 0.258, 3.5)], 0.03, 'rust_dark', seg=6)
m.box((0.05, 0.02, 0.5), (0, -0.09, 3.0), 'rust_dark', rot=(20, 0, 0))
m.box((0.05, 0.02, 0.5), (0, 0.09, 3.0), 'rust_dark', rot=(-20, 0, 0))

# Barbed wire strands run along X through the arms; they reach the x bounds so post-to-post they suggest a fence.
STRANDS = [(-0.15, 3.38), (0.15, 3.38)]
for y, z in STRANDS:
    m.box((0.6, 0.014, 0.014), (0, y, z), 'steel_dark')
    for i, x in enumerate((-0.2, 0.0, 0.2)):
        lean = 1 if i % 2 else -1
        m.box((0.018, 0.07, 0.018), (x, y, z), 'steel', rot=(lean * 42, 0, lean * 28))

# Razor-wire coil hung off each arm tip: a loose helix along X, what actually says "barbed wire" at distance.
for sy in (-1, 1):
    pts = []
    n = 36
    for i in range(n + 1):
        t = i / n
        a = t * 2 * math.pi * 6
        pts.append((-0.285 + 0.57 * t, sy * 0.21 + 0.075 * math.cos(a), 3.46 + 0.075 * math.sin(a)))
    m.tube(pts, 0.01, 'steel', seg=3)

# Hazard diamond sign (blank), bolted to the -Y flange on a backing plate
SZ, SY = 1.88, -0.094
DI = 0.295
# Bracket that actually carries the sign off the flange
m.box((0.1, 0.016, 0.42), (0, -0.086, SZ), 'gunmetal')
m.poly([(0, SZ - DI), (DI, SZ), (0, SZ + DI), (-DI, SZ)], 0.012, (0, SY, 0), 'steel_dark')
D2 = 0.262
m.poly([(0, SZ - D2), (D2, SZ), (0, SZ + D2), (-D2, SZ)], 0.008, (0, SY - 0.008, 0), 'black')
D3 = 0.225
m.poly([(0, SZ - D3), (D3, SZ), (0, SZ + D3), (-D3, SZ)], 0.006, (0, SY - 0.012, 0), 'hazard_yellow')
# Rust chewed the lower corner of the paint; two bolts hold it
m.poly([(0, SZ - D3), (0.09, SZ - D3 + 0.09), (0.03, SZ - D3 + 0.12), (-0.05, SZ - D3 + 0.07), (-0.06, SZ - D3 + 0.06)],
       0.003, (0, SY - 0.0155, 0), 'rust')
m.poly([(0.1, SZ + 0.05), (0.13, SZ + 0.065), (0.08, SZ + 0.115), (0.05, SZ + 0.09)], 0.003, (0, SY - 0.0155, 0), 'rust_dark')
for dz in (-0.13, 0.13):
    m.cyl(0.016, 0.012, (0, SY - 0.018, SZ + dz), 'gunmetal', rot=(90, 0, 0), seg=6)

finish(m, bounds={'x': (-0.3, 0.3), 'y': (-0.3, 0.3), 'z': (0, 3.6)})
