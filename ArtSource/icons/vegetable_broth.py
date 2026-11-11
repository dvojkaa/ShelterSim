from ss_art import Model, finish_icon
m = Model('vegetable_broth', seed=618)
m.cyl(.07,.02,(0,0,.01),'paint_white',seg=14,r2=.04)
m.cyl(.12,.08,(0,0,.06),'paint_white',seg=16,r2=.14)
m.cyl(.125,.012,(0,0,.1),'orange',seg=16)
m.cyl(.105,.01,(0,0,.1),'orange',seg=16,r2=.105)
for x,y,c in [(-.04,.02,'leaf_green'),(.04,-.03,'rust_light'),(.02,.05,'hazard_yellow'),(-.03,-.04,'leaf_dark')]:
    m.blob((.04,.04,.03),(x,y,.11),c,rough=.15)
finish_icon(m)
