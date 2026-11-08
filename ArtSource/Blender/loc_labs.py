# Lab backdrops: underground (level 1) and surface lab (level 2). Interior y range 0..8.4 (ceiling slab above).
# Run in Blender:  ONLY = ['u_back', ...]; exec(open(r"...\loc_labs.py", encoding="utf-8").read())
import random, math
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\neon_kit.py", encoding="utf-8").read(), globals())

CYAN = (0.25, 0.92, 1.0)
PINK = (1.0, 0.28, 0.72)
AMBER = (1.0, 0.7, 0.32)
GREEN = (0.4, 1.0, 0.45)
TOXIC = (0.55, 1.0, 0.3)
VIOLET = (0.72, 0.42, 1.0)
RED = (1.0, 0.18, 0.16)
WARM = (1.0, 0.82, 0.55)
Z0 = -1.0


def cone_light(b, x, ztop, zbot, w_top, w_bot, col, alpha=0.05, depth=-0.5, layers=7):
    """Soft volumetric light cone: nested translucent trapezoids that get shorter and narrower,
    so the light is densest at the lamp and fades out towards the floor."""
    for i in range(layers):
        t = i / layers
        zb = zbot + (ztop - zbot) * t * 0.75
        k = (zb - ztop) / (zbot - ztop)            # width at that height
        wb = w_top + (w_bot - w_top) * k
        shrink = 1 - 0.35 * t
        a = alpha * 0.32
        b.prism(flat(f'Cone{col}{i}{alpha:.3f}', col, a),
                [(x - w_top * shrink / 2, ztop), (x + w_top * shrink / 2, ztop), (x + wb * shrink / 2, zb), (x - wb * shrink / 2, zb)],
                depth - i * 0.01, depth - i * 0.01 + 0.001, c='NoLine')


def hanging_lamp(b, x, z, col=WARM, cone=True, cord=3.0, depth=-0.6):
    b.box(toon('Cord', (0.05, 0.05, 0.07)), x - 0.015, x + 0.015, z + 0.25, z + 0.25 + cord, depth + 0.1, depth + 0.12)
    b.prism(toon('LampShade', (0.18, 0.2, 0.22)), [(x - 0.32, z), (x + 0.32, z), (x + 0.1, z + 0.28), (x - 0.1, z + 0.28)], depth, depth + 0.3)
    b.box(emit(f'Bulb{col}', col), x - 0.18, x + 0.18, z - 0.04, z + 0.01, depth - 0.02, depth, c='NoLine')
    if cone:
        cone_light(b, x, z - 0.02, 0.0, 0.5, 3.4, col, alpha=0.028, depth=depth - 0.05)


def pipe_run(b, z, r, mat, depth, valves=(), W=40):
    b.cyl(mat, W / 2, z, r, W + 4, axis='X', y=depth, seg=16)
    for vx in valves:
        b.box(toon('Flange', (0.25, 0.27, 0.3)), vx - 0.06, vx + 0.06, z - r * 1.35, z + r * 1.35, depth - r * 1.4, depth + r)
        b.box(toon('Flange', (0.25, 0.27, 0.3)), vx + 0.22, vx + 0.34, z - r * 1.35, z + r * 1.35, depth - r * 1.4, depth + r)


