# MOP-3000, the grumpy cleaning drone. Three sprites: body (2.2x2.2, centred), eye (0.6x0.6, tinted in game),
# mop (3x3, grip at centre so it can swing around the pivot).
import math, random
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\neon_kit.py", encoding="utf-8").read(), globals())

PPU = 220
CREAM = (0.74, 0.69, 0.56)
CREAM_D = (0.5, 0.46, 0.4)
DARK = (0.13, 0.13, 0.17)
STEEL = (0.38, 0.4, 0.48)
HAZ = (0.98, 0.72, 0.12)
CYAN = (0.3, 0.95, 1.0)
BLUE = (0.25, 0.55, 0.95)
EYE = (0.3, 0.12, 0.65)  # eye socket centre (body space)


def rounded_box(b, mat, x0, x1, z0, z1, y0, y1, r, seg=6):
    """Rounded rectangle profile extruded in depth (one clean silhouette, no inner seams)."""
    pts = []
    for cx, cz, a0 in ((x1 - r, z0 + r, -90), (x1 - r, z1 - r, 0), (x0 + r, z1 - r, 90), (x0 + r, z0 + r, 180)):
        for i in range(seg + 1):
            a = math.radians(a0 + 90 * i / seg)
            pts.append((cx + r * math.cos(a), cz + r * math.sin(a)))
    b.prism(mat, pts, y0, y1)


def body():
    reset()
    setup_render(2.2, 2.2, PPU, -1.1, -1.1, line=2.4)
    key = bpy.data.objects['Key']
    key.rotation_euler = Vector((-0.5, 0.45, -0.75)).normalized().to_track_quat('-Z', 'Y').to_euler()
    b = Batch()
    cream = toon('MCream', CREAM, steps=(0.6, 0.8, 1.0), cuts=(0.2, 0.5))
    cream_d = toon('MCreamD', CREAM_D)
    dark = toon('MDark', DARK)
    steel = toon('MSteel', STEEL)
    haz = toon('MHaz', HAZ)
    # thruster skirt + jet
    b.box(dark, -0.45, 0.45, -0.62, -0.48, -0.3, 0.3, bevel=0.04)
    b.box(steel, -0.32, 0.32, -0.72, -0.62, -0.25, 0.25, bevel=0.02)
    b.quad(emit('Jet', CYAN), -0.24, 0.24, -0.76, -0.72, y=-0.3, c='NoLine')
    for i in range(6):
        k = i / 6
        b.prism(flat(f'JetCone{i}', CYAN, 0.08), [(-0.24 + k * 0.1, -0.74), (0.24 - k * 0.1, -0.74), (0.12 - k * 0.08, -1.05 + k * 0.15), (-0.12 + k * 0.08, -1.05 + k * 0.15)], -0.31 - i * 0.002, -0.309 - i * 0.002, c='NoLine')
    # back spray tank + hose
    b.cyl(toon('MTank', BLUE), -0.58, 0.05, 0.17, 0.62, y=0.15, seg=18)
    b.sphere(toon('MTank', BLUE), -0.58, 0.36, 0.17, y=0.15, sz=0.6, sub=2)
    b.box(steel, -0.62, -0.54, 0.42, 0.52, 0.1, 0.2)
    b.tube(toon('MHose', (0.2, 0.2, 0.26)), [(-0.58, -0.25), (-0.5, -0.45), (-0.1, -0.5), (0.3, -0.42), (0.5, -0.3)], 0.03, y=-0.32)
    # shell
    rounded_box(b, cream, -0.5, 0.5, -0.48, 0.38, -0.3, 0.3, 0.16)
    b.prism(cream, [(-0.46, 0.36), (-0.38, 0.55), (-0.2, 0.66), (0.05, 0.69), (0.28, 0.64), (0.44, 0.5), (0.48, 0.36)], -0.28, 0.28)  # dome
    b.box(cream_d, -0.5, 0.5, -0.12, -0.08, -0.32, -0.3, c='NoLine')          # panel seam
    b.box(haz, -0.51, 0.51, -0.46, -0.3, -0.33, 0.25)
    for i in range(6):
        x = -0.48 + i * 0.18
        b.prism(flat('MHazInk', (0.06, 0.05, 0.03)), [(x, -0.46), (x + 0.07, -0.46), (x + 0.13, -0.3), (x + 0.06, -0.3)], -0.335, -0.334, c='NoLine')
    # visor + eye socket (the glowing eye is a separate sprite on top)
    b.prism(dark, [(-0.02, 0.42), (0.52, 0.42), (0.56, -0.06), (0.03, -0.06)], -0.38, -0.31)
    b.ngon(steel, EYE[0], EYE[1], 0.27, 28, -0.4, -0.3)
    b.ngon(dark, EYE[0], EYE[1], 0.22, 28, -0.41, -0.39)
    # grumpy brow plate
    b.bar(steel, EYE[0] - 0.3, EYE[1] + 0.33, EYE[0] + 0.22, EYE[1] + 0.25, 0.09, -0.45, -0.38)
    # little arm that holds the mop (grip at (0.62, -0.25) * scale in game)
    b.bar(steel, 0.42, -0.15, 0.66, -0.26, 0.08, -0.36, -0.26)
    b.sphere(dark, 0.42, -0.15, 0.06, y=-0.36, sub=1)
    # antenna
    b.box(steel, -0.18, -0.15, 0.7, 1.0, -0.02, 0.02)
    b.sphere(emit('MAnt', (1.0, 0.45, 0.2)), -0.165, 1.03, 0.055, y=-0.03, sub=2, c='NoLine')
    # side decal + status leds
    text('МОП-3000', flat('MDecal', (0.25, 0.22, 0.2)), -0.22, -0.25, 0.085, y=-0.31, c='NoLine')
    for i, col in enumerate(((0.4, 1, 0.45), HAZ, (1, 0.3, 0.25))):
        b.disc(emit(f'MLed{i}', col), -0.4 + i * 0.07, 0.12, 0.022, y=-0.31, seg=10, c='NoLine')
    b.build()
    render('Mop3000_Body.png', glow_kw=dict(threshold=0.85, radii=(3, 10), strength=(.5, .3), wrap_x=False))


