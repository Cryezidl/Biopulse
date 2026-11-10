# Level 1 — underground lab, NII-punk style. Strip layout matches NeonLevelBuilder.Underground:
#   Lab_Back  40 x 10.4 u @64 ppu (opaque, parallax .78), Lab_Mid 40 x 10.4 @80 (parallax .5),
#   Lab_Front 40 x 5 @80 (top band, parallax -.35), Floor_Lab 4 x 2 @160 (top edge = y 0).
# Run: ONLY = ['mid']; exec(open(r"...\loc_underground.py", encoding="utf-8").read())
import random, math
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\sols_kit.py", encoding="utf-8").read(), globals())

W, H, Z0 = 40.0, 10.4, -1.0
CEIL = 8.4
OVERLAYS = []   # animated decor for Unity (layer-local coords), written next to the strip as JSON

# Palette (display sRGB). Dominant deep teal, support = cold metal, accent = toxic lime (tanks only), warm lamps rare.
C = dict(
    deep=(0.03, 0.06, 0.07), dark=(0.07, 0.13, 0.14), wall=(0.12, 0.215, 0.22), wall2=(0.145, 0.25, 0.25),
    light=(0.3, 0.42, 0.39), metal=(0.2, 0.3, 0.3), haze=(0.17, 0.33, 0.31),
    screen=(0.42, 1.0, 0.78), lamp=(1.0, 0.74, 0.42), toxic=(0.7, 1.0, 0.3), ink=(0.015, 0.05, 0.055),
)


def palette(k=1.0):
    """Materials for one depth band; k scales value (mid layer slightly darker than gameplay)."""
    s = lambda c: tuple(min(1, v * k) for v in c)
    return Pal(
        wall=s(C['wall']), screen=C['screen'], lamp=C['lamp'],
        m_wall=sol('Wall', s(C['wall'])), m_wall2=sol('Wall2', s(C['wall2'])), m_light=sol('Light', s(C['light'])),
        m_dark=sol('Dark', s(C['dark'])), m_deep=sol('Deep', s(C['deep']), lights=False),
        m_metal=sol('Metal', s(C['metal']), pat='hatch', pat_scale=0.8, pat_k=0.18),
        m_seam=sol('WallSeam', s(C['wall']), pat='seam', pat_scale=0.3, pat_k=0.75),
        m_hatch=sol('WallHatch', s(C['wall2']), pat='hatch', pat_scale=0.45, pat_k=0.55),
        m_glyph=sol('DeepGlyph', s(C['dark']), pat='glyph', pat_scale=0.24, pat_k=0.85, pat_light=True),
    )


# ---------------------------------------------------------------- set pieces
def pillar(b, p, x, w, y, z1=CEIL):
    """Structural column with stacked collars — rhythm of the hall."""
    plate(b, p.m_dark, x - w / 2, x + w / 2, 0, z1, y, 0.6, 0.1)
    plate(b, p.m_hatch, x - w / 2 + 0.12, x + w / 2 - 0.12, 0.2, z1 - 0.2, y - 0.08, 0.2, 0.06)
    for z in (0.0, 2.6, 5.4):
        plate(b, p.m_light, x - w / 2 - 0.1, x + w / 2 + 0.1, z, z + 0.35, y - 0.15, 0.7, 0.06, cut=0.12, corners=(1, 1, 0, 0))
        plate(b, p.m_dark, x - w / 2 + 0.05, x + w / 2 - 0.05, z + 0.35, z + 0.5, y - 0.12, 0.6, 0.03)
        rivets(b, p, x - w / 2 + 0.05, x + w / 2 - 0.05, z + 0.18, y - 0.16, step=0.2)
    slats(b, p.m_dark, x - w / 2 + 0.25, x + w / 2 - 0.25, 3.3, 4.9, y - 0.12)
    plate(b, p.m_wall2, x - w / 2 - 0.25, x + w / 2 + 0.25, z1 - 0.9, z1 - 0.3, y - 0.2, 0.8, 0.06, cut=0.3, corners=(1, 1, 0, 0))   # capital