# ------------------------------------------------------------------ props for the lab mid layer
def tank(b, x, w, h, liquid=TOXIC, specimen=None, depth=-0.2, rng=None):
    steel = toon('TankSteel', (0.24, 0.3, 0.32))
    dark = toon('TankDark', (0.1, 0.13, 0.14))
    b.box(dark, x - w / 2 - 0.15, x + w / 2 + 0.15, 0, 0.7, depth - 0.2, depth + 0.8, bevel=0.04)             # plinth
    b.box(emit('PlinthLed', liquid), x - w / 2 + 0.1, x + w / 2 - 0.1, 0.48, 0.53, depth - 0.22, depth - 0.2, c='NoLine')
    # glass column: liquid gradient, highlight streak, rim
    gz0, gz1 = 0.7, 0.7 + h
    b.quad(gradient_mat(f'Liquid{liquid}', tuple(c * 0.75 for c in liquid), tuple(c * 0.2 for c in liquid), gz0, gz1, 0.95, 0.85),
           x - w / 2, x + w / 2, gz0, gz1 - 0.25, y=depth + 0.3, c='NoLine')
    b.quad(flat('GlassTop', (0.5, 0.7, 0.65), 0.18), x - w / 2, x + w / 2, gz1 - 0.25, gz1, y=depth + 0.29, c='NoLine')
    b.quad(flat('Shine', (1, 1, 1), 0.35), x - w / 2 + 0.12, x - w / 2 + 0.2, gz0 + 0.2, gz1 - 0.3, y=depth + 0.1, c='NoLine')
    b.quad(flat('Shine2', (1, 1, 1), 0.15), x - w / 2 + 0.28, x - w / 2 + 0.32, gz0 + 0.4, gz1 - 0.6, y=depth + 0.1, c='NoLine')
    for sx in (-1, 1):
        b.box(steel, x + sx * w / 2 - 0.06, x + sx * w / 2 + 0.06, gz0, gz1, depth - 0.05, depth + 0.6)
    b.box(steel, x - w / 2 - 0.12, x + w / 2 + 0.12, gz1, gz1 + 0.35, depth - 0.15, depth + 0.8, bevel=0.03)   # cap
    b.box(dark, x - 0.12, x + 0.12, gz1 + 0.35, 8.6, depth + 0.3, depth + 0.5)                                # feed pipe
    rng = rng or random.Random(int(x * 10))
    for _ in range(int(h * 5)):                                                                                # bubbles
        bx, bz = rng.uniform(x - w / 2 + 0.15, x + w / 2 - 0.15), rng.uniform(gz0 + 0.1, gz1 - 0.35)
        r = rng.uniform(0.025, 0.07)
        b.disc(flat('Bubble', tuple(min(1, c * 1.1 + 0.25) for c in liquid), 0.75), bx, bz, r, y=depth + 0.2, seg=12, c='NoLine')
    if specimen == 'duck':
        yel = toon('Duck', (1.0, 0.82, 0.15))
        cz = gz0 + h * 0.55
        b.sphere(yel, x, cz, 0.3, y=depth + 0.25, sx=1.2, sz=0.75, sub=3)
        b.sphere(yel, x + 0.25, cz + 0.3, 0.17, y=depth + 0.24, sub=3)
        b.prism(toon('Beak', (1.0, 0.45, 0.1)), [(x + 0.38, cz + 0.33), (x + 0.55, cz + 0.29), (x + 0.38, cz + 0.25)], depth + 0.15, depth + 0.2)
        b.disc(flat('Eye', (0.05, 0.05, 0.05)), x + 0.3, cz + 0.36, 0.03, y=depth + 0.12, c='NoLine')
    elif specimen == 'hand':  # a prototype bionic hand floating in the tank
        metal = toon('Proto', (0.5, 0.52, 0.6))
        cz = gz0 + h * 0.45
        b.box(metal, x - 0.12, x + 0.12, cz - 0.5, cz + 0.05, depth + 0.2, depth + 0.3, bevel=0.03)
        b.box(metal, x - 0.2, x + 0.2, cz + 0.05, cz + 0.38, depth + 0.2, depth + 0.3, bevel=0.04)
        for i, fx in enumerate((-0.15, -0.05, 0.05, 0.15)):
            b.box(metal, x + fx - 0.035, x + fx + 0.035, cz + 0.38, cz + 0.62 + (0.05 if i in (1, 2) else 0), depth + 0.2, depth + 0.3, bevel=0.015)
        b.bar(metal, x - 0.2, cz + 0.15, x - 0.36, cz + 0.36, 0.07, depth + 0.2, depth + 0.3)
        b.box(emit('ProtoLed', AMBER), x - 0.1, x + 0.1, cz - 0.2, cz - 0.16, depth + 0.18, depth + 0.2, c='NoLine')
    elif specimen == 'eye':  # a giant eyeball that follows you (well, it would)
        cz = gz0 + h * 0.55
        b.sphere(toon('EyeWhite', (0.92, 0.9, 0.86)), x, cz, 0.36, y=depth + 0.25, sub=3)
        b.disc(flat('Iris', (0.2, 0.75, 0.9)), x + 0.1, cz + 0.02, 0.16, y=depth - 0.15, seg=28, c='NoLine')
        b.disc(flat('Pupil', (0.02, 0.02, 0.04)), x + 0.12, cz + 0.02, 0.08, y=depth - 0.16, seg=20, c='NoLine')
        b.disc(flat('Glint', (1, 1, 1)), x + 0.06, cz + 0.08, 0.03, y=depth - 0.17, seg=10, c='NoLine')
        for k in range(3):
            a = 3.6 + k * 0.4
            b.tube(flat('Vein', (0.75, 0.2, 0.25)), [(x + 0.34 * math.cos(a), cz + 0.34 * math.sin(a)), (x + 0.22 * math.cos(a + 0.2), cz + 0.22 * math.sin(a + 0.2))], 0.01, y=depth - 0.14, c='NoLine')
        b.tube(toon('Nerve', (0.8, 0.45, 0.5)), [(x - 0.3, cz - 0.2), (x - 0.4, cz - 0.6), (x - 0.3, gz0 + 0.3)], 0.04, y=depth + 0.3)


