import os, sys, math; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('concrete_mixer', seed=75)
# frame and wheels
m.box((0.9, 0.5, 0.08), (0, 0.0, 0.5), 'steel_dark')
for s in (-1, 1):
    m.cyl(0.25, 0.1, (s * 0.5, 0.0, 0.25), 'rubber', rot=(0, 90, 0), seg=10)
    m.cyl(0.1, 0.12, (s * 0.5, 0.0, 0.25), 'steel', rot=(0, 90, 0), seg=8)
    beam(m, (s * 0.35, 0.5), (s * 0.55, 0.0), 0.05, 0.05, 0.0, 'steel') if False else None
    m.box((0.08, 0.08, 0.3), (s * 0.4, 0.0, 0.3), 'steel_dark')
m.box((0.08, 0.08, 0.3), (0.0, 0.4, 0.3), 'steel_dark') if False else None
# legs (tow stand)
m.box((0.06, 0.5, 0.06), (0.0, -0.6, 0.45), 'steel_dark') if False else None
m.box((0.06, 0.06, 0.4), (0.0, -0.58, 0.3), 'steel')
m.box((0.2, 0.2, 0.03), (0.0, -0.58, 0.03), 'steel_dark') if False else None
m.box((0.08, 0.7, 0.08), (0.0, -0.3, 0.5), 'steel_dark')
# motor
m.box((0.3, 0.25, 0.25), (0.0, 0.4, 0.7), 'warning_red', bevel=0.02)
m.cyl(0.07, 0.1, (0.0, 0.4, 0.88), 'gunmetal', seg=6)
# yoke
for s in (-1, 1): m.box((0.06, 0.06, 0.5), (s * 0.3, 0.0, 0.8), 'steel_dark')
m.box((0.66, 0.08, 0.06), (0.0, 0.0, 0.55), 'steel_dark') if False else None
# drum (tilted cone pair)
m.cyl(0.5, 0.6, (0.0, 0.0, 1.0), 'orange', rot=(-40, 0, 0), seg=10, r2=0.5)
m.cyl(0.5, 0.35, (0.0, 0.18, 0.95), 'orange', rot=(-40, 0, 0), seg=10, r2=0.2) if False else None
m.cyl(0.5, 0.4, (0.0, 0.22 * 0.64 * 1.0 + 0.1, 1.0 - 0.22 * 0.77 - 0.0), 'orange', rot=(-40, 0, 0), seg=10, r2=0.2) if False else None
ang = math.radians(40)
dy, dz = -math.sin(ang), math.cos(ang)
c = 0.82; R = 0.4
m.cyl(R, 0.4, (0.0, 0.03 + dy * 0.45, c + dz * 0.45), 'orange', rot=(-40, 0, 0), seg=10, r2=0.2)
m.cyl(R, 0.4, (0.0, 0.03 - dy * 0.4, c - dz * 0.4), 'orange', rot=(-40, 0, 0), seg=10, r2=0.3)
m.cyl(0.22, 0.04, (0.0, 0.03 + dy * 0.66, c + dz * 0.66), 'concrete_dark', rot=(-40, 0, 0), seg=10)
m.cyl(R + 0.02, 0.06, (0.0, 0.03, c), 'steel_dark', rot=(-40, 0, 0), seg=10)
# handwheel
m.cyl(0.12, 0.04, (0.32, 0.0, 0.62), 'steel', rot=(0, 90, 0), seg=10)
m.box((0.02, 0.28, 0.03), (0.35, 0.0, 0.62), 'steel_dark')
# cement splatter
m.blob((0.3, 0.3, 0.05), (0.0, -0.9, 0.0), 'concrete_light', rough=0.15, subdiv=1) if False else None
finish(m, bounds={'x': (-0.7, 0.7), 'y': (-0.7, 0.7), 'z': (0, 1.6)})
