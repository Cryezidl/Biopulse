# Level 2 — surface lab (alarm!), NII-punk style. Strips: S_Back 40 x 10.4 @64 (opaque, city through the windows),
# S_Mid 40 x 10.4 @80, S_Front 40 x 5 @80 (top band), S_Floor 4 x 2 @160.
# Run: ONLY = ['mid', ...]; exec(open(r"...\loc_surface.py", encoding="utf-8").read())
import random, math
_JOBS_WANTED = globals().get('ONLY')
ONLY = []                                   # import the underground set pieces without running its jobs
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\loc_underground.py", encoding="utf-8").read(), globals())
ONLY = _JOBS_WANTED

# Ink-blue institute at night; support = concrete/glass; accent = alarm red (beacons, lasers); cold fluorescent light.
C = dict(
    deep=(0.035, 0.04, 0.075), dark=(0.075, 0.085, 0.14), wall=(0.13, 0.15, 0.23), wall2=(0.16, 0.18, 0.27),
    light=(0.36, 0.38, 0.5), metal=(0.22, 0.25, 0.34), haze=(0.2, 0.23, 0.36),
    screen=(0.5, 0.8, 1.0), lamp=(0.85, 0.93, 1.0), toxic=(0.5, 0.8, 1.0), ink=(0.02, 0.02, 0.05),
    alarm=(1.0, 0.27, 0.22), window=(1.0, 0.72, 0.42),
)


def window_bay(b, p, x0, x1, z0, z1, y, rng):
    """Big institute window: frame + mullions, translucent glass (the city strip shows through), radiator below."""
    plate(b, p.m_dark, x0 - 0.25, x1 + 0.25, z0 - 0.3, z0, y - 0.1, 0.6, 0.05)                 # sill
    plate(b, p.m_light, x0 - 0.3, x1 + 0.3, z0 - 0.05, z0 + 0.06, y - 0.2, 0.8, 0.02)
    plate(b, p.m_dark, x0 - 0.25, x1 + 0.25, z1, z1 + 0.35, y - 0.1, 0.6, 0.05, cut=0.2, corners=(1, 1, 0, 0))
    n = max(2, int((x1 - x0) / 1.6))
    for i in range(n + 1):                                                                      # mullions
        mx = x0 + (x1 - x0) * i / n
        plate(b, p.m_wall2, mx - 0.07, mx + 0.07, z0, z1, y, 0.3, 0.03)
    plate(b, p.m_wall2, x0, x1, z0 + (z1 - z0) * 0.66 - 0.05, z0 + (z1 - z0) * 0.66 + 0.05, y, 0.3, 0.03)
    b.quad(flat('Glass', (0.3, 0.4, 0.6), 0.16), x0, x1, z0, z1, y=y + 0.2, c='NoLine')
    for i in range(n):                                                                          # glass glints
        gx = x0 + (x1 - x0) * (i + 0.2) / n
        b.prism(flat('Glint', (0.75, 0.85, 1.0), 0.12), [(gx, z0 + 0.3), (gx + 0.12, z0 + 0.3), (gx + 0.5, z1 - 0.3), (gx + 0.38, z1 - 0.3)], y + 0.15, y + 0.151, c='NoLine')
    # cast-iron radiator under the window
    rx0, rx1 = x0 + 0.6, min(x1 - 0.6, x0 + 3.2)
    plate(b, p.m_dark, rx0, rx1, 0.25, 0.95, y - 0.3, 0.3, 0.02)
    k = rx0 + 0.08
    while k < rx1 - 0.08:
        plate(b, p.m_metal, k, k + 0.12, 0.28, 0.92, y - 0.35, 0.2, 0.03)
        k += 0.16
    pipe_h(b, p.m_metal, x0 - 0.3, x1 + 0.3, 0.18, 0.04, y - 0.33)


