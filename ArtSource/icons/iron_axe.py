import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('iron_axe', seed=412)
C(m,.035,.8,0,0,'wood_light')
PL(m,[(0.03,.2),(.2,.38),(.3,.34),(.27,.0),(.2,.06),(.03,.1)],.06,'steel_dark')
B(m,(.06,.08,.2),-.0,.15,'rust_dark')
PL(m,[(.24,.34),(.3,.34),(.27,.0),(.24,.04)],.07,'rust_light')
finish_icon(m)