def control_bay(b, p, x, y, rng):
    """The hero set piece: a big EMG monitor wall over a sloped desk; main teal key light."""
    sw, sh, sz = 5.2, 2.7, 3.3
    # surround
    plate(b, p.m_wall2, x - sw / 2 - 0.6, x + sw / 2 + 0.6, sz - 0.6, sz + sh + 0.7, y + 0.1, 0.3, 0.1)
    plate(b, p.m_dark, x - sw / 2 - 0.25, x + sw / 2 + 0.25, sz - 0.25, sz + sh + 0.25, y, 0.2, 0.06)
    face = mix(C['screen'], (0, 0, 0), 0.55)
    b.quad(glowm('BigFace', face), x - sw / 2, x + sw / 2, sz, sz + sh, y=y - 0.01, c='NoLine')
    # two EMG lanes (flexor / extensor) + labels — the game's own motif
    screen_glyphs(b, x - sw / 2 + 0.2, x + sw / 2 - 0.2, sz + sh * 0.12, sz + sh * 0.88, y - 0.02, C['screen'], rng, 'emg2')
    g = glowm(f'Glyph{C["screen"]}', C['screen'])
    text('С', g, x - sw / 2 + 0.35, sz + sh * 0.7, 0.32, y=y - 0.03, c='NoLine')
    text('Р', g, x - sw / 2 + 0.35, sz + sh * 0.2, 0.32, y=y - 0.03, c='NoLine')
    light(x, sz + sh / 2 - 0.4, 5.2, C['screen'], 1.1)
    # side cabinets with lamp grids
    for sx in (-1, 1):
        cx = x + sx * (sw / 2 + 1.25)
        plate(b, p.m_wall, cx - 0.6, cx + 0.6, 0, 2.9, y - 0.4, 0.5, 0.08)
        mod_lamps(b, p, (cx - 0.45, cx + 0.45, 1.6, 2.6), y - 0.48, rng)
        slats(b, p.m_dark, cx - 0.45, cx + 0.45, 0.25, 1.3, y - 0.47)
    # sloped console desk
    d0 = y - 0.9
    b.prism(p.m_wall2, [(x - sw / 2, 0), (x + sw / 2, 0), (x + sw / 2, 1.05), (x + sw / 2 - 0.3, 1.45), (x - sw / 2 + 0.3, 1.45), (x - sw / 2, 1.05)], d0, d0 + 0.8)
    b.box(p.m_light, x - sw / 2 - 0.05, x + sw / 2 + 0.05, 1.42, 1.55, d0 - 0.05, d0 + 0.8, bevel=0.03)
    for i in range(4):
        kx = x - sw / 2 + 0.6 + i * (sw - 1.2) / 3
        screen(b, p, kx - 0.42, kx + 0.42, 0.55, 1.0, d0 - 0.02, rng, kind=rng.choice(['bars', 'rows']), power=0.35)
    # cable spaghetti from the monitor to the ceiling
    for i in range(5):
        cx = x - sw / 2 + 0.4 + i * 0.25
        cable(b, p.m_deep, cx, sz + sh + 0.6, cx - 1.5 + i * 0.4, CEIL, -0.4 - i * 0.1, 0.04, y + 0.05)