def bench(b, x, w, rng, depth=-0.3):
    top = toon('BenchTop', (0.3, 0.32, 0.36))
    body = toon('BenchBody', (0.14, 0.17, 0.2))
    b.box(top, x - w / 2, x + w / 2, 1.0, 1.1, depth - 0.3, depth + 0.4)
    b.box(body, x - w / 2 + 0.05, x + w / 2 - 0.05, 0, 1.0, depth - 0.2, depth + 0.4)
    for k in range(int(w / 0.8)):
        dx = x - w / 2 + 0.1 + k * 0.8
        b.box(toon('Drawer', (0.18, 0.22, 0.25)), dx, dx + 0.7, 0.55, 0.92, depth - 0.25, depth - 0.15, bevel=0.01)
        b.box(toon('Handle', (0.45, 0.48, 0.52)), dx + 0.25, dx + 0.45, 0.8, 0.83, depth - 0.28, depth - 0.25)
    # monitor with a live graph
    mx = x - w / 4
    b.box(toon('MonFrame', (0.09, 0.1, 0.12)), mx - 0.5, mx + 0.5, 1.35, 2.0, depth - 0.1, depth + 0.05, bevel=0.02)
    b.box(toon('MonFrame', (0.09, 0.1, 0.12)), mx - 0.05, mx + 0.05, 1.1, 1.35, depth, depth + 0.05)
    col = rng.choice([CYAN, GREEN, AMBER])
    b.quad(emit(f'Scr{col}', tuple(c * 0.25 for c in col)), mx - 0.44, mx + 0.44, 1.41, 1.94, y=depth - 0.11, c='NoLine')
    pts = [(mx - 0.4 + i * 0.04, 1.66 + 0.14 * math.sin(i * 0.9 + x) * (1 if i % 5 else 1.8)) for i in range(21)]
    b.tube(emit(f'Graph{col}', col), pts, 0.012, y=-0.0 + depth - 0.13, seg=4, c='NoLine')
    for i in range(3):
        b.box(emit(f'ScrTxt{col}', tuple(c * 0.7 for c in col)), mx - 0.4, mx - 0.4 + rng.uniform(0.15, 0.4), 1.85 - i * 0.05, 1.87 - i * 0.05, depth - 0.12, depth - 0.115, c='NoLine')
    # flasks + beakers with glowing liquids
    for k in range(rng.randint(3, 5)):
        fx = x + rng.uniform(0, w / 2 - 0.2)
        fc = rng.choice([TOXIC, CYAN, PINK, VIOLET, AMBER])
        hgt = rng.uniform(0.18, 0.4)
        r = rng.uniform(0.06, 0.1)
        b.quad(flat('Glass', (0.6, 0.8, 0.85), 0.25), fx - r, fx + r, 1.1, 1.1 + hgt, y=depth - 0.25, c='NoLine')
        b.quad(emit(f'Fl{fc}', tuple(c * 0.8 for c in fc)), fx - r + 0.01, fx + r - 0.01, 1.11, 1.1 + hgt * 0.55, y=depth - 0.26, c='NoLine')
        b.box(toon('Glass2', (0.4, 0.55, 0.6)), fx - r, fx + r, 1.1 + hgt, 1.1 + hgt + 0.03, depth - 0.27, depth - 0.24)
    # microscope
    if rng.random() < 0.5:
        sx = x + w / 2 - 0.4
        mm = toon('Micro', (0.82, 0.84, 0.88))
        b.box(mm, sx - 0.15, sx + 0.15, 1.1, 1.16, depth - 0.2, depth)
        b.bar(mm, sx + 0.1, 1.16, sx - 0.02, 1.62, 0.07, depth - 0.18, depth - 0.1)
        b.bar(mm, sx - 0.04, 1.6, sx - 0.12, 1.36, 0.07, depth - 0.2, depth - 0.12)


def shelf(b, x, w, h, rng, depth=0.2):
    wood = toon('ShelfMetal', (0.2, 0.22, 0.25))
    for sx in (-1, 1):
        b.box(wood, x + sx * w / 2 - 0.05, x + sx * w / 2 + 0.05, 0, h, depth - 0.1, depth + 0.3)
    rows = int(h / 0.7)
    for r in range(rows):
        z = 0.5 + r * 0.7
        b.box(wood, x - w / 2, x + w / 2, z - 0.05, z, depth - 0.12, depth + 0.3)
        cx = x - w / 2 + 0.12
        while cx < x + w / 2 - 0.15:
            kind = rng.random()
            if kind < 0.45:  # jar with glowing sample
                jw, jh = rng.uniform(0.12, 0.2), rng.uniform(0.2, 0.4)
                fc = rng.choice([TOXIC, CYAN, PINK, VIOLET, AMBER, GREEN])
                b.quad(emit(f'Jar{fc}', tuple(c * 0.55 for c in fc)), cx, cx + jw, z, z + jh * 0.8, y=depth - 0.15, c='NoLine')
                b.box(toon('Lid', (0.3, 0.3, 0.34)), cx - 0.01, cx + jw + 0.01, z + jh * 0.8, z + jh * 0.8 + 0.05, depth - 0.17, depth - 0.1)
                cx += jw + 0.06
            elif kind < 0.8:  # binders
                for k in range(rng.randint(2, 5)):
                    bw, bh = 0.06, rng.uniform(0.3, 0.45)
                    bc = rng.choice([(0.5, 0.15, 0.2), (0.15, 0.3, 0.45), (0.5, 0.42, 0.18), (0.2, 0.38, 0.25)])
                    b.box(toon(f'Binder{bc}', bc), cx, cx + bw, z, z + bh, depth - 0.14, depth + 0.1)
                    cx += bw + 0.005
                cx += 0.06
            else:  # box
                bw = rng.uniform(0.25, 0.4)
                b.box(toon('Carton', (0.36, 0.28, 0.2)), cx, cx + bw, z, z + 0.25, depth - 0.14, depth + 0.1, bevel=0.01)
                cx += bw + 0.06


