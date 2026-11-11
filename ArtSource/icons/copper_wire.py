from ss_art import Model, finish_icon
m = Model('copper_wire', seed=614)
m.cyl(.2,.05,(0,0,-.05),'wood_dark',seg=14)
m.cyl(.2,.05,(0,0,.17),'wood_dark',seg=14)
m.cyl(.14,.17,(0,0,.06),'rust_light',seg=14)
m.cyl(.145,.03,(0,0,.06),'rust',seg=14)
m.tube([(.14,0,.1),(.3,-.1,.0),(.38,-.15,-.05)],.012,'rust_light',seg=5)
finish_icon(m)