def tank(b, p, x, w, h, y, rng, specimen=None):
    """Glass culture column with glowing toxic medium — the lime accent of the level."""
    tz0 = 0.9
    plate(b, p.m_dark, x - w / 2 - 0.25, x + w / 2 + 0.25, 0, tz0, y - 0.1, 0.9, 0.08)
    mod_lamps(b, p, (x - w / 2, x + w / 2, 0.25, 0.6), y - 0.18, rng)
    liquid_top = tz0 + h - 0.45
    b.quad(gradient_mat(f'Liquid', mix(C['toxic'], C['deep'], 0.25), mix(C['toxic'], C['deep'], 0.75), tz0, liquid_top, cat='liquid'),
           x - w / 2, x + w / 2, tz0, liquid_top, y=y + 0.3, c='NoLine')
    b.quad(flat('Air', mix(C['toxic'], C['deep'], 0.82)), x - w / 2, x + w / 2, liquid_top, tz0 + h, y=y + 0.31, c='NoLine')
    b.quad(flat('Meniscus', mix(C['toxic'], (1, 1, 1), 0.3), cat='liquid'), x - w / 2, x + w / 2, liquid_top - 0.04, liquid_top + 0.02, y=y + 0.29, c='NoLine')
    b.quad(flat('Shine', (0.85, 1, 0.9), 0.28), x - w / 2 + 0.12, x - w / 2 + 0.22, tz0 + 0.3, tz0 + h - 0.3, y=y + 0.05, c='NoLine')
    for _ in range(int(h * 4)):
        bx, bz = rng.uniform(x - w / 2 + 0.2, x + w / 2 - 0.2), rng.uniform(tz0 + 0.2, liquid_top - 0.2)
        b.disc(flat('Bubble', mix(C['toxic'], (1, 1, 1), 0.4), 0.7, cat='liquid'), bx, bz, rng.uniform(0.03, 0.07), y=y + 0.2, seg=10, c='NoLine')
    OVERLAYS.append(dict(kind='bubbles', x=x, z=tz0 + 0.2, w=w * 0.8, h=liquid_top - tz0 - 0.3))
    if specimen:
        OVERLAYS.append(dict(kind='specimen_' + specimen, x=x, z=tz0 + h * 0.45, w=w, h=h))
    # frame: rings + 3 rods
    for sx in (-1, 0, 1):
        b.box(p.m_metal, x + sx * w / 2 - 0.05, x + sx * w / 2 + 0.05, tz0, tz0 + h, y - 0.05, y + 0.1) if sx else None
    plate(b, p.m_wall2, x - w / 2 - 0.2, x + w / 2 + 0.2, tz0 + h, tz0 + h + 0.4, y - 0.15, 0.9, 0.08)
    plate(b, p.m_light, x - w / 2 - 0.12, x + w / 2 + 0.12, tz0 - 0.06, tz0 + 0.1, y - 0.15, 0.9, 0.03)
    pipe_v(b, p.m_metal, x, tz0 + h + 0.4, CEIL, 0.16, y + 0.2, p.m_dark, 1.6)
    light(x, tz0 + h * 0.45, w + 2.4, C['toxic'], 1.1)


def manipulator(b, p, x, y):
    """Ceiling-mounted industrial arm — echoes the hero's bionic arm."""
    plate(b, p.m_dark, x - 0.9, x + 0.9, CEIL - 0.5, CEIL, y, 0.5, 0.08)
    b.cyl(p.m_metal, x, CEIL - 0.7, 0.32, 0.6, axis='Y', y=y + 0.2, seg=16)
    pts = [(x, CEIL - 0.7), (x + 1.4, CEIL - 2.4), (x + 0.6, CEIL - 4.0)]
    for (ax, az), (bx, bz) in zip(pts[:-1], pts[1:]):
        b.bar(p.m_wall2, ax, az, bx, bz, 0.42, y, y + 0.4)
        b.bar(p.m_dark, ax, az, bx, bz, 0.12, y - 0.04, y - 0.02, c='NoLine')
    for (jx, jz) in pts[1:]:
        b.cyl(p.m_light, jx, jz, 0.26, 0.6, axis='Y', y=y + 0.2, seg=16)
        b.cyl(p.m_dark, jx, jz, 0.1, 0.65, axis='Y', y=y + 0.18, seg=10)
    hx, hz = pts[-1]
    for a in (-0.5, 0.0, 0.5):
        b.bar(p.m_metal, hx, hz, hx + math.sin(a) * 0.6, hz - 0.55, 0.12, y, y + 0.3)
    cable(b, p.m_deep, x + 0.2, CEIL - 0.6, hx + 0.1, hz + 0.2, 0.5, 0.05, y - 0.1)


