from ss_art import Model, finish_icon
m = Model('nails', seed=9)
m.box((.2,.14,.07),(0,0,.035),'wood_dark')
for i,(x,y) in enumerate([(-.05,-.03),(.0,.03),(.05,-.02),(-.02,.0),(.04,.04)]):
    m.cyl(.012,.17,(x,y,.15+.0*i),'steel',seg=6)
    m.cyl(.03,.012,(x,y,.24),'steel_dark',seg=8)
finish_icon(m)
