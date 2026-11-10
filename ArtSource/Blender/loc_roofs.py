# Level 3 — rooftops, NII-punk city. Strips (match NeonLevelBuilder.Rooftops):
#   R_Sky 40 x 20 @48 (opaque, bottom -4, parallax .97), R_Far 48 x 14 @80 (bottom -2, .82), R_Mid 48 x 13 @80 (bottom -2.5, .55),
#   R_Front 40 x 5 @80 (top band), R_Floor 4 x 2 @160 (roof edge, top = y 0), R_Wall 4 x 4 @80 (facade below the roof line).
# Run: ONLY = [...]; exec(open(r"...\loc_roofs.py", encoding="utf-8").read())
import random, math
_JOBS_WANTED = globals().get('ONLY')
ONLY = []
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\loc_underground.py", encoding="utf-8").read(), globals())
ONLY = _JOBS_WANTED

# Night violet, warm smog glow on the horizon, warm windows, a few pink/cyan-free neon signs (cyan is reserved for С).
C = dict(
    deep=(0.04, 0.03, 0.07), dark=(0.09, 0.06, 0.13), wall=(0.16, 0.1, 0.2), wall2=(0.2, 0.13, 0.24),
    light=(0.42, 0.3, 0.42), metal=(0.24, 0.18, 0.28), haze=(0.42, 0.22, 0.36),
    screen=(1.0, 0.45, 0.7), lamp=(1.0, 0.72, 0.45), toxic=(1.0, 0.45, 0.7), ink=(0.03, 0.015, 0.04),
    window=(1.0, 0.7, 0.42), neon_pink=(1.0, 0.36, 0.66), neon_amber=(1.0, 0.68, 0.25), neon_red=(1.0, 0.3, 0.25),
    sky_top=(0.05, 0.035, 0.12), sky_mid=(0.2, 0.09, 0.24), horizon=(0.78, 0.36, 0.38),
)
SIGNS = ['АПТЕКА', 'РЕМОНТ ПРОТЕЗОВ', 'ГАСТРОНОМ', '24 ЧАСА', 'ГОСТИНИЦА', 'ПРОКАТ', 'КИНО', 'БИОНИКА']


def neon_sign(b, word, x, z, size, col, y, vertical=False, plate_mat=None):
    """Neon lettering on a dark backing; flickers in game (mask R) and lights the facade."""
    g = glowm(f'Neon{col}', col, cat='screen')
    if vertical:
        n = len(word)
        h = n * size * 1.05
        if plate_mat:
            plate(b, plate_mat, x - size * 0.75, x + size * 0.75, z - h - 0.05, z + size * 0.3, y + 0.05, 0.1, 0.02)
        text(word, g, x, z - size, size, y=y, vertical=True, c='NoLine')
        light(x, z - h / 2, h * 0.7 + 0.8, col, 0.6)
    else:
        w = text_width(word, size)
        if plate_mat:
            plate(b, plate_mat, x - w / 2 - 0.15, x + w / 2 + 0.15, z - 0.12, z + size * 0.85, y + 0.05, 0.1, 0.02)
        text(word, g, x, z, size, y=y, c='NoLine')
        light(x, z + size * 0.4, w * 0.7 + 0.8, col, 0.6)


def windows(b, x0, x1, z0, z1, y, rng, win, dark, fh=0.55, cw=0.5, ww=0.26, wh=0.3, tv=None):
    """Window grid with clustered lit runs (a whole flat lit, a dark stairwell column), not uniform noise."""
    cols = max(1, int((x1 - x0) / cw))
    nf = max(1, int((z1 - z0) / fh))
    stair = rng.randint(0, cols - 1) if cols > 3 else -1
    for f in range(nf):
        run = (rng.randint(0, cols), rng.randint(1, 3)) if rng.random() < 0.4 else (-9, 0)
        for c in range(cols):
            wx = x0 + (x1 - x0) * (c + 0.5) / cols - ww / 2
            wz = z0 + f * fh + (fh - wh) * 0.5
            on = run[0] <= c < run[0] + run[1] or rng.random() < 0.03
            if c == stair:
                on = f % 2 == 0
            mat = win if on else dark
            if on and tv and rng.random() < 0.15:
                mat = tv
            b.quad(mat, wx, wx + ww, wz, wz + wh, y=y - 0.01, c='NoLine')


