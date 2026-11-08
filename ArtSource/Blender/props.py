# Gameplay props in the Neon Noir style. Framing (size + pivot) matches what NeonLevelBuilder expects.
# Run in Blender:  ONLY = ['crate', ...]; exec(open(r"...\props.py", encoding="utf-8").read())
import random, math
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\neon_kit.py", encoding="utf-8").read(), globals())

PPU = 200
CYAN = (0.25, 0.92, 1.0)
PINK = (1.0, 0.28, 0.72)
AMBER = (1.0, 0.72, 0.25)
RED = (1.0, 0.2, 0.18)
GREEN = (0.45, 1.0, 0.45)
VIOLET = (0.72, 0.45, 1.0)

STEEL = (0.30, 0.32, 0.42)
STEEL_D = (0.17, 0.18, 0.26)
STEEL_DD = (0.09, 0.09, 0.14)
HAZARD = (0.98, 0.72, 0.12)


def mats():
    return dict(steel=toon('Steel', STEEL), steel_d=toon('SteelD', STEEL_D), steel_dd=toon('SteelDD', STEEL_DD),
                hazard=toon('Hazard', HAZARD), ink=flat('InkFlat', (0.03, 0.03, 0.05)),
                rubber=toon('Rubber', (0.12, 0.12, 0.16)))


def hazard_band(b, m, x0, x1, z0, z1, y0, y1, step=0.16, slant=0.12):
    """Yellow band with diagonal black chevrons."""
    b.box(m['hazard'], x0, x1, z0, z1, y0 + 0.002, y1)
    x = x0 - (z1 - z0)
    while x < x1:
        pts = [(max(x0, x), z0), (min(x1, x + step / 2), z0)]
        a, c = x + step / 2 + slant, x + slant
        poly = [(x, z0), (x + step / 2, z0), (x + step / 2 + slant, z1), (x + slant, z1)]
        clipped = [(min(max(px, x0), x1), pz) for px, pz in poly]
        if clipped[1][0] - clipped[0][0] > 1e-3 or clipped[2][0] - clipped[3][0] > 1e-3:
            b.prism(m['ink'], clipped, y0 - 0.001, y0 + 0.001, c='NoLine')
        x += step


def bolt(b, m, x, z, y, r=0.022):
    b.ngon(m['steel'], x, z, r, 6, y - 0.012, y, rot=0.3)