def grow_bed(b, x, w, rng, depth=-0.1):
    b.box(toon('Planter', (0.18, 0.15, 0.13)), x - w / 2, x + w / 2, 0.6, 1.0, depth - 0.2, depth + 0.3, bevel=0.02)
    b.box(toon('PlanterLeg', (0.12, 0.12, 0.14)), x - w / 2 + 0.1, x - w / 2 + 0.18, 0, 0.6, depth - 0.1, depth)
    b.box(toon('PlanterLeg', (0.12, 0.12, 0.14)), x + w / 2 - 0.18, x + w / 2 - 0.1, 0, 0.6, depth - 0.1, depth)
    leaves = [toon('LeafA', (0.12, 0.45, 0.28)), toon('LeafB', (0.22, 0.6, 0.25)), toon('LeafC', (0.1, 0.35, 0.3))]
    px = x - w / 2 + 0.2
    while px < x + w / 2 - 0.15:
        hgt = rng.uniform(0.3, 0.8)
        for k in range(rng.randint(3, 6)):
            b.sphere(rng.choice(leaves), px + rng.uniform(-0.12, 0.12), 1.0 + rng.uniform(0.05, hgt), rng.uniform(0.13, 0.22), y=depth - 0.25, sx=1.4, sub=2)
        if rng.random() < 0.4:
            b.sphere(emit('Bloom', PINK), px, 1.0 + hgt + 0.05, 0.05, y=depth - 0.35, sub=1, c='NoLine')
        px += rng.uniform(0.22, 0.35)
    # purple grow light bar on chains
    lz = 2.5
    b.box(toon('GrowBar', (0.12, 0.12, 0.15)), x - w / 2, x + w / 2, lz, lz + 0.12, depth - 0.3, depth)
    b.box(emit('Grow', VIOLET), x - w / 2 + 0.05, x + w / 2 - 0.05, lz - 0.03, lz, depth - 0.31, depth - 0.29, c='NoLine')
    for sx in (-1, 1):
        b.box(toon('Chain', (0.2, 0.2, 0.24)), x + sx * (w / 2 - 0.1) - 0.01, x + sx * (w / 2 - 0.1) + 0.01, lz + 0.12, 8.6, depth - 0.1, depth - 0.08)
    cone_light(b, x, lz - 0.03, 1.0, w * 0.95, w * 1.15, VIOLET, alpha=0.05, depth=depth + 0.5)


def server_rack(b, x, rng, depth=0.0, h=2.6, w=0.9):
    b.box(toon('Rack', (0.08, 0.09, 0.12)), x - w / 2, x + w / 2, 0, h, depth - 0.2, depth + 0.6, bevel=0.02)
    for k in range(int((h - 0.3) / 0.22)):
        z = 0.15 + k * 0.22
        b.box(toon('RackUnit', (0.14, 0.15, 0.2)), x - w / 2 + 0.06, x + w / 2 - 0.06, z, z + 0.18, depth - 0.24, depth - 0.2)
        for j in range(rng.randint(1, 5)):
            col = rng.choice([GREEN, GREEN, CYAN, AMBER, RED])
            lx = x - w / 2 + 0.12 + j * 0.07
            b.box(emit(f'Led{col}', col), lx, lx + 0.03, z + 0.07, z + 0.1, depth - 0.26, depth - 0.24, c='NoLine')


def wall_sign(b, word, x, z, size, col, depth=0.4, box=True):
    tw = text_width(word, size)
    if box:
        b.box(toon('SignPanel', (0.05, 0.05, 0.07)), x - tw / 2 - 0.25, x + tw / 2 + 0.25, z - 0.18, z + size + 0.12, depth, depth + 0.1)
    text(word, emit(f'WS{col}', col), x, z, size, y=depth - 0.02, c='NoLine')


def logo(b, x, z, r, col, depth=0.35):
    """Biopulse emblem: a neon ring with a pulse line through it."""
    seg = 48
    for i in range(seg):
        a0, a1 = i * 2 * math.pi / seg, (i + 1) * 2 * math.pi / seg
        b.bar(emit(f'Logo{col}', col), x + r * math.cos(a0), z + r * math.sin(a0), x + r * math.cos(a1), z + r * math.sin(a1), 0.07, depth - 0.02, depth, c='NoLine')
    pulse = [(-1.0, 0), (-0.45, 0), (-0.3, 0.35), (-0.12, -0.55), (0.05, 0.75), (0.22, -0.25), (0.35, 0), (1.0, 0)]
    b.tube(emit(f'Logo{col}', col), [(x + px * r * 0.95, z + pz * r * 0.6) for px, pz in pulse], 0.035, y=depth - 0.03, seg=5, c='NoLine')


