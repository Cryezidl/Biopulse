# Hero as a 2D cut-out puppet (flat polygons, cel shadow shapes, per-part ink), NII-punk style.
# Silhouette from MainCharacter.png: pointed hood with dark face + yellow triangle eye, asymmetric cloak with a
# pointed hem, bionic near arm (steel + brass, yellow strips), dark suit and boots.
# Frames match the old sprite contract: cell 2.2 x 2.4 u, feet 0.3 above the bottom, 160 ppu, names run_00.. etc.
# Run: HERO_SET = None or ['run_00', ...]; exec(open(r"...\hero2d.py", encoding="utf-8").read())
import math, os, shutil
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\sols_kit.py", encoding="utf-8").read(), globals())

HERO_DIR = SPRITES + r"\Hero"
UNITY_HERO = ROOT + r"\Assets\Biopulse\Art\Neon\Hero"
CELL_W, CELL_H, FEET, HERO_PPU = 2.2, 2.4, 0.3, 160

COL = dict(
    cloth=(0.8, 0.38, 0.19), cloth_s=(0.56, 0.22, 0.13), cloth_l=(0.93, 0.55, 0.3), lining=(0.32, 0.12, 0.09),
    suit=(0.2, 0.19, 0.22), suit_s=(0.11, 0.1, 0.13), boot=(0.28, 0.2, 0.16), boot_s=(0.15, 0.1, 0.09),
    metal=(0.7, 0.72, 0.7), metal_s=(0.42, 0.44, 0.45), brass=(0.86, 0.66, 0.32), face=(0.05, 0.05, 0.06),
    eye=(1.0, 0.86, 0.22), ink=(0.06, 0.025, 0.02),
)


def Mh(k):
    if k == 'eye':
        return glowm('HEye', COL['eye'])
    return sol('H_' + k, COL[k], lit=COL[k], dark=COL[k], lights=False, haze=False)


# ---------------------------------------------------------------- 2D helpers
def rot2(p, a):
    c, s = math.cos(a), math.sin(a)
    return (p[0] * c - p[1] * s, p[0] * s + p[1] * c)


def add(p, q):
    return (p[0] + q[0], p[1] + q[1])


class Puppet:
    """Collects (depth, material, polygon) and emits prisms; depth = draw order (smaller = in front)."""

    def __init__(self, b, pivot=(0, 0.95), tilt=0.0):
        self.b, self.pivot, self.tilt = b, pivot, tilt

    def T(self, p):
        if self.tilt:
            q = rot2((p[0] - self.pivot[0], p[1] - self.pivot[1]), self.tilt)
            return (q[0] + self.pivot[0], q[1] + self.pivot[1])
        return p

    def poly(self, mat, pts, depth, ink=True):
        pts = [self.T(p) for p in pts]
        self.b.prism(Mh(mat), pts, depth, depth + 0.02, c='Main' if ink else 'NoLine')


def bone(origin, ang, L):
    """ang: radians from straight down, + = forward (+X). Returns end point and the (d, n) frame."""
    d = (math.sin(ang), -math.cos(ang))
    n = (math.cos(ang), math.sin(ang))
    return (origin[0] + d[0] * L, origin[1] + d[1] * L), d, n


def seg_poly(o, ang, L, w0, w1, u0=0.0, back=None):
    """Tapered limb polygon along a bone. back=(f0, f1): only the back strip (for cel shadow)."""
    _, d, n = bone(o, ang, L)
    P = lambda u, w: (o[0] + d[0] * u + n[0] * w, o[1] + d[1] * u + n[1] * w)
    if back is None:
        return [P(u0, w0 / 2), P(L, w1 / 2), P(L, -w1 / 2), P(u0, -w0 / 2)]
    f0, f1 = back
    return [P(u0, -w0 / 2 + w0 * f0), P(L, -w1 / 2 + w1 * f1), P(L, -w1 / 2), P(u0, -w0 / 2)]


def circle(c, r, n=14):
    return [(c[0] + r * math.cos(2 * math.pi * i / n), c[1] + r * math.sin(2 * math.pi * i / n)) for i in range(n)]


def local(origin, ang, pts):
    """pts in a frame where x = forward, z = up, rotated by ang (radians, + = lean forward / clockwise)."""
    return [add(origin, rot2(p, -ang)) for p in pts]


