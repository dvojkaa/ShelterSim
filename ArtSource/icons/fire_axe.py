import math
from ss_art import Model, finish_icon
A=42
def P(x,z,y=0):
    c,s=math.cos(math.radians(A)),math.sin(math.radians(A)); return (x*c+z*s,y,-x*s+z*c)
def B(m,sz,x,z,col,y=0,da=0,**k): m.box(sz,P(x,z,y),col,rot=(0,A+da,0),**k)
def C(m,r,h,x,z,col,y=0,da=0,**k): m.cyl(r,h,P(x,z,y),col,rot=(0,A+da,0),**k)
def PL(m,pts,th,col,y=0): m.poly(pts,th,P(0,0,y),col,rot=(0,A,0))
m = Model('fire_axe', seed=435)
C(m,.04,.9,0,0,'wood_light')
C(m,.045,.14,0,-.4,'warning_red')
B(m,(.14,.1,.2),0,.36,'red_bright')
PL(m,[(0.04,.52),(.28,.5),(.34,.38),(.24,.3),(.28,.22),(.04,.2)],.06,'red_bright')
PL(m,[(.26,.5),(.34,.38),(.24,.3),(.28,.22),(.22,.22),(.2,.5)],.065,'steel')
PL(m,[(-.04,.44),(-.22,.36),(-.04,.28)],.06,'steel_dark')
finish_icon(m)
