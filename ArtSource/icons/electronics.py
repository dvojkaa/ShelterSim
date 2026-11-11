from ss_art import Model, finish_icon
m = Model('electronics', seed=613)
m.box((.5,.36,.03),(0,0,0),'paint_green')
m.box((.16,.16,.05),(-.08,0,.04),'black')
m.cyl(.04,.08,(.14,.08,.05),'steel',seg=8)
m.cyl(.04,.08,(.14,-.07,.05),'hazard_yellow',seg=8)
m.box((.1,.05,.04),(.0,-.12,.035),'rust_light')
m.tube([(-.2,.12,.02),(-.05,.12,.02),(.05,.08,.02)],.008,'chrome',seg=4)
finish_icon(m)