# ============================================================ CRATE 0.8 x 0.8 (pivot bottom, 0.05 margin)
def crate(variant=0):
    reset()
    setup_render(0.9, 0.9, PPU, -0.45, -0.05, line=2.2)
    b = Batch()
    m = mats()
    y0 = -0.4
    if variant == 0:
        b.box(m['steel_d'], -0.4, 0.4, 0, 0.8, y0, 0.4, bevel=0.025)
        # outer frame
        for (a, c, d, e) in [(-0.4, 0.4, 0.0, 0.09), (-0.4, 0.4, 0.71, 0.8), (-0.4, -0.31, 0.09, 0.71), (0.31, 0.4, 0.09, 0.71)]:
            b.box(m['steel'], a, c, d, e, y0 - 0.03, y0 + 0.1, bevel=0.012)
        # X brace
        b.bar(m['steel'], -0.29, 0.11, 0.29, 0.69, 0.07, y0 - 0.02, y0 + 0.05)
        b.bar(m['steel'], -0.29, 0.69, 0.29, 0.11, 0.07, y0 - 0.025, y0 + 0.05)
        # amber corner caps
        for sx in (-1, 1):
            for sz in (0, 1):
                cx, cz = sx * 0.4, sz * 0.8
                pts = [(cx, cz), (cx - sx * 0.15, cz), (cx - sx * 0.15, cz + (0.05 if sz == 0 else -0.05)),
                       (cx - sx * 0.05, cz + (0.05 if sz == 0 else -0.05)), (cx - sx * 0.05, cz + (0.15 if sz == 0 else -0.15)),
                       (cx, cz + (0.15 if sz == 0 else -0.15))]
                b.prism(m['hazard'], pts, y0 - 0.05, y0 + 0.05)
                bolt(b, m, cx - sx * 0.035, cz + (0.035 if sz == 0 else -0.035), y0 - 0.05, 0.015)
        # glowing label strip + stencil
        b.box(emit('LabelGlow', CYAN), -0.16, 0.16, 0.355, 0.445, y0 - 0.045, y0 - 0.03, c='NoLine')
        b.box(m['steel_dd'], -0.18, 0.18, 0.34, 0.46, y0 - 0.035, y0 - 0.02)
        text('BIO-7', flat('LabelInk', (0.02, 0.1, 0.12)), 0, 0.37, 0.065, y=y0 - 0.05, c='NoLine')
    else:
        b.box(m['steel_d'], -0.4, 0.4, 0, 0.8, y0, 0.4, bevel=0.03)
        for k in range(4):  # horizontal slats
            z = 0.06 + k * 0.18
            b.box(m['steel'], -0.37, 0.37, z, z + 0.14, y0 - 0.03, y0 + 0.1, bevel=0.015)
        hazard_band(b, m, -0.4, 0.4, 0.33, 0.47, y0 - 0.05, y0 + 0.02)
        for sx in (-1, 1):
            b.box(m['steel_dd'], sx * 0.4 - 0.05, sx * 0.4 + 0.05, 0, 0.8, y0 - 0.06, y0 + 0.1)
            for z in (0.08, 0.72):
                bolt(b, m, sx * 0.4, z, y0 - 0.06, 0.018)
        b.box(emit('Pip', PINK), 0.22, 0.3, 0.62, 0.66, y0 - 0.06, y0 - 0.04, c='NoLine')
    b.build()
    render(f'Crate{"" if variant == 0 else "2"}.png', glow_kw=dict(threshold=0.7, radii=(3, 8), strength=(.4, .2), wrap_x=False))


