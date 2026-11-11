from ss_art import Model, finish_icon
m = Model('rope', seed=8)
for k,z in enumerate((.03,.08,.13)):
    for a in range(8):
        import math
        t=math.radians(a*45)
        m.box((.1,.04,.045),(.1*math.cos(t),.1*math.sin(t),z),'rope' if (a+k)%2 else 'wood_light',rot=(0,0,a*45+90))
m.cyl(.015,.22,(.1,-.14,.04),'rope',rot=(0,75,30),seg=6)
finish_icon(m)