def server_racks(b, p, x0, n, y, rng, h=5.6):
    """Row of tall racks with blinking LED rows and patch cables."""
    w = 1.15
    for i in range(n):
        rx = x0 + i * (w + 0.12)
        plate(b, p.m_dark, rx, rx + w, 0, h, y, 0.6, 0.06, cut=0.12, corners=(0, 0, 1, 1))
        plate(b, p.m_wall, rx + 0.08, rx + w - 0.08, 0.15, h - 0.25, y - 0.05, 0.3, 0.03)
        z = 0.3
        while z < h - 0.5:
            uh = rng.choice([0.18, 0.18, 0.3, 0.45])
            plate(b, rng.choice([p.m_wall2, p.m_dark, p.m_metal]), rx + 0.12, rx + w - 0.12, z, z + uh - 0.03, y - 0.1, 0.2, 0.015)
            for k in range(rng.randint(1, 6)):
                col = rng.choice([C['screen'], C['screen'], (0.5, 1.0, 0.6), C['alarm']])
                b.disc(glowm(f'Led{col}', col), rx + 0.22 + k * 0.08, z + uh * 0.5, 0.022, y=y - 0.13, seg=8, c='NoLine')
            z += uh
        for k in range(3):   # patch cables drooping between racks
            cable(b, p.m_deep, rx + w - 0.15, h - 0.6 - k * 0.5, rx + w + 0.25, h - 0.9 - k * 0.5, 0.3 + 0.1 * k, 0.02, y - 0.15)
    plate(b, p.m_wall2, x0 - 0.1, x0 + n * (w + 0.12), h, h + 0.25, y - 0.15, 0.8, 0.03)    # cable tray
    light(x0 + n * (w + 0.12) / 2, h * 0.5, n * 0.9, C['screen'], 0.35)


def alarm_panel(b, p, x, z, y):
    plate(b, p.m_dark, x - 1.0, x + 1.0, z, z + 0.8, y, 0.3, 0.05, cut=0.15, corners=(1, 1, 1, 1))
    b.quad(glowm('AlarmFace', mix(C['alarm'], (0, 0, 0), 0.35)), x - 0.85, x + 0.85, z + 0.12, z + 0.68, y=y - 0.01, c='NoLine')
    text('ТРЕВОГА', glowm('AlarmText', (1.0, 0.85, 0.8)), x, z + 0.26, 0.28, y=y - 0.02, c='NoLine')
    light(x, z + 0.4, 3.0, C['alarm'], 0.9)


def tube_lamp(b, p, x, z, y, w=2.0):
    """Fluorescent tube fixture — cold key light of the institute."""
    plate(b, p.m_dark, x - w / 2 - 0.1, x + w / 2 + 0.1, z, z + 0.18, y, 0.3, 0.03)
    b.box(glowm('Tube', C['lamp'], cat='lamp'), x - w / 2, x + w / 2, z - 0.06, z, y - 0.05, y, c='NoLine')
    light_shaft(b, x, z - 0.05, x, 0.0, w, w + 2.2, C['lamp'], 0.055, y - 0.3, n=6)
    light(x, z - 1.8, 3.0, C['lamp'], 0.4)


def blast_door(b, p, x, y):
    """Sealed hermetic door with wheel (гермодверь)."""
    plate(b, p.m_dark, x - 1.4, x + 1.4, 0, 4.4, y + 0.1, 0.4, 0.06, cut=0.5, corners=(0, 0, 1, 1))
    plate(b, p.m_hatch, x - 1.1, x + 1.1, 0.1, 4.0, y, 0.3, 0.05, cut=0.4, corners=(0, 0, 1, 1))
    b.ngon(p.m_metal, x, 2.0, 0.45, 20, y - 0.1, y)
    b.ngon(p.m_dark, x, 2.0, 0.33, 20, y - 0.11, y - 0.1)
    for a in range(4):
        ang = a * math.pi / 2 + 0.4
        b.bar(p.m_metal, x, 2.0, x + math.cos(ang) * 0.42, 2.0 + math.sin(ang) * 0.42, 0.06, y - 0.13, y - 0.11)
    b.ngon(p.m_light, x, 2.0, 0.08, 10, y - 0.15, y - 0.13)
    stripes_bar(b, p, x - 1.1, x + 1.1, 0.1, 0.35, y - 0.02)
    sign(b, 'ГЕРМОЗОНА', x, 4.55, 0.2, p.m_light, sol('Ink', C['deep'], lights=False), y - 0.1)


