from ss_art import Model, finish_icon
m = Model('capacitor', seed=23950)
m.cyl(.11,.3,(0,0,0),'plastic_blue',rot=(70,0,0),seg=14)
m.cyl(.112,.04,(0,.04,.05),'chrome',rot=(70,0,0),seg=14)
m.cyl(.01,.22,(-.05,-.2,-.05),'chrome',rot=(70,0,0))
m.cyl(.01,.22,(.05,-.2,-.05),'chrome',rot=(70,0,0))
m.box((.05,.02,.2),(0,-.01,.07),'hazard_yellow',rot=(70,0,0))
finish_icon(m)
