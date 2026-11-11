from ss_art import Model, finish_icon
m = Model('first_aid', seed=612)
m.box((.24,.09,.17),(0,0,.085),'paint_white',bevel=.012)
m.box((.1,.095,.035),(0,0,.09),'red_bright')
m.box((.035,.095,.1),(0,0,.09),'red_bright')
m.box((.1,.03,.025),(0,0,.19),'steel_dark',bevel=.008)
m.box((.02,.095,.02),(-.1,0,.085),'steel')
m.box((.02,.095,.02),(.1,0,.085),'steel')
finish_icon(m)