# ============================================================ UNDERGROUND
def u_back():
    reset()
    W, H = 40, 10.4
    setup_render(W, H, 64, 0, Z0, line=1.2)
    rng = random.Random(31)
    b = Batch()
    wall = toon('UWall', (0.07, 0.11, 0.12), steps=(.7, .85, 1.0))
    panel = toon('UPanel', (0.09, 0.14, 0.15), steps=(.7, .85, 1.0))
    pillar = toon('UPillar', (0.11, 0.15, 0.17))
    b.box(wall, -W, 2 * W, Z0, 9.6, 6, 8)
    for i in range(int(W / 2) + 1):          # wall panels
        x = i * 2.0
        for z in (0.2, 3.0, 5.8):
            b.box(panel, x + 0.08, x + 1.92, z, z + 2.6, 5.85, 6, bevel=0.03)
    for x in (0.0, 10.0, 20.0, 30.0):         # arches between pillars
        b.box(pillar, x - 0.5, x + 0.5, Z0, 9.6, 4.5, 5.5)
        b.box(toon('UPillarCap', (0.15, 0.2, 0.22)), x - 0.7, x + 0.7, 7.0, 7.4, 4.4, 5.6)
        b.box(toon('UPillarCap', (0.15, 0.2, 0.22)), x - 0.7, x + 0.7, Z0, 0.6, 4.4, 5.6)
        b.box(emit('PillarStrip', (0.15, 0.5, 0.45)), x - 0.04, x + 0.04, 1.2, 6.6, 4.38, 4.4, c='NoLine')
    # catwalk with railing at z=5
    cw = toon('Catwalk', (0.13, 0.17, 0.19))
    b.box(cw, -W, 2 * W, 4.7, 4.85, 3, 5)
    b.box(cw, -W, 2 * W, 5.55, 5.62, 3, 3.1)
    for k in range(int(W / 0.5) + 1):
        b.box(cw, k * 0.5, k * 0.5 + 0.04, 4.85, 5.55, 3, 3.05)
    # huge silhouetted tanks in the gloom
    for x in (5.0, 15.5, 26.0, 35.0):
        w = rng.uniform(2.2, 3.0)
        b.box(toon('BigTank', (0.08, 0.16, 0.15)), x - w / 2, x + w / 2, Z0, 6.8, 4, 4.5)
        b.cyl(toon('BigTankCap', (0.11, 0.18, 0.18)), x, 6.8, w / 2, 0.6, y=4.2, seg=24, r2=w / 4)
        b.quad(gradient_mat('BigTankGlow', (0.2, 0.55, 0.28), (0.05, 0.15, 0.1), 0, 6, 0.65, 0.2), x - w / 2 + 0.25, x + w / 2 - 0.25, 0.4, 6.2, y=3.95, c='NoLine')
        for k in range(4):
            b.box(toon('Band', (0.13, 0.2, 0.2)), x - w / 2 - 0.05, x + w / 2 + 0.05, 1 + k * 1.5, 1.15 + k * 1.5, 3.9, 4.6)
    # vault door
    vx = 21.0
    b.cyl(toon('Vault', (0.16, 0.2, 0.22)), vx, 2.5, 1.8, 0.4, axis='Y', y=5.6, seg=40)
    b.cyl(toon('VaultIn', (0.12, 0.15, 0.17)), vx, 2.5, 1.4, 0.4, axis='Y', y=5.5, seg=40)
    for k in range(8):
        a = k * math.pi / 4
        b.bar(toon('VaultSpoke', (0.2, 0.25, 0.27)), vx, 2.5, vx + 1.3 * math.cos(a), 2.5 + 1.3 * math.sin(a), 0.12, 5.25, 5.3)
    b.cyl(emit('VaultCore', (0.3, 0.9, 0.6)), vx, 2.5, 0.25, 0.1, axis='Y', y=5.2, seg=24)
    # hanging work lamps with warm cones
    for x in (2.5, 12.5, 23.5, 32.5):
        hanging_lamp(b, x, 7.6, WARM, cone=True, cord=1.6, depth=2.0)
    # pipes along the top
    pipe_run(b, 8.3, 0.18, toon('UPipe', (0.14, 0.24, 0.22)), 2.5, valves=(6, 17, 29), W=W)
    pipe_run(b, 7.95, 0.1, toon('UPipe2', (0.28, 0.16, 0.12)), 2.4, valves=(11, 24), W=W)
    # sign on the back wall
    wall_sign(b, 'СЕКТОР Б', 10.0, 7.65, 0.32, (0.4, 0.9, 0.7), depth=4.3, box=True)
    b.build(wrap_width=W)
    wrap_objects([o for c in ('Main', 'NoLine') for o in coll(c).objects if o.name.startswith('Sign_')], W)
    render('Lab_Back.png', glow_kw=dict(threshold=0.55, radii=(4, 18), strength=(.5, .4), opaque=True))


def u_mid():
    reset()
    W, H = 40, 10.4
    setup_render(W, H, 80, 0, Z0, line=1.6)
    rng = random.Random(41)
    b = Batch()
    # lay out stations left to right
    x = 1.0
    plan = ['tank', 'bench', 'shelf', 'grow', 'tank2', 'rack', 'bench', 'logo', 'tank3', 'shelf', 'grow', 'rack']
    specimens = iter(['duck', 'hand', 'eye'])
    for item in plan:
        if item.startswith('tank'):
            n = 2 if item == 'tank' else 1
            for k in range(n):
                tank(b, x + 0.9, 1.3, rng.uniform(3.6, 4.4), liquid=rng.choice([TOXIC, TOXIC, (0.3, 0.95, 0.85)]),
                     specimen=next(specimens, None) if k == 0 else None, rng=rng)
                x += 2.1
        elif item == 'bench':
            w = rng.uniform(2.6, 3.4)
            bench(b, x + w / 2, w, rng)
            hanging_lamp(b, x + w / 2, 3.6, WARM, cone=True, cord=5.0, depth=-0.6)
            x += w + 0.6
        elif item == 'shelf':
            w = rng.uniform(1.6, 2.2)
            shelf(b, x + w / 2, w, rng.uniform(2.6, 3.4), rng)
            x += w + 0.5
        elif item == 'grow':
            w = rng.uniform(2.2, 2.8)
            grow_bed(b, x + w / 2, w, rng)
            x += w + 0.6
        elif item == 'rack':
            for k in range(2):
                server_rack(b, x + 0.5 + k * 0.95, rng)
            x += 2.3
        elif item == 'logo':
            logo(b, x + 1.4, 4.5, 0.9, GREEN)
            wall_sign(b, 'БИОПУЛЬС', x + 1.4, 3.0, 0.38, GREEN, box=False)
            x += 3.0
    # hazard sign + door
    for sx in (13.2, 33.0):
        b.prism(toon('HazSign', (0.95, 0.72, 0.12)), [(sx, 6.2), (sx + 0.35, 5.6), (sx - 0.35, 5.6)], 0.3, 0.35)
        text('!', flat('HazInk', (0.05, 0.04, 0.02)), sx, 5.66, 0.38, y=0.28, c='NoLine')
    b.build(wrap_width=W)
    wrap_objects([o for c in ('Main', 'NoLine') for o in coll(c).objects if o.name.startswith('Sign_')], W)
    print('u_mid width used', x)
    render('Lab_Mid.png', glow_kw=dict(threshold=0.55, radii=(4, 14), strength=(.55, .35)))


