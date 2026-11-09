"""Shared construction for the seven M22 loot props."""
from ss_art import Model, finish

BOUNDS = {
    'loot_bag': (0.65, 0.50, 0.45),
    'loot_boss_cache': (1.0, 0.70, 0.65),
    'item_fuel_can': (0.40, 0.22, 0.60),
    'ammo_box_pistol': (0.30, 0.20, 0.16),
    'ammo_box_rifle': (0.40, 0.25, 0.20),
    'ammo_box_shell': (0.35, 0.23, 0.20),
    'energy_cell': (0.16, 0.16, 0.40),
}

def build(name):
    sx, sy, h = BOUNDS[name]
    bound_y = sy / 2
    if name == 'item_fuel_can':
        sy *= .90  # keep the handle and pressed front panel within the requested depth
    x, y = sx / 2, sy / 2
    m = Model(name, seed=221)
    if name == 'loot_bag':
        # Slumped canvas duffel, visibly stuffed, with gathered top and shoulder straps.
        m.blob((sx*.82, sy*.80, h*.65), (0, .015, h*.40), 'fabric_stain', rough=.14, subdiv=2,
               colors=['fabric_stain','fabric_grey','fabric_stain','army_green'])
        m.box((sx*.50, sy*.72, h*.12), (0, .015, h*.78), 'army_green', bevel=.035, taper=(.78,.8))
        m.box((sx*.24, sy*.20, h*.08), (0, -.01, h*.91), 'rope', bevel=.025)
        # paired broad curved-over-shoulder webbing loops
        for side in (-1,1):
            xx=side*sx*.28
            m.box((sx*.13, .055, h*.40), (xx, sy*.25, h*.51), 'leather_dark', rot=(0,0,side*10), bevel=.018)
            m.box((sx*.11, .025, h*.35), (xx, sy*.28, h*.51), 'army_green_dark', rot=(0,0,side*10))
            m.box((sx*.18, .05, .075), (xx, -.015, h*.27), 'leather', bevel=.012)
        m.box((sx*.45, .025, .07), (0,-sy*.39,h*.53), 'fabric_blue', bevel=.012)
        m.box((.045,.018,.04),(0,-sy*.42,h*.53),'steel_dark')
        m.box((sx*.62,.035,.035),(0,-sy*.20,h*.18),'leather_dark')
    elif name == 'loot_boss_cache':
        # Reinforced hard case with lifted armored lid, broad corner guards and twin clasps.
        m.box((sx*.88,sy*.84,h*.68),(0,0,h*.36),'army_green_dark',bevel=.045)
        m.box((sx*.78,sy*.76,h*.54),(0,0,h*.35),'army_green',bevel=.04)
        m.box((sx*.94,sy*.90,h*.16),(0,0,h*.78),'mustard_dark',bevel=.035)
        m.box((sx*.88,sy*.84,h*.08),(0,0,h*.88),'mustard',bevel=.025)
        # heavy crossing bands
        for xx in (-sx*.25,sx*.25):
            m.box((sx*.12,sy*.93,h*.72),(xx,0,h*.40),'steel_dark',bevel=.012)
            m.box((sx*.08,sy*.96,h*.72),(xx,0,h*.40),'rust',bevel=.009)
        for xx in (-sx*.28,sx*.28):
            for yy in (-sy*.38,sy*.38):
                m.box((sx*.14,sy*.12,h*.42),(xx,yy,h*.30),'steel_dark',bevel=.025)
                m.box((sx*.10,sy*.09,.035),(xx,yy,h*.53),'chrome',bevel=.008)
        for xx in (-sx*.20,sx*.20):
            m.box((.10,.035,.11),(xx,-sy*.46,h*.58),'rust',bevel=.012)
            m.box((.045,.02,.08),(xx,-sy*.485,h*.58),'chrome',bevel=.008)
        m.box((sx*.30,.025,.10),(0,-sy*.46,h*.51),'warning_red',bevel=.012)
    elif name == 'item_fuel_can':
        # Pressed steel jerrycan: tapered shoulders, recessed front panel, handle and offset spout.
        m.box((sx*.88,sy*.88,h*.70),(0,0,h*.39),'rust_dark',bevel=.025,taper=(.90,.94))
        m.box((sx*.79,sy*.83,h*.67),(0,-sy*.025,h*.39),'army_green',bevel=.025,taper=(.92,.94))
        m.box((sx*.57,.018,h*.31),(0,-sy*.47,h*.38),'army_green_dark',bevel=.018)
        m.box((sx*.48,.012,h*.23),(0,-sy*.49,h*.38),'mustard_dark',bevel=.015)
        # handle bridge leaves a strong open slot
        for xx in (-sx*.22,sx*.22):
            m.box((sx*.12,sy*.30,.055),(xx,0,h*.84),'rust',bevel=.012)
        m.box((sx*.54,sy*.23,.055),(0,0,h*.94),'rust',bevel=.012)
        m.cyl(.045,.045,(sx*.20,-sy*.25,h*.83),'steel_dark',seg=8)
        m.cyl(.029,.035,(sx*.20,-sy*.25,h*.87),'rust_light',seg=8)
        m.box((.045,.10,.035),(sx*.20,-sy*.22,h*.91),'rust',rot=(0,0,-25),bevel=.008)
        for zz in (.22,.33,.44,.55):
            m.box((sx*.54,.012,.018),(0,-sy*.49,zz),'mustard',bevel=.003)
    elif name.startswith('ammo_box_'):
        # Compact stamped steel tins; silhouette and palette identify each ammo class.
        pistol=name.endswith('pistol'); rifle=name.endswith('rifle')
        body=('steel_dark' if pistol else 'army_green_dark' if rifle else 'rust_dark')
        top=('fabric_blue' if pistol else 'army_green' if rifle else 'warning_red')
        stripe=('plastic_blue' if pistol else 'hazard_yellow' if rifle else 'mustard')
        m.box((sx*.90,sy*.88,h*.75),(0,0,h*.42),body,bevel=.018)
        m.box((sx*.94,sy*.92,h*.18),(0,0,h*.84),top,bevel=.012)
        m.box((sx*.72,.018,h*.25),(0,-sy*.46,h*.40),stripe,bevel=.006)
        # distinct lid and closure hardware
        m.box((sx*.76,sy*.98,.018),(0,0,h*.96),'steel',bevel=.004)
        if pistol:
            # small rounded-corner-like low tin with two front latch tabs
            for xx in (-sx*.25,sx*.25):
                m.box((.035,.02,.045),(xx,-sy*.49,h*.70),'chrome',bevel=.006)
            m.cyl(.025,.018,(0,0,h*.98),'steel_dark',seg=8)
        elif rifle:
            # elongated ribbed field can with a carry bail
            for yy in (-sy*.22,0,sy*.22):
                m.box((sx*.80,.018,.025),(0,yy,h*.53),'mustard_dark',bevel=.004)
            m.box((sx*.36,.025,.025),(0,0,h*1.0),'chrome',bevel=.004)
        else:
            # shell tin is squat, warm, and has three raised cartridge-like brass caps
            for xx in (-sx*.24,0,sx*.24):
                m.cyl(.025,.025,(xx,-sy*.12,h*.97),'ore_metal',seg=8)
            m.box((.025,.02,.07),(0,-sy*.49,h*.58),'mustard',bevel=.004)
    elif name == 'energy_cell':
        # Chunky faceted cartridge, keyed foot, bright stacked charge ribs and insulated contacts.
        m.box((sx*.86,sy*.86,h*.76),(0,0,h*.48),'gunmetal',bevel=.022,taper=(.80,.82))
        m.box((sx*.71,sy*.71,h*.64),(0,0,h*.48),'plastic_blue',bevel=.018,taper=(.84,.86))
        for zz in (h*.27,h*.42,h*.57,h*.72):
            m.box((sx*.90,sy*.90,.027),(0,0,zz),'chrome',bevel=.004)
        m.box((sx*.58,sy*.16,h*.14),(0,-sy*.43,h*.49),'lamp_warm',bevel=.01)
        m.box((sx*.40,sy*.34,h*.09),(0,0,h*.92),'steel',bevel=.012)
        m.cyl(.025,.025,(0,0,h*.96),'mustard',seg=6)
    pad = .015 if name.startswith('ammo_box_') else (.003 if name == 'energy_cell' else 0)
    finish(m, bounds={'x':(-x,x),'y':(-bound_y-pad,bound_y+pad),'z':(-pad,h+pad)})