# ============================================================ FLOORS: 4 x 2 tile, pivot top (top surface at z=0)
def floor(kind):
    reset()
    W, H = 4.0, 2.0
    setup_render(W, H, 160, 0, -H, line=2.0)
    b = Batch()
    m = mats()
    rng = random.Random(7)
    if kind == 'lab':
        top = toon('LabTop', (0.36, 0.40, 0.48))
        grate = toon('Grate', (0.14, 0.16, 0.21))
        b.box(top, -1, W + 1, -0.1, 0, -0.5, 0.5)                     # walking plate
        b.box(emit('LabEdge', CYAN), -1, W + 1, -0.13, -0.1, -0.52, -0.5, c='NoLine')
        b.box(grate, -1, W + 1, -0.62, -0.13, -0.45, 0.5)
        for i in range(int(W / 0.2) + 2):                              # grate slots
            x = i * 0.2
            b.box(m['steel_dd'], x + 0.04, x + 0.15, -0.55, -0.2, -0.47, -0.44)
        hazard_band(b, m, -1, W + 1, -0.8, -0.65, -0.5, -0.3)
        b.box(m['steel_d'], -1, W + 1, -1.05, -0.8, -0.4, 0.5)          # I-beam
        b.box(m['steel'], -1, W + 1, -0.86, -0.8, -0.46, -0.38)
        b.box(m['steel'], -1, W + 1, -1.05, -0.99, -0.46, -0.38)
        for x in (0.5, 2.5):                                           # beam brackets + rivets
            b.box(m['steel'], x - 0.12, x + 0.12, -1.05, -0.65, -0.5, -0.4, bevel=0.01)
            for z in (-0.72, -0.98):
                bolt(b, m, x - 0.06, z, -0.5)
                bolt(b, m, x + 0.06, z, -0.5)
        pipe = toon('Pipe', (0.20, 0.45, 0.42))
        pipe2 = toon('Pipe2', (0.42, 0.22, 0.18))
        b.cyl(pipe, W / 2, -1.3, 0.11, W + 2, axis='X', y=-0.2, seg=20)
        b.cyl(pipe2, W / 2, -1.6, 0.07, W + 2, axis='X', y=-0.1, seg=16)
        for x in (1.0, 3.0):
            b.box(m['steel_d'], x - 0.06, x + 0.06, -1.75, -1.15, -0.45, -0.3)
            b.box(emit('PipeLed', (0.4, 1, 0.5)), x + 0.15, x + 0.21, -1.36, -1.24, -0.35, -0.33, c='NoLine')
        b.quad(gradient_mat('FloorFade', (0.035, 0.04, 0.06), (0.035, 0.04, 0.06), -H, -1.0, 1.0, 0.0), -1, W + 1, -H, -1.0, y=-0.9, c='NoLine')
        name = 'Floor_Lab.png'
    elif kind == 'office':
        tile = toon('Tile', (0.42, 0.40, 0.48))
        b.box(tile, -1, W + 1, -0.12, 0, -0.5, 0.5)
        for i in range(int(W / 0.5) + 2):
            x = i * 0.5
            b.box(m['steel_dd'], x - 0.01, x + 0.01, -0.12, 0, -0.52, -0.5, c='NoLine')
        b.box(emit('OfficeEdge', RED), -1, W + 1, -0.16, -0.12, -0.52, -0.5, c='NoLine')
        slab = toon('Slab', (0.24, 0.22, 0.28))
        b.box(slab, -1, W + 1, -0.75, -0.16, -0.45, 0.5)
        for x in (0.0, 2.0):                                           # access hatches
            b.box(toon('Hatch', (0.3, 0.28, 0.34)), x + 0.3, x + 1.0, -0.62, -0.26, -0.48, -0.44, bevel=0.01)
            b.box(emit('HatchLed', AMBER), x + 0.85, x + 0.93, -0.36, -0.32, -0.5, -0.48, c='NoLine')
            text('Э-12' if x == 0 else 'Э-13', flat('Stencil', (0.12, 0.1, 0.14)), x + 0.55, -0.5, 0.11, y=-0.5, c='NoLine')
        tray = toon('Tray', (0.2, 0.2, 0.27))
        b.box(tray, -1, W + 1, -1.0, -0.88, -0.4, 0.2)
        for col, z in ((PINK, -0.93), (CYAN, -0.96), (AMBER, -0.99)):
            b.cyl(toon(f'Cable{col}', tuple(c * 0.45 for c in col)), W / 2, z + 0.1, 0.03, W + 2, axis='X', y=-0.42, seg=8)
        for x in (0.7, 2.7):
            b.box(m['steel_d'], x - 0.04, x + 0.04, -1.4, -0.75, -0.42, -0.35)
        b.quad(gradient_mat('FloorFade', (0.05, 0.045, 0.06), (0.05, 0.045, 0.06), -H, -1.1, 1.0, 0.0), -1, W + 1, -H, -1.1, y=-0.9, c='NoLine')
        name = 'Floor_Office.png'
    else:  # roof: parapet coping + facade top
        brick = toon('RoofBrick', (0.21, 0.09, 0.14))
        mortar = flat('Mortar', (0.11, 0.045, 0.08))
        cope = toon('Coping', (0.46, 0.42, 0.52))
        b.box(cope, -1, W + 1, -0.16, 0, -0.55, 0.5)
        for i in range(int(W / 0.8) + 2):
            x = i * 0.8
            b.box(m['steel_dd'], x - 0.008, x + 0.008, -0.16, 0, -0.57, -0.55, c='NoLine')
        b.box(toon('CopingLip', (0.3, 0.27, 0.36)), -1, W + 1, -0.24, -0.16, -0.6, 0.5)
        b.box(brick, -1, W + 1, -H - 0.2, -0.24, -0.4, 0.5)
        for row in range(14):                                          # brick courses
            z = -0.24 - (row + 1) * 0.13
            b.box(mortar, -1, W + 1, z - 0.012, z + 0.012, -0.42, -0.41, c='NoLine')
            off = 0.0 if row % 2 == 0 else 0.15
            for k in range(int(W / 0.3) + 2):
                x = k * 0.3 + off
                b.box(mortar, x - 0.01, x + 0.01, z, z + 0.13, -0.42, -0.41, c='NoLine')
        # window heads of the top floor (lit) + a sign bracket
        for x in (0.9, 3.1):
            b.box(m['steel_dd'], x - 0.42, x + 0.42, -2.3, -0.95, -0.44, -0.4)
            b.quad(emit('RoofWin', (0.36, 0.21, 0.15)), x - 0.36, x + 0.36, -2.3, -1.01, y=-0.45, c='NoLine')
            b.box(toon('Lintel', (0.36, 0.3, 0.38)), x - 0.5, x + 0.5, -0.95, -0.82, -0.5, -0.4)
            b.box(toon('Blinds', (0.36, 0.2, 0.13)), x - 0.36, x + 0.36, -1.2, -1.01, -0.47, -0.46, c='NoLine')
        b.cyl(toon('Drain', (0.25, 0.25, 0.32)), 2.0, -1.2, 0.05, 2.2, y=-0.5, seg=12)
        for z in (-0.6, -1.5):
            b.box(m['steel_d'], 1.93, 2.07, z, z + 0.05, -0.6, -0.42)
        name = 'Floor_Roof.png'
    b.build(wrap_width=W)
    wrap_objects([o for c in ('Main', 'NoLine') for o in coll(c).objects if o.name.startswith('Sign_')], W)
    render(name, glow_kw=dict(threshold=0.7, radii=(3, 10), strength=(.45, .25), wrap_x=True))


