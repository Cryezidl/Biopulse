# MOP-3000 restyle: flat 2D shapes + cel shadow shapes + tinted ink (same sprite contract as mop.py):
#   Mop3000_Body.png 2.2 x 2.2 centred, eye socket at (0.3, 0.12), mop grip at (0.66, -0.26);
#   Mop3000_Eye.png 0.6 x 0.6 (tinted in game); Mop3000_Mop.png 3 x 3, grip at the centre.
# Look: a Soviet household appliance gone feral — ivory enamel, red band, chrome bezel, brass tank, warm thruster.
import math, random
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\sols_kit.py", encoding="utf-8").read(), globals())

PPU = 220
EYE = (0.3, 0.12)
K = dict(
    ivory=(0.86, 0.8, 0.66), ivory_s=(0.6, 0.53, 0.44), ivory_l=(0.97, 0.94, 0.84),
    red=(0.78, 0.2, 0.15), red_s=(0.5, 0.11, 0.1), chrome=(0.72, 0.74, 0.76), chrome_s=(0.4, 0.42, 0.46),
    dark=(0.1, 0.1, 0.12), brass=(0.8, 0.58, 0.28), brass_s=(0.52, 0.34, 0.16), wood=(0.56, 0.33, 0.18), wood_s=(0.36, 0.2, 0.11),
    strand=(0.82, 0.84, 0.8), strand_s=(0.55, 0.58, 0.58), ink=(0.06, 0.03, 0.03),
)


def Mk(k):
    return sol('M_' + k, K[k], lit=K[k], dark=K[k], lights=False, haze=False)


