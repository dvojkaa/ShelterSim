from ss_art import Model, finish
m = Model('bld_stair_rail', seed=73)

HX = 1.2
# posts: thin half-posts on the tile ends so two tiles join as one post; full posts inside
for x, w in ((-HX + 0.03, 0.06), (-0.4, 0.06), (0.4, 0.06), (HX - 0.03, 0.06)):
    m.box((w, 0.07, 1.0), (x, 0, 0.5), 'steel_dark', bevel=0.004)
    m.box((w, 0.1, 0.03), (x, 0, 0.015), 'rust_dark')
for x in (-0.4, 0.4):
    m.box((0.1, 0.012, 0.1), (x, 0.0, 0.08), 'rust')   # gusset
# rails run full width
m.box((2 * HX, 0.05, 0.05), (0, 0, 0.97), 'steel', bevel=0.004)
m.box((2 * HX, 0.04, 0.04), (0, 0, 0.52), 'steel_dark')
m.box((2 * HX, 0.04, 0.04), (0, 0, 0.1), 'rust')
# rungs between mid rail and bottom rail
missing = {3, 8}
bent = {5: 14, 11: -20}
for k in range(12):
    x = -1.04 + k * 0.19
    if abs(x - (-0.4)) < 0.06 or abs(x - 0.4) < 0.06 or k in missing:
        continue
    if k in bent:
        m.box((0.02, 0.02, 0.4), (x + 0.04, 0.0, 0.31), 'rust_light', rot=(0, bent[k], 0))
    else:
        m.box((0.02, 0.02, 0.4), (x, 0, 0.31), 'steel')
# upper balusters (mid to top) sparse
for k in range(6):
    x = -0.95 + k * 0.38
    if abs(x - (-0.4)) < 0.06 or abs(x - 0.4) < 0.06 or k == 2:
        continue
    m.box((0.02, 0.02, 0.43), (x, 0, 0.745), 'steel_dark')
# dangling snapped bar and grime
m.box((0.02, 0.02, 0.25), (0.62, 0.0, 0.8), 'rust', rot=(15, 0, 0))
m.box((0.5, 0.05, 0.012), (-0.7, 0, 0.975), 'rust_dark')

finish(m, bounds={'x': (-1.2, 1.2), 'y': (-0.05, 0.05), 'z': (0, 1.0)})