def stripes_bar(b, p, x0, x1, z0, z1, y, step=0.22):
    yel = sol('Warn', (0.85, 0.62, 0.2), lights=False)
    x = x0
    while x < x1:
        pts = [(x, z0), (min(x1, x + step / 2), z0), (min(x1, x + step / 2 + (z1 - z0) * 0.6), z1), (min(x1, x + (z1 - z0) * 0.6), z1)]
        b.poly(yel, pts, y=y, c='NoLine')
        x += step


def lab_bench(b, p, x, w, y, rng):
    plate(b, p.m_wall2, x - w / 2, x + w / 2, 0.95, 1.08, y, 0.7, 0.02)
    for sx in (x - w / 2 + 0.15, x + w / 2 - 0.45):
        plate(b, p.m_dark, sx, sx + 0.3, 0, 0.95, y + 0.05, 0.6, 0.02)
    for i in range(2):
        mx = x - w / 2 + 0.7 + i * (w - 1.4)
        screen(b, p, mx - 0.38, mx + 0.38, 1.35, 1.9, y - 0.05, rng, kind=rng.choice(['emg', 'bars', 'hand']), power=0.6)
        plate(b, p.m_dark, mx - 0.05, mx + 0.05, 1.08, 1.25, y, 0.1, 0.01)
    for k in range(3):   # glassware
        gx = x - 0.3 + k * 0.25
        b.quad(flat('Flask', (0.6, 0.8, 1.0), 0.3), gx - 0.06, gx + 0.06, 1.08, 1.3 + 0.08 * k, y=y - 0.1, c='NoLine')


# ---------------------------------------------------------------- layers
def mid():
    new_scene()
    style(haze=C['haze'], haze_near=-1.0, haze_far=6.0, haze_max=0.16, ink=C['ink'], dark_top=(2.5, 8.0, 0.42), light_gain=3.2)
    rng = random.Random(21)
    OVERLAYS.clear()
    setup_render(W, H, 80, 0, Z0, line=1.6)
    ink(1.7)
    b = Batch()
    p = palette(0.75)
    for ux in (5.0, 15.0, 25.0, 35.0):
        light(ux, -0.6, 3.4, (0.45, 0.6, 1.0), 0.4)
    # bays: window | servers | window | lab + door
    window_bay(b, p, 0.9, 9.1, 1.3, 6.4, 2.0, rng)
    window_bay(b, p, 20.9, 29.1, 1.3, 6.4, 2.0, rng)
    machine_wall(b, p, 0.6, 9.4, 6.8, CEIL - 1.6, 3.0, rng, min_size=0.5, quiet=0.2)
    machine_wall(b, p, 20.6, 29.4, 6.8, CEIL - 1.6, 3.0, rng, min_size=0.5, quiet=0.2)
    machine_wall(b, p, 10.6, 19.4, 0, CEIL - 1.6, 3.4, rng, min_size=0.7, quiet=0.2)
    server_racks(b, p, 11.0, 6, 2.6, rng)
    machine_wall(b, p, 30.6, 39.4, 0, CEIL - 1.6, 3.4, rng, min_size=0.65, quiet=0.15)
    lab_bench(b, p, 33.0, 3.4, 2.2, rng)
    blast_door(b, p, 37.6, 2.4)
    alarm_panel(b, p, 33.0, 4.6, 2.5)
    for px in (0.0, 10.0, 20.0, 30.0, 40.0):
        pillar(b, p, px, 1.1, 2.0)
    buttress(b, p, 10.55, CEIL - 1.55, 5.4, 1.5, 2.1, side=1)
    buttress(b, p, 19.45, CEIL - 1.55, 5.4, 1.5, 2.1, side=-1)
    ceiling_works(b, p, 1.2, rng)
    for x in (5.0, 15.0, 25.0, 35.0):
        tube_lamp(b, p, x, CEIL - 1.75, 1.0)
    for x in (10.0, 30.0):
        OVERLAYS.append(dict(kind='alarm', x=x, z=CEIL - 2.2, w=0, h=0))
        light(x, CEIL - 2.6, 4.0, C['alarm'], 0.8)
    OVERLAYS.append(dict(kind='steam', x=17.0, z=CEIL - 1.9, w=0.6, h=0))
    sign(b, 'КОРПУС 2 · ВЫЧИСЛИТЕЛЬНЫЙ ЦЕНТР', 15.0, 6.25, 0.22, p.m_light, sol('Ink', C['deep'], lights=False), 2.2)
    finalize_lights(wrap=W)
    b.build(wrap_width=W)
    render('S_Mid.png', glow_kw=dict(threshold=0.6, radii=(4, 16), strength=(.45, .35)))
    write_overlays('S_Mid', W)


