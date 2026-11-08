"""Pipe run wall dressing. Origin at wall surface, floor. Wall at y=0, protrudes to y=-0.25.
Clear of z<1.9 except the corner riser at x<-2.6."""
import math
from ss_art import Model, finish
m = Model('bunker_pipes', seed=47)
XA = -2.88; XAE = 2.88


def run_x(x0, x1, y, z, r, c, seg=6):
    m.cyl(r, abs(x1 - x0), ((x0 + x1) / 2, y, z), c, rot=(0, 90, 0), seg=seg, caps=False)


def ring_x(x, y, z, r, w, c, seg=6, caps=True):
    m.cyl(r, w, (x, y, z), c, rot=(0, 90, 0), seg=seg, caps=caps)


def bracket(x, y, z, r, c='steel_dark'):
    """Wall saddle with a strap over the pipe."""
    m.box((0.05, abs(y), 0.06), (x, y / 2, z), c)
    ring_x(x, y, z, r + 0.007, 0.04, c, seg=SEG_OF[r], caps=False)


def elbow(x, y, z, r, c):
    d = 2 * r * 1.15
    m.blob((d, d, d), (x, y, z), c, rough=0.0, subdiv=1)


def into_wall(x, y, z, r, c):
    """Turns a run back into the wall with a flange plate."""
    elbow(x, y, z, r, c)
    m.tube([(x, y, z), (x, 0, z)], r, c, seg=6)
    m.cyl(r + 0.03, 0.02, (x, -0.01, z), 'steel_dark', rot=(90, 0, 0), seg=6)


def wheel(cx, y, cz, r, c, n=8):
    pts = [(cx + r * math.cos(2 * math.pi * i / n), y, cz + r * math.sin(2 * math.pi * i / n)) for i in range(n + 1)]
    m.tube(pts, 0.012, c, seg=4)
    for i in range(3):
        a = 2 * math.pi * i / 3 + 0.5
        m.tube([(cx, y, cz), (cx + r * math.cos(a), y, cz + r * math.sin(a))], 0.009, c, seg=4)
    m.cyl(0.024, 0.03, (cx, y, cz), 'steel_dark', rot=(90, 0, 0), seg=6)


# --- A: big rusty main high on the wall ---
AY, AZ, AR = -0.135, 2.62, 0.075
SEG_OF = {0.075: 8, 0.05: 6}
run_x(XA, XAE, AY, AZ, AR, 'rust', seg=8)
into_wall(XA, AY, AZ, AR, 'rust')
into_wall(XAE, AY, AZ, AR, 'rust')
for x in (-1.9, -0.3, 1.25, 2.3):
    ring_x(x, AY, AZ, AR + 0.022, 0.05, 'rust_dark', seg=8)
for x in (-2.3, -0.9, 0.55, 1.75):
    bracket(x, AY, AZ, AR)
for x, w in ((-1.45, 0.34), (0.85, 0.22), (2.55, 0.18)):
    ring_x(x, AY, AZ, AR + 0.004, w, 'rust_dark', seg=8, caps=False)

# --- B: steel water main, comes up the corner riser ---
BY, BZ, BR = -0.11, 2.32, 0.05
RX = -2.8
m.cyl(BR, BZ, (RX, BY, BZ / 2), 'steel', seg=6)
elbow(RX, BY, BZ, BR, 'steel')
run_x(RX, 2.78, BY, BZ, BR, 'steel', seg=6)
into_wall(2.78, BY, BZ, BR, 'steel')
m.cyl(0.078, 0.022, (RX, BY, 0.011), 'steel_dark', seg=6)             # floor flange
for z in (0.42, 1.72):
    m.cyl(BR + 0.018, 0.045, (RX, BY, z), 'steel_dark', seg=6)
for z in (0.8, 1.95):
    m.box((0.06, abs(BY), 0.05), (RX, BY / 2, z), 'steel_dark')
    m.cyl(BR + 0.007, 0.04, (RX, BY, z), 'steel_dark', seg=6)
m.cyl(BR + 0.004, 0.32, (RX, BY, 0.2), 'rust', seg=6, caps=False)                 # rust at the damp floor end
for x in (-2.0, -0.8, 0.6, 1.75):
    bracket(x, BY, BZ, BR)
for x in (-1.3, 1.45):
    ring_x(x, BY, BZ, BR + 0.018, 0.045, 'steel_dark')
ring_x(-0.5, BY, BZ, BR + 0.004, 0.26, 'rust', caps=False)
ring_x(2.2, BY, BZ, BR + 0.004, 0.16, 'rust_dark', caps=False)

