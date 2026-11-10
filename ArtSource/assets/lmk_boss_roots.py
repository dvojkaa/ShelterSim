import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish

m = Model('lmk_boss_roots', seed=1402)
r = random.Random(1402)
def root(pts, r0, r1, cols=('bark', 'bark_dead', 'bark')):
    n = len(pts) - 1
    for i in range(n):
        rr = r0 + (r1 - r0) * i / n
        cl = lambda p: (max(-2.25, min(2.25, p[0])), max(-1.6, min(1.85, p[1])), max(0.1, min(3.0, p[2])))
        m.tube([cl(pts[i]), cl(pts[i + 1])], rr, cols[i % len(cols)], seg=6)
def wobble(a, b, steps, amp, up=0.0):
    out = []
    for i in range(steps + 1):
        t = i / steps
        out.append((a[0] + (b[0] - a[0]) * t + r.uniform(-amp, amp) * math.sin(t * 3.14),
                    a[1] + (b[1] - a[1]) * t + r.uniform(-amp, amp) * math.sin(t * 3.14),
                    a[2] + (b[2] - a[2]) * t + up * math.sin(t * 3.14)))
    return out
# glass panes at the back and sides: some intact-looking leaning panes with shards, framed by posts
for sx in (-1, 1):
    for sz in (-1, 1):
        m.box((0.1, 0.1, 2.4), (sx * 2.2, sz * 1.6, 1.2), 'paint_white') if False else None
# broken panes: jagged glass polygons with teeth missing, in frames
def pane(w, h, loc, rot, col):
    pts = [(-w / 2, 0)]
    n = 6
    for i in range(n + 1):
        pts.append((-w / 2 + w * i / n, h * (0.7 + 0.3 * r.random()) if i % 2 else h))
    pts.append((w / 2, 0))
    pts = [pts[0]] + pts[1:]
    m.poly(pts, 0.03, loc, col, rot=rot)
    m.box((w, 0.07, 0.08), (loc[0], loc[1], loc[2] + 0.04), 'steel_dark', rot=rot)
pane(3.0, 1.9, (0.4, 1.7, 0.05), (0, 0, 0), 'glass_dirty')
pane(1.4, 1.5, (-1.7, 1.7, 0.05), (-14, 0, 0), 'glass')
for sx in (-1, 1):
    pane(1.8, 1.6, (sx * 2.2, 0.8, 0.05), (0, 0, 90), 'glass_dirty')
# shards on the ground
for i in range(14):
    a = r.choice([(-1.9, 1.3), (1.9, 1.3), (-1.9, -0.2), (1.9, -0.2), (0.3, 1.5)])
    m.box((r.uniform(0.1, 0.3), r.uniform(0.1, 0.3), 0.02), (a[0] + r.uniform(-0.4, 0.4), a[1] + r.uniform(-0.4, 0.4), 0.03), 'glass', rot=(0, 0, r.uniform(0, 90)))
# big root masses: back corners and the east/west walls; central aisle |x|<0.7 / y<0.6 stays open
masses = [(-1.9, 1.4), (1.9, 1.4), (-2.0, -0.3), (2.0, -0.3)]
for (cx, cy) in masses:
    m.blob((0.9, 0.9, 0.55), (cx, cy, 0.25), 'bark', rough=0.25, subdiv=1, colors=['bark', 'bark_dead', 'leaf_dark'], flat_bottom=0.0)
    for k in range(5):
        tx = cx + r.uniform(-0.2, 0.2) + (0.3 if cx < 0 else -0.3) * r.uniform(0, 1)
        ty = cy + r.uniform(-0.7, 0.7)
        top = (tx + r.uniform(-0.3, 0.3), ty, r.uniform(1.6, 3.0))
        root(wobble((cx, cy, 0.3), top, 12, 0.3, up=0.2), 0.14 + r.uniform(0, 0.06), 0.05)
# thick roots arching over the roof panes along the ridge height
for (a, b) in (((-2.0, 1.3, 1.5), (-0.5, 1.5, 3.0)), ((2.0, 1.3, 1.5), (0.6, 1.5, 3.0)), ((-2.0, -0.3, 1.2), (-2.0, 1.0, 3.0)), ((2.0, -0.3, 1.2), (2.0, 1.0, 3.0))):
    root(wobble(a, b, 10, 0.2, up=0.25), 0.16, 0.06)
# roots breaking through glass panes at the back wall
for x in (-1.2, -0.2, 0.9, 1.5):
    root(wobble((x, 1.2, 0.3 + r.uniform(0, 0.4)), (x + r.uniform(-0.3, 0.3), 1.95, r.uniform(1.0, 2.2)), 8, 0.2), 0.1, 0.04)
    m.blob((0.3, 0.25, 0.35), (x, 1.75, 1.0 + r.uniform(0, 1.0)), 'bark', rough=0.2, colors=['bark', 'leaf_dark'])
# creeping vines with leaves
for i in range(10):
    x = r.choice([-1, 1]) * r.uniform(1.4, 2.1); y = r.uniform(-0.5, 1.6)
    m.blob((0.35, 0.3, 0.2), (x, y, r.uniform(0.7, 2.6)), 'leaf_dark', rough=0.2, colors=['leaf_dark', 'leaf_green'])
finish(m, bounds={'x': (-2.5, 2.5), 'y': (-1.8, 2.1), 'z': (0, 3.1)})
