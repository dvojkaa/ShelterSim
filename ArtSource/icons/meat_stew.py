from ss_art import Model, finish_icon
m = Model('meat_stew', seed=607)
m.cyl(.11,.02,(0,0,.01),'black',seg=14)
m.cyl(.12,.1,(0,0,.07),'steel_dark',seg=14,r2=.13)
m.cyl(.11,.01,(0,0,.123),'rust_dark',seg=14)
for x,y in [(-.04,.02),(.04,-.03),(.02,.05)]:
    m.blob((.05,.045,.04),(x,y,.14),'rust_light',rough=.15)
m.blob((.035,.035,.03),(-.04,-.04,.14),'leaf_green',rough=.15)
m.box((.14,.02,.02),(.15,0,.12),'black')
finish_icon(m)
