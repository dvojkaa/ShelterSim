from ss_art import Model, finish_icon
m = Model('kit_storage_crate', seed=716)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.2,.14,.1),(0,0,z+.06),'wood_light',bevel=.008)
m.box((.215,.155,.025),(.0,-.0,z+.12),'wood',rot=(0,0,0))
m.box((.02,.155,.11),(-.07,0,z+.06),'steel_dark')
m.box((.02,.155,.11),(.07,0,z+.06),'steel_dark')
m.box((.05,.02,.03),(0,-.075,z+.1),'chrome')
m.box((.05,.1,.012),(.0,0,z+.145),'wood_dark',rot=(0,0,0)) if False else None
finish_icon(m)
