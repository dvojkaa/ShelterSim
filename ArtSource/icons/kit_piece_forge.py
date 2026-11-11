from ss_art import Model, finish_icon
m = Model('kit_piece_forge', seed=706)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.14,.1,.05),(.03,0,z+.025),'gunmetal')
m.box((.2,.07,.035),(.03,0,z+.07),'steel_dark')
m.box((.06,.05,.03),(-.1,0,z+.075),'steel_dark',taper=(.4,1))
m.box((.1,.1,.06),(-.06,0,z+.03),'brick')
m.box((.06,.01,.03),(-.06,-.052,z+.035),'orange')
finish_icon(m)
