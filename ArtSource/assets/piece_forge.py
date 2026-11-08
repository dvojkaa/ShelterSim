import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('piece_forge', seed=82)
# brick hearth
m.box((0.8, 0.8, 0.5), (-0.25, 0, 0.35), 'brick', bevel=0.01)
for z in (0.2, 0.3, 0.4, 0.5):
    m.box((0.82, 0.82, 0.015), (-0.25, 0, z), 'brick_dark')
m.box((0.7, 0.7, 0.04), (-0.25, 0, 0.62), 'brick_dark')
m.box((0.6, 0.6, 0.08), (-0.25, 0, 0.6), 'rust_dark') if False else None
# coals bed
m.box((0.5, 0.5, 0.05), (-0.25, 0, 0.64), 'orange')
m.blob((0.4, 0.4, 0.14), (-0.25, 0, 0.66), 'warning_red', rough=0.3, colors=['orange', 'lamp_warm', 'warning_red', 'orange'])
# hood and chimney at back
m.box((0.78, 0.12, 0.7), (-0.25, 0.34, 0.95), 'brick', bevel=0.01)
m.box((0.3, 0.3, 0.2), (-0.25, 0.25, 1.28), 'steel_dark', taper=(0.7, 0.7)) if False else None
m.box((0.78, 0.4, 0.1), (-0.25, 0.2, 1.1), 'steel_dark', rot=(30, 0, 0)) if False else None
m.tube([(-0.25, 0.3, 1.2), (-0.25, 0.3, 1.3)], 0.12, 'steel_dark', seg=6)
for s in (-1, 1): m.box((0.1, 0.55, 0.5), (-0.25 + s * 0.34, 0.12, 0.95), 'brick')
m.box((0.78, 0.5, 0.06), (-0.25, 0.1, 1.2), 'steel_dark', rot=(0, 0, 0))
m.cyl(0.18, 0.1, (-0.25, 0.3, 1.25), 'steel_dark', seg=8, r2=0.1) if False else None
# bellows on the left
m.box((0.1, 0.2, 0.2), (-0.66, -0.1, 0.55), 'leather_dark') if False else None
m.cyl(0.04, 0.3, (-0.6, -0.15, 0.62), 'steel_dark', rot=(0, 90, 0), seg=6) if False else None
m.tube([(-0.55, -0.1, 0.6), (-0.65, -0.1, 0.6)], 0.03, 'gunmetal', seg=6) if False else None
m.box((0.25, 0.2, 0.12), (0.0, 0.0, 0.0), 'black') if False else None
m.box((0.16, 0.34, 0.12), (-0.6, -0.15, 0.55), 'leather', rot=(0, 0, 0))
m.box((0.14, 0.3, 0.1), (-0.68, -0.15, 0.67), 'leather_dark', taper=(0.6, 1.0)) if False else None
m.box((0.12, 0.2, 0.1), (-0.6, -0.05, 0.68), 'leather_dark', rot=(15, 0, 0))
m.tube([(-0.55, 0.0, 0.58), (-0.45, 0.1, 0.62)], 0.025, 'gunmetal', seg=6)
m.box((0.05, 0.3, 0.04), (-0.6, -0.28, 0.66), 'wood_dark', rot=(0, 0, 0))
# anvil on stump (right)
m.cyl(0.18, 0.4, (0.5, -0.1, 0.2), 'wood_dark', seg=8)
m.box((0.3, 0.14, 0.08), (0.5, -0.1, 0.44), 'gunmetal')
m.box((0.14, 0.12, 0.1), (0.5, -0.1, 0.38), 'steel_dark', taper=(0.7, 0.8))
m.box((0.22, 0.12, 0.06), (0.5, -0.1, 0.5), 'steel_dark')
m.box((0.12, 0.08, 0.04), (0.62, -0.1, 0.5), 'steel_dark', rot=(0, 0, 0))
m.cyl(0.015, 0.3, (0.38, 0.1, 0.55), 'wood', rot=(0, 90, 0), seg=4) if False else None
m.box((0.05, 0.05, 0.05), (0.5, -0.1, 0.53), 'steel')  # nothing hot
# tongs on hearth edge
m.box((0.04, 0.4, 0.02), (-0.25, -0.38, 0.66), 'steel_dark') if False else None
finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.5, 0.5), 'z': (0, 1.3)})