# Reachable shut-off valve on the riser at chest height
VZ = 1.3
m.box((0.13, 0.13, 0.17), (RX, BY, VZ), 'steel_dark')
m.tube([(RX, BY - 0.06, VZ), (RX, -0.215, VZ)], 0.014, 'chrome', seg=6)
wheel(RX, -0.218, VZ, 0.105, 'warning_red', n=7)

# Big valve on the main
WX = 1.05
m.box((0.17, 0.13, 0.15), (WX, BY, BZ), 'gunmetal')
m.tube([(WX, BY - 0.06, BZ), (WX, -0.215, BZ)], 0.016, 'chrome', seg=6)
wheel(WX, -0.218, BZ, 0.135, 'warning_red')

# Pressure gauge on a stub
GX = -0.5
m.tube([(GX, BY, BZ), (GX, BY, 2.43)], 0.014, 'steel_dark', seg=6)
m.cyl(0.062, 0.03, (GX, BY - 0.006, 2.465), 'gunmetal', rot=(90, 0, 0), seg=10)
m.cyl(0.05, 0.006, (GX, BY - 0.024, 2.465), 'paint_white', rot=(90, 0, 0), seg=10)
m.box((0.006, 0.004, 0.04), (GX + 0.011, BY - 0.028, 2.475), 'warning_red', rot=(0, 28, 0))

# Cross-over between the mains, with its own flanges
CX = -1.8
elbow(CX, BY, BZ, 0.05, 'steel_dark')
m.tube([(CX, BY, BZ), (CX, AY, AZ)], 0.036, 'steel_dark', seg=6)
m.cyl(0.058, 0.03, (CX, -0.122, 2.475), 'rust', seg=6)

# Dead drain leg dropping off the main; hangs just above head height
DX = 0.28
elbow(DX, BY, BZ, 0.035, 'steel')
m.tube([(DX, BY, BZ), (DX, BY, 1.98)], 0.03, 'steel', seg=6)
m.cyl(0.046, 0.04, (DX, BY, 2.14), 'steel_dark', seg=6)
m.cyl(0.042, 0.06, (DX, BY, 1.95), 'rust', seg=6)

# --- C: thin conduit with junction boxes ---
CY, CZ, CR = -0.035, 2.06, 0.018
CRX = -2.68
m.tube([(CRX, CY, 0.0), (CRX, CY, CZ), (2.95, CY, CZ)], CR, 'gunmetal', seg=5)
elbow(CRX, CY, CZ, CR, 'gunmetal')
m.box((0.05, 0.016, 0.03), (CRX, -0.008, 0.015), 'steel_dark')
for z in (0.7, 1.55):
    m.box((0.05, 0.06, 0.018), (CRX, -0.03, z), 'steel_dark')
for x in (-2.0, -0.6, 0.75, 2.55):
    m.box((0.018, 0.06, 0.05), (x, -0.03, CZ), 'steel_dark')

# --- D: upper cable tray conduit ---
DY, DZ, DR = -0.032, 2.88, 0.016
m.tube([(-2.98, DY, DZ), (2.98, DY, DZ)], DR, 'black', seg=5)
for x in (-2.3, 0.2, 2.5):
    m.box((0.018, 0.055, 0.045), (x, -0.028, DZ), 'steel_dark')


def junction(x, w, h, body, cover, stripe=False):
    z = CZ
    m.box((w, 0.085, h), (x, -0.0425, z), body)
    m.box((w - 0.03, 0.008, h - 0.03), (x, -0.087, z), cover)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.cyl(0.008, 0.007, (x + sx * (w / 2 - 0.028), -0.094, z + sz * (h / 2 - 0.028)), 'chrome', rot=(90, 0, 0), seg=4)
    if stripe:
        m.box((w - 0.03, 0.004, 0.03), (x, -0.092, z + h / 2 - 0.05), 'hazard_yellow')
        m.poly([(x - 0.032, z - 0.052), (x + 0.032, z - 0.052), (x, z + 0.006)], 0.004, (0, -0.092, 0), 'warning_red')
    # riser to the upper tray
    m.tube([(x, DY, z + h / 2 - 0.005), (x, DY, DZ)], 0.014, 'gunmetal', seg=5)
    m.box((0.05, 0.05, 0.045), (x, -0.03, DZ), body)


junction(-1.25, 0.26, 0.24, 'army_green', 'army_green_dark', stripe=True)
junction(2.05, 0.21, 0.27, 'steel_dark', 'steel', stripe=False)
m.box((0.08, 0.004, 0.09), (2.0, -0.092, 2.02), 'rust')
# Loose sagging cable from the second box to the first conduit clip
m.tube([(1.95, -0.05, 1.96), (1.82, -0.055, 1.93), (1.62, -0.05, 1.96), (1.5, -0.04, 2.03)], 0.008, 'black', seg=4)

finish(m, bounds={'x': (-3, 3), 'y': (-0.25, 0), 'z': (0, 3)})