# ---------------------------------------------------------------- the figure
def draw(P, pose):
    g = lambda k, dflt=0.0: math.radians(pose.get(k, dflt))
    lean = g('lean')
    hip = (0.0, 0.95 - pose.get('drop', 0.0))
    up = (math.sin(lean), math.cos(lean))
    neck = (hip[0] + up[0] * 0.5, hip[1] + up[1] * 0.5)
    head_c = add(neck, rot2((0.04, 0.17), -(lean * 0.5 + g('head'))))

    def leg(side, depth):
        far = side == 'far'
        th = lean * 0.3 + g(f'thigh_{side}')
        knee_p, _, _ = bone(hip, th, 0.45)
        sh = th - g(f'knee_{side}')
        ank, _, _ = bone(knee_p, sh, 0.44)
        fa = sh + math.radians(90) + g(f'foot_{side}')
        s = 's' if far else ''
        P.poly('suit_s' if far else 'suit', seg_poly(hip, th, 0.47, 0.19, 0.14), depth)
        P.poly('suit_s', seg_poly(hip, th, 0.47, 0.19, 0.14, back=(0.35, 0.35)), depth - 0.005, ink=False) if not far else None
        P.poly('suit_s' if far else 'suit', seg_poly(knee_p, sh, 0.46, 0.14, 0.11), depth + 0.01)
        if not far:
            P.poly('suit_s', seg_poly(knee_p, sh, 0.46, 0.14, 0.11, back=(0.4, 0.4)), depth + 0.005, ink=False)
        P.poly('metal_s', circle(knee_p, 0.05, 10), depth - 0.01)
        # boot: heel behind the ankle, toe forward along the foot angle
        boot = local(ank, -(fa - math.radians(90)), [(-0.07, 0.06), (0.05, 0.07), (0.17, 0.02), (0.18, -0.05), (-0.08, -0.05)])
        P.poly('boot_s' if far else 'boot', boot, depth - 0.015)

    def arm(side, depth):
        far = side == 'far'
        sh_p = add(neck, rot2((0.0, -0.1), -lean))
        ua = lean + g(f'sh_{side}')
        el, _, _ = bone(sh_p, ua, 0.3)
        fa = ua + g(f'el_{side}')
        wr, _, _ = bone(el, fa, 0.28)
        ha = fa + g(f'wr_{side}')
        if far:   # cloth-wrapped human arm, mostly hidden
            P.poly('cloth_s', seg_poly(sh_p, ua, 0.31, 0.13, 0.11), depth)
            P.poly('suit_s', seg_poly(el, fa, 0.29, 0.1, 0.085), depth + 0.01)
            P.poly('suit_s', seg_poly(wr, ha, 0.1, 0.08, 0.07), depth + 0.01)
            return
        # bionic arm: steel shells, brass joints, yellow strips
        P.poly('metal', seg_poly(sh_p, ua, 0.31, 0.15, 0.12), depth)
        P.poly('metal_s', seg_poly(sh_p, ua, 0.31, 0.15, 0.12, back=(0.38, 0.38)), depth - 0.004, ink=False)
        P.poly('eye', seg_poly(sh_p, ua, 0.22, 0.025, 0.025, u0=0.09), depth - 0.008, ink=False)
        P.poly('metal', circle(sh_p, 0.082, 14), depth - 0.006)
        P.poly('metal', seg_poly(el, fa, 0.29, 0.12, 0.1), depth + 0.01)
        P.poly('metal_s', seg_poly(el, fa, 0.29, 0.12, 0.1, back=(0.4, 0.4)), depth + 0.006, ink=False)
        for u in (0.11, 0.16):
            P.poly('eye', seg_poly(el, fa, u + 0.025, 0.06, 0.06, u0=u), depth + 0.003, ink=False)
        P.poly('brass', circle(el, 0.068, 12), depth - 0.002)
        P.poly('brass', circle(wr, 0.05, 10), depth - 0.003)
        # hand: palm + three fingers + thumb; 'grip' 0 = open, 1 = fist
        grip = pose.get('grip', 0.4)
        palm_end, _, _ = bone(wr, ha, 0.11)
        P.poly('metal', seg_poly(wr, ha, 0.11, 0.1, 0.11, u0=0.02), depth - 0.004)
        for i, off in enumerate((-0.035, 0.0, 0.035)):
            base = add(palm_end, rot2((off, 0.0), ha))
            fang = ha + math.radians(10 + 70 * grip)
            P.poly('metal_s', seg_poly(base, fang, 0.08, 0.028, 0.024), depth - 0.005 - i * 0.001)
        tb = add(wr, rot2((0.05, -0.05), ha))
        P.poly('metal_s', seg_poly(tb, ha + math.radians(-40 + 60 * grip), 0.06, 0.03, 0.025), depth - 0.006)

    def cloak(depth):
        trail, flap = pose.get('trail', 0.2), pose.get('flap', 0.0)
        # shape in the torso frame: neck at (0,0), x forward, z up
        hem = [(0.15, -0.66), (0.04, -0.6 + 0.04 * flap), (-0.14, -0.68 - 0.05 * flap),
               (-0.36 - 0.28 * trail, -0.92 + 0.38 * trail + 0.06 * flap)]
        pts = [(0.1, 0.02), (0.19, -0.16), (0.2, -0.45)] + hem + [(-0.25 - 0.1 * trail, -0.42 + 0.1 * trail), (-0.17, -0.12), (-0.11, 0.03)]
        P.poly('cloth', local(neck, lean, pts), depth)
        # cel shadow: back panel + one diagonal fold (drawn without ink, inside the silhouette)
        shade = [(-0.05, -0.08), (-0.17, -0.12), (-0.25 - 0.1 * trail, -0.42 + 0.1 * trail), hem[3], hem[2], (-0.02, -0.4)]
        P.poly('cloth_s', local(neck, lean, shade), depth - 0.004, ink=False)
        fold = [(0.12, -0.2), (0.17, -0.22), (0.06, -0.6), (0.02, -0.58)]
        P.poly('cloth_s', local(neck, lean, fold), depth - 0.005, ink=False)
        lit = [(0.1, 0.0), (0.18, -0.15), (0.15, -0.17), (0.06, -0.02)]
        P.poly('cloth_l', local(neck, lean, lit), depth - 0.006, ink=False)
        # cowl wrapped round the neck (over the cloak)
        cowl = [(0.13, 0.04), (0.17, -0.1), (0.04, -0.2), (-0.13, -0.15), (-0.15, -0.02), (-0.06, 0.06)]
        P.poly('cloth', local(neck, lean, cowl), depth - 0.01)
        P.poly('cloth_s', local(neck, lean, [(0.04, -0.2), (-0.13, -0.15), (-0.15, -0.02), (-0.05, -0.08)]), depth - 0.012, ink=False)

    def hood(depth):
        ha = lean * 0.5 + g('head')
        shape = [(0.17, 0.08), (0.14, -0.14), (0.01, -0.19), (-0.17, -0.13), (-0.2, 0.06), (-0.38, 0.27),
                 (-0.07, 0.25), (0.1, 0.19)]
        P.poly('cloth', local(head_c, ha, shape), depth)
        P.poly('cloth_s', local(head_c, ha, [(-0.15, -0.12), (-0.18, 0.05), (-0.34, 0.24), (-0.12, 0.12), (-0.06, -0.1)]), depth - 0.004, ink=False)
        face = [(0.135, 0.05), (0.115, -0.115), (0.03, -0.14), (-0.01, -0.02), (0.04, 0.09)]
        P.poly('face', local(head_c, ha, face), depth - 0.008)
        P.poly('lining', local(head_c, ha, [(0.04, 0.09), (-0.01, -0.02), (0.03, -0.14), (-0.03, -0.12), (-0.05, 0.02), (0.0, 0.11)]), depth - 0.007, ink=False)
        P.poly('eye', local(head_c, ha, [(0.07, 0.035), (0.07, -0.035), (0.128, 0.0)]), depth - 0.012, ink=False)
        P.poly('metal_s', local(head_c, ha, [(0.11, -0.06), (0.125, -0.06), (0.115, -0.115), (0.1, -0.115)]), depth - 0.011, ink=False)

    # back to front
    arm('far', 0.5)
    leg('far', 0.4)
    P.poly('suit', local(neck, lean, [(0.12, -0.05), (0.13, -0.45), (-0.13, -0.47), (-0.12, -0.05)]), 0.3)   # torso under the cloak
    leg('near', 0.2)
    cloak(0.1)
    hood(0.0)
    arm('near', -0.2)