def panel_house(b, p, x0, w, top, y, rng, win, dark, near=True):
    """Panel apartment block: facade, balconies with glazing, roof clutter, maybe a sign."""
    x1 = x0 + w
    plate(b, p.m_seam if near else p.m_wall, x0, x1, -2.5, top, y, 0.6, 0.05)
    windows(b, x0 + 0.15, x1 - 0.15, -2.4, top - 0.4, y - 0.02, rng, win, dark, tv=glowm('TV', (0.4, 0.42, 0.7), cat='screen'))
    if near:
        fh = 0.55
        z = -2.4 + fh * 2
        bx = x0 + rng.uniform(0.3, max(0.31, w - 1.6))
        while z < top - 0.8:   # stacked balconies (loggias) on one column
            plate(b, p.m_wall2, bx, bx + 1.2, z - 0.06, z + 0.24, y - 0.25, 0.3, 0.03)
            b.quad(flat('BalGlass', (0.9, 0.6, 0.5), 0.12), bx + 0.05, bx + 1.15, z + 0.24, z + 0.5, y=y - 0.24, c='NoLine')
            if rng.random() < 0.3:
                b.sphere(sol('Plant', (0.12, 0.2, 0.12), lights=False), bx + 0.25, z + 0.33, 0.1, y=y - 0.27, sub=1)
            z += fh
    # parapet + roof clutter
    plate(b, p.m_dark, x0 - 0.05, x1 + 0.05, top, top + 0.18, y - 0.05, 0.7, 0.02)
    r = rng.random()
    if r < 0.35:   # water tank on stilts
        tx = x0 + w * rng.uniform(0.25, 0.7)
        for sx in (-0.35, 0.35):
            b.box(p.m_metal, tx + sx - 0.03, tx + sx + 0.03, top + 0.18, top + 0.9, y - 0.1, y)
        plate(b, p.m_wall2, tx - 0.5, tx + 0.5, top + 0.9, top + 1.7, y - 0.15, 0.6, 0.06, cut=0.2, corners=(0, 0, 1, 1))
    elif r < 0.65:   # antennas + dish
        for k in range(rng.randint(2, 4)):
            ax = x0 + rng.uniform(0.2, w - 0.2)
            b.box(p.m_metal, ax - 0.02, ax + 0.02, top + 0.18, top + rng.uniform(0.6, 1.5), y - 0.1, y)
        b.ngon(p.m_light, x0 + w * 0.3, top + 0.55, 0.28, 12, y - 0.12, y - 0.1)
    else:   # lift machine room
        plate(b, p.m_wall, x0 + w * 0.2, x0 + w * 0.5, top + 0.18, top + 0.9, y - 0.05, 0.5, 0.04)


def tower(b, x, w, top, y, mat, win, rng):
    """Megastructure tower: stepped silhouette, vertical light strips, crown with aircraft lights."""
    steps = rng.randint(2, 4)
    cur_w = w
    z = -2.0
    seg_h = (top + 2.0) / steps
    for i in range(steps):
        b.box(mat, x - cur_w / 2, x + cur_w / 2, z, z + seg_h + 0.05, y, y + 0.5)
        for k in range(int(cur_w / 0.5)):
            sx = x - cur_w / 2 + 0.25 + k * 0.5
            if rng.random() < 0.55:
                zz = z + rng.uniform(0.1, seg_h * 0.3)
                b.quad(win, sx - 0.04, sx + 0.04, zz, zz + rng.uniform(seg_h * 0.3, seg_h * 0.9), y=y - 0.01, c='NoLine')
        z += seg_h
        cur_w *= rng.uniform(0.62, 0.82)
    b.box(mat, x - 0.04, x + 0.04, z, z + 1.4, y, y + 0.2)
    b.disc(glowm('Air', C['neon_red'], cat='screen'), x, z + 1.4, 0.07, y=y - 0.05, seg=8, c='NoLine')


