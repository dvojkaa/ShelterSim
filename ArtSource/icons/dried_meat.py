from ss_art import Model, finish_icon
m = Model('dried_meat', seed=616)
for i,(x,y,r) in enumerate([(-.04,-.04,15),(.04,.03,-20),(0,.0,40)]):
    m.box((.2,.07,.025),(x,y,.015+i*.03),'rust_dark',rot=(0,0,r),bevel=.01)
    m.box((.14,.04,.012),(x,y,.03+i*.03),'rust',rot=(0,0,r))
m.box((.012,.012,.1),(.03,0,.17),'rope')
finish_icon(m)
