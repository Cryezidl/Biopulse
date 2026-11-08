# The hooded runner with the bionic arm (from MainCharacter.png), as a posable low-poly 3D model.
# Rigid parts hang off a joint hierarchy (Empties); poses are set procedurally and rendered to sprite frames.
# Run in Blender:  HERO_ONLY = ['sheet'] ; exec(open(r"...\hero.py", encoding="utf-8").read())
import math, os
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\neon_kit.py", encoding="utf-8").read(), globals())

HERO_DIR = SPRITES + r"\Hero"
CLOTH = (0.56, 0.57, 0.63)
CLOTH_D = (0.4, 0.41, 0.47)
SUIT = (0.24, 0.25, 0.3)
METAL = (0.5, 0.51, 0.57)
METAL_D = (0.32, 0.33, 0.38)
MASK = (0.13, 0.13, 0.16)
YELLOW = (1.0, 0.82, 0.1)

J = {}  # joints by name


def joint(name, parent, loc):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.05
    coll('Main').objects.link(e)
    if parent:
        e.parent = J[parent]
    e.location = loc
    e.rotation_mode = 'XYZ'
    J[name] = e
    return e


def part(name, parent, bm_fn, mat, c='Main'):
    """Build a mesh with bm_fn(bm) in the joint's local space and parent it to the joint."""
    bm = bmesh.new()
    bm_fn(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = False
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    coll(c).objects.link(ob)
    ob.parent = J[parent]
    return ob


def limb(r0, r1, length, seg=6, sy=1.0):
    """Tapered faceted limb hanging down -Z from the joint."""
    def f(bm):
        bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r0, depth=length,
                              matrix=Matrix.Translation((0, 0, -length / 2)) @ Matrix.Diagonal((1, sy, 1, 1)))
    return f


def cube(sx, sy, sz, off=(0, 0, 0), bevel=0.0):
    def f(bm):
        res = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(off) @ Matrix.Diagonal((sx, sy, sz, 1)))
        if bevel:
            vs = res['verts']
            bmesh.ops.bevel(bm, geom=vs + list({e for v in vs for e in v.link_edges}), offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
    return f


def ico(r, off=(0, 0, 0), scale=(1, 1, 1), sub=1):
    def f(bm):
        bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r, matrix=Matrix.Translation(off) @ Matrix.Diagonal(scale + (1,)))
    return f


def frustum(z0, z1, rx0, ry0, rx1, ry1, seg=10, hem=0.0, xoff0=0.0, xoff1=0.0):
    """Cloak shell: ring at z0 (top) to ring at z1 (bottom); hem>0 makes the back hang lower."""
    def f(bm):
        top, bot = [], []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ca, sa = math.cos(a), math.sin(a)
            top.append(bm.verts.new((xoff0 + rx0 * ca, ry0 * sa, z0)))
            back = max(0.0, -ca)
            bot.append(bm.verts.new((xoff1 + rx1 * ca, ry1 * sa, z1 - hem * back + hem * 0.25 * ca)))
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new([top[i], top[j], bot[j], bot[i]])
        bm.faces.new(top[::-1])
        bm.faces.new(bot)
    return f


def profile_prism(pts, half_w, bevel=0.0, taper=1.0):
    """Side-profile polygon (x, z) extruded across the body (Y), optionally narrower at the back (taper)."""
    def f(bm):
        n = len(pts)
        xs = [p[0] for p in pts]
        x0, x1 = min(xs), max(xs)
        def w(x):
            t = (x - x0) / max(1e-6, x1 - x0)
            return half_w * (taper + (1 - taper) * t)
        A = [bm.verts.new((p[0], -w(p[0]), p[1])) for p in pts]
        B = [bm.verts.new((p[0], w(p[0]), p[1])) for p in pts]
        area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
        order = list(range(n)) if area > 0 else list(range(n))[::-1]
        bm.faces.new([A[i] for i in order])
        bm.faces.new([B[i] for i in order[::-1]])
        for k in range(n):
            i, j = order[k], order[(k + 1) % n]
            bm.faces.new([A[i], B[i], B[j], A[j]])
        if bevel:
            bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
    return f


# hood silhouette seen from the side: brow in front, peak swept back (concept: faceted pointed hood)
HOOD = [(0.15, -0.13), (0.19, 0.04), (0.17, 0.15), (0.08, 0.24), (-0.06, 0.27), (-0.26, 0.33),
        (-0.2, 0.17), (-0.17, -0.02), (-0.12, -0.17), (0.02, -0.2)]
FACE = [(0.1, -0.12), (0.17, -0.1), (0.2, 0.03), (0.17, 0.13), (0.1, 0.15), (0.06, 0.0)]


