import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bunker_locker', seed=67)
cols = ['army_green', 'army_green_dark', 'army_green', 'steel_dark']
for i in range(4):
    x = -0.45 + i * 0.3
    m.box((0.29, 0.5, 1.9), (x, 0, 1.0), cols[i], bevel=0.01)
    m.box((0.24, 0.02, 0.5), (x, -0.24, 1.55), 'gunmetal')
    for z in (1.7, 1.62, 1.54): m.box((0.14, 0.015, 0.02), (x, -0.262, z), 'steel') if False else None
    for k in range(4):
        m.box((0.16, 0.01, 0.018), (x, -0.245, 1.68 + k * 0.04), 'black')
    m.box((0.05, 0.03, 0.12), (x + 0.08, -0.24, 1.1), 'chrome') if False else m.box((0.04, 0.02, 0.1), (x + 0.08, -0.24, 1.1), 'chrome')
    m.box((0.24, 0.01, 0.02), (x, -0.245, 0.9), 'steel_dark')
for x in (-0.6, -0.15, 0.3, 0.6): m.box((0.01, 0.01, 0.0001), (x, 0, 0.0), 'black') if False else None
m.box((1.24, 0.5, 0.06), (0, 0, 1.97), 'steel_dark') if False else m.box((1.2, 0.5, 0.06), (0, 0, 1.97), 'steel_dark')
m.box((1.2, 0.5, 0.08), (0, 0, 0.04), 'gunmetal')
m.box((0.12, 0.02, 0.2), (-0.15, -0.26, 0.4), 'paint_white') if False else m.box((0.12, 0.01, 0.1), (-0.15, -0.245, 1.35), 'paint_white')
finish(m, bounds={'x': (-0.6, 0.6), 'y': (-0.25, 0.25), 'z': (0, 2.0)})