def u_front():
    reset()
    W, H = 40, 5
    setup_render(W, H, 80, 0, 0, line=0)
    rng = random.Random(51)
    b = Batch()
    sil = flat('UFG', (0.015, 0.025, 0.03))
    b.cyl(sil, W / 2, H - 0.35, 0.28, W + 4, axis='X', seg=16)
    b.cyl(sil, W / 2, H - 0.85, 0.14, W + 4, axis='X', seg=12)
    for x in (4.0, 17.0, 29.0):
        b.box(sil, x, x + 0.5, H - 1.0, H, -0.1, 0.1)
        b.box(sil, x + 6, x + 6.15, H - 2.2, H, -0.1, 0.1)
        b.box(sil, x + 5.6, x + 6.6, H - 2.35, H - 2.15, -0.1, 0.1)
    x = 0
    while x < W:
        x1 = x + rng.uniform(4, 8)
        b.tube(sil, catenary(x, H - 0.4, x1, H - 0.4, rng.uniform(0.6, 1.6), n=20), 0.04, seg=6)
        x = x1 - rng.uniform(0, 1.5)
    for x in (10.5, 33.5):  # dripping valve with a tiny green light
        b.box(sil, x - 0.06, x + 0.06, H - 1.6, H - 0.35, -0.1, 0.1)
        b.sphere(emit('FGLed', GREEN), x + 0.15, H - 1.2, 0.05, y=-0.2, sub=1)
    b.build(wrap_width=W)
    render('Lab_Front.png', glow_kw=dict(threshold=0.5, radii=(4, 10), strength=(.6, .3)))


# ============================================================ SURFACE LAB (red alert, windows to the night city)
def city_through_windows(b, W, rng, z_lo=1.4, z_hi=7.6, depth=30):
    b.quad(gradient_mat('NightSky', (0.22, 0.05, 0.2), (0.02, 0.015, 0.05), z_lo, z_hi), -W, 2 * W, z_lo - 1, z_hi + 1, y=depth + 10, c='NoLine')
    x = 0
    while x < W:
        w = rng.uniform(1.0, 2.4)
        top = rng.uniform(3.0, 8.0)
        b.box(toon('CityBld', (0.1, 0.07, 0.17), steps=(.8, .9, 1.0)), x, x + w, z_lo - 1, top, depth, depth + 2)
        for i in range(int(w / 0.28)):
            for j in range(int((top - z_lo) / 0.4)):
                if rng.random() < 0.22:
                    col = rng.choice([AMBER, AMBER, CYAN, PINK])
                    wx, wz = x + 0.12 + i * 0.28, z_lo + j * 0.4
                    b.quad(emit(f'CW{col}', tuple(c * 0.6 for c in col)), wx, wx + 0.1, wz, wz + 0.15, y=depth - 0.01, c='NoLine')
        if rng.random() < 0.3:
            col = rng.choice([PINK, CYAN, VIOLET])
            sz = rng.uniform(z_lo + 1, top - 1)
            b.box(emit(f'CSign{col}', col), x + 0.2, x + 0.32, sz, sz + 1.2, depth - 0.2, depth - 0.1, c='NoLine')
        x += w + rng.uniform(0.05, 0.5)


def s_back():
    reset()
    W, H = 40, 10.4
    setup_render(W, H, 64, 0, Z0, line=1.2)
    rng = random.Random(61)
    b = Batch()
    wall = toon('SWall', (0.17, 0.13, 0.17), steps=(.7, .85, 1.0))
    trim = toon('STrim', (0.09, 0.07, 0.1))
    city_through_windows(b, W, rng)
    # wall with big windows every 8 units
    for i in range(5):
        x = i * 8.0
        b.box(wall, x, x + 1.6, Z0, 9.6, 5, 6)            # pier
        b.box(wall, x + 1.6, x + 8, Z0, 1.4, 5, 6)        # sill wall
        b.box(wall, x + 1.6, x + 8, 7.6, 9.6, 5, 6)       # lintel wall
        b.box(trim, x + 1.6, x + 8, 1.25, 1.45, 4.7, 5.2)
        for k in range(1, 4):                              # mullions
            mx = x + 1.6 + k * 1.6
            b.box(trim, mx - 0.05, mx + 0.05, 1.4, 7.6, 4.8, 5.1)
        b.box(trim, x + 1.6, x + 8, 4.45, 4.55, 4.8, 5.1)
        b.quad(flat('GlassTint', (0.4, 0.15, 0.3), 0.12), x + 1.6, x + 8, 1.4, 7.6, y=4.75, c='NoLine')
        # rain streaks on glass
        for _ in range(10):
            rx = rng.uniform(x + 1.7, x + 7.9)
            rz = rng.uniform(2.0, 7.2)
            b.quad(flat('Rain', (0.7, 0.8, 1.0), 0.18), rx, rx + 0.015, rz, rz + rng.uniform(0.2, 0.6), y=4.7, c='NoLine')
        # red alarm beacon on each pier + its wash
        bx = x + 0.8
        b.box(trim, bx - 0.2, bx + 0.2, 6.6, 6.75, 4.6, 5)
        b.sphere(emit('Alarm', RED), bx, 6.95, 0.2, y=4.5, sz=0.9, sub=2, c='NoLine')
        cone_light(b, bx, 6.9, 0.0, 0.3, 4.5, RED, alpha=0.035, depth=4.4)
    b.box(trim, -W, 2 * W, 8.6, 9.0, 4.5, 6)
    b.build(wrap_width=W)
    render('Lab2_Back.png', glow_kw=dict(threshold=0.55, radii=(4, 18), strength=(.55, .45), opaque=True))


