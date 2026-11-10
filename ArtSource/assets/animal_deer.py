"""Lean scarred wasteland deer. Feet z=0, front +Y (like enemy creatures). Parts: Torso Head Foreleg_L/R Hindleg_L/R."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ss_art import Model, finish
from _animal_shift import ShModel
_m = Model('animal_deer', seed=31)
m = ShModel(_m, -.15)

t = m.part('Torso', (0, 0, 1.0))
t.blob((.40, 1.0, .46), (0, -.04, 1.06), 'leaf_brown', rough=.08, subdiv=2, colors=['leaf_brown', 'leaf_brown', 'wood_light'])
t.blob((.30, .55, .22), (0, -.02, .93), 'sand', rough=.06)               # pale belly
t.blob((.34, .34, .40), (0, .36, 1.12), 'leaf_brown', rough=.06)         # chest
t.blob((.34, .36, .38), (0, -.38, 1.06), 'leaf_brown', rough=.06)        # rump
t.box((.06, .10, .2), (0, -.54, 1.10), 'sand', rot=(-25, 0, 0))         # tail
t.box((.012, .5, .04), (.1, -.1, 1.28), 'leather_dark', rot=(0, 0, 10))  # scars
t.box((.012, .35, .04), (-.1, .05, 1.2), 'leather_dark', rot=(0, 0, -12))
t.box((.14, .22, .02), (.19, -.3, 1.15), 'bone', rot=(0, 25, 0))         # ribs/bare patch

h = m.part('Head', (0, .42, 1.3))
h.tube([(0, .34, 1.15), (0, .56, 1.55), (0, .64, 1.66)], .1, 'leaf_brown', seg=6)       # neck
h.blob((.2, .3, .24), (0, .7, 1.7), 'leaf_brown', rough=.05)
h.blob((.13, .2, .13), (0, .81, 1.62), 'sand', rough=.05)               # muzzle
h.box((.05, .04, .04), (0, .92, 1.63), 'black')
for s in (-1, 1):
    h.box((.04, .03, .035), (s*.085, .76, 1.72), 'black')                 # eyes
    h.blob((.06, .16, .14), (s*.11, .62, 1.76), 'leaf_brown', rot=(0, s*45, 0), rough=.05)  # ears
    h.tube([(s*.06, .6, 1.78), (s*.12, .56, 1.98), (s*.28, .52, 2.18), (s*.3, .5, 2.38)], .022, 'bone', seg=5)
    h.tube([(s*.12, .56, 1.98), (s*.08, .66, 2.14), (s*.1, .7, 2.26)], .017, 'bone', seg=5)
    h.tube([(s*.22, .53, 2.1), (s*.36, .5, 2.2)], .016, 'bone', seg=5)

def leg(name, x, y):
    s = 1 if x > 0 else -1
    p = m.part(name, (x, y, .95))
    hind = y < 0
    kx = .0
    knee = (x, y + (-.08 if hind else .04), .5)
    p.blob((.19, .26, .36), (x, y, .86), 'leaf_brown', rough=.05)          # thigh/shoulder
    p.tube([(x, y, .9), knee, (x, y + (.04 if hind else -.01), .09)], .05, 'wood', seg=6)
    p.blob((.11, .11, .12), knee, 'wood', rough=.04)
    p.box((.075, .12, .09), (x, y + .02, .045), 'leather_dark', bevel=.01)  # hoof
for s, n in ((-1, 'L'), (1, 'R')):
    leg('Foreleg_' + n, s*.12, .34)
    leg('Hindleg_' + n, s*.12, -.38)

finish(_m, bounds={'x': (-.4, .4), 'y': (-.8, .8), 'z': (0, 2.4)})
