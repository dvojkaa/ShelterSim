import os, sys, math, random; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _lmk_sign import sign_base
m = Model('lmk_sign_hospital', seed=1101)
sign_base(m, 'paint_white')
h, w = 0.25, 0.7
m.poly([(-w, -h), (-h, -h), (-h, -w), (h, -w), (h, -h), (w, -h), (w, h), (h, h), (h, w), (-h, w), (-h, h), (-w, h)], 0.05, (-0.8, -0.12, 2.5), 'red_bright')
m.box((0.3, 0.06, 0.9), (0.9, -0.11, 2.75), 'plaster_dirty')
m.box((1.0, 0.06, 0.18), (1.1, -0.11, 2.35), 'paint_green')
m.box((1.0, 0.06, 0.18), (1.1, -0.11, 2.05), 'paint_green')
m.box((0.8, 0.06, 0.18), (1.0, -0.11, 2.75), 'paint_green')
finish(m, bounds={'x': (-2.0, 2.0), 'y': (-0.3, 0.4), 'z': (0, 3.6)})
