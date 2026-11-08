from ss_art import Model, finish
m = Model('piece_workbench', seed=21)
TOP = 0.74  # bench surface height

# --- Bench top: three heavy, scarred planks, set back so the vice can overhang the front
for y, d, col, dz in ((-0.19, 0.23, 'wood', 0.0), (0.04, 0.22, 'wood_light', -0.003), (0.27, 0.22, 'wood', 0.002)):
    m.box((1.5, d, 0.06), (0, y, TOP - 0.03 + dz), col)
m.box((1.44, 0.68, 0.03), (0, 0.04, TOP - 0.07), 'wood_dark')
# Scorch marks / worn patches let into the planks, and a steel edge strip along the front
m.box((0.22, 0.1, 0.004), (0.22, -0.16, TOP + 0.001), 'wood_dark', rot=(0, 0, 17))
m.box((0.14, 0.09, 0.004), (-0.3, 0.06, TOP + 0.001), 'wood_dark', rot=(0, 0, -23))
m.box((1.5, 0.012, 0.075), (0, -0.307, TOP - 0.038), 'steel_dark')

# --- Welded steel legs and stretchers
for x in (-0.68, 0.68):
    for y in (-0.24, 0.31):
        m.box((0.07, 0.07, TOP - 0.1), (x, y, (TOP - 0.1) / 2 + 0.03), 'steel_dark')
        m.box((0.11, 0.11, 0.03), (x, y, 0.015), 'rust_dark')
    m.box((0.05, 0.5, 0.05), (x, 0.035, 0.18), 'gunmetal')
m.box((1.32, 0.05, 0.05), (0, 0.31, 0.18), 'gunmetal')
m.box((1.32, 0.05, 0.06), (0, 0.31, TOP - 0.12), 'gunmetal')
m.box((0.012, 0.09, 0.16), (-0.716, 0.31, 0.46), 'rust')
# Diagonal back brace
m.tube([(-0.64, 0.33, 0.2), (0.0, 0.33, TOP - 0.13)], 0.012, 'rust', seg=6)
m.tube([(0.64, 0.33, 0.2), (0.0, 0.33, TOP - 0.13)], 0.012, 'rust', seg=6)

# --- Lower shelf
for i, col in enumerate(('wood_grey', 'wood_dark', 'wood_grey', 'wood')):
    m.box((1.34, 0.125, 0.025), (0, -0.16 + i * 0.13, 0.215), col)

# --- Drawer unit, right side (viewed from the front it is on the right = +X)
m.box((0.5, 0.56, 0.42), (0.38, 0.02, 0.45), 'paint_green')
for z, col in ((0.34, 'paint_green'), (0.5, 'paint_green')):
    m.box((0.45, 0.02, 0.145), (0.38, -0.265, z), col)
    m.box((0.14, 0.03, 0.02), (0.38, -0.285, z + 0.02), 'chrome')
    m.box((0.08, 0.004, 0.035), (0.25, -0.277, z + 0.02), 'paint_white')
m.box((0.17, 0.006, 0.09), (0.53, -0.277, 0.31), 'rust')
m.box((0.1, 0.006, 0.05), (0.26, -0.277, 0.46), 'rust_dark')

# --- Toolbox and paint can on the shelf, left side
m.box((0.42, 0.22, 0.17), (-0.4, -0.04, 0.315), 'warning_red')
m.box((0.43, 0.23, 0.02), (-0.4, -0.04, 0.35), 'cloth_red')
m.box((0.2, 0.025, 0.02), (-0.4, -0.04, 0.425), 'black')
for x in (-0.49, -0.31):
    m.box((0.02, 0.025, 0.03), (x, -0.04, 0.41), 'black')
m.box((0.05, 0.01, 0.05), (-0.4, -0.154, 0.33), 'chrome')
m.cyl(0.075, 0.16, (-0.03, 0.03, 0.308), 'steel', seg=8)
m.cyl(0.078, 0.05, (-0.03, 0.03, 0.31), 'hazard_yellow', seg=8)

# --- The vice: the bench's signature prop, bolted over the front-left corner
vx = -0.48
VICE = 'army_green'
m.box((0.2, 0.2, 0.025), (vx, -0.2, TOP + 0.0125), 'gunmetal')              # swivel base
for sx, sy in ((-0.08, -0.28), (0.08, -0.12)):
    m.box((0.022, 0.022, 0.01), (vx + sx, sy, TOP + 0.028), 'chrome')
m.box((0.13, 0.16, 0.08), (vx, -0.19, TOP + 0.065), VICE)     # fixed body
m.box((0.17, 0.05, 0.075), (vx, -0.15, TOP + 0.11), VICE)     # fixed jaw
m.box((0.09, 0.14, 0.04), (vx, -0.31, TOP + 0.075), 'steel_dark')           # slide bar
m.box((0.17, 0.05, 0.11), (vx, -0.3, TOP + 0.09), VICE)        # moving jaw
m.box((0.17, 0.012, 0.03), (vx, -0.27, TOP + 0.135), 'steel')               # jaw plates
m.box((0.17, 0.012, 0.03), (vx, -0.181, TOP + 0.135), 'steel')
m.cyl(0.016, 0.07, (vx, -0.355, TOP + 0.085), 'chrome', rot=(90, 0, 0), seg=8)
m.cyl(0.024, 0.02, (vx, -0.378, TOP + 0.085), 'steel_dark', rot=(90, 0, 0), seg=8)
m.tube([(vx - 0.1, -0.378, TOP + 0.05), (vx + 0.1, -0.378, TOP + 0.12)], 0.008, 'chrome', seg=6)
for sx in (-0.1, 0.1):
    t = 0.05 if sx < 0 else 0.12
    m.cyl(0.015, 0.02, (vx + sx, -0.378, TOP + t), 'gunmetal', seg=6)
