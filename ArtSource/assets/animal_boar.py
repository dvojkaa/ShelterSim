"""Bristly dark wasteland boar with tusks. Feet z=0, front +Y. Parts: Torso Head Foreleg_L/R Hindleg_L/R."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _animal_shift import ShModel
_m = Model('animal_boar', seed=32)
m = ShModel(_m, -.14)

t = m.part('Torso', (0, 0, .55))
t.blob((.62, .95, .58), (0, 0, .62), 'bark', rough=.1, subdiv=2, colors=['bark', 'bark', 'leather_dark', 'bark_light'])
t.blob((.5, .4, .5), (0, .28, .66), 'leather_dark', rough=.08)             # heavy shoulders
t.blob((.5, .4, .46), (0, -.34, .6), 'bark', rough=.08)
for i in range(7):                                                        # bristle ridge
    y = -.4 + i*.14
    t.box((.04, .09, .12), (0, y, .9 + (.03 if 1 < i < 5 else 0)), 'black', rot=(18, 0, 0), taper=(.2, .5))
t.tube([(0, -.5, .72), (0, -.58, .66), (.03, -.6, .54)], .018, 'bark_light', seg=4)  # tail
t.box((.012, .4, .04), (.31, .05, .75), 'bone', rot=(0, 0, -8))           # scar

h = m.part('Head', (0, 0.400, .62))
h.blob((.4, .46, .4), (0, 0.500, .62), 'bark', rough=.07, subdiv=2)
h.box((.2, .24, .17), (0, 0.760, .52), 'bark_light', bevel=.03)             # snout
h.box((.17, .03, .13), (0, 0.885, .52), 'skin_dark')
for s in (-1, 1):
    h.box((.03, .03, .03), (s*.07, 0.900, .52), 'black')
    h.cyl(.032, .24, (s*.1, 0.820, .44), 'bone', rot=(-60, s*20, 0), seg=5, r2=.004)  # tusk
    h.box((.03, .04, .04), (s*.14, 0.660, .72), 'hazard_yellow')           # eye glint
    h.blob((.08, .14, .12), (s*.15, 0.460, .8), 'leather_dark', rot=(0, s*30, 0), rough=.05)  # ears
    h.box((.03, .1, .13), (s*.13, 0.500, .86), 'black', rot=(0, 0, s*10), taper=(.2, .4))

def leg(name, x, y, hind):
    p = m.part(name, (x, y, .6))
    p.blob((.17, .24, .3), (x, y, .52), 'bark', rough=.05)
    p.tube([(x, y, .6), (x, y + (-.03 if hind else 0), .3), (x, y, .07)], .052, 'bark_light', seg=6)
    p.box((.1, .13, .08), (x, y + .02, .04), 'black', bevel=.01)
for s, n in ((-1, 'L'), (1, 'R')):
    leg('Foreleg_' + n, s*.2, .32, False)
    leg('Hindleg_' + n, s*.2, -.34, True)

finish(_m, bounds={'x': (-.4, .4), 'y': (-.8, .8), 'z': (0, 1.0)})
