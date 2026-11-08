import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('bunker_bulkhead_lamp', seed=65)
m.box((0.2, 0.04, 0.3), (0, -0.02, 0), 'steel_dark')
m.box((0.3, 0.04, 0.08), (0, -0.02, 0.0), 'steel_dark') if False else None
m.cyl(0.085, 0.11, (0, -0.115, 0), 'lamp_warm', rot=(90, 0, 0), seg=10, r2=0.08)
m.cyl(0.12, 0.02, (0, -0.06, 0), 'steel', rot=(90, 0, 0), seg=10)
m.cyl(0.1, 0.02, (0, -0.17, 0), 'steel', rot=(90, 0, 0), seg=10)
for x in (-0.085, 0.0, 0.085):
    m.box((0.015, 0.1, 0.22), (x, -0.1, 0.0), 'gunmetal') if False else None
for a in range(0, 180, 45):
    m.box((0.02, 0.02, 0.24), (0.0, -0.105, 0.0), 'gunmetal', rot=(0, a, 0)) if False else None
for x in (-0.09, 0, 0.09):
    m.box((0.012, 0.012, 0.24), (x, -0.165, 0), 'gunmetal'); m.box((0.012, 0.1, 0.012), (x, -0.115, 0.115), 'gunmetal'); m.box((0.012, 0.1, 0.012), (x, -0.115, -0.115), 'gunmetal')
for z in (-0.09, 0, 0.09):
    m.box((0.24, 0.1, 0.015), (0, -0.105, z), 'gunmetal') if False else None
for z in (-0.12, 0.12): m.cyl(0.02, 0.02, (0, -0.045, z), 'steel', rot=(90, 0, 0), seg=6)
m.box((0.35, 0.02, 0.05), (0, -0.01, 0.0), 'steel_dark', ) if False else None
finish(m, bounds={'x': (-0.175, 0.175), 'y': (-0.2, 0.0), 'z': (-0.15, 0.15)})
