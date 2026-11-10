# Gameplay props in the NII-punk style. Same file names / sizes / pivots as the builder expects (see NeonLevelBuilder.PrepareAssets).
# Gameplay objects are the highest-contrast thing on screen: warm khaki / brass on the cool location palettes,
# thicker ink, a single warm warning accent. Signal colours (С cyan, Р magenta, С+Р violet) are left to the engine overlays.
# Run: ONLY = ['crate', ...]; exec(open(r"...\props_sols.py", encoding="utf-8").read())
import math, random
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\sols_kit.py", encoding="utf-8").read(), globals())

PPU = 200
INKP = (0.025, 0.03, 0.03)
P = dict(
    khaki=(0.6, 0.53, 0.36), khaki_d=(0.36, 0.31, 0.2), khaki_l=(0.78, 0.71, 0.5),
    steel=(0.46, 0.5, 0.48), steel_d=(0.2, 0.23, 0.23), steel_dd=(0.09, 0.11, 0.11), steel_l=(0.66, 0.7, 0.66),
    brass=(0.78, 0.6, 0.3), rubber=(0.07, 0.07, 0.07), warn=(0.96, 0.66, 0.18), red=(0.95, 0.25, 0.2),
    enamel=(0.86, 0.84, 0.74), stencil=(0.12, 0.1, 0.07),
)


def prop_scene(w, h, x0, z0, ppu=PPU, line=2.4):
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0, shade_k=0.6, light_k=1.32)
    setup_render(w, h, ppu, x0, z0, line=line)
    ink(line, color=INKP)
    return Batch()


def M(name):
    return sol('P_' + name, P[name], lights=False)


def stripes(b, x0, x1, z0, z1, y, step=0.12, c1='warn', c2='rubber'):
    """Diagonal warning band (only a small accent, never large areas)."""
    b.box(M(c2), x0, x1, z0, z1, y + 0.01, y + 0.03, c='NoLine')
    x = x0 - (z1 - z0)
    while x < x1:
        pts = [(max(x0, x), z0), (min(x1, x + step / 2), z0), (min(x1, x + step / 2 + (z1 - z0)), z1), (max(x0, x + (z1 - z0)), z1)]
        pts = [(min(max(px, x0), x1), pz) for px, pz in pts]
        if pts[1][0] - pts[0][0] > 1e-3 or pts[2][0] - pts[3][0] > 1e-3:
            b.poly(M(c1), pts, y=y, c='NoLine')
        x += step


# ============================================================ CRATE 0.9 x 0.9, pivot .05 from bottom; stacks at .8
def crate(variant=0):
    b = prop_scene(0.9, 0.9, -0.45, -0.05)
    y = -0.3
    body, dark, lite = (M('khaki'), M('khaki_d'), M('khaki_l')) if variant == 0 else (M('steel'), M('steel_d'), M('steel_l'))
    plate(b, body, -0.4, 0.4, 0.0, 0.8, y, 0.6, 0.06)
    # reinforced corners and rim
    plate(b, dark, -0.4, -0.31, 0.0, 0.8, y - 0.03, 0.6, 0.025)
    plate(b, dark, 0.31, 0.4, 0.0, 0.8, y - 0.03, 0.6, 0.025)
    plate(b, dark, -0.4, 0.4, 0.72, 0.8, y - 0.04, 0.6, 0.02)
    plate(b, dark, -0.4, 0.4, 0.0, 0.08, y - 0.04, 0.6, 0.02)
    if variant == 0:
        # stencilled lid number + enamel label (institute property)
        plate(b, lite, -0.24, 0.24, 0.42, 0.62, y - 0.05, 0.1, 0.015)
        text('НИИ-7', M('stencil'), 0.0, 0.47, 0.1, y=y - 0.07, c='NoLine')
        b.box(M('stencil'), -0.18, 0.18, 0.2, 0.235, y - 0.06, y - 0.05, c='NoLine')
        b.box(M('stencil'), -0.18, 0.06, 0.27, 0.3, y - 0.06, y - 0.05, c='NoLine')
    else:
        # steel case: vent louvres + handle + small warning corner
        slats(b, dark, -0.24, 0.24, 0.3, 0.62, y - 0.06, n=4, gap=0.035)
        b.box(M('steel_dd'), -0.14, 0.14, 0.14, 0.2, y - 0.08, y - 0.05, bevel=0.012)
        stripes(b, 0.18, 0.31, 0.1, 0.2, y - 0.07, step=0.07)
    bolts(b, M('steel_dd'), -0.4, 0.4, 0.0, 0.8, y - 0.06, inset=0.05, r=0.018)
    b.build()
    render(f'Crate{"" if variant == 0 else "2"}.png', glow_kw=None)


