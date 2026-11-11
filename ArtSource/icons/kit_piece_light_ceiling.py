from ss_art import Model, finish_icon
m = Model('kit_piece_light_ceiling', seed=708)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.09,.09,.015),(0,0,z+.2),'steel_dark')
m.cyl(.004,.1,(0,0,z+.145),'black',seg=4)
m.cyl(.045,.05,(0,0,z+.1),'mustard',seg=12,r2=.015)
m.cyl(.025,.015,(0,0,z+.07),'lamp_warm',seg=10)
m.cyl(.012,.12,(.1,.07,z+.06),'steel_dark',seg=4)
finish_icon(m)
