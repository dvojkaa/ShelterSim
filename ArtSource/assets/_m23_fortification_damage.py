"""Same-footprint wear and breach silhouettes for outdoor player defenses."""
from ss_art import Model, finish


def fence(m, critical):
    for x in (-.95,.95):
        m.box((.1,.10,1.2),(x,0,.6),'steel_dark')
    # Rails break and sag visibly at the critical stage.
    if critical:
        m.box((.90,.05,.075),(-.48,0,.79),'rust_dark',rot=(0,-7,0))
        m.box((.78,.05,.075),(.57,0,.34),'wood_dark',rot=(0,10,0))
    else:
        m.box((1.9,.05,.08),(0,0,.22),'rust_dark')
        m.box((1.86,.05,.08),(-.02,0,1.0),'rust_dark',rot=(0,-2,0))
    missing = {3,4,5,7} if critical else {4}
    for i in range(9):
        if i in missing: continue
        x=-.8+i*.2
        h=.91 if (i%3) else .78
        z=.62 if h>.8 else .57
        if critical and i in (2,6):
            m.box((.105,.05,.48),(x,-.005,z),'wood_grey',rot=(0,12 if i==2 else -14,0))
        else:
            m.box((.105,.05,h),(x,-.015,z),('wood','wood_grey','wood_dark')[i%3])
    if critical:
        # Exposed splintered ends and a loose board lying at the foot of the gap.
        for x,a in ((-.38,18),(.42,-16)):
            m.poly([(-.052,.48),(.052,.48),(.035,.74),(0,.83),(-.045,.68)],.05,(x,-.015,0),'wood_light',rot=(0,a,0))


def gate(m, critical):
    # Perimeter boards and rails remain under both states so the same gate collider fits.
    for x in (-.95,.95):
        m.box((.1,.095,1.12),(x,0,.58),'wood_dark')
    if critical:
        m.box((.60,.02,.11),(-.59,-.025,.94),'rust_dark',rot=(0,-18,0))
        m.box((.74,.02,.10),(.53,-.025,.27),'rust_dark',rot=(0,14,0))
        m.box((.40,.04,.88),(-.66,0,.58),'wood_grey',rot=(0,-7,0))
        m.box((.35,.04,.62),(.62,0,.39),'wood_dark',rot=(0,9,0))
        # Open breach between the surviving gate leaves.
        for x,z,a in ((-.23,.0,-20),(.20,.0,19)):
            m.poly([(-.09,.05),(.09,.05),(.07,.42),(-.02,.55),(-.1,.31)],.038,(x,-.002,z),'wood_light',rot=(0,a,0))
    else:
        for z in (.2, .98): m.box((1.82,.02,.10),(0,-.025,z),'rust_dark')
        for i in range(9):
            x=-.85+i*.2125
            if i==6:
                m.box((.19,.04,.48),(x,-.026,.40),'wood_light',rot=(0,7,0))
                m.box((.19,.04,.37),(x,-.026,.86),'wood_grey',rot=(0,-8,0))
            else:
                m.box((.19,.04,1.04),(x,-.025,.61),('wood','wood_grey','wood_dark')[i%3])
    # Hinges, latch and dropped bolt detail.
    for z in (.27,.9):
        m.box((.10,.095,.14),(-.95,0,z),'steel')
    m.box((.16,.025,.13),(.83,-.035,.58),'steel_dark')