def ceiling_works(b, p, y, rng):
    """Duct + pipe bundle band under the ceiling slab."""
    plate(b, p.m_dark, -1, W + 1, CEIL - 0.25, CEIL + 2, y + 0.5, 0.5, 0.0)
    pipe_h(b, p.m_metal, -1, W + 1, CEIL - 0.55, 0.2, y + 0.1, p.m_dark, 3.3)
    pipe_h(b, p.m_wall2, -1, W + 1, CEIL - 0.95, 0.12, y + 0.25, p.m_dark, 2.2)
    x = 0.0
    while x < W:
        seg = rng.uniform(3, 6)
        plate(b, p.m_wall, x + 0.05, x + seg - 0.05, CEIL - 1.55, CEIL - 1.1, y - 0.1, 0.5, 0.06)
        x += seg


def lamp(b, p, x, z, y, col):
    """Small caged work-lamp with a soft beam (rare warm accent)."""
    cable(b, p.m_deep, x, CEIL - 1.1, x, z + 0.3, 0.0, 0.02, y)
    b.prism(p.m_dark, [(x - 0.3, z), (x + 0.3, z), (x + 0.12, z + 0.3), (x - 0.12, z + 0.3)], y - 0.1, y + 0.2)
    b.quad(glowm(f'Bulb{col}', col, cat='lamp'), x - 0.2, x + 0.2, z - 0.05, z + 0.01, y=y - 0.12, c='NoLine')
    light_shaft(b, x, z, x, 0.0, 0.4, 3.2, col, 0.22, y - 0.3, n=8)
    light(x, z - 2.2, 3.4, col, 0.7)


def railing(b, p, x0, x1, z, y, h=1.0, post=1.2, c='Main'):
    """Guard rail: posts + two rails + kick plate."""
    plate(b, p.m_dark, x0, x1, z, z + 0.14, y, 0.1, 0.02, c=c)
    for zz in (z + h * 0.55, z + h):
        b.box(p.m_metal, x0, x1, zz - 0.05, zz + 0.05, y, y + 0.08, bevel=0.02, c=c)
    x = x0
    while x <= x1 + 1e-3:
        b.box(p.m_metal, x - 0.05, x + 0.05, z, z + h + 0.05, y - 0.02, y + 0.1, bevel=0.02, c=c)
        x += post


def catwalk(b, p, x0, x1, z, y, rng):
    plate(b, p.m_dark, x0, x1, z - 0.3, z, y, 0.8, 0.04)
    x = x0 + 0.3
    while x < x1:
        b.box(p.m_deep, x, x + 0.06, z - 0.26, z - 0.04, y - 0.02, y, c='NoLine')
        x += 0.25
    railing(b, p, x0, x1, z, y - 0.05, 0.9, 1.0)
    for hx in (x0 + 0.8, x1 - 0.8):
        b.box(p.m_metal, hx - 0.03, hx + 0.03, z, CEIL - 1.1, y + 0.3, y + 0.35)   # hangers


