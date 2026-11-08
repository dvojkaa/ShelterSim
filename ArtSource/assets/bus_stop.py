import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bus_stop', seed=40)
m.box((2.9, 1.1, 0.06), (0, 0, 0.03), 'concrete_light') if False else None
for s in (-1, 1): m.box((0.07, 0.07, 2.35), (s * 1.4, 0.45, 1.175), 'steel_dark')
for s in (-1, 1): m.box((0.07, 0.07, 2.35), (s * 1.4, -0.45, 1.175), 'steel_dark')
m.box((3.0, 1.2, 0.08), (0, 0, 2.38), 'plastic_blue', rot=(-4, 0, 0), bevel=0.02)
m.box((3.0, 0.08, 0.12), (0, -0.62, 2.3), 'steel_dark') if False else None
# back wall (glass panels) and side
m.box((2.78, 0.04, 1.5), (0, 0.5, 1.2), 'glass')
for x in (-0.46, 0.46):
    m.box((0.05, 0.06, 1.55), (x, 0.5, 1.2), 'steel_dark')
m.box((2.8, 0.06, 0.05), (0, 0.5, 0.45), 'steel_dark')
m.box((0.04, 0.9, 1.5), (-1.4, 0, 1.2), 'glass')
# bench
m.box((1.8, 0.4, 0.06), (0, 0.15, 0.5), 'wood_light', bevel=0.01)
for s in (-1, 1): m.box((0.06, 0.38, 0.5), (s * 0.8, 0.15, 0.25), 'steel_dark')
m.box((1.8, 0.05, 0.3), (0, 0.33, 0.8), 'wood_light')
# sign
m.cyl(0.03, 2.45, (1.75, -0.4, 1.225), 'steel', seg=6) if False else None
m.box((0.5, 0.04, 0.5), (-0.9, 0.46, 1.8), 'hazard_yellow') if False else None
m.box((0.9, 0.05, 0.6), (0.9, 0.47, 1.65), 'paint_white')
m.box((0.5, 0.06, 0.3), (0.9, 0.465, 1.65), 'plastic_blue')
finish(m, bounds={'x': (-1.5, 1.5), 'y': (-0.6, 0.6), 'z': (0, 2.5)})
