from ss_art import Model, finish_icon
m = Model('iron_ingot', seed=11)
m.box((.26,.12,.07),(0,0,.035),'steel_dark',taper=(.85,.8))
m.box((.2,.08,.012),(0,0,.075),'steel')
m.box((.26,.12,.07),(.02,-.02,.105),'steel_dark',taper=(.85,.8),rot=(0,0,90)) if False else None
finish_icon(m)
