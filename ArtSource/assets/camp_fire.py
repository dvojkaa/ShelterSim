import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('camp_fire', seed=702)
m.cyl(0.62, 0.04, (0, 0, 0.02), 'black', seg=12)                          # scorched ground
m.cyl(0.5, 0.05, (0, 0, 0.06), 'stone_dark', seg=10)                        # ash bed
for i, (rx, rz) in enumerate([(0.2, 0.0), (-0.3, 0.2), (0.05, -0.3), (-0.1, 0.15)]):
    m.blob((0.18, 0.14, 0.02), (rx, rz, 0.11), 'lamp_warm' if i % 2 == 0 else 'hazard_yellow', rough=0.1, subdiv=0)   # ember patches
n = 9
for i in range(n):
    a = i * 2 * math.pi / n + 0.2
    r = 0.74 + (0.04 if i % 2 else -0.02)
    cols = ['stone', 'stone_dark', 'stone_light']
    m.blob((0.34 + 0.06 * (i % 3), 0.28, 0.26 + 0.04 * (i % 2)), (math.cos(a) * r, math.sin(a) * r, 0.17), cols[i % 3], rough=0.2, subdiv=1, colors=[cols[i % 3], 'stone_dark'], flat_bottom=0.0)
# charred logs, low radial pile
for i in range(5):
    a = i * 72 + 15
    m.cyl(0.075, 0.75, (0.0, 0.0, 0.115), 'black' if i % 2 else 'bark_dead', rot=(84, 0, a), seg=6)
for (x, y) in [(0.0, 0.0), (0.18, 0.1), (-0.15, -0.12), (0.1, -0.2), (-0.2, 0.15)]:
    m.blob((0.14, 0.12, 0.03), (x, y, 0.16), 'lamp_warm', rough=0.1, subdiv=0)
for (x, y) in [(0.0, 0.2), (-0.2, 0.0), (0.2, -0.05)]:
    m.blob((0.12, 0.1, 0.03), (x, y, 0.17), 'hazard_yellow', rough=0.1, subdiv=0)
# tripod with a dented pot hanging over the logs
for k in range(3):
    a = math.radians(k * 120 + 90)
    m.tube([(math.cos(a) * 0.62, math.sin(a) * 0.62, 0.03), (0.0, 0.0, 0.47)], 0.022, 'wood_dark', seg=5)
m.cyl(0.03, 0.04, (0.0, 0.0, 0.47), 'rust_dark', seg=6)
m.tube([(0.0, 0.0, 0.46), (0.0, 0.0, 0.4)], 0.01, 'steel_dark', seg=4)
m.cyl(0.15, 0.15, (0.0, 0.0, 0.31), 'gunmetal', seg=10, r2=0.19)
m.cyl(0.2, 0.025, (0.0, 0.0, 0.39), 'steel_dark', seg=10)
m.cyl(0.14, 0.01, (0.0, 0.0, 0.405), 'rust_dark', seg=8)
finish(m, bounds={'x': (-0.95, 0.95), 'y': (-0.95, 0.95), 'z': (0, 0.5)})
