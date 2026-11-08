from ss_art import Model, finish
m = Model('piece_crate', seed=47)
B = 0.47        # outer board plane
SEAM = 0.70     # lid seam height
TOP = 0.965     # top of lid boards

# Dark inner carcass and a pair of chunky skids
m.box((0.92, 0.92, 0.92), (0, 0, 0.5), 'wood_dark')
for x in (-0.32, 0.32):
    m.box((0.14, 0.98, 0.05), (x, 0, 0.025), 'wood_dark')

# --- Painted plank walls (body + lid)
rows = [(0.09, 0.14), (0.24, 0.14), (0.39, 0.14), (0.54, 0.14), (0.77, 0.12), (0.895, 0.12)]
paint = ['army_green', 'army_green_dark', 'army_green', 'army_green', 'army_green_dark', 'army_green']
for i, ((z, h), col) in enumerate(zip(rows, paint)):
    # front/back boards span the full width; side boards stop short so no end-grain is coplanar
    m.box((0.94, 0.02, h), (0, -B + 0.01, z + 0.0), col)
    m.box((0.94, 0.02, h), (0, B - 0.01, z), paint[(i + 2) % 6])
    m.box((0.02, 0.92, h), (-B + 0.01, 0, z), paint[(i + 1) % 6])
    m.box((0.02, 0.92, h), (B - 0.01, 0, z), paint[(i + 3) % 6])

# Heavy rail where the lid closes: the shadow gap sells "it opens"
m.box((0.95, 0.95, 0.035), (0, 0, SEAM - 0.04), 'steel_dark')
m.box((0.95, 0.95, 0.03), (0, 0, SEAM + 0.015), 'gunmetal')

# --- Lid: slatted top with raised battens
for i, col in enumerate(('army_green', 'army_green_dark', 'army_green', 'army_green', 'army_green_dark')):
    x = -0.376 + i * 0.188
    m.box((0.18, 0.94, 0.02), (x, 0, TOP - 0.01), col)
for y in (-0.36, 0.36):
    m.box((0.94, 0.07, 0.022), (0, y, TOP + 0.011), 'army_green_dark')

# --- Paint chips, a scrap repair plank and a blank stencil plate
m.box((0.2, 0.004, 0.08), (0.28, -B - 0.002, 0.2), 'rust')
m.box((0.11, 0.004, 0.05), (-0.34, -B - 0.002, 0.51), 'wood_grey')
m.box((0.004, 0.17, 0.09), (-B - 0.002, 0.2, 0.26), 'rust_dark')
m.box((0.004, 0.13, 0.06), (B + 0.002, -0.24, 0.5), 'wood_grey')
m.box((0.2, 0.006, 0.09), (0.3, 0.473, 0.4), 'rust')
m.box((0.3, 0.012, 0.09), (-0.05, 0.476, 0.17), 'wood_grey', rot=(0, 12, 0))     # repair plank, back
for x in (-0.17, 0.07):
    m.box((0.016, 0.006, 0.016), (x, 0.484, 0.17 + (x + 0.05) * -0.2), 'steel')
# Painted stencil on the front: pale block with hazard chevron band
m.box((0.26, 0.004, 0.12), (0.0, -B - 0.002, 0.39), 'paint_white')
m.box((0.26, 0.006, 0.03), (0.0, -B - 0.003, 0.335), 'hazard_yellow')
m.box((0.2, 0.006, 0.014), (0.0, -B - 0.003, 0.415), 'black')
m.box((0.14, 0.006, 0.014), (0.0, -B - 0.003, 0.385), 'black')

# --- Steel corner posts and caps
for sx in (-1, 1):
    for sy in (-1, 1):
        cx, cy = sx * 0.455, sy * 0.455
        m.box((0.075, 0.075, 0.645), (cx, cy, 0.34), 'steel_dark')
        m.box((0.075, 0.075, 0.25), (cx, cy, 0.85), 'steel_dark')
        m.box((0.09, 0.09, 0.075), (cx, cy, 0.0875), 'gunmetal')  # foot cap
        m.box((0.09, 0.09, 0.09), (cx, cy, 0.945), 'gunmetal')    # lid cap
        m.box((0.082, 0.082, 0.05), (cx, cy, 0.68), 'rust')
        for dz in (0.2, 0.5):
            m.box((0.018, 0.008, 0.018), (cx - sx * 0.018, sy * 0.495, dz), 'chrome')
            m.box((0.008, 0.018, 0.018), (sx * 0.495, cy - sy * 0.018, dz), 'chrome')

# --- Two leather straps wrapping up over the lid and down the front, with buckles
ST = 'leather_dark'
for x in (-0.25, 0.25):
    m.box((0.075, 0.03, 0.67), (x, -0.481, 0.65), ST)     # front run (buckled down at z 0.33)
    m.box((0.075, 0.03, 0.78), (x, 0.475, 0.59), ST)     # back run
    m.box((0.075, 0.99, 0.012), (x, 0, 0.99), ST)         # over the top
        # Buckle at lid seam height
    m.box((0.1, 0.016, 0.085), (x, -0.486, 0.59), 'chrome')
    m.box((0.064, 0.019, 0.05), (x, -0.4875, 0.59), 'leather')
    m.box((0.012, 0.021, 0.1), (x, -0.489, 0.59), 'steel_dark')
    # Loose strap end
    m.box((0.075, 0.03, 0.13), (x, -0.48, 0.28), 'leather')
    m.box((0.075, 0.032, 0.018), (x, -0.481, 0.22), 'leather_dark')

# --- Hasp and padlock, centre front
m.box((0.11, 0.03, 0.16), (0, -0.477, 0.68), 'steel')
m.box((0.13, 0.03, 0.05), (0, -0.478, 0.77), 'steel_dark')
m.tube([(-0.02, -0.492, 0.59), (-0.02, -0.492, 0.625), (0.02, -0.492, 0.625), (0.02, -0.492, 0.59)], 0.006, 'chrome', seg=5)
m.box((0.075, 0.02, 0.07), (0, -0.487, 0.56), 'mustard')
m.box((0.014, 0.004, 0.02), (0, -0.498, 0.56), 'black')

# --- Rope grab handles on the sides, on steel eyelets
for sx in (-1, 1):
    for y in (-0.18, 0.18):
        m.box((0.012, 0.05, 0.05), (sx * 0.478, y, 0.6), 'steel')
    m.tube([(sx * 0.484, -0.18, 0.6), (sx * 0.493, -0.08, 0.53), (sx * 0.493, 0.08, 0.53), (sx * 0.484, 0.18, 0.6)], 0.0065, 'rope', seg=4)

finish(m, bounds={'x': (-0.5, 0.5), 'y': (-0.5, 0.5), 'z': (0, 1.0)})
