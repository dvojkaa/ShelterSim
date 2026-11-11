import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('stone_pickaxe', seed=421)
C(m,.035,.8,0,0,'wood_light')
C(m,.06,.1,0,.36,'rope')
C(m,.05,.3,.18,.36,'stone_light',da=90,r2=.005)
C(m,.05,.3,-.18,.36,'stone_light',da=-90,r2=.005)
C(m,.075,.04,0,.3,'rope')
C(m,.075,.04,0,.42,'rope')
finish_icon(m)
