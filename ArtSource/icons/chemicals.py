from ss_art import Model, finish_icon
m = Model('chemicals', seed=612)
m.cyl(.15,.35,(0,0,0),'plastic_blue',seg=12)
m.cyl(.08,.06,(0,0,.2),'hazard_yellow',seg=10)
m.box((.2,.02,.14),(0,-.15,.0),'paint_white')
m.box((.1,.02,.1),(0,-.16,.0),'warning_red')
finish_icon(m)
