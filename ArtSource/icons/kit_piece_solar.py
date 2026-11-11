from ss_art import Model, finish_icon
m = Model('kit_piece_solar', seed=712)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.015,.02,.07),(-.09,.06,z+.035),'steel_dark')
m.box((.015,.02,.07),(.09,.06,z+.035),'steel_dark')
m.box((.26,.15,.015),(0,0,z+.09),'chrome',rot=(35,0,0))
m.box((.24,.13,.018),(0,0,z+.09),'plastic_blue',rot=(35,0,0))
for x in (-.06,.0,.06):
    m.box((.004,.13,.02),(x,0,z+.09),'chrome',rot=(35,0,0))
m.box((.24,.004,.02),(0,0,z+.09),'chrome',rot=(35,0,0))
finish_icon(m)
