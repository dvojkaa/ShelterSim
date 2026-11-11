from ss_art import Model, finish_icon
m = Model('kit_piece_farm', seed=705)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.24,.15,.03),(0,0,z+.015),'wood_dark')
m.box((.21,.12,.02),(0,0,z+.035),'dirt')
for x in (-.07,0,.07):
    for y in (-.035,.035):
        m.cyl(.006,.05,(x,y,z+.07),'leaf_green',seg=5)
        m.box((.04,.02,.01),(x+.02,y,z+.09),'leaf_green',rot=(0,0,30))
        m.box((.04,.02,.01),(x-.02,y,z+.09),'leaf_dark',rot=(0,0,-30))
finish_icon(m)
