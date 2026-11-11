from ss_art import Model, finish_icon
m = Model('ammo_shell', seed=3259)
for x,y in ((-.12,0),(0,.0),(.12,0)):
    m.cyl(.06,.2,(x,y,0),'warning_red',seg=10)
    m.cyl(.065,.05,(x,y,-.1),'mustard',seg=10)
m.cyl(.06,.02,(-.12,0,.11),'mustard_dark',seg=10)
m.cyl(.06,.02,(0,0,.11),'mustard_dark',seg=10)
m.cyl(.06,.02,(.12,0,.11),'mustard_dark',seg=10)
finish_icon(m)
