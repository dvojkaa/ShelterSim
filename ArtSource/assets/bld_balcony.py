from ss_art import Model, finish
m = Model('bld_balcony', seed=62)

# Slab: top at z 0, 0.15 thick, from the wall (y 0) out to y -1.3. Tapered underside lip + front fascia.
m.box((3.0, 1.3, 0.15), (0, -0.65, -0.075), 'concrete', bevel=0.01)
m.box((3.0, 0.08, 0.2), (0, -1.255, -0.1), 'concrete', bevel=0.008)       # thicker front fascia
m.box((2.9, 1.2, 0.01), (0, -0.62, 0.003), 'concrete_light')                   # worn floor skin
m.box((0.5, 0.4, 0.012), (-0.7, -0.5, 0.005), 'dirt')

# Railing on three sides: post, mid + top rail, bars.
H = 1.1
XL, XR, YF = -1.44, 1.44, -1.22
posts_front = [XL, -0.72, 0.0, 0.72, XR]
for x in posts_front:
    m.box((0.05, 0.05, H), (x, YF, H / 2), 'steel_dark')
for y in (-0.4, -0.81):
    for x in (XL, XR):
        m.box((0.05, 0.05, H), (x, y, H / 2), 'steel_dark')
# front rails
m.box((2.93, 0.06, 0.05), (0, YF, H), 'rust')
m.box((2.9, 0.04, 0.04), (0, YF, 0.55), 'rust_dark')
m.box((2.9, 0.04, 0.05), (0, YF, 0.1), 'rust_dark')
# side rails
for x in (XL, XR):
    m.box((0.06, 1.2, 0.05), (x, -0.62, H), 'rust')
    m.box((0.04, 1.2, 0.04), (x, -0.62, 0.55), 'rust_dark')
    m.box((0.04, 1.2, 0.05), (x, -0.62, 0.1), 'rust_dark')
# balusters (some bent/missing)
for i in range(11):
    x = -1.3 + 0.26 * i
    if i == 6:
        continue
    a = 8 if i == 3 else 0
    m.box((0.02, 0.02, 1.0), (x, YF, 0.58), 'steel', rot=(0, a, 0))
for x in (XL, XR):
    for y in (-0.2, -0.6, -1.0):
        m.box((0.02, 0.02, 1.0), (x, y, 0.58), 'steel')
# rust blotches on the slab front
m.box((0.5, 0.01, 0.12), (0.5, -1.293, -0.1), 'rust_dark')
m.box((0.8, 0.01, 0.1), (-0.8, -1.293, -0.12), 'plaster_dirty')
m.poly([(0.9, 0.0), (0.95, 0.0), (0.98, 0.18), (0.92, 0.1)], 0.01, (0, -0.01, -0.1), 'concrete_dark')

# Leaning bike frame against the left railing (frame only, no wheels).
bx, by = -1.0, -0.9
m.tube([(bx - 0.05, by, 0.35), (bx + 0.35, by, 0.6), (bx + 0.35, by, 0.28)], 0.022, 'rust_light', seg=5)   # top tube + seat tube
m.tube([(bx + 0.35, by, 0.6), (bx + 0.75, by, 0.55), (bx + 0.75, by, 0.28)], 0.022, 'rust_light', seg=5)
m.tube([(bx + 0.75, by, 0.55), (bx + 0.85, by, 0.68)], 0.012, 'steel_dark', seg=5)
m.cyl(0.14, 0.03, (bx - 0.05, by, 0.2), 'rubber', rot=(90, 0, 0), seg=10)      # front wheel
m.cyl(0.11, 0.03, (bx + 0.75, by, 0.2), 'rubber', rot=(90, 0, 0), seg=10)

# Laundry rack on the right: A-frame with cloth.
rx = 0.75
for dy in (-0.25, 0.25):
    m.tube([(rx - 0.4, -0.7 + dy, 0.0), (rx - 0.38, -0.7 + dy, 0.95)], 0.012, 'steel', seg=5)
    m.tube([(rx + 0.4, -0.7 + dy, 0.0), (rx + 0.38, -0.7 + dy, 0.95)], 0.012, 'steel', seg=5)
for z in (0.95, 0.65):
    m.box((0.82, 0.03, 0.03), (rx, -0.45, z), 'steel')
    m.box((0.82, 0.03, 0.03), (rx, -0.95, z), 'steel')
m.box((0.36, 0.02, 0.45), (rx - 0.2, -0.45, 0.72), 'cloth_red', rot=(0, 5, 0))
m.box((0.3, 0.02, 0.35), (rx + 0.2, -0.95, 0.78), 'fabric_stain', rot=(0, -4, 0))
m.box((0.3, 0.02, 0.4), (rx + 0.2, -0.45, 0.72), 'fabric_grey')
# a flower pot with dead plant
m.cyl(0.12, 0.2, (0.0, -1.0, 0.1), 'rust_dark', seg=7, r2=0.09)
m.blob((0.18, 0.18, 0.14), (0.0, -1.0, 0.27), 'leaf_dry', rough=0.2)

finish(m, bounds={'x': (-1.5, 1.5), 'y': (-1.3, 0.0), 'z': (-0.2, 1.2)})