# ============================================================ TERMINAL 1.4 x 1.9, pivot .1 from bottom; screen centre ~(0.03, 1.3), tilted -4.4°
def terminal():
    b = prop_scene(1.4, 1.9, -0.7, -0.1)
    y = -0.35
    plate(b, M('steel_d'), -0.55, 0.55, 0.0, 0.16, y, 0.7, 0.03)
    b.prism(M('khaki'), [(-0.34, 0.16), (0.34, 0.16), (0.28, 0.95), (-0.28, 0.95)], y + 0.05, y + 0.6)
    slats(b, M('khaki_d'), -0.2, 0.2, 0.24, 0.5, y + 0.02, n=4, gap=0.03)
    # head: chunky tube-era cabinet, screen window tilted like the overlay
    b.prism(M('khaki'), [(-0.62, 0.92), (0.62, 0.92), (0.68, 1.66), (-0.56, 1.76)], y, y + 0.65)
    b.prism(M('khaki_l'), [(-0.62, 1.66), (0.68, 1.66), (0.66, 1.76), (-0.56, 1.8)], y - 0.02, y + 0.6)
    b.prism(M('steel_dd'), [(-0.47, 1.06), (0.53, 1.06), (0.56, 1.52), (-0.44, 1.6)], y - 0.03, y + 0.02)
    b.prism(sol('ScreenOff', (0.04, 0.06, 0.07), lights=False), [(-0.43, 1.1), (0.49, 1.1), (0.52, 1.48), (-0.4, 1.55)], y - 0.04, y - 0.03, c='NoLine')
    # two big toggle pads (left = С, right = Р) — the overlay colours them in game
    for sx, lab in ((-0.3, 'С'), (0.3, 'Р')):
        plate(b, M('steel_d'), sx - 0.2, sx + 0.2, 0.62, 0.86, y - 0.04, 0.1, 0.02)
        plate(b, M('enamel'), sx - 0.15, sx + 0.15, 0.65, 0.83, y - 0.07, 0.04, 0.012)
        text(lab, M('stencil'), sx, 0.68, 0.13, y=y - 0.1, c='NoLine')
    # brass nameplate + lamp + cable
    plate(b, M('brass'), -0.2, 0.2, 0.96, 1.02, y - 0.05, 0.05, 0.01)
    b.box(M('steel'), 0.48, 0.53, 1.76, 1.88, y + 0.1, y + 0.2)
    b.disc(glowm('TermLamp', (0.75, 0.5, 1.0)), 0.505, 1.9, 0.04, y=y + 0.05, c='NoLine')
    b.tube(M('rubber'), [(0.3, 0.4), (0.45, 0.25), (0.52, 0.06), (0.65, 0.02)], 0.025, y=y + 0.3)
    b.build()
    render('Terminal.png', glow_kw=dict(threshold=0.75, radii=(3, 8), strength=(.5, .25), wrap_x=False))


# ============================================================ GATE 0.9 x 4, pivot .1 from bottom (collider .6 x 3.8)
def gate():
    b = prop_scene(0.9, 4.0, -0.45, -0.1)
    y = -0.3
    plate(b, M('steel_dd'), -0.33, 0.33, 0.0, 3.85, y + 0.05, 0.6, 0.02)
    seg = 3.8 / 6
    for k in range(6):
        z = k * seg
        plate(b, M('steel'), -0.28, 0.28, z + 0.03, z + seg - 0.03, y - 0.02, 0.2, 0.04)
        b.box(M('steel_d'), -0.2, 0.2, z + seg * 0.45, z + seg * 0.55, y - 0.05, y - 0.02, c='NoLine')
    stripes(b, -0.28, 0.28, 0.08, 0.32, y - 0.06, step=0.12)
    # status slit (tinted by lock state in engine)
    plate(b, M('steel_dd'), -0.07, 0.07, 1.0, 2.9, y - 0.07, 0.04, 0.01)
    b.box(glowm('GateLed', P['red']), -0.03, 0.03, 1.05, 2.85, y - 0.09, y - 0.08, c='NoLine')
    b.build()
    render('Gate.png', glow_kw=dict(threshold=0.75, radii=(3, 8), strength=(.5, .25), wrap_x=False))