def panel_block(b, x0, x1, z0, z1, y, rng, wall, win, dark, floors=None):
    """Soviet panel apartment block with clustered lit windows (not uniform random)."""
    b.box(wall, x0, x1, z0, z1, y, y + 0.5)
    fh = 0.42
    nf = floors or int((z1 - z0) / fh)
    cols = int((x1 - x0) / 0.36)
    lit_runs = {}
    for f in range(nf):
        if rng.random() < 0.55:
            a = rng.randint(0, max(0, cols - 2)); lit_runs[f] = (a, a + rng.randint(1, 4))
    for f in range(nf):
        zf = z0 + 0.15 + f * fh
        if zf + 0.22 > z1: break
        for c in range(cols):
            wx = x0 + 0.1 + c * 0.36
            on = f in lit_runs and lit_runs[f][0] <= c <= lit_runs[f][1] or rng.random() < 0.04
            b.quad(win if on else dark, wx, wx + 0.18, zf, zf + 0.22, y=y - 0.01, c='NoLine')


def back():
    """Night city through the institute windows: hazy panel blocks, TV tower, searchlights."""
    new_scene()
    hz = (0.24, 0.27, 0.42)
    style(haze=hz, haze_near=0.0, haze_far=30.0, haze_max=0.75, dark_top=None, light_gain=0.0)
    rng = random.Random(31)
    setup_render(W, H, 64, 0, Z0, line=0)
    ink(0)
    b = Batch()
    b.quad(gradient_mat('Sky', (0.2, 0.2, 0.36), (0.03, 0.035, 0.08), Z0, H + Z0), -2, W + 2, Z0, H + Z0, y=40, c='NoLine')
    # TV tower on the horizon
    tw = sol('Tower', (0.1, 0.11, 0.2), lights=False)
    b.prism(tw, [(18.6, -1), (19.4, -1), (19.08, 8.5), (18.92, 8.5)], 30, 30.2)
    b.prism(tw, [(18.6, 5.6), (19.4, 5.6), (19.25, 6.0), (18.75, 6.0)], 29.9, 30.2)
    b.box(tw, 18.97, 19.03, 8.5, 9.4, 29.9, 30.2)
    for z in (6.0, 8.5, 9.35):
        b.disc(glowm('Beacon', C['alarm'], cat='screen'), 19.0, z, 0.06, y=29.8, seg=8, c='NoLine')
    # searchlights sweeping the sky
    for (x, ang) in ((6.0, 0.35), (31.0, -0.28)):
        light_shaft(b, x, -1.0, x + math.sin(ang) * 10, 10.0, 0.3, 2.2, (0.7, 0.75, 1.0), 0.25, 35.0, n=8)
    # three bands of panel blocks: far (pale, hazy) -> near (darker, clearer)
    for (band_y, zmax, wmin, wmax, wall_c) in ((28.0, 6.5, 2.0, 4.0, (0.15, 0.16, 0.27)), (18.0, 5.0, 2.5, 5.0, (0.11, 0.12, 0.21)),
                                                 (8.0, 3.6, 3.0, 6.0, (0.08, 0.085, 0.15))):
        wall = sol(f'Block{band_y}', wall_c, lights=False, pat='noise', pat_scale=0.3, pat_k=0.15)
        win = glowm(f'Win{band_y}', mix(C['window'], hz, band_y / 40), cat='none')
        dark = sol(f'WinD{band_y}', mix(wall_c, (0, 0, 0), 0.4), lights=False)
        x = rng.uniform(-1, 1)
        while x < W:
            w = rng.uniform(wmin, wmax)
            top = rng.uniform(zmax * 0.55, zmax)
            panel_block(b, x, x + w, -1, top, band_y, rng, wall, win, dark)
            if rng.random() < 0.3:   # rooftop antenna / sign frame
                b.box(wall, x + w * 0.4, x + w * 0.42, top, top + 0.8, band_y - 0.1, band_y)
            x += w + rng.uniform(0.1, 0.9)
    finalize_lights(wrap=W)
    b.build(wrap_width=W)
    render('S_Back.png', glow_kw=dict(threshold=0.55, radii=(5, 20), strength=(.4, .4), opaque=True))


