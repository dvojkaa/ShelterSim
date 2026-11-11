from ss_art import Model, finish_icon
m = Model('kit_piece_sandbag', seed=710)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
for i,x in enumerate((-.07,.0,.07)):
    m.box((.075,.12,.05),(x,0,z+.025),'sand',bevel=.012)
    m.box((.002+.065,.1,.015),(x,0,z+.052),'plaster',bevel=.006)
for x in (-.035,.035):
    m.box((.075,.12,.05),(x,0,z+.075),'plaster',bevel=.012)
m.box((.075,.12,.05),(0,0,z+.125),'sand',bevel=.012)
finish_icon(m)
