from ss_art import Model, finish_icon
m = Model('kit_piece_scout_buggy', seed=711)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.18,.1,.03),(0,0,z+.05),'army_green')
m.box((.07,.08,.04),(-.03,0,z+.085),'army_green_dark')
for x in (-.07,.07):
    for y in (-.065,.065):
        m.cyl(.035,.02,(x,y,z+.04),'rubber',rot=(90,0,0),seg=12)
m.tube([(-.07,-.04,z+.06),(-.07,-.04,z+.14),(.0,-.04,z+.14),(.0,-.04,z+.1)],.006,'steel')
m.tube([(-.07,.04,z+.06),(-.07,.04,z+.14),(.0,.04,z+.14),(.0,.04,z+.1)],.006,'steel')
m.box((.03,.06,.02),(.1,0,z+.065),'lamp_warm')
finish_icon(m)
