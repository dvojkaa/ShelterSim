from ss_art import Model, finish_icon
m = Model('sugar', seed=4)
m.box((.16,.1,.2),(0,0,.1),'paint_white',taper=(.9,.9))
m.box((.165,.105,.05),(0,0,.12),'berry_light')
m.box((.16,.1,.03),(0,0,.215),'fabric_white',taper=(.8,.7))
m.box((.04,.04,.04),(.12,.1,.02),'fabric_white',rot=(0,0,20))
m.box((.04,.04,.04),(.17,.06,.02),'fabric_white',rot=(0,0,50))
finish_icon(m)