# ============================================================ WALL under the roof floor (tiles in X and Z): 4 x 4
def roof_wall():
    reset()
    W, H = 4.0, 4.0
    setup_render(W, H, 80, 0, 0, line=1.4)
    b = Batch()
    m = mats()
    rng = random.Random(9)
    brick = toon('RoofBrickW', (0.15, 0.065, 0.1))
    b.box(brick, -1, W + 1, -1, H + 1, 0, 0.5)
    for x in (0.9, 3.1):
        for z in (0.35, 2.35):
            lit = rng.random() < 0.4
            b.box(m['steel_dd'], x - 0.42, x + 0.42, z, z + 1.3, -0.04, 0)
            col = rng.choice([(0.45, 0.28, 0.17), (0.38, 0.2, 0.3), (0.18, 0.3, 0.36)])
            if lit:
                b.quad(emit(f'WW{col}', col), x - 0.36, x + 0.36, z + 0.06, z + 1.24, y=-0.05, c='NoLine')
            b.box(toon('LintelW', (0.27, 0.2, 0.28)), x - 0.5, x + 0.5, z + 1.3, z + 1.42, -0.1, 0)
            b.box(toon('SillW', (0.27, 0.2, 0.28)), x - 0.48, x + 0.48, z - 0.06, z, -0.12, 0)
    b.build(wrap_width=W)
    # vertical wrap too
    for ob in list(coll('Main').objects) + list(coll('NoLine').objects):
        for off in (-H, H):
            d = ob.copy()
            d.location.z += off
            for cl in ob.users_collection:
                cl.objects.link(d)
    render('Wall_Roof.png', glow_kw=None)