def barricade(m, critical):
    # A-frame foot structure stays inside the existing 0.4 m depth envelope.
    for x in (-.68,.68):
        m.box((.12,.30,.1),(x,0,.11),'wood_dark')
        for side in (-1,1):
            m.poly([(-.16,0),(.16,0),(.045,1.08),(-.045,1.08)],.075,(x,side*.09,.0),'wood_dark')
    if critical:
        m.box((.92,.045,.20),(-.44,-.13,.32),'wood_grey',rot=(0,-8,0))
        m.box((1.1,.045,.20),(.41,.12,.60),'rust',rot=(0,6,0))
        m.box((.78,.045,.19),(-.42,-.10,.88),'steel_dark',rot=(0,-5,0))
        # Large bite-sized breach; fractured board ends frame the opening.
        for x,ang in ((-.12,-14),(.13,16)):
            m.poly([(-.09,.02),(.09,.02),(.075,.22),(.02,.30),(-.09,.20)],.05,(x,-.16,.48),'wood_light',rot=(0,ang,0))
        m.box((.36,.10,.05),(0,.02,.12),'wood_grey',rot=(0,8,0))
    else:
        for side in (-1,1):
            for i,z in enumerate((.30,.58,.86)):
                m.box((1.88,.04,.22),(0,side*(.15-i*.045),z),('rust','steel_dark','wood_grey')[i])
        m.box((.45,.025,.16),(-.35,-.177,.60),'wood_light',rot=(0,-14,0))
        m.box((.04,.025,.26),(-.35,-.17,.61),'black',rot=(0,14,0))


def palisade(m, critical):
    heights=[2.72+(i%3)*.08 for i in range(8)]
    if critical: heights[3]=1.55; heights[4]=1.82; heights[5]=2.1
    elif not critical: heights[4]-=.38
    for i,h in enumerate(heights):
        x=-.875+i*.25
        if critical and i==4:
            # A gap opens through the central row; broken stakes remain at both edges.
            for dx,z,angle in ((-.045,.48,-11),(.045,1.18,13)):
                m.box((.13,.24,z),(x+dx,0,z/2+.015),'wood_dark',rot=(0,angle,0))
            continue
        body_h=h-.16
        m.box((.23,.26,body_h),(x,0,body_h/2),'wood_dark' if i%3==0 else 'wood')
        m.cyl(.12,.24,(x,0,body_h+.12),'wood_grey' if i%2 else 'wood',seg=4,r2=0)
        if (critical and i in (3,5)) or (not critical and i==4):
            # Splintered top cap is shorter than the original spike and stays in the bounds.
            m.poly([(-.12,body_h-.18),(.12,body_h-.18),(.08,body_h+.12),(0,body_h+.28),(-.1,body_h+.06)],.24,(x,0,0),'wood_light')
        for z in (.52,1.47):
            if z < h-.08: m.cyl(.018,.012,(x,-.14,z),'steel_dark',rot=(90,0,0),seg=5)
    for z in (.54,1.49):
        if critical:
            m.box((.73,.02,.12),(-.58,-.135,z),'rust_dark',rot=(0,-4,0))
            m.box((.69,.02,.12),(.59,-.135,z),'rust_dark',rot=(0,5,0))
        else:
            m.box((2,.02,.15),(0,-.135,z),'rust_dark')
    for x in (-.94,.94): m.box((.12,.3,2),(x,0,1),'steel_dark')


BUILD={
    'piece_fence_damaged':lambda m:fence(m,False),
    'piece_fence_critical':lambda m:fence(m,True),
    'piece_gate_damaged':lambda m:gate(m,False),
    'piece_gate_critical':lambda m:gate(m,True),
    'piece_barricade_damaged':lambda m:barricade(m,False),
    'piece_barricade_critical':lambda m:barricade(m,True),
    'piece_palisade_damaged':lambda m:palisade(m,False),
    'piece_palisade_critical':lambda m:palisade(m,True),
}


def build(name):
    m=Model(name,seed=232750+sum(map(ord,name)))
    BUILD[name](m)
    base=name[:-len('_damaged')] if name.endswith('_damaged') else name[:-len('_critical')]
    bounds={'piece_fence':((-1,1),(-.05,.05),(0,1.2)),
            'piece_gate':((-1,1),(-.05,.05),(0,1.2)),
            'piece_barricade':((-1,1),(-.2,.2),(0,1.2)),
            'piece_palisade':((-1,1),(-.15,.15),(0,3))}[base]
    finish(m,bounds={'x':bounds[0],'y':bounds[1],'z':bounds[2]})
