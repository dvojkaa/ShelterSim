import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
m = Model('lair_warning_post', seed=303)
# rubble footing
m.blob((0.8, 0.8, 0.3), (0, 0, 0.1), 'stone_dark', rough=0.08, subdiv=1, colors=['stone_dark', 'concrete_dark'], flat_bottom=-0.1)
m.box((0.34, 0.34, 0.12), (0, 0, 0.3), 'rust_dark', bevel=0.02)
# main post with hazard stripe bands
m.box((0.18, 0.18, 2.5), (0, 0, 1.5), 'wood_dark', bevel=0.015)
for i in range(6):
    m.box((0.2, 0.2, 0.13), (0, 0, 0.55 + i * 0.26), 'hazard_yellow' if i % 2 == 0 else 'black', bevel=0.005)
for z in (0.9, 1.9):
    m.box((0.26, 0.04, 0.05), (0, -0.1, z + 0.3), 'rust_dark')
# braces
for s in (-1, 1):
    m.box((0.06, 0.06, 0.6), (s * 0.18, 0, 0.7), 'wood', rot=(0, s * 30, 0))
# crossbar with hanging scrap plates (blank)
m.box((0.8, 0.07, 0.07), (0, 0, 2.15), 'wood', bevel=0.01)
for i, x in enumerate((-0.32, 0.32)):
    m.tube([(x, 0, 2.12), (x, 0, 1.9)], 0.01, 'steel_dark', seg=4)
    m.box((0.2, 0.025, 0.28 + 0.06 * i), (x, 0, 1.74 - 0.03 * i), 'rust' if i == 0 else 'steel', rot=(0, 0, 8 - 16 * i), bevel=0.005)
    m.box((0.2, 0.01, 0.06), (x, -0.02, 1.84 - 0.03 * i), 'hazard_yellow', rot=(0, 0, 8 - 16 * i))
# skull totem on top
m.blob((0.4, 0.34, 0.34), (0, 0, 2.68), 'bone', rough=0.05, subdiv=1, colors=['bone', 'bone'], seed=3)
m.box((0.2, 0.2, 0.12), (0, -0.02, 2.46), 'bone', bevel=0.02, taper=(0.8, 0.8))   # jaw
for x in (-0.08, 0.08):
    m.box((0.1, 0.05, 0.11), (x, -0.16, 2.69), 'black', rot=(0, 0, 0))             # eye sockets
    m.box((0.025, 0.025, 0.12), (x * 0.5, -0.16, 2.45), 'black') if False else None
m.box((0.04, 0.04, 0.07), (0, -0.15, 2.57), 'black', rot=(0, 0, 45))               # nose
for i in range(4):
    m.box((0.02, 0.02, 0.07), (-0.07 + i * 0.047, -0.11, 2.45), 'gunmetal')       # teeth gaps
# crossed bones under the skull
m.cyl(0.03, 0.7, (0, -0.05, 2.35), 'bone', rot=(0, 55, 0), seg=6)
m.cyl(0.03, 0.7, (0, -0.07, 2.35), 'bone', rot=(0, -55, 0), seg=6)
for s in (-1, 1):
    for t in (-1, 1):
        m.blob((0.09, 0.08, 0.08), (s * 0.2, -0.06, 2.35 + t * 0.2), 'bone', rough=0.04, subdiv=0, colors=['bone']) if False else None
# spikes on top
for x in (-0.12, 0.12):
    m.cyl(0.025, 0.25, (x, 0.0, 2.9), 'steel_dark', seg=5, r2=0.002)
# red warning lamp at the very top (code-colourable)
lamp = m.part('Lamp', pivot=(0, 0, 3.05))
lamp.cyl(0.07, 0.14, (0, 0, 3.12), 'red_bright', seg=8, r2=0.05)
m.cyl(0.09, 0.05, (0, 0, 2.86), 'gunmetal', seg=8)
m.cyl(0.015, 0.3, (0, 0, 2.97), 'steel_dark', seg=5) if False else None
m.cyl(0.04, 0.12, (0, 0, 2.93), 'gunmetal', seg=6)
finish(m, bounds={'x': (-0.4, 0.4), 'y': (-0.4, 0.4), 'z': (0, 3.3)})