# ---------------------------------------------------------------- layers
def sky():
    new_scene()
    style(haze=C['haze'], haze_near=0, haze_far=40, haze_max=0.0, dark_top=None, light_gain=0.0)
    rng = random.Random(41)
    Wsk, Hsk, z0 = 40.0, 20.0, -4.0
    setup_render(Wsk, Hsk, 48, 0, z0, line=0)
    ink(0)
    b = Batch()
    b.quad(gradient_mat('SkyLow', C['horizon'], C['sky_mid'], -4.0, 6.0), -2, Wsk + 2, z0, 6.0, y=50, c='NoLine')
    b.quad(gradient_mat('SkyHigh', C['sky_mid'], C['sky_top'], 6.0, 16.0), -2, Wsk + 2, 6.0, 16.0, y=50, c='NoLine')
    b.disc(flat('MoonHalo', (0.95, 0.75, 0.85), 0.12), 28.0, 11.5, 2.2, y=48, seg=40, c='NoLine')
    b.disc(flat('Moon', (1.0, 0.9, 0.86)), 28.0, 11.5, 1.1, y=47.9, seg=40, c='NoLine')
    b.disc(flat('MoonShade', (0.85, 0.7, 0.75)), 28.35, 11.7, 0.95, y=47.8, seg=40, c='NoLine')
    for (z, a) in ((9.0, 0.22), (7.4, 0.3), (12.6, 0.15)):   # long cloud bands lit from below
        x = rng.uniform(-2, 0)
        while x < Wsk:
            w = rng.uniform(4, 9)
            b.prism(flat(f'Cloud{z}', mix(C['horizon'], C['sky_mid'], 0.5), a), [(x, z), (x + w, z), (x + w * 0.85, z + 0.35), (x + w * 0.2, z + 0.5)], 46, 46.01, c='NoLine')
            x += w + rng.uniform(1, 4)
    for (x, ang) in ((5.0, 0.25), (14.0, -0.15), (33.0, 0.32)):   # searchlights
        light_shaft(b, x, -2.0, x + math.sin(ang) * 16, 15.0, 0.3, 2.6, (1.0, 0.85, 0.8), 0.22, 45.0, n=8)
    b.build(wrap_width=Wsk)
    render('R_Sky.png', glow_kw=dict(threshold=0.7, radii=(8, 26), strength=(.4, .4), opaque=True))


def far():
    new_scene()
    hz = mix(C['horizon'], C['sky_mid'], 0.4)
    style(haze=hz, haze_near=0, haze_far=20, haze_max=0.62, dark_top=None, light_gain=0.0)
    rng = random.Random(43)
    Wf, Hf, z0 = 48.0, 14.0, -2.0
    setup_render(Wf, Hf, 80, 0, z0, line=0)
    ink(0)
    b = Batch()
    p = palette(1.0)
    mat = sol('FarTower', (0.12, 0.07, 0.16), lights=False, pat='hatch', pat_scale=0.6, pat_k=0.25)
    win = glowm('FarWin', mix(C['window'], hz, 0.35), cat='none')
    x = 0.5
    while x < Wf:
        w = rng.uniform(1.4, 3.2)
        tower(b, x + w / 2, w, rng.uniform(4.5, 10.5), 12.0, mat, win, rng)
        x += w + rng.uniform(0.2, 1.6)
    # landmark: institute megatower with a hologram hand (story beacon)
    tower(b, 30.0, 4.2, 10.6, 10.0, sol('MegaTower', (0.1, 0.06, 0.14), lights=False, pat='seam', pat_scale=0.3, pat_k=0.35), win, random.Random(7))
    hol = glowm('Holo', (0.75, 0.3, 0.5), cat='screen')
    cx, cz = 30.0, 9.2
    for i, L in enumerate((0.7, 0.85, 0.8, 0.6)):
        b.bar(hol, cx - 0.42 + i * 0.28, cz, cx - 0.42 + i * 0.28, cz + L, 0.11, 9.8, 9.81, c='NoLine')
    b.bar(hol, cx - 0.55, cz - 0.6, cx + 0.45, cz - 0.6, 1.0, 9.8, 9.81, c='NoLine')
    b.bar(hol, cx - 0.6, cz - 0.4, cx - 0.95, cz + 0.2, 0.14, 9.8, 9.81, c='NoLine')
    text('НИИ БИОПУЛЬС', hol, cx, cz - 1.55, 0.36, y=9.8, c='NoLine')
    light(cx, cz, 3.0, (1.0, 0.55, 0.75), 0.5)
    b.build(wrap_width=Wf)
    render('R_Far.png', glow_kw=dict(threshold=0.6, radii=(4, 16), strength=(.5, .4)))


