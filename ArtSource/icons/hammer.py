import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('hammer', seed=401)
C(m,.04,.7,0,-.05,'wood_light')
B(m,(.36,.14,.15),0,.32,'steel_dark')
B(m,(.06,.15,.16),-.2,.32,'chrome')
B(m,(.06,.15,.16),.2,.32,'chrome')
B(m,(.07,.07,.1),0,-.4,'rope')
finish_icon(m)