# ============================================================ GATE 0.9 x 4 (pivot .1 from bottom)
def gate():
    reset()
    setup_render(0.9, 4.0, PPU, -0.45, -0.1, line=2.2)
    b = Batch()
    m = mats()
    y0 = -0.3
    b.box(m['steel_d'], -0.3, 0.3, 0, 3.8, y0, 0.3, bevel=0.02)
    seg = 3.8 / 7
    for k in range(7):  # segmented plates
        z = k * seg
        b.box(m['steel'], -0.27, 0.27, z + 0.03, z + seg - 0.03, y0 - 0.03, y0 + 0.1, bevel=0.012)
        bolt(b, m, -0.21, z + 0.08, y0 - 0.03, 0.016)
        bolt(b, m, 0.21, z + 0.08, y0 - 0.03, 0.016)
    hazard_band(b, m, -0.3, 0.3, 0.12, 0.42, y0 - 0.05, y0 + 0.02, step=0.14, slant=0.1)
    hazard_band(b, m, -0.3, 0.3, 3.38, 3.68, y0 - 0.05, y0 + 0.02, step=0.14, slant=0.1)
    # central status light (tinted red in-game by the lock state)
    b.box(m['steel_dd'], -0.06, 0.06, 0.8, 3.0, y0 - 0.06, y0)
    b.box(emit('GateLed', RED), -0.03, 0.03, 0.85, 2.95, y0 - 0.07, y0 - 0.06, c='NoLine')
    # side rails
    for sx in (-1, 1):
        b.box(m['steel_dd'], sx * 0.3 - 0.05 * (1 if sx > 0 else -1) * 0, sx * 0.3 + sx * 0.12, -0.05, 3.85, y0 + 0.05, 0.4)
    b.build()
    render('Gate.png', glow_kw=dict(threshold=0.7, radii=(3, 10), strength=(.5, .3), wrap_x=False))


# ============================================================ TERMINAL 1.4 x 1.9 (pivot .1 from bottom); screen centre (0, 1.3)
def terminal():
    reset()
    setup_render(1.4, 1.9, PPU, -0.7, -0.1, line=2.2)
    b = Batch()
    m = mats()
    y0 = -0.35
    body = toon('TermBody', (0.22, 0.22, 0.32))
    b.box(m['steel_dd'], -0.5, 0.5, 0, 0.14, y0, 0.3, bevel=0.02)                 # base
    b.prism(body, [(-0.32, 0.14), (0.32, 0.14), (0.26, 0.92), (-0.26, 0.92)], y0 + 0.05, 0.25)  # column
    b.box(m['steel'], -0.22, 0.22, 0.3, 0.36, y0 + 0.03, y0 + 0.08)
    b.box(m['steel'], -0.22, 0.22, 0.45, 0.51, y0 + 0.03, y0 + 0.08)
    # head with angled screen bezel
    b.prism(body, [(-0.6, 0.92), (0.6, 0.92), (0.66, 1.62), (-0.54, 1.72)], y0, 0.3)
    b.prism(m['steel_dd'], [(-0.48, 1.04), (0.5, 1.04), (0.53, 1.52), (-0.45, 1.6)], y0 - 0.02, y0 + 0.02)
    b.prism(flat('Screen', (0.04, 0.05, 0.08)), [(-0.44, 1.07), (0.46, 1.07), (0.49, 1.49), (-0.41, 1.56)], y0 - 0.03, y0 - 0.02, c='NoLine')
    # the two channel pads: С (cyan) and Р (magenta)
    for sx, col, lab in ((-0.3, CYAN, 'С'), (0.3, PINK, 'Р')):
        b.box(m['steel_dd'], sx - 0.2, sx + 0.2, 0.6, 0.86, y0 - 0.02, y0 + 0.05, bevel=0.015)
        b.box(emit(f'Pad{col}', tuple(c * 0.8 for c in col)), sx - 0.16, sx + 0.16, 0.63, 0.83, y0 - 0.03, y0 - 0.02, c='NoLine')
        text(lab, flat('PadInk', (0.03, 0.03, 0.06)), sx, 0.66, 0.15, y=y0 - 0.04, c='NoLine')
    # antenna + status lamp, cable to the floor
    b.box(m['steel'], 0.48, 0.52, 1.66, 1.82, 0, 0.1)
    b.sphere(emit('TermLamp', VIOLET), 0.5, 1.83, 0.035, y=-0.05, c='NoLine')
    b.tube(m['rubber'], [(0.32, 0.4), (0.45, 0.25), (0.52, 0.06), (0.62, 0.02)], 0.025, y=y0 + 0.3)
    b.build()
    render('Terminal.png', glow_kw=dict(threshold=0.7, radii=(3, 10), strength=(.5, .3), wrap_x=False))


