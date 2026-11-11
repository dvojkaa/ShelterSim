from ss_art import Model, finish_icon
m = Model('kit_piece_turret_mg', seed=715)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.cyl(.04,.05,(0,0,z+.025),'army_green_dark',seg=10)
m.box((.1,.07,.05),(0,0,z+.075),'army_green')
m.cyl(.012,.15,(.1,0,z+.085),'gunmetal',rot=(0,90,0),seg=8)
m.cyl(.018,.04,(.17,0,z+.085),'steel_dark',rot=(0,90,0),seg=8)
m.box((.05,.07,.05),(-.04,0,z+.125),'rust_dark')
m.box((.07,.08,.01),(-.01,0,z+.108),'hazard_yellow')
m.box((.05,.04,.06),(-.02,0,z+.1),'gunmetal') if False else None
finish_icon(m)