def eye():
    reset()
    setup_render(0.6, 0.6, PPU, -0.3, -0.3, line=0)
    b = Batch()
    b.disc(emit('EyeIris', (1, 1, 1)), 0, 0, 0.2, y=0, seg=40)
    b.disc(flat('EyeRing', (0.75, 0.75, 0.75)), 0, 0, 0.15, y=-0.01, seg=40)
    b.disc(flat('EyePupil', (0.05, 0.05, 0.07)), 0.04, 0.0, 0.085, y=-0.02, seg=32)
    b.disc(emit('EyeGlint', (1, 1, 1)), 0.0, 0.06, 0.03, y=-0.03, seg=12)
    b.build()
    render('Mop3000_Eye.png', glow_kw=dict(threshold=0.8, radii=(3, 8), strength=(.4, .2), wrap_x=False))


def mop():
    reset()
    setup_render(3.0, 3.0, PPU, -1.5, -1.5, line=2.2)
    b = Batch()
    rng = random.Random(4)
    handle = toon('MHandle', (0.55, 0.32, 0.18))
    steel = toon('MSteel2', STEEL)
    hx, hz = 0.5, -0.78                        # mop head position (grip at the origin)
    b.bar(handle, 0.0, 0.05, hx, hz + 0.1, 0.07, -0.05, 0.05)
    b.sphere(toon('MGrip', (0.15, 0.15, 0.2)), 0.0, 0.05, 0.06, y=-0.06, sub=1)
    b.box(steel, hx - 0.28, hx + 0.28, hz, hz + 0.12, -0.12, 0.12, bevel=0.02)
    strands = [toon('MStrand', (0.78, 0.84, 0.88), steps=(0.55, 0.8, 1.0)), toon('MStrand2', (0.6, 0.7, 0.78), steps=(0.55, 0.8, 1.0))]
    for i in range(16):
        x = hx - 0.26 + i * 0.035
        L = rng.uniform(0.32, 0.48)
        bend = rng.uniform(-0.12, 0.05)
        pts = [(x, hz), (x + bend * 0.4, hz - L * 0.5), (x + bend, hz - L)]
        b.tube(strands[i % 2], pts, 0.022, y=-0.15 + (i % 3) * 0.01, seg=5)
    for _ in range(5):                           # drips of soapy water
        x = hx + rng.uniform(-0.3, 0.3)
        z = hz - rng.uniform(0.55, 0.85)
        b.sphere(emit('Drop', (0.55, 0.9, 1.0)), x, z, 0.03, y=-0.3, sz=1.4, sub=1, c='NoLine')
    b.build()
    render('Mop3000_Mop.png', glow_kw=dict(threshold=0.75, radii=(3, 8), strength=(.4, .2), wrap_x=False))


for job in globals().get('MOP_ONLY', ['body', 'eye', 'mop']):
    globals()[job]()
