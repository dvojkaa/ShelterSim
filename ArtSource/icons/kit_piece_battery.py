from ss_art import Model, finish_icon
m = Model('kit_piece_battery', seed=703)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
for i,x in enumerate((-.07,0,.07)):
    m.box((.06,.1,.1),(x,0,z+.05),'army_green_dark')
    m.box((.06,.1,.015),(x,0,z+.105),'hazard_yellow')
    m.cyl(.01,.03,(x-.015,0,z+.13),'chrome',seg=6)
    m.cyl(.01,.03,(x+.015,0,z+.13),'rust_light',seg=6)
finish_icon(m)
