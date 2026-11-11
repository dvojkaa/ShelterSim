import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('weapon_upgrade', seed=433)
C(m,.035,.3,0,-.3,'leather_dark')
C(m,.04,.04,0,-.25,'rope'); C(m,.04,.04,0,-.35,'rope')
PL(m,[(-.07,-.15),(.07,-.15),(.08,.2),(.0,.5),(-.07,.3),(-.09,.1)],.03,'rust_light')
B(m,(.04,.04,.55),.05,.17,'steel')
B(m,(.2,.06,.05),0,-.15,'rust_dark')
finish_icon(m)