# ---------------------------------------------------------------- poses (degrees)
def run_pose(t):
    ph = 2 * math.pi * t
    p = dict(lean=16, head=-4, trail=0.55 + 0.12 * math.sin(2 * ph), flap=math.sin(2 * ph + 1), drop=0.04 - 0.04 * abs(math.sin(ph)), grip=0.7)
    for side, q in (('near', ph), ('far', ph + math.pi)):
        th = 38 * math.sin(q) + 4
        swing = max(0.0, math.cos(q))
        p[f'thigh_{side}'] = th
        p[f'knee_{side}'] = 15 + 85 * swing ** 1.3
        p[f'foot_{side}'] = 10 if math.sin(q) > 0.3 else -15
    p['sh_near'], p['el_near'], p['wr_near'] = -35 * math.sin(ph) + 5, 75, 5
    p['sh_far'], p['el_far'], p['wr_far'] = 35 * math.sin(ph) + 5, 75, 5
    return p


def idle_pose(t):
    b = math.sin(2 * math.pi * t)
    return dict(lean=2 + 1.2 * b, head=-3, trail=0.05 + 0.03 * b, flap=0.3 * b, drop=0.01 * (1 + b),
                thigh_near=6, knee_near=8, thigh_far=-6, knee_far=6, sh_near=6, el_near=20 + 3 * b, wr_near=5,
                sh_far=2, el_far=14 + 3 * b, grip=0.35)


