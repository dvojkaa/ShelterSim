import os, sys, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('bld_roof_stairhut', seed=92)
r = random.Random(9201)
H = 2.85           # wall top
Y0, Y1 = -2.7, 3.0  # outer wall faces in y
# side walls (outer x +-1.7, 0.2 thick), back wall solid, floor open
for s in (-1, 1):
    m.box((0.2, Y1 - Y0, H), (s * 1.6, (Y0 + Y1) / 2, H / 2), 'concrete', bevel=0.01)
m.box((3.2, 0.2, H), (0, Y1 - 0.1, H / 2), 'concrete', bevel=0.01)
# front wall: two stubs beside the 1.8 doorway + header above 2.2
for s in (-1, 1):
    m.box((0.6, 0.2, H), (s * 1.2, Y0 + 0.1, H / 2), 'concrete', bevel=0.01)
m.box((1.8, 0.2, H - 2.2), (0, Y0 + 0.1, 2.2 + (H - 2.2) / 2), 'concrete', bevel=0.01)
# painted band + door frame lining
for s in (-1, 1):
    m.box((0.06, 0.24, 2.2), (s * 0.93, Y0 + 0.1, 1.1), 'steel_dark')
m.box((1.98, 0.24, 0.06), (0, Y0 + 0.1, 2.23), 'steel_dark')
# roof slab with overhang, edge lip
m.box((3.6, 6.1, 0.15), (0, 0.05, H + 0.075), 'concrete_light', bevel=0.01)
m.box((3.6, 0.08, 0.1), (0, -2.96, H + 0.2), 'concrete_light')
m.box((3.6, 0.08, 0.1), (0, 3.09, H + 0.2), 'concrete_light') if False else None
# roof grime and patches
for i in range(7):
    m.box((r.uniform(0.3, 0.8), r.uniform(0.4, 1.0), 0.01), (r.uniform(-1.3, 1.3), r.uniform(-2.5, 2.8), H + 0.155), r.choice(('asphalt', 'concrete_dark', 'rust_dark')))
# vent cap
m.cyl(0.15, 0.06, (-0.8, 1.4, H + 0.195), 'steel_dark', seg=8)
m.cyl(0.28, 0.035, (-0.8, 1.4, H + 0.2325), 'steel', seg=8, r2=0.05)
m.box((0.4, 0.4, 0.03), (-0.8, 1.4, H + 0.165), 'rust')
# lamp over the door, under the overhang
m.box((0.1, 0.12, 0.1), (0, Y0 - 0.07, 2.55), 'gunmetal')
m.cyl(0.07, 0.12, (0, Y0 - 0.16, 2.5), 'lamp_warm', seg=6)
m.box((0.2, 0.05, 0.14), (0, Y0 - 0.12, 2.6), 'steel_dark')
# cable / pipe run on the right outer wall and over the roof edge
m.tube([(1.74, -2.5, 0.4), (1.74, -2.5, 2.6), (1.74, -1.0, 2.6), (1.74, 1.5, 2.6)], 0.035, 'rust_dark', seg=6)
m.tube([(1.74, -1.0, 2.5), (1.74, -1.0, 1.4)], 0.02, 'black', seg=5)
for y in (-2.0, 0.0, 1.5):
    m.box((0.05, 0.08, 0.14), (1.73, y, 2.6), 'steel_dark')
m.box((0.05, 0.3, 0.3), (1.73, -1.0, 1.35), 'gunmetal')
# stains and wear on walls
for s in (-1, 1):
    for i in range(4):
        m.box((0.012, r.uniform(0.3, 0.8), r.uniform(0.5, 1.2)), (s * 1.707, r.uniform(-2.0, 2.5), r.uniform(0.3, 1.8)), r.choice(('concrete_dark', 'concrete_dark', 'rust_dark')))
m.box((0.5, 0.012, 0.6), (-1.2, Y0 - 0.006, 0.5), 'concrete_dark')
m.box((0.5, 0.012, 1.0), (1.2, Y0 - 0.006, 1.4), 'concrete_dark')
m.box((0.4, 0.012, 0.35), (-1.2, Y0 - 0.006, 1.7), 'brick_dark')  # plaster missing
# step-out concrete kerb beside door (outside opening, no threshold)
for s in (-1, 1):
    m.box((0.6, 0.2, 0.08), (s * 1.2, Y0 - 0.1, 0.04), 'concrete_dark')
# opening clearance proof
bm = m.default.bm; bad = 0
for v in bm.verts:
    bad += abs(v.co.x) < 0.9 - 1e-4 and 0.02 < v.co.z < 2.2 - 1e-4 and abs(v.co.y - (Y0 + 0.1)) < 0.2
for e in bm.edges:
    a, b = e.verts[0].co, e.verts[1].co
    for k in range(1, 8):
        p = a.lerp(b, k / 8)
        hit = abs(p.x) < 0.9 - 1e-4 and 0.02 < p.z < 2.2 - 1e-4 and abs(p.y - (Y0 + 0.1)) < 0.2
        bad += hit
        if hit: open('/tmp/dbg.txt','a').write(str((tuple(a),tuple(b)))+'\n')
print(f"ASSET_INFO bld_roof_stairhut doorway check: {bad} samples inside")
if bad:
    for v in bm.verts:
        if abs(v.co.x) < 0.9 - 1e-4 and 0.02 < v.co.z < 2.2 - 1e-4 and abs(v.co.y - (Y0 + 0.1)) < 0.2: open('/tmp/dbg.txt','a').write('V'+str(tuple(v.co))+'\n')
assert bad == 0
finish(m, bounds={'x': (-1.8, 1.8), 'y': (-3.0, 3.1), 'z': (0, 3.1)})
