from ss_art import Model, finish_icon
m = Model('energy_cell', seed=10271)
m.cyl(.1,.3,(0,0,0),'steel_dark',seg=12)
m.cyl(.103,.12,(0,0,.02),'glass',seg=12)
m.cyl(.07,.1,(0,0,.02),'lamp_warm',seg=10)
m.cyl(.105,.03,(0,0,.17),'chrome',seg=12)
m.cyl(.04,.03,(0,0,.2),'chrome',seg=8)
m.cyl(.105,.03,(0,0,-.16),'hazard_yellow',seg=12)
finish_icon(m)
