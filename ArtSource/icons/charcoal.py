from ss_art import Model, finish_icon
m = Model('charcoal', seed=10)
m.blob((.12,.1,.08),(0,0,.04),'black',rough=.2)
m.blob((.1,.09,.08),(.1,.04,.04),'gunmetal',rough=.2)
m.blob((.1,.1,.08),(-.02,.09,.12),'black',rough=.2)
m.blob((.08,.08,.06),(.04,-.08,.03),'gunmetal',rough=.2)
m.blob((.05,.05,.03),(.03,.02,.14),'rust',rough=.1)
finish_icon(m)
