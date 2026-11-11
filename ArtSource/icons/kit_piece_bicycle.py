from ss_art import Model, finish_icon
m = Model('kit_piece_bicycle', seed=704)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
for x in (-.08,.08):
    m.cyl(.07,.015,(x,0,z+.075),'rubber',rot=(90,0,0),seg=16)
    m.cyl(.05,.018,(x,0,z+.075),'steel',rot=(90,0,0),seg=16)
    m.cyl(.04,.02,(x,0,z+.075),'concrete_dark',rot=(90,0,0),seg=16)
m.tube([(-.08,0,z+.075),(-.01,0,z+.12),(.06,0,z+.12),(.08,0,z+.075)],.008,'car_red')
m.tube([(-.01,0,z+.12),(0,0,z+.08),(-.08,0,z+.075)],.008,'car_red')
m.tube([(.06,0,z+.12),(.05,0,z+.16),(.08,0,z+.17)],.008,'steel_dark')
m.box((.04,.02,.01),(-.02,0,z+.14),'leather_dark')
finish_icon(m)
