"""Damage stages for brick / reinforced / concrete wall tiers. Footprint x 2.0, y 0.2, z 0..3."""
import random
from ss_art import Model, finish

BOUNDS = {'x': (-1, 1), 'y': (-.1, .1), 'z': (0, 3)}


def _rubble(m, rng, n, cols, spread=.9, big=.2):
    for _ in range(n):
        s = rng.uniform(.1, big)
        m.blob((s, min(s, .12), s * .8), (rng.uniform(-spread, spread), rng.uniform(-.01, .01), s * .4),
               rng.choice(cols), rough=.25, seed=rng.randint(0, 9999), flat_bottom=-s * .2)


def brick(m, crit, rng):
    cw, ch = .25, .2
    # core strips: mortar/back layer, broken to varying heights
    tops = []
    for i in range(8):
        if crit:
            top = rng.uniform(1.5, 2.9)
            if i in (3, 4):
                top = rng.uniform(.5, .9)
        else:
            top = 3.0 if i not in (2, 6) else rng.uniform(2.5, 2.8)
        tops.append(top)
        x = -1 + cw * (i + .5)
        m.box((cw, .10, top), (x, 0, top / 2), 'plaster_dirty')
    for r in range(15):
        z = r * ch + ch / 2
        off = .125 if r % 2 else 0
        for i in range(-1, 8):
            x0 = -1 + i * cw + off
            x1 = x0 + cw
            x0, x1 = max(x0, -1 + .01), min(x1, 1 - .01)
            if x1 - x0 < .06:
                continue
            col = min(7, max(0, int(((x0 + x1) / 2 + 1) / cw)))
            if z + ch / 2 > tops[col] + .05:
                continue
            miss = rng.random() < (.30 if crit else .11)
            if miss and (r > 0 or crit):
                # missing brick: leave dark recess; sometimes a cracked half brick
                if rng.random() < .3:
                    m.box(((x1 - x0) * .5 - .01, .17, ch - .03), (x0 + (x1 - x0) * .25, 0, z), 'brick_dark',
                          rot=(0, rng.uniform(-6, 6), 0))
                continue
            c = rng.choice(['brick', 'brick', 'brick_dark', 'rust_dark']) if not crit else rng.choice(['brick', 'brick_dark', 'brick_dark', 'rust_dark'])
            p = rng.random() < (.25 if crit else .10)  # pushed-out brick
            m.box(((x1 - x0) - .02, .17 if not p else .19, ch - .03), ((x0 + x1) / 2, 0, z), c,
                  rot=(0, rng.uniform(-1, 1) if crit else 0, 0))
    # cracks in mortar
    for _ in range(9 if crit else 4):
        x, z = rng.uniform(-.9, .9), rng.uniform(.3, 2.6)
        for s in (-1, 1):
            m.box((.025, .012, rng.uniform(.3, .6)), (x, s * .092, z), 'black', rot=(0, rng.uniform(-30, 30), 0))
    _rubble(m, rng, 14 if crit else 6, ['brick', 'brick_dark', 'plaster_dirty'], big=.22 if crit else .16)


def reinforced(m, crit, rng):
    m.box((2.0, .12, .12), (0, 0, .06), 'gunmetal')
    m.box((2.0, .12, .10), (0, 0, 2.95), 'gunmetal')
    for x in (-.96, .96):
        m.box((.08, .14, 3.0), (x, 0, 1.5), 'gunmetal')
    # rebar mesh behind the plates
    for z in [.3 + .3 * k for k in range(9)]:
        m.cyl(.014, 1.84, (0, 0, z), 'rust', rot=(0, 90, 0), seg=5)
    for x in [-.85 + .2 * k for k in range(9)]:
        m.cyl(.014, 2.9, (x, .0, 1.5), 'rust_dark', seg=5)
    cells = [(c, r) for c in range(2) for r in range(3)]
    gone = {(1, 2), (0, 1)} if crit else set()
    for (c, r) in cells:
        x, z = -.46 + c * .92, .5 + r * 1.0
        if (c, r) not in gone:
            m.box((.92, .08, 1.0), (x, 0, z), 'steel_dark')
        if (c, r) in gone:
            # torn-away plate: only peeled shards remain on the frame
            for s in (-1, 1):
                m.poly([(-.44, .3), (-.30, .15), (-.16, .45), (-.05, .1), (-.02, .48), (-.4, .48)], .03,
                       (x, s * .075, z - .1), 'rust_dark', rot=(0, 0, 0))
            continue
        if crit:
            tilt = rng.uniform(-5, 5)
            col = rng.choice(['rust_dark', 'steel_dark', 'rust'])
        else:
            tilt = rng.uniform(-2, 2)
            col = rng.choice(['steel', 'steel_dark', 'steel'])
        for s in (-1, 1):
            m.box((.86, .03, .92), (x, s * .08, z), col, rot=(0, tilt, 0))
            # dents
            m.box((.30, .02, .24), (x + rng.uniform(-.2, .2), s * .088, z + rng.uniform(-.25, .25)),
                  'steel_dark' if not crit else 'gunmetal', rot=(0, rng.uniform(-18, 18), 0))
            # rivets: some popped (missing)
            for dx in (-.36, .36):
                for dz in (-.4, .4):
                    if rng.random() < (.55 if crit else .25):
                        m.cyl(.012, .02, (x + dx + rng.uniform(-.02, .02), s * .088, z + dz), 'black', rot=(90, 0, 0), seg=4)
                    else:
                        m.cyl(.022, .02, (x + dx, s * .088, z + dz), 'steel', rot=(90, 0, 0), seg=5)
            # peeled corner lip curling off the face
            sx, sz = rng.choice((-1, 1)), rng.choice((-1, 1))
            m.box((.42 if crit else .3, .02, .28 if crit else .2), (x + sx * .32, s * .07, z + sz * .34), 'rust_light',
                  rot=(sz * 6, 0, 0)) if True else None
    for (c, r) in (cells[:2] if not crit else []):
        pass
    # rust streaks
    for _ in range(6 if crit else 3):
        x = rng.uniform(-.8, .8)
        for s in (-1, 1):
            m.box((.07, .008, rng.uniform(.3, .8)), (x, s * .094, rng.uniform(.5, 2.5)), 'rust')
    if crit:
        # bent rebar ends sticking out of the torn hole
        for k in range(4):
            m.cyl(.016, .5, (.2 + k * .14, 0, 2.4 + .05 * k), 'rust', rot=(0, 35 - 10 * k, 0), seg=5)
    _rubble(m, rng, 8 if crit else 3, ['steel_dark', 'rust_dark', 'gunmetal'], big=.12)


