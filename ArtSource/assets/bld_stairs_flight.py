import random
from ss_art import Model, finish
m = Model('bld_stairs_flight', seed=72)
r = random.Random(7201)

N, RISE, RUN, Y0, W = 8, 0.1875, 0.3, -1.2, 1.5
for i in range(N):
    y0 = Y0 + i * RUN
    zt = (i + 1) * RISE
    c = 'concrete' if i % 2 else 'concrete_light'
    # solid block down to the floor
    m.box((W, RUN, zt - 0.008), (0, y0 + RUN / 2, (zt - 0.008) / 2), c)
    # worn darker walking strip
    m.box((0.7, RUN * 0.6, 0.008), (r.uniform(-0.1, 0.1), y0 + RUN * 0.55, zt - 0.004), 'concrete_dark')
    # yellow edge stripe at the nosing, partly worn off
    segs = [(-0.7, -0.15), (-0.08, 0.4), (0.48, 0.7)]
    for a, b in segs:
        if r.random() < 0.28:
            continue
        m.box((b - a, 0.045, 0.008), ((a + b) / 2, y0 + 0.03, zt - 0.004), 'hazard_yellow')
    # dark grime line under nosing
    m.box((W - 0.04, 0.012, 0.03), (0, y0 + 0.01, zt - 0.02), 'concrete_dark')

# chips: missing corners and rubble on steps
for i, sx in ((2, 1), (5, -1), (6, 1)):
    zt = (i + 1) * RISE
    y0 = Y0 + i * RUN
    m.box((0.14, 0.1, 0.05), (sx * 0.6, y0 + 0.04, zt - 0.005), 'concrete_dark', rot=(0, 0, 25 * sx))
m.blob((0.12, 0.1, 0.06), (0.3, Y0 + 3 * RUN + 0.2, 4 * RISE + 0.03), 'stone', subdiv=0)
m.blob((0.1, 0.08, 0.05), (-0.4, Y0 + 6 * RUN + 0.15, 7 * RISE + 0.03), 'stone_dark', subdiv=0)
# exposed rebar at a broken corner
m.tube([(0.68, Y0 + 2 * RUN + 0.05, 3 * RISE - 0.02), (0.73, Y0 + 2 * RUN + 0.05, 3 * RISE + 0.07)], 0.012, 'rust', seg=5)
m.tube([(-0.7, Y0 + 5 * RUN + 0.05, 6 * RISE - 0.02), (-0.73, Y0 + 5 * RUN + 0.06, 6 * RISE + 0.08)], 0.012, 'rust', seg=5)
# side grime stripes on the stringer faces
for s in (-1, 1):
    m.box((0.01, N * RUN * 0.9, 0.03), (s * 0.745, 0.0, 0.05), 'concrete_dark')

finish(m, bounds={'x': (-0.75, 0.75), 'y': (-1.2, 1.2), 'z': (0, 1.5)})