def front():
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    rng = random.Random(25)
    Hf = 5.0
    setup_render(W, Hf, 80, 0, 0, line=0)
    ink(0)
    b = Batch()
    sil = sol('Sil', (0.025, 0.03, 0.06), lit=(0.08, 0.09, 0.16), dark=(0.012, 0.015, 0.035), lights=False)
    rim = sol('SilRim', (0.07, 0.08, 0.14), lights=False)
    plate(b, sil, -1, W + 1, Hf - 0.6, Hf + 0.5, 0, 0.6, 0.0)
    x = 0.0
    while x < W:   # cable trays and ducts
        seg = rng.uniform(5, 10)
        plate(b, sil, x, x + seg - 0.4, Hf - 1.0, Hf - 0.6, -0.1, 0.6, 0.06, cut=0.2, corners=(1, 1, 0, 0))
        b.box(rim, x + 0.2, x + seg - 0.6, Hf - 1.05, Hf - 0.98, -0.2, -0.1)
        x += seg
    for x in (8.0, 26.0):   # fluorescent fixture silhouettes hanging closer to camera
        cable(b, sil, x - 1.0, Hf - 1.0, x - 1.0, Hf - 2.2, 0, 0.02, -0.3)
        cable(b, sil, x + 1.0, Hf - 1.0, x + 1.0, Hf - 2.2, 0, 0.02, -0.3)
        plate(b, sil, x - 1.3, x + 1.3, Hf - 2.45, Hf - 2.2, -0.35, 0.2, 0.03)
        b.box(glowm('FTube', C['lamp'], cat='lamp'), x - 1.2, x + 1.2, Hf - 2.52, Hf - 2.45, -0.45, -0.4, c='NoLine')
    for (x0, x1, sag, r) in ((2.0, 7.0, 1.8, 0.06), (13.0, 19.0, 2.4, 0.08), (30.0, 37.0, 2.0, 0.07)):
        cable(b, sil, x0, Hf - 1.0, x1, Hf - 1.0, sag, r, -0.4)
    b.build(wrap_width=W)
    render('S_Front.png', glow_kw=dict(threshold=0.6, radii=(4, 12), strength=(.6, .3)))


def floor():
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0)
    Wf, Hf = 4.0, 2.0
    setup_render(Wf, Hf, 160, 0, -Hf, line=0)
    ink(2.0, color=(0.01, 0.01, 0.03), calligraphy=False)
    b = Batch()
    lip = sol('Lip', (0.22, 0.24, 0.36), lit=(0.55, 0.58, 0.78), lights=False)
    slab = sol('Slab', (0.06, 0.065, 0.11), lights=False, pat='hatch', pat_scale=1.2, pat_k=0.25)
    deep = sol('SlabDeep', (0.025, 0.028, 0.05), lights=False)
    b.quad(deep, -0.5, Wf + 0.5, -Hf - 0.5, 0, y=1.0, c='NoLine')
    plate(b, lip, -0.2, Wf + 0.2, -0.14, 0.0, 0.0, 0.3, 0.03)
    for i in range(4):   # raised-floor tiles (computer room)
        x0 = i * 1.0
        plate(b, slab, x0 + 0.03, x0 + 0.97, -0.7, -0.17, 0.05, 0.3, 0.04)
        b.ngon(deep, x0 + 0.5, -0.43, 0.05, 8, 0.0, 0.05, c='NoLine')
    plate(b, slab, 0.03, Wf - 0.03, -1.95, -0.75, 0.1, 0.3, 0.05)
    b.build(wrap_width=Wf)
    render('S_Floor.png', glow_kw=None)


JOBS = dict(mid=mid, back=back, front=front, floor=floor)
for job in (ONLY if ONLY is not None else list(JOBS)):
    with_mask(JOBS[job]) if job in ('mid', 'back') else JOBS[job]()