def build():
    reset()
    J.clear()
    cloth = toon('HCloth', CLOTH, steps=(0.52, 0.76, 1.0), cuts=(0.2, 0.5))
    cloth_d = toon('HClothD', CLOTH_D, steps=(0.52, 0.76, 1.0), cuts=(0.2, 0.5))
    suit = toon('HSuit', SUIT)
    metal = toon('HMetal', METAL, steps=(0.5, 0.78, 1.0), cuts=(0.2, 0.5))
    metal_d = toon('HMetalD', METAL_D)
    mask = toon('HMask', MASK)
    glow = emit('HGlow', YELLOW, 1.0)

    joint('root', None, (0, 0, 0.93))
    joint('spine', 'root', (0, 0, 0.05))
    part('torso', 'spine', cube(0.22, 0.3, 0.46, (0, 0, 0.22), 0.03), suit)
    joint('head', 'spine', (0.03, 0, 0.6))
    part('hood', 'head', profile_prism(HOOD, 0.17, bevel=0.03, taper=0.55), cloth)
    part('face', 'head', profile_prism(FACE, 0.125, bevel=0.012), mask)
    part('jaw', 'head', cube(0.07, 0.17, 0.05, (0.15, 0, -0.08), 0.01), metal_d)

    def eye(bm):
        # glowing triangle on the camera side of the mask, pointing forward (concept)
        y = -0.128
        vs = [bm.verts.new(p) for p in ((0.115, y, 0.085), (0.115, y, 0.01), (0.185, y, 0.047))]
        bm.faces.new(vs)
        vs2 = [bm.verts.new((p.co.x, y + 0.002, p.co.z)) for p in vs]
        bm.faces.new(vs2[::-1])
    part('eye', 'head', eye, glow, c='NoLine')
    part('eye_front', 'head', cube(0.012, 0.06, 0.055, (0.197, -0.06, 0.047)), glow, c='NoLine')

    # cloak: upper shell on the spine, lower skirt on its own joint so it can trail
    part('cloak_up', 'spine', frustum(0.52, 0.05, 0.17, 0.22, 0.28, 0.26, seg=10, hem=0.03), cloth)
    part('cowl', 'spine', frustum(0.58, 0.3, 0.15, 0.18, 0.27, 0.3, seg=10, hem=0.08), cloth_d)
    joint('cloak', 'spine', (-0.02, 0, 0.1))
    part('cloak_low', 'cloak', frustum(0.03, -0.55, 0.28, 0.26, 0.36, 0.3, seg=10, hem=0.26, xoff1=-0.05), cloth)

    # legs (far first so the near leg draws in front)
    for side, y in (('far', 0.09), ('near', -0.09)):
        dark = side == 'far'
        joint(f'hip_{side}', 'root', (0, y, 0))
        part(f'thigh_{side}', f'hip_{side}', limb(0.075, 0.06, 0.43), suit)
        joint(f'knee_{side}', f'hip_{side}', (0, 0, -0.43))
        part(f'kneecap_{side}', f'knee_{side}', ico(0.06, (0.02, 0, 0)), metal_d)
        part(f'shin_{side}', f'knee_{side}', limb(0.06, 0.05, 0.41), suit)
        joint(f'ankle_{side}', f'knee_{side}', (0, 0, -0.42))
        part(f'boot_{side}', f'ankle_{side}', cube(0.22, 0.11, 0.1, (0.05, 0, -0.03), 0.02), metal_d if not dark else suit)

    # arms: far arm is cloth-wrapped, near arm is the bionic one
    joint('sh_far', 'spine', (0.0, 0.19, 0.45))
    part('upper_far', 'sh_far', limb(0.06, 0.05, 0.28), cloth_d)
    joint('el_far', 'sh_far', (0, 0, -0.28))
    part('fore_far', 'el_far', limb(0.05, 0.045, 0.25), suit)
    joint('wr_far', 'el_far', (0, 0, -0.25))
    part('hand_far', 'wr_far', cube(0.08, 0.06, 0.1, (0, 0, -0.05), 0.015), suit)

    joint('sh_near', 'spine', (0.0, -0.21, 0.45))
    part('shoulder_near', 'sh_near', ico(0.085, (0, 0, -0.01), (1, 1, 0.9)), metal)
    part('upper_near', 'sh_near', limb(0.065, 0.055, 0.29, seg=6), metal)
    part('strip_up', 'sh_near', cube(0.025, 0.01, 0.13, (0.0, -0.063, -0.15)), glow, c='NoLine')
    part('strip_up2', 'sh_near', cube(0.06, 0.01, 0.018, (0.0, -0.063, -0.05)), glow, c='NoLine')
    joint('el_near', 'sh_near', (0, 0, -0.29))
    part('elbow_near', 'el_near', ico(0.065), metal_d)
    part('fore_near', 'el_near', limb(0.06, 0.05, 0.27, seg=6), metal)
    part('strip_fo', 'el_near', cube(0.04, 0.01, 0.018, (0.0, -0.058, -0.1)), glow, c='NoLine')
    part('strip_fo2', 'el_near', cube(0.04, 0.01, 0.018, (0.0, -0.058, -0.14)), glow, c='NoLine')
    joint('wr_near', 'el_near', (0, 0, -0.27))
    part('wrist_near', 'wr_near', ico(0.045), metal_d)
    part('palm_near', 'wr_near', cube(0.1, 0.05, 0.1, (0.01, 0, -0.06), 0.015), metal)
    for i, fx in enumerate((-0.035, -0.01, 0.015, 0.04)):
        joint(f'fing{i}', 'wr_near', (fx, 0, -0.11))
        part(f'finger{i}', f'fing{i}', limb(0.013, 0.011, 0.08, seg=5), metal_d)
    joint('thumb', 'wr_near', (0.05, -0.02, -0.06))
    part('thumbp', 'thumb', limb(0.014, 0.012, 0.06, seg=5), metal_d)
    return J


