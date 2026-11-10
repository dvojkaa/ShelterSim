"""Wall-mounted pegboard with hanging tools. Origin at wall surface, floor. Wall at y=0, protrudes to -Y (<=0.15)."""
import math
from ss_art import Model, finish
m = Model('prop_tool_wall', seed=63)

# Board and frame
BZ0, BZ1 = 0.14, 1.56
m.box((1.96, 0.03, BZ1 - BZ0), (0, -0.0165, (BZ0 + BZ1) / 2), 'wood_dark')
for x in (-0.98, 0.98):
    m.box((0.04, 0.05, BZ1 - BZ0 + 0.04), (x*0.99, -0.0265, (BZ0 + BZ1) / 2), 'steel_dark')
for z in (BZ0 - 0.01, BZ1 + 0.01):
    m.box((1.98, 0.05, 0.05), (0, -0.0265, z), 'steel_dark')
# Weathered boards, stains
m.box((0.5, 0.004, 0.2), (-0.65, -0.032, 1.25), 'wood')
m.box((0.35, 0.004, 0.12), (0.7, -0.032, 0.62), 'rust_dark')
# Tool silhouettes outlines (darker shadow plates drawn behind)

def poly(pts, y, c, t=0.012, dx=0, dz=0, rot=0):
    m.poly(pts, t, (dx, y, dz), c, rot=(90, rot, 0))

def peg(x, z, l=0.05):
    m.cyl(0.006, l, (x, -0.03 - l / 2, z), 'chrome', rot=(90, 0, 0), seg=4)

def wrench(x, z, L, c, rot=0, open_end=True):
    """Spanner: long handle with a jaw head. Local frame hangs from top; rotates about (x,z)."""
    a = math.radians(rot)
    def T(px, pz):
        return (x + px * math.cos(a) + pz * math.sin(a), z - px * math.sin(a) + pz * math.cos(a))
    # handle
    hx, hz = T(0, -L / 2)
    m.box((0.035, 0.014, L), (hx, -0.045, hz), c, rot=(0, rot, 0))
    # head: jaw block + notch
    jx, jz = T(0, -L - 0.01)
    m.box((0.085, 0.014, 0.075), (jx, -0.045, jz), c, rot=(0, rot, 0))
    nx, nz = T(0, -L - 0.035)
    m.box((0.032, 0.018, 0.05), (nx, -0.045, nz), 'black', rot=(0, rot, 0))
    # hang hole
    px, pz = T(0, -0.018)
    m.cyl(0.011, 0.018, (px, -0.045, pz), 'black', rot=(90, 0, 0), seg=6)
    peg(*T(0, -0.018))

# Row 1 (top): wrenches by size, hanging from pegs
for i, (x, L) in enumerate([(-0.88, 0.42), (-0.80, 0.50), (-0.72, 0.58), (-0.64, 0.66)]):
    wrench(x, 1.50, L, 'steel' if i % 2 else 'chrome')
# Large pipe wrench
m.box((0.045, 0.016, 0.62), (-0.50, -0.046, 1.20), 'rust_light')
m.box((0.11, 0.016, 0.10), (-0.50, -0.046, 0.88), 'rust')
m.box((0.05, 0.02, 0.06), (-0.50, -0.048, 0.855), 'black')
peg(-0.50, 1.49)

# Handsaw (tooth edge down): blade poly + wooden handle
saw = [(-0.30, -0.12), (0.30, -0.02), (0.30, 0.14), (-0.30, 0.14)]
m.poly([(-0.32, -0.04), (0.28, -0.14), (0.28, 0.04), (-0.32, 0.04)], 0.008, (0.15, -0.045, 1.30), 'steel')
for k in range(10):
    zz = 1.256 - (k * 0.01)
    m.box((0.04, 0.01, 0.018), (-0.14 + k * 0.058, -0.046, 1.27 - k * 0.0095), 'steel_dark', rot=(0, 45, 0))
m.poly([(-0.07, -0.06), (0.07, -0.06), (0.08, 0.05), (-0.08, 0.04), (-0.12, 0.0)], 0.02, (-0.20, -0.048, 1.31), 'wood')
m.cyl(0.012, 0.03, (-0.20, -0.054, 1.31), 'black', rot=(90, 0, 0), seg=5)
peg(0.30, 1.48)
peg(-0.20, 1.45)

# Hammers on pegs (right-top), claw and sledge
def hammer(x, z, L, head_w, c, handle='wood'):
    m.box((0.035, 0.03, L), (x, -0.05, z - L / 2), handle)
    m.box((head_w, 0.04, 0.06), (x, -0.05, z - 0.035), c)
    m.box((0.02, 0.025, 0.05), (x - head_w / 2 - 0.02, -0.05, z - 0.03), c, rot=(0, 0, 20))   # claw
    m.box((0.02, 0.025, 0.05), (x + head_w / 2 + 0.015, -0.05, z - 0.03), 'steel_dark')
    peg(x, z - L + 0.03, 0.08)
    peg(x + 0.08, z - 0.01, 0.06)

hammer(0.58, 1.50, 0.40, 0.14, 'steel_dark')
m.box((0.05, 0.04, 0.55), (0.82, -0.055, 1.20), 'wood_dark')   # sledge
m.box((0.14, 0.07, 0.10), (0.82, -0.058, 1.52 - 0.0 - 0.15), 'gunmetal')
peg(0.82, 0.93, 0.08)
# Screwdrivers on a rail
m.box((0.5, 0.05, 0.03), (-0.05, -0.04, 0.95), 'steel_dark')
for i, c in enumerate(['warning_red', 'mustard', 'plastic_blue', 'orange', 'army_green']):
    x = -0.25 + i * 0.1
    m.box((0.032, 0.03, 0.10), (x, -0.062, 0.99), c)
    m.box((0.01, 0.01, 0.14), (x, -0.062, 0.87), 'chrome')