# ---------------------------------------------------------------- layers
def mid():
    new_scene()
    style(haze=C['haze'], haze_near=-1.0, haze_far=6.0, haze_max=0.16, ink=C['ink'], dark_top=(2.5, 8.0, 0.42), light_gain=3.2)
    rng = random.Random(7)
    OVERLAYS.clear()
    setup_render(W, H, 80, 0, Z0, line=1.6)
    ink(1.7)
    b = Batch()
    p = palette(0.72)
    for ux in (2.0, 12.0, 19.0, 25.0, 31.0, 38.0):   # floor-level uplight pools (machines lit from below)
        light(ux, -0.6, 3.6, (0.35, 0.95, 0.75), 0.55)
    # cathedral struts framing the two set pieces (diagonals = the big shapes of the reference)
    buttress(b, p, 4.6, CEIL - 1.55, 4.6, 1.7, 2.0, side=1)
    buttress(b, p, 14.9, CEIL - 1.55, 4.6, 1.7, 2.0, side=-1)
    buttress(b, p, 27.6, CEIL - 1.55, 5.2, 1.4, 2.0, side=1)
    buttress(b, p, 34.9, CEIL - 1.55, 5.2, 1.4, 2.0, side=-1)

    # wall bays between pillars; two bays are OPEN onto the great hall (back layer) for depth
    machine_wall(b, p, 21.6, 26.4, 0.0, CEIL - 1.6, 3.0, rng, min_size=0.6, quiet=0.1)
    for px in (4.0, 15.5, 21.0, 27.0, 35.5):
        pillar(b, p, px, 1.2, 2.2)
    # set pieces
    machine_wall(b, p, 4.6, 14.9, 0, CEIL - 1.6, 3.4, rng, min_size=0.75, quiet=0.15)   # quiet backing behind monitor
    control_bay(b, p, 9.75, 2.6, rng)
    machine_wall(b, p, 27.6, 34.9, 4.6, CEIL - 1.6, 3.0, rng, min_size=0.6, quiet=0.1)
    plate(b, p.m_glyph, 27.6, 34.9, 0, 4.6, 3.5, 0.2, 0.0)                                    # engraved alcove behind tanks
    tank(b, p, 29.6, 1.5, 3.8, 1.6, rng, specimen='hand')
    tank(b, p, 32.6, 1.3, 3.2, 1.9, rng)
    for i, x in enumerate((28.2, 31.1, 34.2)):
        pipe_v(b, p.m_metal, x, 0, CEIL, 0.1 + 0.03 * i, 2.4, p.m_dark, 1.3)
    # open bays: railing at the floor edge, a catwalk across, hanging cables
    railing(b, p, 16.1, 20.4, 0.0, 1.6, 1.05)
    railing(b, p, 36.1, 43.4, 0.0, 1.6, 1.05)
    catwalk(b, p, 36.1, 43.4, 4.3, 2.6, rng)
    manipulator(b, p, 18.2, 1.8)
    for (x0, x1, sag) in ((15.9, 20.6, 1.6), (16.4, 19.6, 2.4), (36.0, 39.8, 1.2), (0.3, 3.6, 1.9)):
        cable(b, p.m_deep, x0, CEIL - 1.1, x1, CEIL - 1.1, sag, 0.05, 1.4)
    ceiling_works(b, p, 1.2, rng)
    for (x, w, h) in ((22.4, 1.1, 2.2), (23.6, 1.4, 1.2)):
        plate(b, p.m_wall2, x - w / 2, x + w / 2, 0, h, 1.0, 0.8, 0.08)
        mod_vent(b, p, (x - w / 2 + 0.15, x + w / 2 - 0.15, 0.2, h * 0.55), 0.96, rng)
        bolts(b, p.m_dark, x - w / 2, x + w / 2, 0, h, 0.97)
    sign(b, 'КОРПУС 7', 24.0, 5.2, 0.36, p.m_light, sol('Ink', C['deep'], lights=False), 2.9)
    sign(b, 'ЛАБОРАТОРИЯ БИОНИКИ', 31.1, 5.7, 0.26, p.m_light, sol('Ink', C['deep'], lights=False), 1.0)
    lamp(b, p, 24.2, 4.8, 0.8, C['lamp'])
    OVERLAYS.append(dict(kind='steam', x=22.4, z=2.2, w=0.4, h=0))
    OVERLAYS.append(dict(kind='steam', x=37.0, z=4.4, w=0.4, h=0))
    OVERLAYS.append(dict(kind='spark', x=18.8, z=CEIL - 4.4, w=0.2, h=0))
    finalize_lights(wrap=W)
    b.build(wrap_width=W)
    render('U_Mid.png', glow_kw=dict(threshold=0.6, radii=(4, 16), strength=(.45, .35)))
    write_overlays('U_Mid', W)


