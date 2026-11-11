from ss_art import Model, finish_icon
m = Model('stone', seed=602)
m.blob((.4,.34,.26),(0,0,0),'stone',colors=['stone','stone_light','stone_dark'],rough=.2,seed=3)
m.blob((.2,.18,.14),(.22,-.12,-.05),'stone_light',seed=5)
finish_icon(m)
