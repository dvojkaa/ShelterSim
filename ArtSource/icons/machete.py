import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('machete', seed=434)
C(m,.04,.3,0,-.35,'black')
B(m,(.12,.05,.04),0,-.19,'steel_dark')
PL(m,[(-.05,-.18),(.04,-.18),(.04,.35),(.14,.5),(.14,.56),(-.05,.5)],.025,'steel')
PL(m,[(-.05,-.18),(-.03,-.18),(-.03,.5),(-.05,.5)],.04,'steel_dark')
finish_icon(m)
