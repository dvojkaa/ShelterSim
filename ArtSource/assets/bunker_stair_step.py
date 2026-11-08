import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bunker_stair_step', seed=66)
m.box((2.8, 0.3, 0.18), (0, 0, -0.09), 'concrete', bevel=0.01)
m.box((2.8, 0.05, 0.03), (0, -0.125, -0.015), 'steel')
m.box((2.8, 0.05, 0.03), (0, -0.125, -0.015), 'steel') if False else None
m.box((0.08, 0.3, 0.03), (-1.36, 0, -0.015), 'steel_dark')
m.box((0.08, 0.3, 0.03), (1.36, 0, -0.015), 'steel_dark')
m.box((2.8, 0.06, 0.04), (0, 0.12, -0.02), 'hazard_yellow')
m.box((2.6, 0.01, 0.01), (0, 0.0, 0.0), 'concrete_dark') if False else None
for x in (-1.2, -0.7, -0.2, 0.3, 0.8, 1.3):
    m.box((0.18, 0.04, 0.005), (x, -0.02, 0.0), 'concrete_light') if False else None
finish(m, bounds={'x': (-1.4, 1.4), 'y': (-0.15, 0.15), 'z': (-0.18, 0.0)})
