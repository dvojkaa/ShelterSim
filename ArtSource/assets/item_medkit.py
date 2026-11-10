from ss_art import Model, finish
m = Model('item_medkit', seed=303)
# base
m.box((.32,.22,.09),(0,0,.045),'paint_white',bevel=.01)
m.box((.28,.18,.03),(0,0,.095),'fabric_stain')  # inside tray
# bandage roll visible in open tray
m.cyl(.035,.09,(-.05,0,.12),'fabric_white',rot=(0,90,0),seg=6)
m.cyl(.036,.03,(.0,0,.12),'fabric_stain',rot=(0,90,0),seg=6)
m.box((.05,.05,.03),(.08,.02,.115),'plastic_blue')
# lid hinged at back, opened 35 deg (front edge rises)
import math
th=math.radians(-24)
py,pz=.105,.09
def lid(size,ly,lz,col,bevel=0.0,lx=0):
    wy=py+ly*math.cos(th)-lz*math.sin(th)
    wz=pz+ly*math.sin(th)+lz*math.cos(th)
    m.box(size,(lx,wy,wz),col,rot=(-24,0,0),bevel=bevel)
lid((.32,.22,.04),-.11,.02,'paint_white')
lid((.28,.18,.01),-.11,-.005,'fabric_stain',.003)
lid((.04,.14,.008),-.11,.044,'warning_red',.002)
lid((.14,.04,.008),-.11,.0455,'warning_red',.002)
# red cross sticker on base front, latches at corners
m.box((.026,.006,.07),(0,-.113,.05),'warning_red')
m.box((.07,.006,.026),(0,-.1145,.05),'warning_red')
for x in (-.11,.11):
    m.box((.05,.02,.04),(x,-.115,.085),'steel_dark')
    m.box((.03,.012,.03),(x,-.122,.085),'chrome')
# grime + scuffs
m.box((.07,.005,.03),(-.07,-.111,.03),'plaster_dirty')
m.box((.04,.005,.025),(.09,-.111,.025),'rust')
m.box((.02,.1,.02),(-.165,0,.07),'steel_dark')  # handle bracket
finish(m, bounds={'x':(-.175,.175),'y':(-.13,.13),'z':(0,.2)})
