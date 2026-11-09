"""Swap-ready low-poly wall/door damage silhouettes for health-state visuals."""
from ss_art import Model, finish


def crack(m, points, y, color='black', thickness=.006):
    """Narrow extruded fracture on the front X/Z plane."""
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        dx, dz = x1-x0, z1-z0
        length = (dx*dx+dz*dz)**.5
        angle = -__import__('math').degrees(__import__('math').atan2(dz, dx))
        m.box((length, .006, thickness), ((x0+x1)/2, y, (z0+z1)/2), color,
              rot=(0, angle, 0))


def chip(m, x, z, sx, sz, y, color='wood_dark', rot=0):
    m.poly([(-sx/2,-sz/2),(sx*.35,-sz/2),(sx/2,sz*.15),(-sx*.2,sz/2),(-sx/2,0)],
           .008,(x,y,z),color,rot=(0,rot,0))


def steel_wall(name, critical):
    m=Model(name,seed=232701 if critical else 232700)
    # Four-by-three welded plates; deep backing reads as the wall core.
    if critical:
        # Keep the backing open behind the torn center plate so the breach is a real sightline.
        m.box((.48,.14,2.94),(-.74,0,1.5),'gunmetal')
        m.box((.98,.14,2.94),(.49,0,1.5),'gunmetal')
        m.box((.50,.14,1.02),(-.25,0,2.48),'gunmetal')
        m.box((.50,.14,1.02),(-.25,0,.52),'gunmetal')
    else:
        m.box((1.96,.14,2.94),(0,0,1.5),'gunmetal')
    for i,x in enumerate((-.75,-.25,.25,.75)):
        for j,z in enumerate((.51,1.5,2.49)):
            # One plate has been torn out in the critical silhouette.
            if critical and i==1 and j==1:
                continue
            c=('steel','steel_dark','steel')[((i*3+j)%3)]
            m.box((.47,.018,.94),(x,-.082,z),c,bevel=.008)
            m.box((.47,.018,.94),(x,.082,z),'steel_dark' if j%2 else 'steel',bevel=.008)
            for xx in (x-.19,x+.19):
                for zz in (z-.36,z+.36):
                    m.cyl(.018,.012,(xx,-.094,zz),'gunmetal',rot=(90,0,0),seg=6)
                    m.cyl(.018,.012,(xx,.094,zz),'gunmetal',rot=(-90,0,0),seg=6)
    # Welded frame and horizontal seam remain intact until the wall is destroyed.
    for x in (-.96,.96): m.box((.08,.18,3),(x,0,1.5),'rust_dark')
    for z in (.08,2.92): m.box((2,.18,.16),(0,0,z),'gunmetal')
    for z in (1.0,2.0): m.box((2,.14,.035),(0,0,z),'rust_dark')
    if not critical:
        # Split, buckled center plate with clear crack lines and fresh rust at the seam.
        crack(m,[(-.34,1.94),(-.26,1.72),(-.31,1.53),(-.22,1.31),(-.27,1.05)],-.096,'black',.012)
        crack(m,[(.04,1.93),(-.04,1.78),(.02,1.59),(-.08,1.4),(-.02,1.2)],-.097,'rust',.012)
        m.box((.42,.012,.2),(-.25,-.09,1.53),'rust',rot=(0,-12,0))
        for x,z in ((-.30,1.92),(-.25,1.28),(.12,1.8)):
            m.cyl(.026,.01,(x,-.09,z),'rust_light',rot=(90,0,0),seg=5)
    else:
        # Exposed bent braces and jagged torn plate lips around the open breach.
        m.box((.05,.11,.68),(-.48,-.025,1.5),'steel',rot=(0,-14,0))
        m.box((.05,.11,.58),(.04,.025,1.5),'rust',rot=(0,18,0))
        chip(m,-.30,1.99,.38,.16,-.095,'rust',-10)
        chip(m,-.27,1.02,.40,.18,-.095,'steel',12)
        for x,z,angle in ((-.48,1.77,-18),(-.07,1.74,22),(-.45,1.24,12),(-.08,1.25,-20)):
            m.box((.34,.018,.045),(x,-.08,z),'rust_light',rot=(0,angle,0))
        for z in (.84,2.16):
            m.cyl(.022,.18,(-.25,0,z),'ore_metal',rot=(90,0,0),seg=6)
    finish(m,bounds={'x':(-1,1),'y':(-.1,.1),'z':(0,3)})


