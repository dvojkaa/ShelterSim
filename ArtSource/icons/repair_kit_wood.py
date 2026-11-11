from ss_art import Model, finish_icon
m = Model('repair_kit_wood', seed=17)
m.box((.26,.16,.1),(0,0,.05),'wood',bevel=.01)
m.box((.27,.17,.03),(0,0,.1),'wood_light')
m.box((.04,.17,.105),(0,0,.05),'steel_dark') if False else None
m.box((.03,.17,.11),(-.07,0,.05),'steel_dark')
m.box((.03,.17,.11),(.07,0,.05),'steel_dark')
m.box((.1,.05,.03),(0,0,.14),'steel_dark')
m.box((.015,.015,.2),(.1,.1,.1),'wood_light',rot=(50,0,0)) if False else None
m.cyl(.012,.2,(.04,-.02,.19),'wood_dark',rot=(0,70,20),seg=6)
m.box((.07,.04,.05),(.1,.0,.22),'steel',rot=(0,70,20))
finish_icon(m)