def back():
    """The great hall seen through the openings: huge hazy forms, no ink, light shafts from ceiling grates."""
    new_scene()
    hz = (0.2, 0.4, 0.37)
    style(haze=hz, haze_near=0.0, haze_far=30.0, haze_max=0.6, dark_top=(5.0, 9.4, 0.5), light_gain=1.8)
    rng = random.Random(11)
    setup_render(W, H, 64, 0, Z0, line=0)
    ink(0)
    b = Batch()
    p = palette(0.8)
    b.quad(gradient_mat('Air', (0.12, 0.25, 0.24), (0.03, 0.07, 0.08), Z0, H + Z0), -2, W + 2, Z0, H + Z0, y=30, c='NoLine')
    plate(b, p.m_glyph, -1, W + 1, -1, 12, 14.0, 0.2, 0.0)
    machine_wall(b, p, 0, W, -1, 2.2, 12.0, rng, min_size=1.2, quiet=0.3)
    for x in (3.0, 13.5, 24.0, 33.0):
        buttress(b, p, x + 0.9, 10.5, 3.0, 3.6, 7.0, side=1, w=1.4)
        buttress(b, p, x - 0.9, 10.5, 3.0, 3.6, 7.0, side=-1, w=1.4)
    for x in (3.0, 13.5, 24.0, 33.0):
        plate(b, p.m_dark, x - 0.9, x + 0.9, -1, 12, 6.0, 1.0, 0.12)
        for z in (1.5, 5.0, 8.5):
            plate(b, p.m_wall2, x - 1.05, x + 1.05, z, z + 0.4, 5.8, 1.2, 0.08)
    for (x, r, h) in ((8.0, 1.6, 6.5), (19.0, 2.1, 7.5), (29.0, 1.4, 5.2), (37.5, 1.8, 6.8)):
        b.cyl(p.m_wall, x, -1 + h / 2, r, h, axis='Z', y=8.0, seg=24)
        for z in range(1, int(h), 2):
            b.cyl(p.m_dark, x, -1 + z, r * 1.04, 0.18, axis='Z', y=8.0, seg=24)
        b.cyl(p.m_metal, x, -1 + h + 0.3, r * 0.4, 0.6, axis='Z', y=8.0, seg=16)
        pipe_v(b, p.m_dark, x, -1 + h + 0.6, 12, r * 0.25, 8.2)
    for (z, y) in ((3.2, 4.0), (6.4, 7.0)):
        plate(b, p.m_dark, -1, W + 1, z - 0.25, z, y, 0.6, 0.03)
        x = 0.0
        while x < W:
            b.box(p.m_metal, x - 0.04, x + 0.04, z, z + 0.8, y - 0.02, y + 0.05)
            x += 1.0
        b.box(p.m_metal, -1, W + 1, z + 0.75, z + 0.82, y - 0.02, y + 0.05)
    for x in (6.0, 21.0, 31.5):
        b.quad(glowm('Grate', (0.55, 0.95, 0.85)), x - 1.0, x + 1.0, 9.0, 9.25, y=3.0, c='NoLine')
        light_shaft(b, x, 9.2, x - 1.6, -1.0, 1.9, 5.5, (0.55, 0.95, 0.85), 0.3, 2.0, n=10)
        light(x - 0.8, 4.0, 5.0, (0.5, 0.9, 0.8), 0.5)
    for _ in range(14):
        x, z = rng.uniform(0, W), rng.choice([3.4, 3.4, 6.6, 6.6, 1.2])
        b.disc(glowm('Far lamp', (0.9, 0.75, 0.5)), x, z + 0.2, 0.05, y=3.9, seg=8, c='NoLine')
    finalize_lights(wrap=W)
    b.build(wrap_width=W)
    render('U_Back.png', glow_kw=dict(threshold=0.55, radii=(6, 24), strength=(.4, .45), opaque=True))


