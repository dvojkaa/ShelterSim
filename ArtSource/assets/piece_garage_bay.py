from pathlib import Path
import bpy
from ss_art import Model, finish

m = Model('piece_garage_bay', seed=3302)

# pad: concrete slab, oil stains, hazard edge strips
m.box((5.9, 5.9, .12), (0, 0, .06), 'concrete')
m.box((5.9, .14, .02), (0, -2.88, .125), 'hazard_yellow')
m.box((.14, 5.9, .02), (-2.88, 0, .125), 'hazard_yellow')
m.box((.14, 5.9, .02), (2.88, 0, .125), 'hazard_yellow')
m.box((1.6, 1.2, .01), (.3, .4, .125), 'concrete_dark', rot=(0, 0, 20))
m.box((3.4, .05, .01), (0, -2.0, .125), 'hazard_yellow')
# low drive-on lift rails (two) + hydraulic housings
for x in (-1.2, 1.2):
    m.box((.55, 3.0, .12), (x, .0, .19), 'steel_dark')
    m.box((.45, 2.9, .03), (x, .0, .26), 'steel')
    m.box((.5, .7, .10), (x, -1.9, .17), 'steel_dark', rot=(8, 0, 0))   # entry ramp
    m.box((.3, .4, .14), (x, 1.75, .19), 'hazard_yellow')
m.cyl(.12, .5, (0, 2.0, .35), 'steel', seg=8)  # hydraulic pump piston
m.box((.5, .5, .3), (0, 2.35, .27), 'orange')

# posts and roof frame
for x in (-2.8, 2.8):
    for y in (-2.8, 2.8):
        m.box((.2, .2, 2.34), (x, y, 1.29), 'steel_dark')
        m.box((.34, .34, .1), (x, y, .17), 'rust_dark')
        m.box((.3, .3, .1), (x, y, 2.34), 'rust')
for y in (-2.8, 2.8):
    m.box((5.8, .2, .2), (0, y, 2.28), 'rust_dark')
for x in (-2.8, 2.8):
    m.box((.2, 5.8, .2), (x, 0, 2.28), 'rust_dark')
for y in (-1.4, 0, 1.4):
    m.box((5.8, .12, .14), (0, y, 2.30), 'steel_dark')
# corrugated roof (faceted ridges)
for i in range(12):
    x = -2.70 + i * .47
    m.box((.26, 5.9, .08), (x, 0, 2.38), 'rust' if i % 3 else 'rust_dark', rot=(0, 0, 0))
    m.box((.26, 5.9, .08), (x + .23, 0, 2.35), 'rust_light' if i % 4 else 'steel')
# gantry crane beam on rails with hoist (high above)
m.box((5.6, .16, .16), (0, 0.6, 2.12), 'hazard_yellow')
m.box((.4, .4, .22), (.8, 0.6, 2.0), 'steel_dark')
m.cyl(.03, .45, (.8, .6, 1.8), 'chrome', seg=6)
# tool wall at back (+Y) with pegboard, tools, shelf and bench
m.box((5.4, .12, 1.7), (0, 2.88, 1.05), 'wood_dark')
m.box((5.2, .06, 1.4), (0, 2.80, 1.15), 'wood_grey')
for i, x in enumerate((-2.2, -1.5, -.8, 0, .7, 1.4, 2.1)):
    m.box((.08, .05, .6 + .1 * (i % 3)), (x, 2.74, 1.35), 'steel' if i % 2 else 'rust_light')
    m.box((.22, .05, .12), (x, 2.74, 1.72 - .1 * (i % 2)), 'steel_dark')
m.box((2.0, .35, .06), (-1.6, 2.65, .98), 'wood')
m.box((.3, .2, .24), (-2.0, 2.65, 1.12), 'warning_red')
m.box((.3, .2, .24), (-1.5, 2.65, 1.12), 'mustard')
m.box((1.8, .6, .85), (1.7, 2.5, .55), 'steel_dark')  # workbench
m.box((1.9, .65, .08), (1.7, 2.5, .99), 'wood_light')
m.box((.2, .14, .12), (2.2, 2.5, 1.09), 'steel')  # vice
# parts bins and barrels along the west side
for y, c in ((-1.0, 'plastic_blue'), (-.3, 'orange'), (.4, 'plastic_blue')):
    m.box((.55, .6, .45), (-2.5, y, .35), c)
    m.box((.45, .5, .06), (-2.5, y, .60), 'steel_dark')
m.box((.6, 1.6, .06), (-2.55, -.3, .62 + .45), 'steel_dark')
for y in (1.2, 1.9):
    m.cyl(.28, .85, (-2.45, y, .55), 'rust_dark' if y > 1.5 else 'army_green', seg=10)
m.cyl(.3, .22, (2.3, -1.3, .23), 'rubber', seg=10)
m.cyl(.3, .22, (2.3, -1.3, .45), 'rubber', seg=10)
m.cyl(.3, .22, (2.35, -.8, .23), 'rubber', seg=10)
# work lamp on an arm from the east-front post
m.tube([(2.8, -2.7, 1.9), (2.4, -2.4, 2.05), (1.9, -2.2, 2.0)], .03, 'black', seg=6)
m.box((.3, .2, .16), (1.9, -2.2, 1.92), 'steel_dark')
m.box((.22, .12, .06), (1.9, -2.2, 1.83), 'lamp_warm')
# warning signs on posts
m.box((.35, .04, .35), (-2.65, -2.92, 1.5), 'hazard_yellow', rot=(0, 45, 0))
m.box((.35, .04, .35), (2.65, -2.92, 1.5), 'warning_red', rot=(0, 45, 0))

finish(m, bounds={'x':(-3.0,3.0),'y':(-3.0,3.0),'z':(0,2.5)})
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(Path(__file__).resolve().parents[1] / 'blend' / 'piece_garage_bay.blend'))