# ============================================================ GRAPPLE ANCHOR 1.0 x 1.0 (pivot centre = grab point)
def anchor():
    reset()
    setup_render(1.0, 1.0, PPU, -0.5, -0.5, line=2.2)
    b = Batch()
    m = mats()
    y0 = -0.2
    b.box(m['steel_dd'], -0.05, 0.05, 0.2, 0.5, y0 + 0.1, 0.2)                     # stem to the cable
    b.ngon(m['steel_d'], 0, 0, 0.36, 6, y0, 0.2, rot=math.pi / 6)                  # hex housing
    b.ngon(m['steel'], 0, 0, 0.3, 6, y0 - 0.03, y0 + 0.05, rot=math.pi / 6)
    b.ngon(m['steel_dd'], 0, 0, 0.22, 24, y0 - 0.05, y0 + 0.02)                     # socket (the Core ring sits here)
    for k in range(6):
        a = math.pi / 6 + k * math.pi / 3
        bolt(b, m, 0.33 * math.cos(a), 0.33 * math.sin(a), y0 - 0.03, 0.02)
    for sx in (-1, 1):                                                              # side clamp fins
        b.prism(m['steel_d'], [(sx * 0.3, 0.1), (sx * 0.44, 0.16), (sx * 0.44, -0.16), (sx * 0.3, -0.1)], y0 + 0.02, 0.1)
    b.build()
    render('Anchor.png', glow_kw=None)


# ============================================================ LASER EMITTER 0.8 x 0.6 (pivot centre = beam origin)
def emitter():
    reset()
    setup_render(0.8, 0.6, PPU, -0.4, -0.3, line=2.0)
    b = Batch()
    m = mats()
    y0 = -0.2
    b.box(m['steel_d'], -0.3, 0.3, -0.16, 0.2, y0, 0.2, bevel=0.03)
    b.box(m['steel'], -0.26, 0.26, 0.08, 0.16, y0 - 0.02, y0 + 0.05)
    hazard_band(b, m, -0.3, 0.3, -0.16, -0.06, y0 - 0.03, y0 + 0.02, step=0.1, slant=0.06)
    b.ngon(m['steel_dd'], 0, 0.0, 0.11, 20, y0 - 0.05, y0)
    b.ngon(emit('Lens', RED), 0, 0.0, 0.07, 20, y0 - 0.06, y0 - 0.05, c='NoLine')
    b.box(m['steel_dd'], -0.08, 0.08, 0.2, 0.3, y0 + 0.05, 0.1)
    b.build()
    render('Emitter.png', glow_kw=dict(threshold=0.7, radii=(3, 8), strength=(.6, .3), wrap_x=False))


def drone():
    reset()
    setup_render(1.2, 0.8, PPU, -0.6, -0.4, line=2.0)
    b = Batch()
    m = mats()
    y0 = -0.2
    b.sphere(m['steel_d'], 0, 0.02, 0.2, y=0, sx=1.3, sz=0.8, sub=3)
    b.box(m['steel'], -0.5, 0.5, 0.14, 0.18, -0.05, 0.05)
    for sx in (-1, 1):
        b.box(m['steel_dd'], sx * 0.45 - 0.03, sx * 0.45 + 0.03, 0.18, 0.26, -0.05, 0.05)
        b.box(flat('Rotor', (0.6, 0.65, 0.75), 0.55), sx * 0.45 - 0.16, sx * 0.45 + 0.16, 0.26, 0.29, -0.1, -0.08, c='NoLine')
    b.ngon(m['steel_dd'], 0, -0.02, 0.09, 20, -0.3, -0.2)
    b.ngon(emit('DLens', RED), 0, -0.02, 0.055, 20, -0.31, -0.3, c='NoLine')
    b.box(emit('DLed', CYAN), -0.2, -0.12, 0.08, 0.1, -0.27, -0.25, c='NoLine')
    b.build()
    render('Drone.png', glow_kw=dict(threshold=0.7, radii=(3, 8), strength=(.6, .3), wrap_x=False))


