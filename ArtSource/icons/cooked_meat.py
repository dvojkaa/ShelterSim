from ss_art import Model, finish_icon
m = Model('cooked_meat', seed=606)
m.blob((.26,.18,.13),(-.02,0,.07),'rust',rough=.1,colors=['rust','rust_dark','rust_light','rust_light'])
m.cyl(.03,.14,(.17,.0,.03),'bone',rot=(0,90,20),seg=8)
m.blob((.08,.07,.07),(.25,.045,.03),'bone',rough=.1)
for y in (-.04,.02):
    m.box((.2,.015,.012),(-.02,y,.14),'black',rot=(0,0,35))
finish_icon(m)
