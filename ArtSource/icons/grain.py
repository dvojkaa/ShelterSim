from ss_art import Model, finish_icon
m = Model('grain', seed=617)
m.cyl(.015,.5,(0,0,0),'leaf_dry',rot=(0,0,0),seg=5)
for a,x in [(-25,-.15),(0,0),(25,.15)]:
    m.cyl(.012,.5,(x,0,0),'leaf_dry',rot=(0,a,0),seg=5)
    for k in range(4):
        m.blob((.05,.045,.1),(x+.08*(a/25)*(.5+k*.18)*0+ (.25*(a/25)*.35)*0+(k*.04*(a/25)),0,.12+k*.08),'mustard',seed=k+a)
m.tube([(-.2,0,-.3),(0,0,-.35),(.2,0,-.3)],.012,'rope',seg=5)
finish_icon(m)
