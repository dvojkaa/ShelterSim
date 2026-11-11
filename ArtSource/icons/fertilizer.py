from ss_art import Model, finish_icon
m = Model('fertilizer', seed=28153)
m.box((.3,.12,.4),(0,0,0),'paint_white',bevel=.02,taper=(.85,1))
m.box((.3,.125,.14),(0,0,-.03),'leaf_green')
m.blob((.2,.1,.12),(0,-.08,-.17),'dirt',rough=.3)
m.blob((.12,.08,.08),(.12,-.1,-.19),'dirt',rough=.3)
finish_icon(m)