# ------------------------------------------------------------------ posing
def rot(name, fwd=0.0, side=0.0, twist=0.0):
    """fwd: swing forward (+) in the run plane, degrees."""
    J[name].rotation_euler = (math.radians(side), math.radians(-fwd), math.radians(twist))


def neutral():
    for n in J:
        if n != 'root':
            J[n].rotation_euler = (0, 0, 0)
    J['root'].location = (0, 0, 0.93)
    J['root'].rotation_euler = (0, 0, math.radians(-22))   # 3/4 turn towards the camera
    for i in range(4):
        rot(f'fing{i}', 25)
    rot('thumb', 20, side=-20)


def leg(side, thigh, knee, foot=None):
    rot(f'hip_{side}', thigh)
    rot(f'knee_{side}', -knee)
    rot(f'ankle_{side}', -(thigh - knee) if foot is None else foot)


def arm(side, shoulder, elbow, wrist=0.0, out=0.0):
    rot(f'sh_{side}', shoulder, side=out)
    rot(f'el_{side}', elbow)
    rot(f'wr_{side}', wrist)


def lean(spine_fwd, head=0.0, cloak=0.0, drop=0.0):
    rot('spine', spine_fwd)
    rot('head', head - spine_fwd * 0.5)
    rot('cloak', -cloak)
    J['root'].location.z = 0.93 - drop


def pose_run(t):
    neutral()
    ph = 2 * math.pi * t
    for side, p in (('near', ph), ('far', ph + math.pi)):
        thigh = 34 * math.sin(p) + 6
        swing = max(0.0, math.cos(p))
        knee = 12 + 78 * swing ** 1.4 + (18 if math.sin(p) < -0.3 else 0)
        foot = -thigh + knee * 0.8 - (20 if math.sin(p) < -0.6 else 0)
        leg(side, thigh, knee, foot)
    arm('near', -30 * math.sin(ph) + 10, 70 + 15 * math.sin(ph), 10)
    arm('far', 30 * math.sin(ph) + 10, 70 - 15 * math.sin(ph), 10)
    bob = 0.035 * (1 - abs(math.cos(ph)))
    lean(14, head=4, cloak=22 + 10 * math.sin(2 * ph), drop=0.05 - bob)


def pose_idle(t):
    neutral()
    b = math.sin(2 * math.pi * t)
    leg('near', 6, 8)
    leg('far', -6, 6)
    arm('near', 8, 22 + 3 * b, 5)
    arm('far', 4, 16 + 3 * b, 5)
    lean(2 + 1.5 * b, head=-2, cloak=4 + 2 * b, drop=0.01 * (1 + b))


def pose_rise():
    neutral()
    leg('near', 70, 100)
    leg('far', -18, 55)
    arm('near', 150, 25)
    arm('far', -40, 40)
    lean(-4, head=6, cloak=40, drop=0.0)


def pose_apex():
    neutral()
    leg('near', 55, 95)
    leg('far', 25, 90)
    arm('near', 95, 40)
    arm('far', 60, 70)
    lean(8, head=2, cloak=55, drop=0.0)


def pose_fall():
    neutral()
    leg('near', 25, 30)
    leg('far', -8, 45)
    arm('near', 125, 30, out=-25)
    arm('far', 110, 40)
    lean(-6, head=-4, cloak=45, drop=0.0)


