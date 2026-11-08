import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('site_terminal', seed=72)
# folding table 0.9 x 0.7 at height 1.0? bounds z up to 1.25 incl. laptop
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.04, 0.04, 0.82), (sx * 0.38, sy * 0.28, 0.41), 'steel')
m.box((0.88, 0.68, 0.04), (0, 0, 0.84), 'wood_light', bevel=0.01)
m.box((0.88, 0.04, 0.04), (0, -0.33, 0.8), 'steel_dark') if False else None
# X braces
for sx in (-1, 1):
    m.box((0.03, 0.6, 0.03), (sx * 0.38, 0, 0.45), 'steel_dark', rot=(0, 0, 0)) if False else None
    beam_yz(m, (-0.28, 0.1), (0.28, 0.75), 0.03, 0.03, sx * 0.38, 'steel_dark')
# rugged laptop (screen faces -Y)
m.box((0.4, 0.3, 0.05), (0.0, 0.05, 0.89), 'gunmetal', bevel=0.01)
m.box((0.34, 0.2, 0.01), (0.0, 0.03, 0.92), 'steel_dark')
m.box((0.4, 0.04, 0.3), (0.0, 0.17, 1.07), 'gunmetal', rot=(-10, 0, 0), bevel=0.01)
m.box((0.34, 0.02, 0.24), (0.0, 0.145, 1.075), 'lamp_warm', rot=(-10, 0, 0))
m.box((0.34, 0.015, 0.24), (0.0, 0.14, 1.075), 'glass') if False else None
for sx in (-1, 1): m.box((0.03, 0.05, 0.3), (sx * 0.2, 0.17, 1.07), 'hazard_yellow', rot=(-10, 0, 0))
m.cyl(0.015, 0.15, (-0.15, 0.17, 1.25 - 0.1), 'black', seg=4) if False else None
# clipboard
m.box((0.2, 0.28, 0.015), (-0.3, -0.15, 0.865), 'wood_dark', rot=(0, 0, 12))
m.box((0.17, 0.24, 0.005), (-0.3, -0.15, 0.875), 'paper' if False else 'fabric_white', rot=(0, 0, 12))
m.box((0.08, 0.03, 0.02), (-0.28, -0.03, 0.88), 'chrome', rot=(0, 0, 12))
# hard hat
m.blob((0.26, 0.3, 0.16), (0.3, -0.18, 0.92), 'hazard_yellow', rough=0.04, subdiv=1, flat_bottom=-0.0)
m.box((0.28, 0.12, 0.015), (0.3, -0.28, 0.87), 'hazard_yellow')
# little pulsing "use me" beacon
m.cyl(0.03, 0.08, (-0.35, 0.22, 0.9), 'steel_dark', seg=6)
m.cyl(0.04, 0.05, (-0.35, 0.22, 0.97), 'lamp_warm', seg=8)
finish(m, bounds={'x': (-0.45, 0.45), 'y': (-0.35, 0.35), 'z': (0, 1.25)})
