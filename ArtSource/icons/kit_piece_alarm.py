from ss_art import Model, finish_icon
m = Model('kit_piece_alarm', seed=702)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.12,.1,.02),(0,0,z+.01),'steel_dark')
m.cyl(.03,.06,(0,0,z+.05),'red_bright',seg=10)
m.cyl(.03,.12,(0,0,z+.12),'warning_red',rot=(0,70,0),seg=10,r2=.07)
m.cyl(.015,.03,(0,0,z+.17),'steel',seg=8)
finish_icon(m)