# ============================================================ GRAPPLE ANCHOR 1 x 1, centre = grab point (engine draws the cyan ring)
def anchor():
    b = prop_scene(1.0, 1.0, -0.5, -0.5, line=2.2)
    y = -0.2
    plate(b, M('steel_d'), -0.26, 0.26, 0.3, 0.5, y, 0.4, 0.04)                  # ceiling clamp
    b.box(M('steel'), -0.06, 0.06, 0.12, 0.32, y + 0.05, y + 0.25, bevel=0.015)  # stem
    b.ngon(M('brass'), 0, 0, 0.2, 20, y, y + 0.2)                                # brass catch ring
    b.ngon(sol('Hole', (0.03, 0.04, 0.04), lights=False), 0, 0, 0.11, 20, y - 0.01, y, c='NoLine')
    b.build()
    render('Anchor.png', glow_kw=None)


# ============================================================ LASER EMITTER 0.8 x 0.6, centre = beam height
def emitter():
    b = prop_scene(0.8, 0.6, -0.4, -0.3, line=2.2)
    y = -0.2
    plate(b, M('steel_d'), -0.2, 0.2, -0.24, 0.24, y, 0.4, 0.04)
    plate(b, M('khaki'), -0.28, 0.1, -0.16, 0.16, y - 0.05, 0.4, 0.03)
    b.cyl(M('steel'), 0.18, 0, 0.09, 0.2, axis='X', y=y - 0.1, seg=14)
    b.disc(glowm('EmitLens', P['red']), 0.29, 0, 0.06, y=y - 0.25, seg=14, c='NoLine')
    b.build()
    render('Emitter.png', glow_kw=dict(threshold=0.75, radii=(2, 6), strength=(.6, .25), wrap_x=False))


# ============================================================ DRONE 1.2 x 0.8, centre = beam height (open-sky lasers)
def drone():
    b = prop_scene(1.2, 0.8, -0.6, -0.4, line=2.2)
    y = -0.2
    b.sphere(M('khaki'), 0, 0, 0.22, y=y, sx=1.3, sz=0.85, sub=3)
    plate(b, M('steel_d'), -0.5, 0.5, 0.16, 0.22, y + 0.1, 0.1, 0.02)
    for sx in (-1, 1):
        b.box(M('steel'), sx * 0.5 - 0.16, sx * 0.5 + 0.16, 0.22, 0.26, y, y + 0.1)
    b.disc(glowm('DroneEye', P['red']), 0.1, -0.02, 0.07, y=y - 0.3, seg=14, c='NoLine')
    b.build()
    render('Drone.png', glow_kw=dict(threshold=0.75, radii=(2, 6), strength=(.6, .25), wrap_x=False))


# ============================================================ CHECKPOINT POST 0.8 x 2.6, pivot .05 from bottom; lamp at 2.25 (engine dot)
def beacon():
    b = prop_scene(0.8, 2.6, -0.4, -0.05)
    y = -0.2
    plate(b, M('steel_d'), -0.25, 0.25, 0.0, 0.18, y, 0.5, 0.03)
    plate(b, M('khaki'), -0.07, 0.07, 0.18, 2.0, y + 0.05, 0.2, 0.02)
    plate(b, M('enamel'), -0.17, 0.17, 1.2, 1.6, y - 0.02, 0.06, 0.015)
    text('КП', M('stencil'), 0, 1.3, 0.16, y=y - 0.05, c='NoLine')
    b.ngon(M('steel_d'), 0, 2.25, 0.2, 16, y, y + 0.3)           # lamp housing (engine draws the light inside)
    b.ngon(sol('LampGlass', (0.1, 0.13, 0.13), lights=False), 0, 2.25, 0.13, 16, y - 0.01, y, c='NoLine')
    b.build()
    render('Beacon.png', glow_kw=None)