# ============================================================ CHECKPOINT BEACON 0.8 x 2.6 (pivot bottom .05); lamp at z 2.25
def beacon():
    reset()
    setup_render(0.8, 2.6, PPU, -0.4, -0.05, line=2.2)
    b = Batch()
    m = mats()
    b.box(m['steel_dd'], -0.32, 0.32, 0, 0.12, -0.3, 0.3, bevel=0.02)
    b.prism(m['steel_d'], [(-0.18, 0.12), (0.18, 0.12), (0.1, 0.4), (-0.1, 0.4)], -0.2, 0.2)
    b.box(m['steel'], -0.04, 0.04, 0.4, 2.0, -0.1, 0.1)
    hazard_band(b, m, -0.07, 0.07, 0.5, 0.9, -0.12, 0.0, step=0.08, slant=0.05)
    b.box(m['steel_d'], -0.18, 0.18, 2.0, 2.08, -0.15, 0.15)
    for sx in (-1, 1):
        b.box(m['steel_d'], sx * 0.16 - 0.025, sx * 0.16 + 0.025, 2.08, 2.42, -0.15, 0.15)
    b.box(m['steel_d'], -0.18, 0.18, 2.42, 2.5, -0.15, 0.15)
    b.box(flat('BeaconGlass', (0.1, 0.12, 0.16)), -0.13, 0.13, 2.08, 2.42, 0.0, 0.05, c='NoLine')
    b.build()
    render('Beacon.png', glow_kw=None)


# ============================================================ EXIT DOOR 3.2 x 4.6 (pivot bottom .1); opening is transparent
def exit_door():
    reset()
    setup_render(3.2, 4.6, PPU * 0.75, -1.6, -0.1, line=2.4)
    b = Batch()
    m = mats()
    y0 = -0.4
    frame = toon('ExitFrame', (0.2, 0.21, 0.3))
    for sx in (-1, 1):
        b.box(frame, sx * 0.9, sx * 1.35, 0, 3.7, y0, 0.3, bevel=0.03)
        hazard_band(b, m, min(sx * 0.9, sx * 1.35), max(sx * 0.9, sx * 1.35), 0.1, 0.5, y0 - 0.03, y0 + 0.02)
        for z in (1.0, 1.8, 2.6):
            bolt(b, m, sx * 1.12, z, y0, 0.03)
        b.box(emit('DoorStrip', GREEN), sx * 0.9 - 0.02 * sx, sx * 0.94, 0.0, 3.4, y0 - 0.05, y0 - 0.03, c='NoLine')
    b.box(frame, -1.35, 1.35, 3.4, 3.85, y0, 0.3, bevel=0.03)
    b.box(m['steel_dd'], -0.95, 0.95, 3.88, 4.42, y0 + 0.05, 0.2)
    b.box(emit('ExitSignBack', (0.06, 0.25, 0.08)), -0.88, 0.88, 3.93, 4.37, y0 + 0.02, y0 + 0.04, c='NoLine')
    text('ВЫХОД', emit('ExitTxt', GREEN), 0, 4.0, 0.3, y=y0 - 0.01, c='NoLine')
    # shutter rolled up inside the lintel
    for k in range(3):
        b.box(m['steel'], -0.9, 0.9, 3.12 + k * 0.09, 3.19 + k * 0.09, y0 + 0.2, y0 + 0.3)
    b.build()
    render('ExitDoor.png', glow_kw=dict(threshold=0.65, radii=(4, 14), strength=(.6, .35), wrap_x=False))


ALL = ['crate', 'crate2', 'floor_lab', 'floor_office', 'floor_roof', 'roof_wall', 'gate', 'terminal', 'anchor', 'emitter',
       'drone', 'beacon', 'exit_door']
JOBS = dict(crate=lambda: crate(0), crate2=lambda: crate(1), floor_lab=lambda: floor('lab'), floor_office=lambda: floor('office'),
            floor_roof=lambda: floor('roof'), roof_wall=roof_wall, gate=gate, terminal=terminal, anchor=anchor, emitter=emitter,
            drone=drone, beacon=beacon, exit_door=exit_door)
for job in globals().get('ONLY', ALL):
    JOBS[job]()