def concrete(m, crit, rng):
    n_x, n_z = 4, 6
    cw, ch = .5, .5
    holes = {(1, 3), (2, 3), (1, 4), (2, 5), (0, 2), (3, 4), (2, 2)} if crit else {(3, 5), (0, 4)}
    chip = {(1, 1), (2, 4), (0, 0), (3, 2)} if not crit else {(1, 1), (3, 1), (0, 5), (3, 3)}
    # exposed rebar grid through the holes
    for z in [.25 + .5 * k for k in range(6)]:
        m.cyl(.018, 2.0, (0, 0, z), 'rust', rot=(0, 90, 0), seg=5)
    for x in [-.75 + .5 * k for k in range(4)]:
        m.cyl(.018, 3.0, (x, 0, 1.5), 'rust_dark', seg=5)
    for i in range(n_x):
        for j in range(n_z):
            x, z = -1 + cw * (i + .5), ch * (j + .5)
            if (i, j) in holes:
                # broken stub of the chunk remains at the edge of the hole
                if rng.random() < .5:
                    m.box((.18, .14, .18), (x + rng.uniform(-.1, .1), 0, z - .15), 'concrete_dark', rot=(0, rng.uniform(-20, 20), 0))
                continue
            col = rng.choice(['concrete_light', 'concrete', 'concrete', 'concrete', 'concrete'])
            if (i, j) in chip:
                m.box((cw, .13, ch), (x, 0, z), col)
                for s in (-1, 1):  # spalled recess showing darker aggregate
                    m.box((cw * .7, .02, ch * .6), (x, s * .066, z), 'concrete_dark', rot=(0, rng.uniform(-8, 8), 0))
                    m.cyl(.016, cw * .7, (x, s * .08, z), 'rust', rot=(0, 90, 0), seg=5)
            else:
                m.box((cw, .16, ch), (x, 0, z), col)
    # cracks on both faces
    for _ in range(14 if crit else 6):
        x, z = rng.uniform(-.9, .9), rng.uniform(.2, 2.8)
        if (min(3, int((x + 1) / cw)), min(5, int(z / ch))) in holes or (min(3, int((x + 1) / cw)), min(5, int((z + .3) / ch))) in holes or (min(3, int((x + 1) / cw)), min(5, int((z - .3) / ch))) in holes:
            continue
        for s in (-1, 1):
            m.box((.03, .012, rng.uniform(.3, .7)), (x, s * .084, z), 'black', rot=(0, rng.uniform(-35, 35), 0))
    if crit:
        # slab edge chunks hanging
        m.box((.3, .14, .08), (-.4, 0, 2.15), 'concrete_light', rot=(0, 25, 0))
    _rubble(m, rng, 16 if crit else 6, ['concrete', 'concrete_dark', 'concrete_light'], big=.22 if crit else .16)


def build(name):
    kind = name.split('_')[2]
    crit = name.endswith('_critical')
    seed = {'brick': 2700, 'reinforced': 2710, 'concrete': 2720}[kind] + (1 if crit else 0)
    m = Model(name, seed=seed)
    rng = random.Random(seed)
    {'brick': brick, 'reinforced': reinforced, 'concrete': concrete}[kind](m, crit, rng)
    finish(m, bounds=BOUNDS)