def wood_door(name, critical):
    m=Model(name,seed=232711 if critical else 232710)
    # Hinge rails and six independently readable salvaged planks.
    for x in (-.755,.755):
        m.box((.09,.06,2.18),(x,0,1.1),'wood_dark',bevel=.012)
    for i in range(6):
        x=-.61+i*.244
        if critical and i in (2,3):
            continue
        zlo=.04; zhi=2.16
        if not critical and i==4:
            # A split plank remains attached by its lower end.
            m.poly([(-.105,zlo),(.105,zlo),(.105,1.15),(.04,1.28),(.105,1.48),(-.105,1.37)],
                   .028,(x,-.005,0),'wood' if i%2==0 else 'wood_grey')
            m.box((.20,.028,.66),(x,-.005,1.82),'wood_dark',rot=(0,-8,0))
            continue
        m.box((.225,.035,zhi-zlo),(x,0,(zhi+zlo)/2),('wood','wood_grey','wood_dark','wood_light')[i%4],bevel=.006)
    # Existing-style crossbars; one is snapped through at the damaged stage.
        m.box((1.48,.022,.12),(0,-.025,.24),'steel_dark')
    if critical:
        m.box((.62,.024,.11),(-.37,-.015,1.76),'rust_dark',rot=(0,-18,0))
        m.box((.62,.024,.11),(.38,-.015,1.95),'rust_dark',rot=(0,16,0))
        for x,z,ang in ((-.2,.8,-16),(.2,.62,21),(.35,1.2,17),(-.38,1.04,-13)):
            m.box((.52,.022,.045),(x,-.025,z),'wood_light',rot=(0,ang,0))
        for x,z in ((-.18,.19),(.22,.19),(-.45,1.85),(.42,.32)):
            chip(m,x,z,.13,.12,-.03,'wood_grey',x*30)
    else:
        m.box((1.44,.022,.12),(0,-.025,1.92),'rust_dark')
        crack(m,[(-.18,1.82),(-.10,1.65),(-.16,1.48),(-.04,1.31),(-.10,1.12)],-.031,'black',.012)
        crack(m,[(.16,.42),(.07,.59),(.13,.78),(.02,.96)],-.031,'wood_dark',.01)
        for x,z in ((-.14,1.54),(.10,.75)):
            chip(m,x,z,.12,.09,-.032,'wood_light',18)
    # Hinge/latch hardware stays visible on every state.
    for z in (.28,1.1,1.92):
        m.box((.075,.08,.14),(-.75,0,z),'steel')
        m.box((.13,.02,.17),(.55,-.02,1.1),'rust')
    finish(m,bounds={'x':(-.8,.8),'y':(-.04,.04),'z':(0,2.2)})


def steel_door(name, critical):
    m=Model(name,seed=232721 if critical else 232720)
    # Outer frame and inset leaf.
    for x in (-.77,.77): m.box((.06,.07,2.2),(x,0,1.1),'gunmetal')
    for z in (.06,2.14): m.box((1.55,.07,.12),(0,0,z),'gunmetal')
    if critical:
        # Leave a real central gap between separated torn sheets.
        for x in (-.43,.43):
            m.box((.57,.045,1.96),(x,0,1.1),'steel_dark' if x<0 else 'rust_dark',bevel=.008)
        m.box((.30,.025,.43),(-.57,-.015,1.79),'steel',rot=(0,-12,0))
        m.box((.28,.025,.38),(.55,-.015,.39),'rust',rot=(0,15,0))
        for x,z,ang in ((-.16,1.73,-15),(.16,1.62,22),(-.18,.54,18),(.20,.42,-16)):
            m.box((.43,.025,.055),(x,-.015,z),'steel',rot=(0,ang,0))
        # Exposed inner brace visible through the ragged breach.
        m.box((.05,.035,1.35),(0,.008,1.1),'ore_metal',rot=(0,16,0))
    else:
        for i,x in enumerate((-.57,-.19,.19,.57)):
            m.box((.34,.045,1.94),(x,0,1.1),('steel','steel_dark','steel','rust_dark')[i],bevel=.008)
        # Shallow dents and split seams; fractured edges have warm exposed metal.
        m.box((.36,.018,.24),(-.16,-.024,1.44),'gunmetal',rot=(0,-9,0),bevel=.012)
        m.box((.32,.018,.18),(.34,-.024,.66),'rust',rot=(0,12,0),bevel=.01)
        crack(m,[(-.08,2.0),(-.02,1.84),(-.1,1.69),(-.03,1.51),(-.1,1.36)],-.031,'black',.011)
        crack(m,[(.36,.47),(.29,.62),(.37,.79),(.29,.94)],-.031,'rust_light',.01)
    for z in (.24,1.96):
        m.box((1.5,.022,.1),(0,-.025,z),'rust_dark')
        for x in (-.63,.63): m.cyl(.02,.012,(x,-.032,z),'chrome',rot=(90,0,0),seg=6)
    m.box((.1,.07,.16),(-.75,0,1.1),'steel')
    m.box((.15,.025,.15),(.57,-.022,1.1),'rust')
    finish(m,bounds={'x':(-.8,.8),'y':(-.04,.04),'z':(0,2.2)})


BUILD={
    'piece_wall_steel_damaged':lambda n:steel_wall(n,False),
    'piece_wall_steel_critical':lambda n:steel_wall(n,True),
    'piece_door_wood_damaged':lambda n:wood_door(n,False),
    'piece_door_wood_critical':lambda n:wood_door(n,True),
    'piece_door_steel_damaged':lambda n:steel_door(n,False),
    'piece_door_steel_critical':lambda n:steel_door(n,True),
}


def build(name):
    BUILD[name](name)
