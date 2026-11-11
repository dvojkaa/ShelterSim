from ss_art import Model, finish_icon
m = Model('bandage', seed=501)
m.cyl(.07,.06,(0,0,.03),'cloth_white' if False else 'wood_light',seg=14)
m.cyl(.072,.012,(0,0,.03),'rust',seg=14)
m.box((.2,.08,.012),(.0,-.1,.006),'wood_light')
finish_icon(m)
