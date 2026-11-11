from ss_art import Model, finish_icon
m = Model('coal', seed=610)
for loc,s,sd in [((0,0,0),(.34,.3,.26),1),((.2,-.05,-.04),(.26,.24,.2),2),((-.18,-.06,-.03),(.24,.22,.2),3),((.03,.1,.16),(.22,.2,.18),4),((.1,-.15,.0),(.18,.16,.14),6)]:
    m.blob(s,loc,'black',colors=['black','gunmetal','concrete_dark'],rough=.2,seed=sd)
finish_icon(m)