def front():
    """Top frame band (parallax -0.35): near-black silhouettes hanging into the view."""
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    rng = random.Random(5)
    Hf = 5.0
    setup_render(W, Hf, 80, 0, 0, line=0)
    ink(0)
    b = Batch()
    sil = sol('Sil', (0.02, 0.045, 0.05), lit=(0.07, 0.13, 0.13), dark=(0.012, 0.03, 0.035), lights=False)
    rim = sol('SilRim', (0.05, 0.1, 0.1), lights=False)
    plate(b, sil, -1, W + 1, Hf - 0.7, Hf + 0.5, 0, 0.6, 0.0)
    x = 0.0
    while x < W:
        seg = rng.uniform(4, 9)
        drop = rng.uniform(0.2, 0.7)
        plate(b, sil, x, x + seg - 0.3, Hf - 0.7 - drop, Hf - 0.6, -0.1, 0.6, 0.08)
        b.box(rim, x + 0.2, x + seg - 0.5, Hf - 0.75 - drop, Hf - 0.68 - drop, -0.2, -0.1)
        x += seg
    for x in (5.0, 19.0, 31.0):
        b.cyl(sil, x, Hf - 1.4, 0.55, 2.0, axis='Z', y=-0.3, seg=20)
        b.cyl(rim, x, Hf - 2.4, 0.62, 0.2, axis='Z', y=-0.3, seg=20)
    for (x0, x1, sag, r) in ((1.0, 8.5, 2.6, 0.08), (2.0, 6.0, 1.6, 0.05), (12.0, 17.0, 2.2, 0.07), (22.0, 30.0, 3.0, 0.09),
                            (25.0, 28.5, 1.4, 0.05), (33.0, 39.5, 2.0, 0.07)):
        cable(b, sil, x0, Hf - 0.8, x1, Hf - 0.8, sag, r, -0.4)
    cable(b, sil, 14.5, Hf - 0.8, 14.5, Hf - 2.6, 0.0, 0.03, -0.4)
    b.prism(sil, [(14.1, Hf - 2.9), (14.9, Hf - 2.9), (14.7, Hf - 2.6), (14.3, Hf - 2.6)], -0.5, -0.3)
    b.quad(glowm('FrontBulb', (1.0, 0.75, 0.45)), 14.25, 14.75, Hf - 2.95, Hf - 2.88, y=-0.55, c='NoLine')
    b.build(wrap_width=W)
    render('U_Front.png', glow_kw=dict(threshold=0.6, radii=(4, 12), strength=(.6, .3)))


def floor():
    """Walkable floor tile 4 x 2 (top edge = y 0). Bright lip reads the walking line; body is dark slab."""
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    Wf, Hf = 4.0, 2.0
    setup_render(Wf, Hf, 160, 0, -Hf, line=0)
    ink(2.0, color=(0.01, 0.03, 0.035), calligraphy=False)
    b = Batch()
    lip = sol('Lip', (0.2, 0.32, 0.3), lit=(0.5, 0.68, 0.6), lights=False)
    slab = sol('Slab', (0.045, 0.085, 0.09), lights=False)
    deep = sol('SlabDeep', (0.03, 0.06, 0.065), lights=False)
    b.quad(deep, -0.5, Wf + 0.5, -Hf - 0.5, 0, y=1.0, c='NoLine')
    plate(b, lip, -0.2, Wf + 0.2, -0.16, 0.0, 0.0, 0.3, 0.035)
    for i in range(2):
        x0 = i * 2.0
        plate(b, slab, x0 + 0.04, x0 + 1.96, -0.95, -0.2, 0.05, 0.3, 0.05)
        for k in range(5):
            sx = x0 + 0.35 + k * 0.28
            b.box(deep, sx, sx + 0.12, -0.75, -0.42, 0.0, 0.05, c='NoLine')
        plate(b, slab, x0 + 0.04, x0 + 1.96, -1.95, -1.0, 0.1, 0.3, 0.05)
    b.build(wrap_width=Wf)
    render('U_Floor.png', glow_kw=None)