def pose_land():
    neutral()
    leg('near', 75, 120)
    leg('far', 40, 115)
    arm('near', 45, 60)
    arm('far', -20, 50)
    lean(32, head=-14, cloak=10, drop=0.25)


def pose_hang():
    neutral()
    arm('near', 172, 6, -6)
    arm('far', 20, 40)
    leg('near', 12, 25)
    leg('far', -6, 18)
    lean(0, head=10, cloak=2, drop=0.0)


def pose_swing_fwd():
    neutral()
    arm('near', 168, 4)
    arm('far', -25, 30)
    leg('near', 55, 40)
    leg('far', 38, 30)
    lean(-10, head=14, cloak=-25, drop=0.0)


def pose_swing_back():
    neutral()
    arm('near', 176, 8)
    arm('far', 50, 45)
    leg('near', -20, 50)
    leg('far', -30, 70)
    lean(10, head=6, cloak=45, drop=0.0)


def pose_reach():
    neutral()
    arm('near', 118, 2, -5)
    for i in range(4):
        rot(f'fing{i}', -5)
    arm('far', -30, 45)
    leg('near', 30, 60)
    leg('far', -10, 40)
    lean(4, head=10, cloak=35, drop=0.0)


def pose_death():
    neutral()
    arm('near', 150, 20, out=-40)
    arm('far', 130, 30, out=30)
    leg('near', 60, 20)
    leg('far', -30, 80)
    lean(-20, head=20, cloak=70, drop=0.0)


POSES = []
for i in range(10):
    POSES.append((f'run_{i:02d}', lambda i=i: pose_run(i / 10)))
for i in range(6):
    POSES.append((f'idle_{i:02d}', lambda i=i: pose_idle(i / 6)))
POSES += [('rise_00', pose_rise), ('apex_00', pose_apex), ('fall_00', pose_fall), ('land_00', pose_land),
          ('hang_00', pose_hang), ('swingf_00', pose_swing_fwd), ('swingb_00', pose_swing_back),
          ('reach_00', pose_reach), ('death_00', pose_death)]

CELL_W, CELL_H, FEET = 2.2, 2.4, 0.3


def hero_light():
    key = bpy.data.objects['Key']
    # light travels towards -X (comes from in front of the runner), downwards, and away from the camera
    d = Vector((-0.55, 0.45, -0.7)).normalized()
    key.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    key.data.energy = 3.4
    bpy.context.scene.view_layers[0].freestyle_settings.crease_angle = math.radians(150)     # frame in world units; feet sit FEET above the bottom edge
HERO_PPU = 160


def render_frames(names=None, sheet=True):
    build()
    setup_render(CELL_W, CELL_H, HERO_PPU, -CELL_W / 2, -FEET, line=2.0, samples=16)
    hero_light()
    os.makedirs(HERO_DIR, exist_ok=True)
    out = []
    for name, fn in POSES:
        if names and name not in names:
            continue
        fn()
        bpy.context.view_layer.update()
        out.append(render(name + '.png', glow_kw=dict(threshold=0.7, radii=(3, 8), strength=(.5, .25), wrap_x=False), folder=HERO_DIR))
    if sheet:
        contact(out, ROOT + r"\output\hero_sheet.png")
    return out


def contact(paths, dst, cols=9, cell=260):
    rows = (len(paths) + cols - 1) // cols
    sheet = np.zeros((rows * cell, cols * cell, 3), np.float32)
    sheet[:] = (0.13, 0.1, 0.2)
    for i, p in enumerate(paths):
        a = _load_np(p)
        h, w = a.shape[:2]
        s = min(cell / w, cell / h)
        nh, nw = int(h * s), int(w * s)
        yi = (np.arange(nh) / s).astype(int)
        xi = (np.arange(nw) / s).astype(int)
        r = a[yi][:, xi]
        r0 = (rows - 1 - i // cols) * cell
        c0 = (i % cols) * cell
        al = r[..., 3:4]
        sheet[r0:r0 + nh, c0:c0 + nw] = sheet[r0:r0 + nh, c0:c0 + nw] * (1 - al) + r[..., :3] * al
    img = bpy.data.images.new('hs', sheet.shape[1], sheet.shape[0], alpha=True)
    img.pixels.foreach_set(np.concatenate([sheet, np.ones(sheet.shape[:2] + (1,), np.float32)], 2).ravel())
    img.filepath_raw = dst
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


if 'HERO_ONLY' in globals():
    if 'build' in HERO_ONLY:
        build(); neutral(); pose_idle(0)
    if 'sheet' in HERO_ONLY:
        render_frames()
