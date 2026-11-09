"""Original M22 construction pieces. Coordinates and colours follow ss_art."""
from ss_art import Model, finish


def box(m, size, pos, colour, bevel=0):
    m.box(size, pos, colour, bevel=bevel)


def bolt(m, x, y, z, colour='gunmetal'):
    m.cyl(.018, .012, (x, y, z), colour, rot=(90, 0, 0), seg=6)


def edge_frame(m, height, width=2, opening=None):
    """Continuously matching edge posts, sill, and lintel."""
    box(m, (width, .18, .12), (0, 0, .06), 'concrete_dark')
    box(m, (width, .18, .12), (0, 0, height-.06), 'concrete')
    for sx in (-1, 1):
        box(m, (.14, .18, height-.24), (sx*(width/2-.07), 0, height/2), 'concrete_light')


def wall_plates(m, z0, z1, width=1.72, reverse=False):
    colors = ('steel_dark', 'rust', 'wood_grey', 'wood', 'rust_dark')
    h = (z1-z0)/5
    for s in (-1, 1):
        for i in range(5):
            c = colors[(i+2 if reverse and s == 1 else i) % 5]
            box(m, (width, .025, h-.012), (0, s*.066, z0+(i+.5)*h), c)
            for x in (-.67, .67):
                bolt(m, x, s*.084, z0+(i+.5)*h)


