import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _sub import *
m = Model('prop_supply_cage', seed=71)
W, D, H = 2.4, 1.2, 2.2
hx, hy = W / 2, D / 2
# floor pallet-style base (low, interior clear)
m.box((W, D, 0.06), (0, 0, 0.03), 'steel_dark', bevel=0.01)
# corner posts
for sx in (-1, 1):
    for sy in (-1, 1):
        m.box((0.1, 0.1, H), (sx * (hx - 0.05), sy * (hy - 0.05), H / 2), 'steel_dark', bevel=0.01)
# top and mid rails
for z in (H - 0.04, 1.1, 0.3):
    for sy in (-1, 1):
        m.box((W, 0.06, 0.07), (0, sy * (hy - 0.05), z), 'gunmetal')
    for sx in (-1, 1):
        m.box((0.06, D, 0.07), (sx * (hx - 0.05), 0, z), 'gunmetal')
# roof frame crossbars
for x in (-0.6, 0.6): m.box((0.05, D, 0.05), (x, 0, H - 0.04), 'gunmetal')
# wire mesh: thin vertical + horizontal wires on back (+Y) and sides, with door on front (-Y)
def wires_x(y, x0, x1, z0, z1, step=0.2):
    n = int((x1 - x0) / step)
    for i in range(n + 1):
        m.box((0.014, 0.014, z1 - z0), (x0 + i * (x1 - x0) / n, y, (z0 + z1) / 2), 'steel')
    k = int((z1 - z0) / step)
    for j in range(k + 1):
        m.box((x1 - x0, 0.014, 0.014), ((x0 + x1) / 2, y, z0 + j * (z1 - z0) / k), 'steel')
def wires_y(x, y0, y1, z0, z1, step=0.4):
    n = int((y1 - y0) / step)
    for i in range(n + 1):
        m.box((0.014, 0.014, z1 - z0), (x, y0 + i * (y1 - y0) / n, (z0 + z1) / 2), 'steel')
    k = int((z1 - z0) / 0.4)
    for j in range(k + 1):
        m.box((0.014, y1 - y0, 0.014), (x, (y0 + y1) / 2, z0 + j * (z1 - z0) / k), 'steel')
wires_x(hy - 0.05, -hx + 0.1, hx - 0.1, 0.1, H - 0.1, 0.4)
for sx in (-1, 1): wires_y(sx * (hx - 0.05), -hy + 0.1, hy - 0.1, 0.1, H - 0.1)
# front: fixed panel left (x -1.15..-0.5), door right (x -0.5..0.5 is door? place door centre-right)
fy = -(hy - 0.15)
wires_x(fy, -hx + 0.1, -0.5, 0.1, H - 0.1, 0.4)
wires_x(fy, 0.5, hx - 0.1, 0.1, H - 0.1, 0.4)
# door frame at x -0.5..0.5
for x in (-0.5, 0.5): m.box((0.06, 0.06, H - 0.1), (x, fy, H / 2), 'steel_dark')
m.box((1.0, 0.06, 0.06), (0, fy, H - 0.12), 'steel_dark')
# door leaf (slightly proud) with mesh
dy = fy - 0.03
for x in (-0.42, 0.42): m.box((0.05, 0.04, 1.95), (x, dy, 1.05), 'steel')
m.box((0.88, 0.04, 0.05), (0, dy, 0.1), 'steel'); m.box((0.88, 0.04, 0.05), (0, dy, 2.02), 'steel')
m.box((0.88, 0.04, 0.05), (0, dy, 1.05), 'steel')
wires_x(dy, -0.4, 0.4, 0.12, 2.0, 0.22)
# hinges on left
for z in (0.4, 1.7): m.cyl(0.04, 0.2, (-0.5, dy, z), 'gunmetal', seg=6)
# hasp + padlock on right
m.box((0.12, 0.03, 0.1), (0.38, dy - 0.03, 1.05), 'steel_dark')
m.box((0.09, 0.03, 0.1), (0.38, dy - 0.06, 0.95), 'chrome') if False else None
m.box((0.08, 0.04, 0.09), (0.38, dy - 0.05, 0.93), 'mustard_dark')
m.tube([(0.35, dy - 0.05, 0.97), (0.35, dy - 0.05, 1.03), (0.41, dy - 0.05, 1.03), (0.41, dy - 0.05, 0.97)], 0.012, 'chrome', seg=5)
# sign plate above door, blank stencil: dark plate, hazard stripes, and a few blank bars
sy_ = -(hy + 0.0)
m.box((1.3, 0.04, 0.32), (-0.0, -hy - 0.02, H + 0.0 - 0.17), 'black') if False else None
m.box((1.4, 0.03, 0.34), (0, -hy + 0.055, H - 0.3), 'paint_white', bevel=0.005)
m.box((1.3, 0.035, 0.24), (0, -hy + 0.025, H - 0.3), 'gunmetal')
for i in range(5):
    m.box((0.2, 0.04, 0.18), (-0.5 + i * 0.25, -hy + 0.025, H - 0.3), 'hazard_yellow' if i % 2 == 0 else 'black')
for x in (-0.7, 0.7):
    for z in (H - 0.16, H - 0.44): m.cyl(0.02, 0.03, (x, -hy + 0.03, z), 'steel', rot=(90, 0, 0), seg=6)
# hanging chains from sign
for x in (-0.55, 0.55): m.box((0.02, 0.02, 0.14), (x, -hy - 0.02, H - 0.05), 'steel_dark') if False else None
# base kick plate on front, floor stripes
m.box((W - 0.2, 0.03, 0.14), (0, -hy - 0.015, 0.13), 'hazard_yellow') if False else None
finish(m, bounds={'x': (-1.2, 1.2), 'y': (-0.6, 0.6), 'z': (0, 2.2)})
