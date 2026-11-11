from ss_art import Model, finish_icon
m = Model('hinges', seed=16)
for y in (-.08,.08):
    m.box((.16,.1,.012),(.0,y,.006),'steel')
    m.cyl(.03,.12,(-.08,y,.04),'steel_dark',rot=(90,0,0),seg=10)
    m.cyl(.01,.14,(-.08,y,.04),'chrome',rot=(90,0,0),seg=6)
    m.cyl(.01,.01,(.04,y,.015),'rust_dark',seg=6)
finish_icon(m)
