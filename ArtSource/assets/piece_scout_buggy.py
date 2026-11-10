from pathlib import Path
import bpy
from ss_art import Model, finish

m = Model('piece_scout_buggy', seed=2611)

# Raised suspension and a square, patched salvage chassis.
m.box((1.95, 4.05, .22), (0, 0, .48), 'gunmetal', bevel=.06)
m.box((1.72, 3.78, .12), (0, 0, .64), 'steel_dark', bevel=.035)
m.box((1.65, 1.25, .52), (0, 1.15, .93), 'army_green', bevel=.07)  # short hood
m.box((1.55, 1.25, .48), (0, -1.10, .86), 'rust_dark', bevel=.055)  # open cargo bed
m.box((1.42, .10, .48), (0, -1.78, .90), 'rust', bevel=.025)

# Heavy all-terrain tires, steel rims and exposed hubs.
for wn, x, y in (('Wheel_FL', -1.57, 1.40), ('Wheel_FR', 1.57, 1.40), ('Wheel_RL', -1.57, -1.40), ('Wheel_RR', 1.57, -1.40)):
    w = m.part(wn, pivot=(x, y, .60))
    w.cyl(.53, .34, (x, y, .60), 'rubber', rot=(0, 90, 0), seg=12)
    w.cyl(.31, .36, (x, y, .60), 'steel_dark', rot=(0, 90, 0), seg=10)
    w.cyl(.14, .38, (x, y, .60), 'rust', rot=(0, 90, 0), seg=8)

def tube(a, b, radius=.055, color='rust'):
    m.tube([a, b], radius, color, seg=7)

# Roll cage doubles as a repairable cabin frame, leaving the top and doors open.
for x in (-.74, .74):
    tube((x, -.58, .70), (x, -.45, 1.63), .055, 'steel_dark')
    tube((x, -.45, 1.63), (x, .72, 1.63), .055, 'steel_dark')
    tube((x, .72, 1.63), (x, .95, .98), .055, 'steel_dark')
    tube((x, -.58, .70), (x, .08, .72), .05, 'rust')
    tube((x, .08, .72), (x, .48, 1.03), .05, 'rust')
    tube((x, .48, 1.03), (x, .95, .98), .05, 'rust')
    tube((x, -.45, 1.63), (x, .72, 1.63), .025, 'rust')

# Slanted windscreen frame, mesh guard, grille and two rough seats.
tube((-.68, .74, 1.02), (-.62, .98, 1.60), .035, 'steel')
tube((.68, .74, 1.02), (.62, .98, 1.60), .035, 'steel')
m.box((1.25, .06, .42), (0, 1.00, 1.37), 'glass')
m.box((1.30, .08, .34), (0, 2.02, .82), 'steel_dark', bevel=.025)
for x in (-.48, -.24, 0, .24, .48):
    m.box((.035, .08, .30), (x, 2.01, .82), 'steel')
for x in (-.36, .36):
    m.box((.48, .48, .16), (x, .20, 1.06), 'cloth_dark', bevel=.045)
    m.box((.48, .16, .43), (x, -.02, 1.28), 'cloth_dark', bevel=.04)
    m.box((.36, .07, .09), (x, .68, 1.16), 'rust_dark', bevel=.02)
m.box((.18, .13, .12), (0, .87, 1.00), 'black', bevel=.02)

# Front lamps, a battered bumper and a small winch.
for x in (-.62, .62):
    m.box((.23, .12, .18), (x, 2.08, 1.08), 'paint_white', bevel=.025)
    m.box((.18, .06, .13), (x, -1.98, 1.00), 'red_bright', bevel=.02)
m.box((1.94, .20, .22), (0, 2.10, .49), 'steel_dark', bevel=.035)
m.cyl(.11, .16, (0, 2.15, .52), 'chrome', rot=(90, 0, 0), seg=8)
for x in (-.82, .82):
    m.box((.18, .20, .36), (x, 2.08, .68), 'rust', bevel=.03)

# Cabin detail visible from the driver seat: dashboard, steering column, wheel.
m.box((1.50, .20, .20), (0, .80, 1.20), 'black', bevel=.03)
m.box((.40, .04, .14), (-.40, .70, 1.27), 'steel_dark', bevel=.01)   # instrument pod
import math
sc = (-.40, .56, 1.22)
ux = (1, 0, 0); uy = (0, math.cos(math.radians(55)), math.sin(math.radians(55)))
def sp(r, a):
    return tuple(sc[i] + r * (math.cos(a) * ux[i] + math.sin(a) * uy[i]) for i in range(3))
m.tube([sp(.17, i * 2 * math.pi / 14) for i in range(15)], .018, 'black', seg=5)
for k in range(3):
    m.tube([sc, sp(.17, math.radians(90 + k * 120))], .014, 'steel_dark', seg=4)
m.cyl(.04, .05, sc, 'steel', rot=(55 - 90, 0, 0), seg=6)
m.tube([sc, (-.40, .82, 1.10)], .025, 'steel_dark', seg=6)
# Anchors read by the game (pivot position only).
for n, p in (('Seat', (-.40, .20, 1.35)), ('Exit_L', (-.95, .20, 0)), ('Exit_R', (.95, .20, 0))):
    m.part(n, pivot=p).box((.01, .01, .01), (p[0], p[1], p[2] + (.02 if p[2] == 0 else -.02)), 'black')

finish(m, bounds={'x':(-1.9,1.9),'y':(-2.3,2.3),'z':(0,2.1)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'piece_scout_buggy.blend'))