def s_mid():
    reset()
    W, H = 40, 10.4
    setup_render(W, H, 80, 0, Z0, line=1.6)
    rng = random.Random(71)
    b = Batch()
    x = 0.8
    plan = ['rack', 'desk', 'plant', 'board', 'cooler', 'rack', 'glass', 'desk', 'poster', 'plant', 'rack', 'desk']
    for item in plan:
        if item == 'rack':
            for k in range(3):
                server_rack(b, x + 0.5 + k * 0.95, rng, h=2.8)
            b.box(toon('Tray2', (0.12, 0.1, 0.14)), x, x + 2.9, 3.0, 3.1, -0.1, 0.4)
            for col in (PINK, CYAN, AMBER):
                b.tube(toon(f'RC{col}', tuple(c * 0.4 for c in col)), catenary(x + 0.2, 3.0, x + 2.7, 3.0, rng.uniform(0.15, 0.35), 10), 0.025, y=-0.15)
            x += 3.3
        elif item == 'desk':
            w = rng.uniform(2.4, 3.0)
            dk = toon('Desk', (0.32, 0.24, 0.24))
            b.box(dk, x, x + w, 0.95, 1.05, -0.4, 0.3)
            for lx in (x + 0.08, x + w - 0.16):
                b.box(toon('DeskLeg', (0.12, 0.1, 0.12)), lx, lx + 0.08, 0, 0.95, -0.3, -0.2)
            for k in range(2):  # dual monitors with UI
                mx = x + 0.7 + k * 0.95
                b.box(toon('MonFrame', (0.07, 0.07, 0.09)), mx - 0.42, mx + 0.42, 1.3, 1.85, -0.2, -0.1, bevel=0.02)
                b.box(toon('MonFrame', (0.07, 0.07, 0.09)), mx - 0.04, mx + 0.04, 1.05, 1.3, -0.15, -0.1)
                col = rng.choice([CYAN, PINK, RED])
                b.quad(emit(f'SScr{col}', tuple(c * 0.22 for c in col)), mx - 0.37, mx + 0.37, 1.35, 1.8, y=-0.21, c='NoLine')
                for j in range(4):
                    b.box(emit(f'SLine{col}', tuple(c * 0.75 for c in col)), mx - 0.32, mx - 0.32 + rng.uniform(0.2, 0.6), 1.72 - j * 0.09, 1.75 - j * 0.09, -0.225, -0.22, c='NoLine')
            # office chair
            cx = x + w - 0.4
            b.box(toon('Chair', (0.15, 0.13, 0.18)), cx - 0.25, cx + 0.25, 0.55, 0.65, -0.6, -0.3, bevel=0.03)
            b.box(toon('Chair', (0.15, 0.13, 0.18)), cx + 0.15, cx + 0.27, 0.65, 1.4, -0.6, -0.3, bevel=0.03)
            b.box(toon('ChairLeg', (0.1, 0.1, 0.12)), cx - 0.03, cx + 0.03, 0.1, 0.55, -0.5, -0.4)
            b.box(toon('ChairLeg', (0.1, 0.1, 0.12)), cx - 0.28, cx + 0.28, 0.05, 0.1, -0.5, -0.4)
            # desk lamp + mug
            b.bar(toon('DLamp', (0.5, 0.15, 0.2)), x + 0.15, 1.05, x + 0.3, 1.5, 0.04, -0.3, -0.25)
            b.prism(toon('DLamp', (0.5, 0.15, 0.2)), [(x + 0.2, 1.48), (x + 0.45, 1.5), (x + 0.4, 1.38)], -0.32, -0.25)
            cone_light(b, x + 0.42, 1.4, 1.05, 0.1, 0.5, WARM, alpha=0.08, depth=-0.4)
            b.box(toon('Mug', (0.6, 0.6, 0.66)), x + w - 1.2, x + w - 1.08, 1.05, 1.2, -0.35, -0.25)
            hanging_lamp(b, x + w / 2, 4.2, (1.0, 0.92, 0.85), cone=True, cord=4.4, depth=-0.7)
            x += w + 0.7
        elif item == 'plant':
            pot = toon('Pot2', (0.55, 0.3, 0.22))
            b.prism(pot, [(x, 0), (x + 0.6, 0), (x + 0.7, 0.6), (x - 0.1, 0.6)], -0.3, 0.1)
            leaves = [toon('LeafA', (0.12, 0.45, 0.28)), toon('LeafB', (0.22, 0.6, 0.25))]
            for k in range(14):
                a = rng.uniform(-1.2, 1.2)
                L = rng.uniform(0.6, 1.5)
                ex, ez = x + 0.3 + math.sin(a) * L, 0.6 + math.cos(a) * L
                mx, mz = x + 0.3 + math.sin(a) * L * 0.55, 0.6 + math.cos(a) * L * 0.55
                nx, nz = math.cos(a) * 0.11, -math.sin(a) * 0.11
                b.prism(rng.choice(leaves), [(x + 0.3, 0.6), (mx + nx, mz + nz), (ex, ez), (mx - nx, mz - nz)], -0.35 + k * 0.01, -0.3 + k * 0.01)
            x += 1.3
        elif item == 'board':
            b.box(toon('Board', (0.5, 0.5, 0.58)), x, x + 2.4, 1.8, 3.3, 0.2, 0.3)
            b.box(toon('BoardFrame', (0.3, 0.3, 0.35)), x - 0.05, x + 2.45, 1.75, 1.82, 0.15, 0.32)
            ink = [flat('InkB', (0.15, 0.3, 0.7)), flat('InkR', (0.7, 0.15, 0.2))]
            pts = [(x + 0.2 + i * 0.1, 2.3 + 0.3 * math.sin(i * 0.6)) for i in range(9)]
            b.tube(ink[0], pts, 0.012, y=0.17, c='NoLine')
            for j in range(4):
                b.box(ink[1], x + 1.3, x + 1.3 + rng.uniform(0.4, 0.9), 2.95 - j * 0.22, 2.97 - j * 0.22, 0.17, 0.18, c='NoLine')
            b.disc(ink[1], x + 0.6, 2.95, 0.15, y=0.17, seg=16, c='NoLine')
            x += 2.9
        elif item == 'cooler':
            b.box(toon('Cooler', (0.45, 0.47, 0.56)), x, x + 0.6, 0, 1.2, -0.2, 0.2, bevel=0.03)
            b.quad(flat('Water', (0.25, 0.45, 0.7), 0.7), x + 0.1, x + 0.5, 1.2, 1.8, y=-0.15, c='NoLine')
            b.box(toon('CoolerTop', (0.6, 0.62, 0.7)), x + 0.08, x + 0.52, 1.8, 1.86, -0.17, 0.0)
            x += 1.1
        elif item == 'glass':  # glass partition with a frosted band and a logo
            b.box(toon('PartFrame', (0.2, 0.2, 0.25)), x, x + 3.2, 0, 0.12, -0.05, 0.05)
            b.box(toon('PartFrame', (0.2, 0.2, 0.25)), x, x + 3.2, 3.6, 3.7, -0.05, 0.05)
            b.quad(flat('PGlass', (0.6, 0.75, 0.9), 0.1), x, x + 3.2, 0.12, 3.6, y=-0.06, c='NoLine')
            b.quad(flat('Frost', (0.85, 0.9, 1.0), 0.18), x, x + 3.2, 1.4, 1.9, y=-0.07, c='NoLine')
            logo(b, x + 1.6, 2.7, 0.45, PINK, depth=-0.08)
            x += 3.6
        elif item == 'poster':
            b.box(toon('PosterBg', (0.1, 0.08, 0.14)), x, x + 1.1, 2.0, 3.5, 0.2, 0.28)
            text('ТРЕВОГА', emit('PosterT', RED), x + 0.55, 3.15, 0.17, y=0.18, c='NoLine')
            b.prism(emit('PosterIcon', AMBER), [(x + 0.55, 2.95), (x + 0.85, 2.35), (x + 0.25, 2.35)], 0.17, 0.18, c='NoLine')
            x += 1.6
    b.build(wrap_width=W)
    wrap_objects([o for c in ('Main', 'NoLine') for o in coll(c).objects if o.name.startswith('Sign_')], W)
    print('s_mid width used', x)
    render('Lab2_Mid.png', glow_kw=dict(threshold=0.55, radii=(4, 14), strength=(.55, .35)))


