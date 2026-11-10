import random
from ss_art import Model, finish
m = Model('piece_foundation_reinforced', seed=42)
r = random.Random(4201)
H = 0.5
m.box((1.98, 1.98, H - 0.01), (0, 0, (H - 0.01) / 2), 'steel_dark')
# gunmetal top edge frame, 0.06 wide, flush to +-1
F = 0.06
for s in (-1, 1):
    m.box((2.0, F, 0.02), (0, s * (1 - F / 2), H - 0.01), 'gunmetal')
    m.box((F, 2 - 2 * F, 0.02), (s * (1 - F / 2), 0, H - 0.01), 'gunmetal')
# deck plates 2x2 on the flat top
P = (2 - 2 * F - 0.01) / 2
for i in (-1, 1):
    for j in (-1, 1):
        cx, cy = i * (P / 2 + 0.005), j * (P / 2 + 0.005)
        m.box((P, P, 0.02), (cx, cy, H - 0.01), r.choice(['steel', 'steel_dark', 'steel']))
        for dx in (-1, 1):
            for dy in (-1, 1):
                m.cyl(0.02, 0.006, (cx + dx * (P / 2 - 0.05), cy + dy * (P / 2 - 0.05), H + 0.001), 'gunmetal', seg=5)
        for k in (-1, 0, 1):   # tread ribs
            m.box((0.34, 0.025, 0.004), (cx, cy + k * 0.2, H + 0.002), 'steel_dark', rot=(0, 0, 0))
# side: horizontal belt plates with bolt rows, vertical seams
for s in (-1, 1):
    m.box((2.0, 0.012, 0.07), (0, s * 0.994, 0.43), 'gunmetal')
    m.box((0.012, 1.97, 0.07), (s * 0.994, 0, 0.43), 'gunmetal')
    m.box((2.0, 0.012, 0.07), (0, s * 0.994, 0.07), 'gunmetal')
    m.box((0.012, 1.97, 0.07), (s * 0.994, 0, 0.07), 'gunmetal')
    for k in range(-1, 2):
        m.box((0.03, 0.012, 0.3), (k * 0.66, s * 0.994, 0.25), 'steel')
        m.box((0.012, 0.03, 0.3), (s * 0.994, k * 0.66, 0.25), 'steel')
    for k in range(4):
        x = -0.75 + k * 0.5
        for z in (0.43, 0.07):
            m.cyl(0.018, 0.008, (x, s * 0.996, z), 'steel', rot=(90, 0, 0), seg=4)
            m.cyl(0.018, 0.008, (s * 0.996, x, z), 'steel', rot=(0, 90, 0), seg=4)
finish(m, bounds={'x': (-1, 1), 'y': (-1, 1), 'z': (0, H + 0.005)})
