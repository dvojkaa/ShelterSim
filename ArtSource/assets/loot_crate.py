import math
from ss_art import Model, finish
m = Model('loot_crate', seed=71)
Z = 0.3             # body height

# Hollow-looking dark core
m.box((0.34, 0.34, Z - 0.025), (0, 0, Z / 2 + 0.01), 'black')

# --- Plank walls: light boards with dark gaps. Row 3 is a faded hazard-yellow band: the
#     loot-me flag that makes this read as a container from across the map.
ROWS = (0.055, 0.13, 0.205, 0.272)
for sy in (-0.176, 0.176):
    for i, z in enumerate(ROWS):
        if i == 3 and sy < 0:
            continue    # snapped front board, built below
        col = ('wood_light', 'wood', 'hazard_yellow', 'wood_light')[i]
        m.box((0.32, 0.024, 0.066), (0, sy, z), col)
for sx in (-0.176, 0.176):
    for i, z in enumerate(ROWS):
        col = ('wood', 'wood_light', 'hazard_yellow', 'wood')[i]
        m.box((0.024, 0.32, 0.066), (sx, 0, z), col)
# Snapped front top board: one half sagging, the gap showing black inside
m.box((0.14, 0.024, 0.066), (0.09, -0.176, 0.272), 'wood_light')
m.box((0.11, 0.024, 0.06), (-0.11, -0.178, 0.264), 'wood_grey', rot=(0, -14, 0))

# --- Dark frame: corner posts + rails, the classic crate read
FR = 'wood_dark'
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.05, 0.05, Z), (sx * 0.17, sy * 0.17, Z / 2), FR)
for z in (0.022, Z - 0.012):
    m.box((0.29, 0.03, 0.044), (0, -0.181, z), FR)
    m.box((0.29, 0.03, 0.044), (0, 0.181, z), FR)
    m.box((0.03, 0.29, 0.044), (-0.181, 0, z), FR)
    m.box((0.03, 0.29, 0.044), (0.181, 0, z), FR)
# Diagonal braces make it unmistakably a crate
m.box((0.03, 0.012, 0.34), (0, -0.191, 0.15), FR, rot=(0, 44, 0))
m.box((0.03, 0.012, 0.34), (0, 0.191, 0.15), FR, rot=(0, -44, 0))
m.box((0.012, 0.03, 0.34), (-0.191, 0, 0.15), FR, rot=(44, 0, 0))
m.box((0.012, 0.03, 0.34), (0.191, 0, 0.15), FR, rot=(-44, 0, 0))

# --- Rusty steel corners, proud of the posts so nothing is coplanar
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.066, 0.066, 0.05), (sx * 0.167, sy * 0.167, 0.03), 'rust')
        m.box((0.066, 0.066, 0.04), (sx * 0.167, sy * 0.167, Z - 0.02), 'steel_dark')

# --- Contents crammed in, glinting through the gap under the sprung lid
m.box((0.3, 0.3, 0.012), (0, 0, Z + 0.002), 'black')
m.blob((0.15, 0.12, 0.06), (-0.075, -0.09, Z + 0.016), 'cloth_red', rough=0.2)
m.cyl(0.03, 0.06, (0.085, -0.1, Z + 0.024), 'chrome', seg=8)
m.box((0.05, 0.03, 0.03), (0.02, -0.03, Z + 0.018), 'mustard', rot=(0, 0, 25))
m.box((0.03, 0.02, 0.05), (0, -0.182, Z + 0.015), 'steel', rot=(8, 0, 0))     # bent hasp

# --- Broken lid, sprung open at the front, three boards with one snapped short.
#     lid() places a piece in the lid's own tilted frame, so everything stays seated on it.
LID = (-8, 0, 0)
LID_Z = Z + 0.043
def lid(size, x, y, zl, col):
    a = math.radians(LID[0])
    m.box(size, (x, y * math.cos(a) - zl * math.sin(a), LID_Z + y * math.sin(a) + zl * math.cos(a)), col, rot=LID)
lid((0.36, 0.37, 0.018), 0, 0, -0.008, 'wood_dark')
lid((0.12, 0.38, 0.02), -0.128, 0, 0.009, 'wood_light')
lid((0.12, 0.38, 0.02), 0.003, 0, 0.009, 'wood')
lid((0.12, 0.27, 0.02), 0.135, 0.055, 0.009, 'wood_light')      # snapped board, the break exposes the backing
lid((0.385, 0.03, 0.012), 0, 0.14, 0.024, 'rust')
lid((0.27, 0.03, 0.012), -0.058, -0.12, 0.024, 'rust')           # front strap stops at the break
# Hinges at the back
for sx in (-0.11, 0.11):
    m.cyl(0.013, 0.05, (sx, 0.185, Z + 0.005), 'steel_dark', rot=(0, 90, 0), seg=6)

finish(m, bounds={'x': (-0.2, 0.2), 'y': (-0.2, 0.2), 'z': (0, 0.4)})
