from ss_art import Model, finish_icon
m = Model('kit_piece_spike_trap', seed=713)
m.box((.28,.19,.1),(0,0,.05),'wood',bevel=.01)
m.box((.29,.2,.025),(0,0,.1),'wood_light')
m.box((.03,.2,.105),(-.08,0,.052),'steel_dark')
m.box((.03,.2,.105),(.08,0,.052),'steel_dark')
m.box((.06,.01,.05),(-.11,-.098,.06),'hazard_yellow')
z=.1125
m.box((.2,.14,.02),(0,0,z+.01),'rust_dark')
for x in (-.07,0,.07):
    for y in (-.04,.04):
        m.cyl(.018,.09,(x,y,z+.065),'steel',seg=5,r2=.002)
finish_icon(m)
