from ss_art import Model, finish
m = Model('item_canned_food', seed=301)
import math
from mathutils import Euler
def can(x,y,z,r,h,body,band,rot=(0,0,0)):
    ax=Euler([math.radians(a) for a in rot]).to_matrix() @ __import__('mathutils').Vector((0,0,1))
    m.cyl(r,h-.02,(x,y,z),body,rot=rot,seg=8)
    m.cyl(r*1.02,h*.34,(x,y,z),band,rot=rot,seg=8)
    for s in (-1,1):
        d=s*(h/2-.006)
        m.cyl(r*1.05,.012,(x+ax.x*d,y+ax.y*d,z+ax.z*d),'steel',rot=rot,seg=8)
# two standing cans
can(-.085,.05,.055,.065,.11,'rust','mustard')
can(.075,.06,.055,.065,.11,'steel_dark','warning_red')
# can lying across the top
can(-.005,.055,.157,.045,.15,'army_green','hazard_yellow',rot=(0,90,0))
# dented can on its side in front
can(-.02,-.075,.058,.058,.13,'rust_dark','mustard',rot=(90,0,12))
finish(m, bounds={'x':(-.16,.16),'y':(-.14,.14),'z':(0,.2)})
