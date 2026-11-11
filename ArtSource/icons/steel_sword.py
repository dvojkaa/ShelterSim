import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('steel_sword', seed=436)
C(m,.035,.22,0,-.38,'leather_dark')
C(m,.05,.06,0,-.52,'chrome')
B(m,(.34,.07,.06),0,-.24,'steel_dark')
PL(m,[(-.06,-.2),(.06,-.2),(.06,.55),(0,.7),(-.06,.55)],.035,'chrome')
B(m,(.025,.05,.7),0,.15,'steel')
finish_icon(m)