def s_front():
    reset()
    W, H = 40, 5
    setup_render(W, H, 80, 0, 0, line=0)
    rng = random.Random(81)
    b = Batch()
    sil = flat('SFG', (0.03, 0.015, 0.025))
    b.box(sil, -1, W + 1, H - 0.5, H + 0.2, -0.1, 0.1)                 # cable tray
    for k in range(int(W / 1.2) + 1):
        b.box(sil, k * 1.2, k * 1.2 + 0.06, H - 0.9, H - 0.5, -0.1, 0.1)
    for x in (6.0, 26.0):                                             # fluorescent fixtures
        b.box(sil, x, x + 2.2, H - 1.35, H - 1.15, -0.1, 0.1)
        b.box(sil, x + 0.2, x + 0.25, H - 1.15, H - 0.5, -0.1, 0.1)
        b.box(sil, x + 1.95, x + 2.0, H - 1.15, H - 0.5, -0.1, 0.1)
        b.box(emit('Fluo', (0.95, 0.85, 0.9)), x + 0.1, x + 2.1, H - 1.4, H - 1.35, -0.2, -0.1)
    for x in (15.0, 35.0):                                            # rotating alarm
        b.box(sil, x - 0.05, x + 0.05, H - 1.0, H - 0.5, -0.1, 0.1)
        b.sphere(emit('FAlarm', RED), x, H - 1.15, 0.16, y=-0.2, sub=2)
    x = 0
    while x < W:
        x1 = x + rng.uniform(3, 7)
        b.tube(sil, catenary(x, H - 0.55, x1, H - 0.55, rng.uniform(0.4, 1.2), 16), 0.035, seg=6)
        x = x1
    b.build(wrap_width=W)
    render('Lab2_Front.png', glow_kw=dict(threshold=0.5, radii=(4, 12), strength=(.6, .35)))


JOBS = dict(u_back=u_back, u_mid=u_mid, u_front=u_front, s_back=s_back, s_mid=s_mid, s_front=s_front)
for job in globals().get('ONLY', list(JOBS)):
    JOBS[job]()