def rounded(x0, x1, z0, z1, r, seg=6):
    pts = []
    for cx, cz, a0 in ((x1 - r, z0 + r, -90), (x1 - r, z1 - r, 0), (x0 + r, z1 - r, 90), (x0 + r, z0 + r, 180)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    return pts


def circ(cx, cz, r, n=24):
    return [(cx + r * math.cos(2 * math.pi * i / n), cz + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def scene(w, h, x0, z0, line=2.4):
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    setup_render(w, h, PPU, x0, z0, line=line)
    ink(line, color=K['ink'])
    return Batch()


def P(b, mat, pts, d, ink_=True):
    b.prism(Mk(mat) if isinstance(mat, str) else mat, pts, d, d + 0.02, c='Main' if ink_ else 'NoLine')


def body():
    b = scene(2.2, 2.2, -1.1, -1.1)
    # thruster skirt + warm exhaust
    P(b, 'dark', [(-0.42, -0.62), (0.42, -0.62), (0.32, -0.74), (-0.32, -0.74)], 0.3)
    P(b, 'chrome_s', [(-0.26, -0.74), (0.26, -0.74), (0.22, -0.8), (-0.22, -0.8)], 0.31)
    for i in range(5):
        k = i / 5
        b.prism(flat(f'Jet{i}', (1.0, 0.62 + 0.25 * k, 0.3 + 0.4 * k), 0.16 + 0.1 * k),
                [(-0.2 + k * 0.1, -0.8), (0.2 - k * 0.1, -0.8), (0.04 - k * 0.03, -1.05 + k * 0.12), (-0.04 + k * 0.03, -1.05 + k * 0.12)],
                0.32 - i * 0.002, 0.321 - i * 0.002, c='NoLine')
    # brass spray tank on the back + hose
    P(b, 'brass', rounded(-0.78, -0.42, -0.3, 0.42, 0.16), 0.25)
    P(b, 'brass_s', [(-0.78, -0.1), (-0.62, -0.1), (-0.62, -0.3), (-0.78, -0.2)], 0.245, False)
    for z in (-0.12, 0.2):
        P(b, 'brass_s', [(-0.79, z), (-0.41, z), (-0.41, z + 0.04), (-0.79, z + 0.04)], 0.24, False)
    b.tube(Mk('dark'), [(-0.6, -0.3), (-0.5, -0.5), (-0.1, -0.56), (0.3, -0.5), (0.5, -0.36)], 0.03, y=0.15)
    # main shell: rounded box + dome
    shell = rounded(-0.5, 0.5, -0.62, 0.4, 0.18)
    P(b, 'ivory', shell, 0.1)
    dome = [(-0.46, 0.36), (-0.38, 0.56), (-0.2, 0.68), (0.05, 0.71), (0.28, 0.66), (0.44, 0.52), (0.48, 0.36)]
    P(b, 'ivory', dome, 0.11)
    # cel shadow (lower-back) and highlight (dome top)
    P(b, 'ivory_s', [(-0.5, -0.1), (-0.5, -0.44), (-0.32, -0.62), (0.2, -0.62), (-0.1, -0.4)], 0.09, False)
    P(b, 'ivory_l', [(-0.38, 0.56), (-0.2, 0.68), (0.05, 0.71), (0.0, 0.62), (-0.24, 0.58)], 0.09, False)
    # red appliance band with stencil
    P(b, 'red', [(-0.51, -0.2), (0.51, -0.2), (0.51, -0.4), (-0.51, -0.4)], 0.08)
    P(b, 'red_s', [(-0.51, -0.34), (0.51, -0.34), (0.51, -0.4), (-0.51, -0.4)], 0.075, False)
    text('МОП-3000', Mk('ivory_l'), -0.05, -0.345, 0.1, y=0.06, c='NoLine')
    # vent louvres
    for i in range(3):
        z = -0.02 - i * 0.06
        P(b, 'ivory_s', [(-0.42, z), (-0.16, z), (-0.16, z + 0.025), (-0.42, z + 0.025)], 0.075, False)
    # visor + chrome eye bezel (eye sprite sits on top)
    P(b, 'dark', [(-0.02, 0.44), (0.54, 0.44), (0.58, -0.08), (0.03, -0.08)], 0.05)
    P(b, 'chrome', circ(EYE[0], EYE[1], 0.27, 28), 0.03)
    P(b, 'chrome_s', [(EYE[0] + 0.27 * math.cos(a), EYE[1] + 0.27 * math.sin(a)) for a in [math.radians(200 + i * 8) for i in range(18)]] + [(EYE[0], EYE[1])], 0.025, False)
    P(b, 'dark', circ(EYE[0], EYE[1], 0.21, 28), 0.02)
    # grumpy brow
    P(b, 'chrome', [(EYE[0] - 0.32, EYE[1] + 0.3), (EYE[0] + 0.24, EYE[1] + 0.24), (EYE[0] + 0.24, EYE[1] + 0.33), (EYE[0] - 0.32, EYE[1] + 0.4)], 0.0)
    # arm to the mop grip
    b.bar(Mk('chrome'), 0.42, -0.15, 0.66, -0.26, 0.08, -0.02, 0.0)
    P(b, 'dark', circ(0.42, -0.15, 0.06, 12), -0.03)
    # antenna + warning lamp
    b.box(Mk('chrome_s'), -0.18, -0.15, 0.68, 1.0, 0.1, 0.12)
    b.disc(glowm('MAnt', (1.0, 0.4, 0.2)), -0.165, 1.03, 0.055, y=0.09, seg=14, c='NoLine')
    # rivets + status leds
    for x in (-0.4, 0.4):
        b.disc(Mk('ivory_s'), x, 0.28, 0.022, y=0.075, seg=8, c='NoLine')
    for i, col in enumerate(((0.45, 1.0, 0.5), (1.0, 0.75, 0.2), (1.0, 0.3, 0.25))):
        b.disc(glowm(f'MLed{i}', col), -0.4 + i * 0.07, 0.14, 0.022, y=0.07, seg=10, c='NoLine')
    b.build()
    render('Mop3000_Body.png', glow_kw=dict(threshold=0.92, radii=(3, 8), strength=(.5, .3), wrap_x=False))


def eye():
    b = scene(0.6, 0.6, -0.3, -0.3, line=0)
    b.disc(glowm('EyeIris', (1, 1, 1)), 0, 0, 0.19, y=0, seg=40, c='NoLine')
    b.disc(flat('EyeRing', (0.72, 0.72, 0.72)), 0, 0, 0.14, y=-0.01, seg=40, c='NoLine')
    b.disc(flat('EyePupil', (0.05, 0.04, 0.05)), 0.04, 0.0, 0.08, y=-0.02, seg=32, c='NoLine')
    b.disc(glowm('EyeGlint', (1, 1, 1)), 0.0, 0.06, 0.028, y=-0.03, seg=12, c='NoLine')
    b.build()
    render('Mop3000_Eye.png', glow_kw=None)


def mop():
    b = scene(3.0, 3.0, -1.5, -1.5, line=2.2)
    rng = random.Random(4)
    hx, hz = 0.5, -0.78
    b.bar(Mk('wood'), 0.0, 0.05, hx, hz + 0.1, 0.075, 0.05, 0.07)
    b.bar(Mk('wood_s'), 0.02, 0.02, hx + 0.02, hz + 0.08, 0.025, 0.04, 0.045, c='NoLine')
    P(b, 'dark', circ(0.0, 0.05, 0.065, 12), 0.03)
    P(b, 'chrome', [(hx - 0.3, hz), (hx + 0.3, hz), (hx + 0.26, hz + 0.13), (hx - 0.26, hz + 0.13)], 0.02)
    P(b, 'chrome_s', [(hx - 0.3, hz), (hx + 0.3, hz), (hx + 0.29, hz + 0.04), (hx - 0.29, hz + 0.04)], 0.015, False)
    # strands as flat ribbons, alternating tone, a little splay
    for i in range(12):
        x = hx - 0.27 + i * 0.049
        L = rng.uniform(0.34, 0.5)
        bend = rng.uniform(-0.14, 0.06)
        pts = [(x - 0.022, hz), (x + 0.022, hz), (x + bend + 0.02, hz - L), (x + bend - 0.02, hz - L + 0.02)]
        P(b, 'strand' if i % 2 else 'strand_s', pts, 0.04 + (i % 3) * 0.004)
    for _ in range(4):
        x = hx + rng.uniform(-0.3, 0.3)
        z = hz - rng.uniform(0.58, 0.85)
        b.disc(flat('Drop', (0.7, 0.85, 0.95), 0.85), x, z, 0.028, y=-0.05, seg=10, c='NoLine')
    b.build()
    render('Mop3000_Mop.png', glow_kw=None)


for job in globals().get('MOP_ONLY', ['body', 'eye', 'mop']):
    globals()[job]()
publish(['Mop3000_Body.png', 'Mop3000_Eye.png', 'Mop3000_Mop.png'])
contact_sheet([('Mop3000_Body.png', PPU), ('Mop3000_Mop.png', PPU), ('Mop3000_Eye.png', PPU)], SPRITES + r'\pv_mop.png', W=1100, H=520, scale=2.2,
              bg=(0.09, 0.17, 0.17))