# Clamp (C-clamp) on pegs, centre
cx, cz = 0.30, 0.88
m.tube([(cx - 0.07, -0.05, cz + 0.1), (cx - 0.07, -0.05, cz - 0.1), (cx + 0.07, -0.05, cz - 0.1), (cx + 0.07, -0.05, cz + 0.1)], 0.012, 'steel_dark', seg=4)
m.tube([(cx - 0.07, -0.05, cz + 0.04), (cx + 0.02, -0.05, cz + 0.04)], 0.009, 'chrome', seg=4)
m.box((0.012, 0.012, 0.06), (cx - 0.095, -0.05, cz + 0.04), 'rust')
m.box((0.05, 0.02, 0.012), (cx + 0.03, -0.05, cz + 0.04), 'chrome')
# Bar clamp (long)
m.box((0.8, 0.02, 0.025), (0.45, -0.05, 0.69 - 0.12), 'steel')
m.box((0.03, 0.03, 0.1), (0.07, -0.052, 0.57), 'warning_red')
m.box((0.03, 0.03, 0.1), (0.62, -0.052, 0.57), 'warning_red')
m.box((0.12, 0.02, 0.03), (0.45, -0.052, 0.455), 'leather_dark')

# Pliers and hand drill hooks left side
for i, (x, c) in enumerate([(-0.88, 'warning_red'), (-0.80, 'mustard')]):
    m.box((0.02, 0.02, 0.16), (x - 0.01, -0.05, 0.95), 'steel_dark', rot=(0, 5, 0))
    m.box((0.02, 0.02, 0.16), (x + 0.012, -0.05, 0.95), 'steel_dark', rot=(0, -5, 0))
    m.box((0.026, 0.03, 0.09), (x - 0.012, -0.055, 0.86), c)
    m.box((0.026, 0.03, 0.09), (x + 0.012, -0.055, 0.86), c)
    peg(x, 1.04)
# Tape and coil of rope
m.cyl(0.07, 0.04, (-0.68, -0.05, 0.98), 'leather', rot=(90, 0, 0), seg=10)
m.cyl(0.035, 0.044, (-0.68, -0.05, 0.98), 'black', rot=(90, 0, 0), seg=8)
m.cyl(0.09, 0.05, (-0.40, -0.06, 0.98), 'rope', rot=(90, 0, 0), seg=10)

# Lower wall: crowbar, axe, mallet
m.tube([(0.50, -0.05, 0.50), (0.50, -0.05, 0.18), (0.56, -0.05, 0.14), (0.60, -0.05, 0.19)], 0.014, 'rust_light', seg=4)
m.box((0.04, 0.03, 0.5), (0.80, -0.05, 0.30), 'wood_light')
m.poly([(0.0, 0.0), (0.16, 0.06), (0.16, -0.12), (0.0, -0.08)], 0.02, (0.80, -0.05, 0.50), 'steel')
m.box((0.05, 0.035, 0.30), (0.20, -0.05, 0.32), 'wood')
m.box((0.16, 0.06, 0.09), (0.20, -0.055, 0.50), 'wood_dark')
m.box((0.014, 0.014, 0.12), (0.935, -0.05, 0.55), 'rope')
# Shelf with vice
SZ = 0.60
m.box((1.0, 0.14, 0.04), (-0.2, -0.07, SZ), 'wood')
m.box((1.0, 0.012, 0.05), (-0.2, -0.141, SZ - 0.01), 'steel_dark')
m.box((1.0, 0.012, 0.03), (-0.2, -0.0075, SZ + 0.035), 'wood_dark')
for x in (-0.6, 0.2):
    m.box((0.04, 0.12, 0.03), (x, -0.07, SZ - 0.035), 'gunmetal')
    m.tube([(x, -0.02, SZ - 0.2), (x, -0.11, SZ - 0.04)], 0.012, 'gunmetal', seg=4)
# Vice on the shelf
vx = -0.55
m.box((0.20, 0.10, 0.07), (vx, -0.07, SZ + 0.055), 'army_green_dark')       # base
m.box((0.18, 0.04, 0.10), (vx, -0.035, SZ + 0.15), 'army_green')            # fixed jaw
m.box((0.14, 0.03, 0.10), (vx, -0.13, SZ + 0.15), 'army_green')             # moving jaw
m.box((0.12, 0.007, 0.06), (vx, -0.0575, SZ + 0.155), 'steel_dark')
# Small tool bits on shelf
m.box((0.20, 0.09, 0.05), (-0.20, -0.08, SZ + 0.045), 'steel_dark')       # tin
m.box((0.21, 0.095, 0.012), (-0.20, -0.08, SZ + 0.075), 'hazard_yellow')
for i in range(4):
    m.cyl(0.02, 0.07, (-0.05 + i * 0.05, -0.07, SZ + 0.055), 'rust' if i % 2 else 'steel', seg=5)
m.box((0.14, 0.06, 0.04), (0.05, -0.07, SZ + 0.04), 'wood_dark')
# Shelf tools: drill hanging below the shelf  (sander block) and a mallet
finish(m, bounds={'x': (-1.0, 1.0), 'y': (-0.15, 0), 'z': (0, 1.6)})