# Workpiece gripped in the jaws: a length of rusty pipe, sticking out sideways
m.cyl(0.018, 0.34, (vx + 0.12, -0.225, TOP + 0.135), 'rust', rot=(0, 90, 0), seg=8)

# --- Pegboard with hanging tools
m.box((1.5, 0.035, 0.26), (0, 0.38, TOP + 0.13), 'wood_grey')
for x in (-0.735, 0.735):
    m.box((0.03, 0.05, 0.26), (x, 0.375, TOP + 0.13), 'steel_dark')
m.box((1.5, 0.05, 0.025), (0, 0.375, 0.9875), 'steel_dark')
Y = 0.354
def rail_peg(x, z):
    m.cyl(0.006, 0.02, (x, Y + 0.005, z), 'black', rot=(90, 0, 0), seg=6)
# Hammer
m.box((0.024, 0.014, 0.16), (-0.52, Y, TOP + 0.13), 'wood_light')
m.box((0.08, 0.022, 0.03), (-0.52, Y - 0.002, TOP + 0.2), 'steel_dark')
# Open-ended wrench
m.box((0.022, 0.01, 0.15), (-0.37, Y, TOP + 0.115), 'chrome')
m.box((0.012, 0.01, 0.035), (-0.388, Y, TOP + 0.205), 'chrome')
m.box((0.012, 0.01, 0.035), (-0.352, Y, TOP + 0.205), 'chrome')
m.box((0.05, 0.01, 0.014), (-0.37, Y, TOP + 0.19), 'chrome')
# Hand saw with a chunky grip
m.poly([(-0.17, -0.02), (0.13, -0.05), (0.13, 0.05), (-0.17, 0.02)], 0.006, (-0.04, Y, TOP + 0.15), 'steel')
m.box((0.075, 0.016, 0.11), (0.12, Y, TOP + 0.15), 'wood_dark')
# Screwdrivers with bright handles
for x, col in ((0.26, 'warning_red'), (0.32, 'hazard_yellow'), (0.38, 'plastic_blue')):
    m.box((0.024, 0.02, 0.075), (x, Y - 0.002, TOP + 0.195), col)
    m.box((0.008, 0.008, 0.1), (x, Y, TOP + 0.11), 'chrome')
# Coil of wire and a spanner on the far right
m.cyl(0.06, 0.018, (0.56, Y - 0.002, TOP + 0.15), 'rust_light', rot=(90, 0, 0), seg=10)
m.cyl(0.036, 0.02, (0.56, Y - 0.003, TOP + 0.15), 'wood_grey', rot=(90, 0, 0), seg=8)

# --- Clutter on the surface
# Mallet lying on the bench
m.box((0.26, 0.026, 0.024), (-0.06, -0.14, TOP + 0.012), 'wood_light', rot=(0, 0, 28))
m.box((0.04, 0.1, 0.05), (0.06, -0.08, TOP + 0.025), 'steel_dark', rot=(0, 0, 28))
# Jar of nails
m.cyl(0.042, 0.11, (0.16, 0.2, TOP + 0.055), 'glass_dirty', seg=7)
m.cyl(0.044, 0.02, (0.16, 0.2, TOP + 0.12), 'rust_light', seg=7)
# Oil can with a long spout
m.cyl(0.06, 0.09, (0.5, 0.05, TOP + 0.045), 'orange', seg=8, r2=0.05)
m.cyl(0.05, 0.05, (0.5, 0.05, TOP + 0.115), 'steel', seg=10, r2=0.012)
m.tube([(0.5, 0.05, TOP + 0.135), (0.42, -0.02, TOP + 0.18)], 0.005, 'steel', seg=5)
# Rag and a wood offcut
m.blob((0.15, 0.1, 0.03), (0.25, -0.17, TOP + 0.01), 'cloth_red', rough=0.22, flat_bottom=-0.005)
m.box((0.2, 0.09, 0.04), (-0.2, 0.2, TOP + 0.02), 'wood_light', rot=(0, 0, -14))
m.box((0.13, 0.07, 0.03), (-0.17, 0.2, TOP + 0.054), 'wood', rot=(0, 0, 8))
# Gooseneck lamp bolted to the back right
m.cyl(0.05, 0.015, (0.62, 0.27, TOP + 0.008), 'gunmetal', seg=8)
m.tube([(0.62, 0.27, TOP + 0.01), (0.62, 0.27, TOP + 0.13), (0.6, 0.2, TOP + 0.2), (0.56, 0.12, TOP + 0.21)], 0.008, 'black', seg=5)
m.cyl(0.03, 0.06, (0.55, 0.1, TOP + 0.19), 'mustard_dark', seg=8, r2=0.02, rot=(-20, 0, 0))
m.cyl(0.056, 0.05, (0.545, 0.095, TOP + 0.145), 'mustard', seg=10, r2=0.03, rot=(-20, 0, 0))
m.cyl(0.026, 0.02, (0.538, 0.087, TOP + 0.113), 'lamp_warm', seg=8, rot=(-20, 0, 0))

finish(m, bounds={'x': (-0.75, 0.75), 'y': (-0.4, 0.4), 'z': (0, 1.0)})