def specimen():
    """Prototype bionic hand floating in the tank (animated overlay). 1.4 x 2.6 u, centre pivot."""
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    setup_render(1.4, 2.6, 120, -0.7, -1.3, line=0)
    ink(0)
    b = Batch()
    body = flat('SpBody', mix(C['toxic'], C['deep'], 0.66))
    rim = flat('SpRim', mix(C['toxic'], (1, 1, 1), 0.1))
    dark = flat('SpDark', mix(C['toxic'], C['deep'], 0.82))
    def seg(ax, az, bx, bz, w, mat, y):
        b.bar(mat, ax, az, bx, bz, w, y, y + 0.01, c='NoLine')
    # forearm with ribbed sleeve + wrist ring
    b.prism(body, [(-0.13, -1.05), (0.13, -1.05), (0.15, -0.2), (-0.15, -0.2)], 0.0, 0.01, c='NoLine')
    for k in range(5):
        z = -0.95 + k * 0.16
        b.prism(dark, [(-0.14, z), (0.14, z), (0.14, z + 0.05), (-0.14, z + 0.05)], -0.01, 0.0, c='NoLine')
    b.prism(rim, [(0.1, -1.05), (0.13, -1.05), (0.15, -0.2), (0.12, -0.2)], -0.02, -0.01, c='NoLine')
    b.ngon(dark, 0, -0.17, 0.17, 16, -0.015, 0.0, c='NoLine')
    # palm
    b.prism(body, [(-0.2, -0.12), (0.2, -0.12), (0.22, 0.25), (-0.22, 0.25)], -0.02, -0.01, c='NoLine')
    b.prism(rim, [(0.17, -0.12), (0.2, -0.12), (0.22, 0.25), (0.19, 0.25)], -0.03, -0.02, c='NoLine')
    # fingers: two phalanges each, slightly curled, joint dots
    for i, (fx, L, curl) in enumerate(((-0.165, 0.36, 0.25), (-0.055, 0.42, 0.15), (0.055, 0.4, 0.2), (0.16, 0.32, 0.35))):
        ax, az = fx, 0.27
        mx, mz = ax + math.sin(curl * 0.4) * L * 0.55, az + math.cos(curl * 0.4) * L * 0.55
        ex, ez = mx + math.sin(curl * 1.3) * L * 0.45, mz + math.cos(curl * 1.3) * L * 0.45
        seg(ax, az, mx, mz, 0.075, body, -0.03)
        seg(mx, mz, ex, ez, 0.065, body, -0.03)
        b.disc(dark, mx, mz, 0.03, y=-0.045, seg=10, c='NoLine')
    seg(-0.2, 0.0, -0.36, 0.22, 0.08, body, -0.03)     # thumb
    seg(-0.36, 0.22, -0.42, 0.38, 0.07, body, -0.03)
    # tethers hanging from the forearm
    for k, (dx, sag) in enumerate(((-0.08, 0.1), (0.0, 0.18), (0.09, 0.12))):
        b.tube(dark, catenary(dx, -1.05, dx * 3, -1.3, sag, n=8), 0.012, y=0.02, seg=5, c='NoLine')
    b.build()
    render('Specimen_Hand.png', glow_kw=None)


def write_overlays(name, wrap):
    if MASK_MODE:
        return
    import json
    with open(os.path.join(SPRITES, name + '_overlays.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(wrap=wrap, z0=Z0, items=OVERLAYS), f)


JOBS = dict(mid=mid, back=back, front=front, floor=floor, specimen=specimen)
for job in globals().get('ONLY', list(JOBS)):
    with_mask(JOBS[job]) if job in ('mid', 'back') else JOBS[job]()
