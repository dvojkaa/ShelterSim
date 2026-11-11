from ss_art import Model, finish_icon
m = Model('water', seed=602)
m.cyl(.06,.17,(0,0,.085),'water',seg=14,r2=.08)
m.cyl(.082,.012,(0,0,.176),'paint_white',seg=14)
m.cyl(.07,.012,(0,0,.18),'lamp_warm',seg=14)
m.cyl(.062,.02,(0,0,.01),'paint_white',seg=14)
finish_icon(m)