POSES = {}
for i in range(10):
    POSES[f'run_{i:02d}'] = run_pose(i / 10)
for i in range(6):
    POSES[f'idle_{i:02d}'] = idle_pose(i / 6)
POSES.update({
    'rise_00': dict(lean=-2, head=8, trail=0.9, flap=0.6, thigh_near=75, knee_near=105, foot_near=10, thigh_far=-20, knee_far=50,
                    sh_near=150, el_near=20, sh_far=-40, el_far=40, grip=0.8),
    'apex_00': dict(lean=8, head=2, trail=0.7, flap=-0.5, thigh_near=60, knee_near=100, thigh_far=25, knee_far=95,
                    sh_near=95, el_near=40, sh_far=60, el_far=70, grip=0.6),
    'fall_00': dict(lean=-6, head=-4, trail=0.15, flap=1.0, thigh_near=28, knee_near=30, thigh_far=-10, knee_far=45,
                    sh_near=125, el_near=30, sh_far=110, el_far=40, grip=0.3),
    'land_00': dict(lean=32, head=-14, trail=0.35, flap=-0.6, drop=0.24, thigh_near=78, knee_near=120, foot_near=5,
                    thigh_far=42, knee_far=115, sh_near=45, el_near=60, sh_far=-20, el_far=50, grip=0.8),
    'hang_00': dict(lean=0, head=10, trail=0.05, flap=0.2, sh_near=172, el_near=6, wr_near=-6, sh_far=20, el_far=40,
                    thigh_near=12, knee_near=25, thigh_far=-6, knee_far=18, grip=1.0),
    'swingf_00': dict(lean=-10, head=14, trail=-0.1, flap=-0.8, sh_near=168, el_near=4, sh_far=-25, el_far=30,
                      thigh_near=55, knee_near=40, thigh_far=38, knee_far=30, grip=1.0),
    'swingb_00': dict(lean=10, head=6, trail=0.9, flap=0.8, sh_near=176, el_near=8, sh_far=50, el_far=45,
                      thigh_near=-20, knee_near=50, thigh_far=-30, knee_far=70, grip=1.0),
    'reach_00': dict(lean=4, head=10, trail=0.7, flap=0.4, sh_near=112, el_near=2, wr_near=-5, sh_far=-30, el_far=45,
                     thigh_near=30, knee_near=60, thigh_far=-10, knee_far=40, grip=0.0),
    'death_00': dict(lean=-20, head=20, trail=1.0, flap=1.0, tilt=-35, sh_near=150, el_near=20, sh_far=130, el_far=30,
                     thigh_near=60, knee_near=20, thigh_far=-30, knee_far=80, grip=0.0),
})


def render_hero(names=None):
    out = []
    for name, pose in POSES.items():
        if names and name not in names:
            continue
        new_scene()
        style(haze_max=0.0, dark_top=None, light_gain=0.0)
        setup_render(CELL_W, CELL_H, HERO_PPU, -CELL_W / 2, -FEET, line=2.4)
        ink(2.3, color=COL['ink'])
        b = Batch()
        P = Puppet(b, tilt=math.radians(pose.get('tilt', 0.0)))
        draw(P, pose)
        b.build()
        out.append(render(name + '.png', glow_kw=dict(threshold=0.97, radii=(2, 6), strength=(.6, .3), wrap_x=False), folder=HERO_DIR))
    for p in out:
        shutil.copyfile(p, os.path.join(UNITY_HERO, os.path.basename(p)))
    contact_sheet([('Hero\\' + os.path.basename(p), HERO_PPU) for p in out], SPRITES + r'\pv_hero.png', W=1900, H=900, scale=2.0,
                  bg=(0.09, 0.17, 0.17))
    return out


render_hero(globals().get('HERO_SET'))
