"""Same-footprint wear and breach variants for the common salvaged wall panel."""
from ss_art import Model, finish


def make(name, critical):
    m = Model(name, seed=232740 if critical else 232739)
    # Keep the concrete sill, cap and side posts to preserve the original wall silhouette.
    m.box((2.0, .18, .32), (0, 0, .16), 'concrete')
    m.box((2.0, .18, .24), (0, 0, 2.88), 'concrete_dark')
    for x in (-.93, .93):
        m.box((.14, .18, 2.44), (x, 0, 1.52), 'concrete_light')
        for z in (.58, 1.48, 2.38):
            for side in (-1, 1):
                m.cyl(.02, .012, (x, side*.078, z), 'rust_dark', rot=(90,0,0), seg=5)

    if critical:
        # A true central breach: split sheet and timber pieces leave a clear view through.
        m.box((.56,.045,1.18),(-.60,0,.94),'rust_dark')
        m.box((.47,.04,.88),(.59,0,2.02),'steel_dark')
        m.box((.44,.03,.46),(-.24,-.045,2.49),'wood_grey',rot=(0,-11,0))
        m.box((.38,.03,.38),(.24,.045,.48),'wood_dark',rot=(0,13,0))
        # Jagged, bent sheet lips around the opening.
        m.poly([(-.34,1.48),(-.10,1.62),(-.18,1.80),(.02,1.94),(-.12,2.12),(-.42,2.0)],.035,(-.08,-.071,0),'rust',rot=(0,0,0))
        m.poly([(.31,1.36),(.08,1.52),(.19,1.70),(.04,1.88),(.38,1.96),(.45,1.52)],.03,(.04,.071,0),'steel',rot=(0,0,0))
        m.box((.045,.12,.94),(-.47,0,1.78),'rust',rot=(0,-13,0))
        m.box((.04,.11,.78),(.44,0,1.18),'steel_dark',rot=(0,16,0))
    else:
        # The panel remains closed, with a buckled sheet and visibly split boards.
        m.box((1.70,.10,2.48),(0,0,1.53),'concrete_dark')
        for side in (-1,1):
            m.box((1.70,.025,.78),(0,side*.066,.75),'rust_dark')
            m.box((1.70,.025,.72),(0,side*.066,2.29),'wood_grey')
            for i in range(7):
                x=-.72+i*.24
                if i == 4:
                    m.box((.20,.024,.45),(x,side*.084,2.03),'wood_light',rot=(0,8,0))
                    m.box((.20,.024,.20),(x,side*.084,2.57),'wood_dark',rot=(0,-9,0))
                else:
                    m.box((.22,.022,.64),(x,side*.084,2.30),('wood','wood_grey','wood_dark')[i%3])
            # Corrugated salvage plate, buckled off the wall face.
            m.box((.72,.025,.88),(-.43,side*.075,.94),'steel_dark',rot=(0,-5,0))
            m.box((.44,.02,.38),(-.46,side*.086,.98),'rust',rot=(0,12,0))
        # Dark, branching cracks drawn as narrow raised splinters on both faces.
        for side in (-1,1):
            y=side*.082
            for x,z,ang in ((-.18,2.18,-18),(-.34,2.0,23),(.18,.78,17),(.36,1.02,-14)):
                m.box((.035,.014,.42),(x,y,z),'black',rot=(0,ang,0))
            for x,z in ((-.46,1.12),(.42,2.55),(-.3,.48)):
                m.cyl(.045,.018,(x,y,z),'concrete_dark',rot=(90,0,0),seg=5)

    # Exposed horizontal tie rail remains recognizable in both damage states.
    m.box((1.70,.035,.09),(0,0,1.47),'rust_dark')
    for x in (-.62,-.2,.2,.62):
        m.cyl(.018,.012,(x,-.078,1.47),'gunmetal',rot=(90,0,0),seg=5)
    finish(m,bounds={'x':(-1,1),'y':(-.1,.1),'z':(0,3)})


def build(name):
    make(name, name.endswith('_critical'))