# ============================================================ EXIT DOOR 3.2 x 4.6 @150, pivot .1 from bottom; opening 1.8 x 3.2 centred, glow behind
def exit_door():
    b = prop_scene(3.2, 4.6, -1.6, -0.1, ppu=150, line=2.6)
    y = -0.3
    # heavy hermetic frame with rounded corners (built from an n-gon ring cut by the opening)
    outer, inner = [], []
    def rounded(w, h, r, n=6):
        pts = []
        for cx, cz, a0 in ((w / 2 - r, h - r, 0), (-w / 2 + r, h - r, 90), (-w / 2 + r, r, 180), (w / 2 - r, r, 270)):
            for i in range(n + 1):
                a = math.radians(a0 + 90 * i / n)
                pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
        return pts
    o = rounded(2.9, 4.1, 0.5)
    ins = rounded(1.84, 3.24, 0.35)
    # frame band (no per-quad ink), then ink only along the two outlines
    for i in range(len(o)):
        j = (i + 1) % len(o)
        b.prism(M('steel'), [o[i], o[j], ins[j], ins[i]], y, y + 0.4, c='NoLine')
    inner2 = rounded(2.06, 3.42, 0.42)
    for i in range(len(inner2)):
        j = (i + 1) % len(inner2)
        b.prism(M('steel_d'), [inner2[i], inner2[j], ins[j], ins[i]], y - 0.01, y, c='NoLine')
    inkm = sol('DoorInk', INKP, lights=False)
    for loop in (o, ins):
        b.tube(inkm, loop + [loop[0]], 0.022, y=y - 0.03, seg=6, c='NoLine')
    for sx in (-1, 1):   # hinge blocks + wheel lock
        plate(b, M('steel_d'), sx * 1.3 - 0.12, sx * 1.3 + 0.12, 0.7, 1.2, y - 0.05, 0.3, 0.03)
        plate(b, M('steel_d'), sx * 1.3 - 0.12, sx * 1.3 + 0.12, 2.6, 3.1, y - 0.05, 0.3, 0.03)
    stripes(b, -1.3, 1.3, 0.0, 0.16, y - 0.06, step=0.18)
    plate(b, M('enamel'), -0.75, 0.75, 3.6, 3.98, y - 0.06, 0.06, 0.02)
    text('ВЫХОД', M('stencil'), 0, 3.67, 0.26, y=y - 0.09, c='NoLine')
    b.build()
    render('ExitDoor.png', glow_kw=None)


# ============================================================ PIT 4 x 4.3 @120, tiled; top edge = floor line (y 0)
def pit(kind='toxic'):
    W, H = 4.0, 4.3
    b = prop_scene(W, H, 0, -H, ppu=120, line=1.8)
    electric = kind == 'electric'
    liq = (0.35, 0.75, 1.0) if electric else (0.7, 1.0, 0.3)
    deep = (0.02, 0.05, 0.1) if electric else (0.04, 0.09, 0.05)
    wall = sol('PitWall', (0.06, 0.1, 0.11) if not electric else (0.07, 0.08, 0.12), lights=False)
    wall_d = sol('PitWallD', (0.03, 0.05, 0.055), lights=False)
    surf = -0.85
    # inner vat wall above the liquid: dark, ribbed
    b.quad(wall_d, -0.5, W + 0.5, surf, 0.0, y=1.0, c='NoLine')
    for i in range(4):
        x0 = i * 1.0
        plate(b, wall, x0 + 0.06, x0 + 0.94, surf + 0.05, -0.08, 0.6, 0.2, 0.04)
    # liquid body: bright skin, gradient into the dark
    b.quad(gradient_mat(f'Liq{kind}', deep, mix(liq, deep, 0.62), -H + 1.5, surf), -0.5, W + 0.5, -H, surf, y=0.2, c='NoLine')
    b.quad(glowm(f'Skin{kind}', mix(liq, (1, 1, 1), 0.25)), -0.5, W + 0.5, surf - 0.05, surf + 0.03, y=0.1, c='NoLine')
    rng = random.Random(3 if electric else 4)
    for _ in range(26):
        bx, bz = rng.uniform(0.1, W - 0.1), rng.uniform(-H + 0.3, surf - 0.15)
        r = rng.uniform(0.025, 0.07) * (1 - (surf - bz) / H * 0.6)
        b.disc(flat(f'Bub{kind}', mix(liq, (1, 1, 1), 0.3), 0.55), bx, bz, r, y=0.15, seg=10, c='NoLine')
    for _ in range(5):   # surface blobs
        bx = rng.uniform(0.2, W - 0.2)
        b.sphere(glowm(f'Skin{kind}', mix(liq, (1, 1, 1), 0.25)), bx, surf + 0.02, rng.uniform(0.06, 0.12), y=0.1, sz=0.55, sub=2, c='NoLine')
    b.build(wrap_width=W)
    render(f'Pit_{kind.capitalize()}.png', glow_kw=dict(threshold=0.6, radii=(3, 10), strength=(.5, .3), wrap_x=True))


JOBS = dict(crate=lambda: (crate(0), crate(1)), terminal=terminal, gate=gate, anchor=anchor, emitter=emitter, drone=drone,
            beacon=beacon, exit=exit_door, pit=lambda: (pit('toxic'), pit('electric')))
for job in globals().get('ONLY', list(JOBS)):
    JOBS[job]()