def rmid():
    new_scene()
    style(haze=mix(C['haze'], C['sky_mid'], 0.3), haze_near=-1, haze_far=10, haze_max=0.2, ink=C['ink'], dark_top=None, light_gain=2.4)
    rng = random.Random(47)
    OVERLAYS.clear()
    Wm, Hm, z0 = 48.0, 13.0, -2.5
    setup_render(Wm, Hm, 80, 0, z0, line=1.3)
    ink(1.3)
    b = Batch()
    p = palette(1.0)
    win = glowm('Win', mix(C['window'], (0.1, 0.05, 0.1), 0.3), cat='none')
    dark = sol('WinDark', (0.07, 0.05, 0.1), lights=False)
    houses = []
    x = 0.3
    while x < Wm - 1:
        w = rng.uniform(3.0, 5.5)
        top = rng.uniform(1.4, 5.0)
        houses.append((x, w, top))
        panel_house(b, p, x, w, top, 2.0 + rng.uniform(0, 0.8), rng, win, dark)
        x += w + rng.uniform(0.4, 1.5)
    # neon signs on a third of the houses
    for i, (hx, hw, top) in enumerate(houses):
        if i % 3 == 1:
            word = SIGNS[i % len(SIGNS)]
            col = [C['neon_pink'], C['neon_amber'], C['neon_red']][i % 3]
            if len(word) <= 6 and rng.random() < 0.6:
                neon_sign(b, word, hx + hw - 0.35, top - 0.4, 0.34, col, 1.6, vertical=True, plate_mat=p.m_dark)
            else:
                neon_sign(b, word, hx + hw / 2, top + 0.35, 0.36, col, 1.6, plate_mat=None)
                for sx in (hx + hw * 0.2, hx + hw * 0.8):
                    b.box(p.m_metal, sx - 0.03, sx + 0.03, top + 0.18, top + 0.4, 1.7, 1.75)
    # wires with bulbs between neighbouring roofs
    for (a, c_) in zip(houses[:-1], houses[1:]):
        x0, z0_ = a[0] + a[1], a[2] - 0.3
        x1, z1 = c_[0], c_[2] - 0.5
        cable(b, p.m_deep, x0, z0_, x1, z1, 0.6, 0.02, 1.3)
        for k in range(1, 6):
            t = k / 6
            bx = x0 + (x1 - x0) * t
            bz = z0_ + (z1 - z0_) * t - 0.6 * 4 * t * (1 - t)
            b.disc(glowm('Bulb', C['lamp'], cat='lamp'), bx, bz - 0.05, 0.05, y=1.25, seg=8, c='NoLine')
    OVERLAYS.append(dict(kind='steam', x=houses[2][0] + 0.6, z=houses[2][2] + 0.4, w=0.4, h=0))
    OVERLAYS.append(dict(kind='steam', x=houses[-2][0] + 1.0, z=houses[-2][2] + 0.4, w=0.4, h=0))
    finalize_lights(wrap=Wm)
    b.build(wrap_width=Wm)
    render('R_Mid.png', glow_kw=dict(threshold=0.6, radii=(4, 14), strength=(.5, .35)))
    global Z0
    _z = Z0; Z0 = z0
    write_overlays('R_Mid', Wm)
    Z0 = _z


def rfront():
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    rng = random.Random(49)
    Hf = 5.0
    setup_render(W, Hf, 80, 0, 0, line=0)
    ink(0)
    b = Batch()
    sil = sol('Sil', (0.03, 0.02, 0.05), lit=(0.1, 0.06, 0.12), dark=(0.015, 0.01, 0.025), lights=False)
    for (x0, x1, sag, r) in ((-1, 9, 1.6, 0.04), (4, 15, 2.2, 0.03), (13, 26, 1.4, 0.04), (22, 33, 2.6, 0.03), (30, 41, 1.8, 0.04)):
        cable(b, sil, x0, Hf - 0.1, x1, Hf - 0.2, sag, r, -0.3)
    # a hanging shop sign + a traffic-light-like lamp in the near foreground
    for (x, word) in ((11.0, 'ПИВО'), (31.0, 'ЧАСЫ')):
        cable(b, sil, x - 0.5, Hf, x - 0.5, Hf - 1.4, 0, 0.02, -0.4)
        cable(b, sil, x + 0.5, Hf, x + 0.5, Hf - 1.4, 0, 0.02, -0.4)
        plate(b, sil, x - 0.8, x + 0.8, Hf - 2.1, Hf - 1.4, -0.45, 0.1, 0.03)
        text(word, glowm(f'FS{word}', mix(C['neon_amber'], (0, 0, 0), 0.2), cat='screen'), x, Hf - 1.95, 0.36, y=-0.5, c='NoLine')
    b.build(wrap_width=W)
    render('R_Front.png', glow_kw=dict(threshold=0.6, radii=(4, 12), strength=(.6, .3)))