# ============================================================ PITS: 4 x 4.3 tile, pivot top (y=0 = floor level), liquid surface at -1.3
def pit(kind):
    reset()
    W, H = 4.0, 4.3
    setup_render(W, H, 120, 0, -H, line=1.6)
    b = Batch()
    m = mats()
    rng = random.Random(13 if kind == 'toxic' else 17)
    wall = toon('PitWall', (0.06, 0.08, 0.09) if kind == 'toxic' else (0.07, 0.06, 0.09))
    b.box(wall, -1, W + 1, -H, 0, 0.5, 1)
    for i in range(3):                                         # wall panels with rivets
        x = i * 1.33
        b.box(toon('PitPanel', (0.09, 0.11, 0.12) if kind == 'toxic' else (0.1, 0.09, 0.12)), x + 0.06, x + 1.27, -1.2, -0.12, 0.45, 0.5, bevel=0.02)
        for z in (-0.22, -1.1):
            bolt(b, m, x + 0.14, z, 0.45, 0.018)
            bolt(b, m, x + 1.19, z, 0.45, 0.018)
    if kind == 'toxic':
        liquid, surf, deep = (0.5, 1.0, 0.3), (0.75, 1.0, 0.45), (0.04, 0.16, 0.06)
        b.ngon(toon('PitPipe', (0.2, 0.3, 0.25)), 1.0, -0.4, 0.1, 20, 0.3, 0.45)
        b.ngon(flat('PitPipeIn', (0.03, 0.05, 0.04)), 1.0, -0.4, 0.065, 20, 0.29, 0.3, c='NoLine')
        b.prism(emit('Drip', liquid), [(0.97, -0.46), (1.03, -0.46), (1.02, -1.25), (0.98, -1.25)], 0.3, 0.32, c='NoLine')
        for _ in range(9):                                     # slime streaks on the wall
            x = rng.uniform(0, W)
            L = rng.uniform(0.15, 0.6)
            b.quad(flat('Slime', (0.25, 0.55, 0.2), 0.6), x, x + 0.05, -1.3, -1.3 + L, y=0.44, c='NoLine')
    else:
        liquid, surf, deep = (0.3, 0.6, 1.0), (0.7, 0.9, 1.0), (0.02, 0.05, 0.16)
        for x in (0.7, 2.7):                                   # sparking cable ends
            b.tube(m['rubber'], [(x, 0), (x + 0.1, -0.4), (x + 0.3, -0.75)], 0.035, y=0.35)
            b.sphere(emit('Spark', (0.85, 0.95, 1.0)), x + 0.3, -0.78, 0.05, y=0.3, sub=1, c='NoLine')
    # liquid body: bright under the surface, fading into darkness
    b.quad(gradient_mat(f'Liq{kind}', deep, tuple(c * 0.55 for c in liquid), -H, -1.3), -1, W + 1, -H, -1.3, y=0.0, c='NoLine')
    # surface band with foam blobs
    b.quad(emit(f'Surf{kind}', surf), -1, W + 1, -1.36, -1.28, y=-0.02, c='NoLine')
    for _ in range(14):
        x = rng.uniform(0, W)
        r = rng.uniform(0.04, 0.12)
        b.disc(emit(f'Foam{kind}', surf), x, -1.3 + r * 0.3, r, y=-0.03, seg=16, c='NoLine')
    for _ in range(22):                                        # rising bubbles
        x, z = rng.uniform(0, W), rng.uniform(-3.5, -1.5)
        r = rng.uniform(0.02, 0.06)
        b.disc(flat(f'Bub{kind}', tuple(min(1, c + 0.2) for c in liquid), 0.55), x, z, r, y=-0.04, seg=12, c='NoLine')
    b.build(wrap_width=W)
    render(f'Pit_{kind.capitalize()}.png', glow_kw=dict(threshold=0.6, radii=(4, 14), strength=(.55, .35), wrap_x=True))


JOBS.update(pit_toxic=lambda: pit('toxic'), pit_electric=lambda: pit('electric'))
for job in globals().get('ONLY_PITS', []):
    JOBS[job]()
