import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('steel_axe', seed=413)
C(m,.035,.8,0,0,'wood_light')
PL(m,[(0.03,.2),(.2,.38),(.3,.34),(.27,.0),(.2,.06),(.03,.1)],.06,'chrome')
B(m,(.08,.1,.2),0,.15,'steel_dark')
C(m,.04,.05,0,-.3,'leather_dark')
C(m,.04,.05,0,-.15,'leather_dark')
finish_icon(m)