def rfloor():
    """Roof edge: tar roof surface + parapet lip; under it the R_Wall facade tiles."""
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    Wf, Hf = 4.0, 2.0
    setup_render(Wf, Hf, 160, 0, -Hf, line=0)
    ink(2.0, color=(0.02, 0.01, 0.03), calligraphy=False)
    b = Batch()
    lip = sol('Lip', (0.32, 0.22, 0.32), lit=(0.62, 0.46, 0.55), lights=False)
    tar = sol('Tar', (0.1, 0.07, 0.12), lights=False, pat='noise', pat_scale=1.5, pat_k=0.3)
    brick = sol('Cornice', (0.24, 0.13, 0.17), lights=False, pat='hatch', pat_scale=1.0, pat_k=0.4)
    plate(b, lip, -0.2, Wf + 0.2, -0.14, 0.0, 0.0, 0.3, 0.03)
    b.quad(tar, -0.2, Wf + 0.2, -0.55, -0.14, y=0.05, c='NoLine')
    plate(b, brick, -0.2, Wf + 0.2, -0.95, -0.55, 0.02, 0.3, 0.04)
    for i in range(8):   # dentil cornice
        plate(b, lip, i * 0.5 + 0.08, i * 0.5 + 0.38, -1.15, -0.95, -0.02, 0.3, 0.02)
    b.quad(sol('Under', (0.07, 0.04, 0.08), lights=False), -0.2, Wf + 0.2, -Hf - 0.2, -1.15, y=0.1, c='NoLine')
    b.build(wrap_width=Wf)
    render('R_Floor.png', glow_kw=None)


def rwall():
    """Brick facade tile (4 x 4) under the roofs: two windows, one lit."""
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    Wf, Hf = 4.0, 4.0
    setup_render(Wf, Hf, 80, 0, 0, line=0)
    ink(1.5, color=(0.02, 0.01, 0.03), calligraphy=False)
    b = Batch()
    brick = sol('Brick', (0.26, 0.14, 0.18), lights=False, pat='seam', pat_scale=1.6, pat_k=0.35)
    b.quad(brick, -0.2, Wf + 0.2, -0.2, Hf + 0.2, y=0.5, c='NoLine')
    for i, on in enumerate((True, False)):
        wx = 0.5 + i * 2.0
        plate(b, sol('Frame', (0.14, 0.08, 0.12), lights=False), wx, wx + 1.0, 1.0, 2.6, 0.2, 0.3, 0.03)
        col = C['window'] if on else (0.06, 0.04, 0.08)
        b.quad(glowm('RW', col, cat='none') if on else sol('RWd', col, lights=False), wx + 0.1, wx + 0.9, 1.1, 2.5, y=0.15, c='NoLine')
        b.box(sol('Frame', (0.14, 0.08, 0.12), lights=False), wx + 0.47, wx + 0.53, 1.1, 2.5, 0.1, 0.15)
        plate(b, sol('Sill', (0.36, 0.24, 0.3), lights=False), wx - 0.1, wx + 1.1, 0.9, 1.0, 0.1, 0.3, 0.02)
    b.build(wrap_width=Wf)
    render('R_Wall.png', glow_kw=None)


JOBS = dict(sky=sky, far=far, mid=rmid, front=rfront, floor=rfloor, wall=rwall)
for job in (ONLY if ONLY is not None else list(JOBS)):
    with_mask(JOBS[job]) if job in ('sky', 'far', 'mid') else JOBS[job]()