def build(name):
    m = Model(name, seed=2200 + sum(ord(c) for c in name))
    b = None
    if name == 'piece_foundation':
        b = dict(x=(-1,1), y=(-1,1), z=(0,.4))
        box(m, (2,2,.28), (0,0,.14), 'concrete_dark')
        box(m, (1.82,1.82,.08), (0,0,.32), 'concrete')
        for x in (-.55,.55):
            box(m, (.7,1.5,.012), (x,0,.366), 'concrete_light')
        for x in (-.92,.92):
            for y in (-.92,.92):
                box(m, (.16,.16,.04), (x,y,.38), 'steel_dark')
                m.cyl(.024,.007,(x,y,.395),'rust',seg=6)
        box(m,(.64,.36,.012),(.28,-.32,.365),'rust_dark')
    elif name == 'piece_wall_half':
        b = dict(x=(-1,1), y=(-.1,.1), z=(0,1.5))
        edge_frame(m,1.5)
        box(m,(1.72,.09,1.26),(0,0,.75),'concrete_dark')
        wall_plates(m,.13,1.37)
        for s in (-1,1):
            box(m,(1.72,.026,.09),(0,s*.083,.78),'rust_dark')
    elif name == 'piece_wall_window':
        b = dict(x=(-1,1), y=(-.1,.1), z=(0,3))
        edge_frame(m,3)
        for sx in (-1,1):
            box(m,(.29,.08,2.76),(sx*.705,0,1.5),'concrete_dark')
            for s in (-1,1):
                for z in (.5,2.55):
                    box(m,(.3,.016,.2),(sx*.695,s*.06,z),'rust')
        box(m,(1.1,.08,.88),(0,0,.56),'concrete_dark')
        box(m,(1.1,.08,.76),(0,0,2.58),'concrete_dark')
        for s in (-1,1):
            for z in (.96,2.14):
                box(m,(1.25,.02,.08),(0,s*.06,z),'steel_dark')
            for x in (-.59,.59):
                box(m,(.055,.025,1.18),(x,s*.065,1.55),'rust_dark')
                for z in (1.05,2.04): bolt(m,x,s*.085,z)
    elif name == 'piece_door_frame':
        b = dict(x=(-1,1), y=(-.1,.1), z=(0,3))
        for sx in (-1,1):
            box(m,(.2,.18,3),(sx*.9,0,1.5),'concrete')
            box(m,(.045,.02,2.16),(sx*.855,-.083,1.08),'steel_dark')
            for z in (.27,1.1,2.02,2.65): bolt(m,sx*.9,-.093,z)
        box(m,(1.6,.18,.8),(0,0,2.6),'concrete_dark')
        box(m,(1.6,.025,.08),(0,-.085,2.25),'rust')
        for x in (-.52,-.23,.06,.35,.64):
            box(m,(.14,.012,.035),(x,-.092,2.72),'hazard_yellow')
        for x in (-.88,.88):
            for z in (.25,1.88):
                box(m,(.075,.035,.14),(x,-.071,z),'rust_dark')
    elif name in ('piece_door_wood','piece_door_steel'):
        b = dict(x=(-.8,.8), y=(-.04,.04), z=(0,2.2))
        steel = name.endswith('steel')
        base = 'steel_dark' if steel else 'wood_dark'
        box(m,(1.56,.055,2.2),(0,0,1.1),base)
        for i in range(6 if not steel else 4):
            if steel:
                x=-.575+i*.38
                box(m,(.34,.012,1.98),(x,-.028,1.1),('steel','rust','steel_dark','steel')[i])
            else:
                x=-.63+i*.25
                box(m,(.22,.018,1.97),(x,-.030,1.1),('wood','wood_grey','wood_light','wood','wood_dark','wood')[i])
        for z in (.23,1.96):
            box(m,(1.5,.015,.12),(0,-.032,z),'rust_dark' if steel else 'steel_dark')
            for x in (-.65,.65): bolt(m,x,-.034,z)
        for z in (.3,1.1,1.9):
            box(m,(.08,.075,.14),(-.76,0,z),'steel')
        box(m,(.18,.02,.16),(.57,-.029,1.12),'rust')
        m.cyl(.035,.012,(.57,-.032,1.12),'steel',rot=(90,0,0),seg=8)
        if steel:
            for x in (-.55,.0,.55):
                for z in (.53,1.62): bolt(m,x,-.034,z)
        else:
            box(m,(1.08,.012,.075),(0,-.034,1.16),'steel_dark',)
    elif name == 'piece_roof_flat':
        b = dict(x=(-1,1), y=(-1,1), z=(0,.2))
        box(m,(2,2,.1),(0,0,.05),'steel_dark')
        for y,c in ((-.75,'rust'),(-.25,'steel'),(.25,'steel_dark'),(.75,'wood_grey')):
            box(m,(1.86,.46,.035),(0,y,.122),c)
            for x in (-.8,.8):
                m.cyl(.019,.007,(x,y,.144),'gunmetal',seg=6)
        for s in (-1,1):
            box(m,(2,.055,.18),(0,s*.972,.09),'rust_dark')
            box(m,(.055,1.89,.18),(s*.972,0,.09),'rust_dark')
    elif name == 'piece_roof_slope':
        b = dict(x=(-1,1), y=(-1,1), z=(0,1.2))
        # Profile is Y/Z after rotating the library's X/Z extrusion 90 degrees.
        m.poly([(-1,0),(1,1.02),(1,1.2),(-1,.18)],2,(0,0,0),'steel_dark',rot=(0,0,90))
        for i in range(5):
            y=-.76+i*.38
            z=.16+(y+1)*.51
            box(m,(1.83,.32,.014),(0,y,z),'rust' if i%2 else 'steel')
            for x in (-.8,.8):
                m.cyl(.017,.006,(x,y,z+.011),'gunmetal',seg=6)
        for x in (-.94,.94):
            m.poly([(-1,.06),(1,1.08),(1,1.17),(-1,.15)],.08,(x,0,0),'rust_dark',rot=(0,0,90))
    elif name == 'piece_ramp':
        b = dict(x=(-1,1), y=(-1,1), z=(0,.4))
        m.poly([(-1,0),(1,0),(1,.39),(-1,.02)],1.9,(0,0,0),'concrete_dark',rot=(0,0,90))
        for i in range(7):
            y=-.83+i*.27
            z=.02+(y+1)*.185
            box(m,(1.82,.075,.016),(0,y,z),'wood_grey' if i%2 else 'rust')
            for x in (-.78,.78): bolt(m,x,y-.042,z)
        for x in (-.94,.94):
            m.poly([(-1,0),(1,.35),(1,.39),(-1,.04)],.07,(x,0,0),'steel_dark',rot=(0,0,90))
    elif name == 'piece_stairs':
        b = dict(x=(-1,1), y=(-2,2), z=(0,3))
        # Twelve walkable salvage steps, each 0.25 m rise and 0.333 m run.
        for i in range(12):
            y=-2+(i+.5)/3
            z=(i+1)*.25
            box(m,(1.82,1/3,.075),(0,y,z-.0375),'wood_grey' if i%3 else 'rust')
            box(m,(1.82,.018,.20),(0,y+1/6-.012,z-.15),'steel_dark')
            for x in (-.78,.78):
                bolt(m,x,y-.14,z-.025)
        for x in (-.94,.94):
            m.poly([(-2,0),(2,2.75),(2,3),(-2,.05)],.08,(x,0,0),'rust_dark',rot=(0,0,90))
    elif name == 'piece_fence':
        b = dict(x=(-1,1), y=(-.05,.05), z=(0,1.2))
        for x in (-.95,.95):
            box(m,(.1,.10,1.2),(x,0,.6),'steel_dark')
        for z in (.22,1.02):
            box(m,(2,.05,.08),(0,0,z),'rust_dark')
        for i in range(9):
            x=-.8+i*.2
            box(m,(.105,.05,.91),(x,-.015,.62),('wood','wood_grey','wood_dark')[i%3])
            for z in (.23,1.02): bolt(m,x,-.043,z)
    elif name == 'piece_gate':
        b = dict(x=(-1,1), y=(-.05,.05), z=(0,1.2))
        box(m,(1.95,.06,1.12),(0,0,.6),'wood_dark')
        for i in range(9):
            x=-.85+i*.2125
            box(m,(.19,.04,1.08),(x,-.025,.61),('wood','wood_grey','wood_dark')[i%3])
        for z in (.2,.98):
            box(m,(1.9,.015,.1),(0,-.04,z),'rust_dark')
            for x in (-.8,0,.8): bolt(m,x,-.044,z)
        for z in (.27,.9):
            box(m,(.1,.095,.14),(-.95,0,z),'steel')
        box(m,(.19,.02,.14),(.83,-.033,.57),'steel_dark')
    elif name == 'piece_barricade':
        b = dict(x=(-1,1), y=(-.2,.2), z=(0,1.2))
        for s in (-1,1):
            for x in (-.68,.68):
                box(m,(.12,.32,.1),(x,s*.01,.11),'wood_dark')
            for i in range(3):
                z=.3+i*.28
                box(m,(1.9,.04,.23),(0,s*(.15-i*.045),z),('rust','steel_dark','wood_grey')[i])
        for x in (-.82,.82):
            m.poly([(-.18,0),(.18,0),(.05,1.2),(-.05,1.2)],.075,(x,0,0),'wood_dark',rot=(0,0,90))
        for x in (-.56,.52):
            box(m,(.13,.12,.27),(x,-.04,1.03),'steel_dark')
    elif name == 'piece_palisade':
        b = dict(x=(-1,1), y=(-.15,.15), z=(0,3))
        for i in range(8):
            x=-.875+i*.25
            h=2.72+(i%3)*.08
            box(m,(.23,.26,h-.16),(x,0,(h-.16)/2),'wood_dark' if i%3==0 else 'wood')
            m.cyl(.12,.24,(x,0,h-.12),'wood_grey' if i%2 else 'wood',seg=4,r2=0)
            for z in (.52,1.47):
                bolt(m,x,-.14,z)
        for z in (.54,1.49):
            box(m,(2,.02,.15),(0,-.135,z),'rust_dark')
        for x in (-.94,.94):
            box(m,(.12,.3,2),(x,0,1),'steel_dark')
    else:
        raise ValueError(name)
    finish(m,bounds=b)
