from ss_art import Model, finish_icon
m = Model('kit_piece_irrigator', seed=707)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.tube([(-.1,0,z+.04),(.05,0,z+.04),(.05,0,z+.1)],.014,'steel')
m.tube([(.05,0,z+.1),(.05,0,z+.12)],.014,'steel')
m.cyl(.04,.03,(.05,0,z+.14),'chrome',seg=10,r2=.015)
m.cyl(.04,.03,(.05,0,z+.14),'chrome',seg=10,r2=.015)
for dx in (-.045,.045):
    m.cyl(.006,.05,(.05+dx,0,z+.17),'water',rot=(0,dx*600,0),seg=5)
m.cyl(.006,.05,(.05,.0,z+.2),'water',seg=5)
m.cyl(.022,.02,(-.1,0,z+.04),'rust',rot=(0,90,0),seg=10)
finish_icon(m)
