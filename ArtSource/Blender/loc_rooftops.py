# Rooftops (level 3) backdrop layers: Roof_Sky, Roof_Far, Roof_Mid, Roof_Front.
# Run in Blender:  exec(open(r"...\ArtSource\Blender\loc_rooftops.py").read())
import random, math
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\neon_kit.py", encoding="utf-8").read(), globals())

PPU = 80
PINK = (1.0, 0.28, 0.72)
CYAN = (0.25, 0.92, 1.0)
AMBER = (1.0, 0.72, 0.36)
VIOLET = (0.66, 0.42, 1.0)
LIME = (0.6, 1.0, 0.45)
RED = (1.0, 0.22, 0.2)
NEONS = [PINK, CYAN, AMBER, VIOLET, PINK, CYAN]
WORDS_V = ['ОТЕЛЬ', 'КАРАОКЕ', 'АПТЕКА', 'БИОПУЛЬС', 'ЛАПША', 'КИБЕР', 'НЕОН', 'ЧАЙ', 'ПРОТЕЗЫ']
WORDS_H = ['ЛАПША', 'БАР', '24/7', 'РАМЕН', 'ЧАЙНАЯ', 'КАРАОКЕ', 'РЕМОНТ', 'ЛОМБАРД', 'КИНО', 'АПТЕКА']


def window_grid(b, x0, x1, z0, z1, rng, lit=0.35, w=0.16, h=0.22, gx=0.42, gz=0.55, y=-0.01, c_lit='NoLine',
                palette=None, dark=None):
    palette = palette or [AMBER, AMBER, CYAN, PINK, (1, .9, .7)]
    cols = max(1, int((x1 - x0 - 0.2) / gx))
    rows = max(1, int((z1 - z0 - 0.2) / gz))
    ox = x0 + (x1 - x0 - (cols - 1) * gx) / 2
    oz = z0 + (z1 - z0 - (rows - 1) * gz) / 2
    for i in range(cols):
        for j in range(rows):
            cx, cz = ox + i * gx, oz + j * gz
            if rng.random() < lit:
                col = rng.choice(palette)
                k = rng.uniform(.55, 1.0)
                b.quad(emit(f'win{col}{k:.1f}', tuple(ch * k for ch in col)), cx - w / 2, cx + w / 2, cz - h / 2, cz + h / 2, y, c=c_lit)
            elif dark is not None:
                b.quad(dark, cx - w / 2, cx + w / 2, cz - h / 2, cz + h / 2, y, c=c_lit)


# ============================================================ SKY
def sky():
    reset()
    W, H, Z0 = 40, 20, -4
    setup_render(W, H, PPU * 0.6, 0, Z0, line=1.0)
    b = Batch()
    b.quad(gradient_mat('Sky', (0.30, 0.07, 0.30), (0.012, 0.01, 0.05), Z0 + 2, Z0 + H * 0.92), -W, 2 * W, Z0, Z0 + H, y=60, c='NoLine')
    # horizon haze band
    b.quad(gradient_mat('Haze', (0.55, 0.14, 0.42), (0.3, 0.07, 0.3), Z0, Z0 + 7, 1.0, 0.0), -W, 2 * W, Z0, Z0 + 7, y=55, c='NoLine')
    rng = random.Random(3)
    for _ in range(420):
        x, z = rng.uniform(0, W), rng.uniform(Z0 + 6, Z0 + H)
        s = rng.choice([0.025, 0.03, 0.04, 0.06]) * (1.4 if rng.random() < .05 else 1)
        col = rng.choice([(1, 1, 1), (0.8, 0.9, 1), (1, 0.85, 0.95)])
        b.quad(emit(f'star{col}', col, 1), x - s, x + s, z - s, z + s, 50, c='NoLine')
    # moon: crisp disc, a few craters, smooth halo from stacked faint discs
    mx, mz = 27.5, Z0 + 14.3
    b.disc(emit('Moon', (1.0, 0.95, 0.9)), mx, mz, 1.45, y=40, seg=72, c='NoLine')
    for ox, oz, r in [(.45, -.35, .3), (-.55, .4, .2), (-.2, -.72, .14), (.6, .55, .12), (-.75, -.25, .1)]:
        b.disc(flat('Crater', (0.93, 0.8, 0.86)), mx + ox, mz + oz, r, y=39.9, seg=24, c='NoLine')
    for i in range(40):
        r = 1.5 + i * 0.1
        b.disc(flat(f'Halo{i}', (0.95, 0.55, 0.85), 0.022 * (1 - i / 40) ** 1.6 + 0.002), mx, mz, r, y=41 + i * .02, seg=72, c='NoLine')

    # 2D cel clouds: shadow body, lit body, rim highlight (ref: soft pink cumulus at night)
    def cumulus(cx, cz, length, scale, tint, depth):
        shadow = flat(f'CS{tint}', [(0.30, 0.12, 0.36), (0.22, 0.09, 0.30)][tint])
        body = flat(f'CB{tint}', [(0.62, 0.25, 0.58), (0.42, 0.17, 0.46)][tint])
        rim = flat(f'CR{tint}', [(0.95, 0.52, 0.82), (0.66, 0.32, 0.66)][tint])
        n = int(length / (0.8 * scale)) + 3
        puffs = []
        for i in range(n):
            t = i / (n - 1)
            hump = math.sin(t * math.pi) ** 0.8
            r = scale * (0.45 + 0.85 * hump * rng.uniform(.75, 1.15))
            puffs.append((cx - length / 2 + t * length, cz + r * 0.45, r))
        for x, z, r in puffs:  # rim layer (back, offset up-left)
            b.disc(rim, x - r * .06, z + r * .08, r, y=depth + .2, seg=40, c='NoLine')
        for x, z, r in puffs:  # body
            b.disc(body, x + r * .04, z - r * .04, r * .96, y=depth, seg=40, c='NoLine')
        for x, z, r in puffs:  # underside shadow, clipped flat
            b.disc(shadow, x + r * .08, z - r * .62, r * .62, y=depth - .2, seg=40, c='NoLine')
    for cx, cz, L, s, t, d in [(4, Z0 + 9.0, 9, 0.85, 0, 30), (17, Z0 + 11.8, 6, 0.6, 1, 34),
                               (33.5, Z0 + 9.8, 10, 0.95, 0, 30), (23.5, Z0 + 7.8, 5, 0.55, 1, 34),
                               (11, Z0 + 15.0, 5, 0.5, 1, 34), (38.5, Z0 + 15.8, 4, 0.45, 1, 34)]:
        cumulus(cx, cz, L, s, t, d)
    b.build(wrap_width=W)
    render('Roof_Sky.png', glow_kw=dict(threshold=0.7, radii=(8, 30), strength=(.5, .4), opaque=True))


# ============================================================ FAR SKYLINE
def tower(b, rng, x0, w, top, depth, body_mat, sign_rate, line_coll='Main'):
    x1 = x0 + w
    zb = -2
    b.box(body_mat, x0, x1, zb, top, depth, depth + 2, c=line_coll)
    # stepped crown / setbacks
    if rng.random() < .55:
        inset = w * rng.uniform(.15, .3)
        t2 = top + rng.uniform(.6, 1.8)
        b.box(body_mat, x0 + inset, x1 - inset, top, t2, depth + .2, depth + 1.8, c=line_coll)
        if rng.random() < .6:
            b.box(body_mat, x0 + w / 2 - .06, x0 + w / 2 + .06, t2, t2 + rng.uniform(.8, 2.0), depth + .5, depth + .7, c=line_coll)
            b.sphere(emit('Blink', RED), x0 + w / 2, t2 + 2.05, .08, y=depth - .2, c='NoLine')
        top_vis = t2
    else:
        top_vis = top
        if rng.random() < .5:  # rooftop clutter
            b.box(body_mat, x0 + .2, x0 + .2 + w * .3, top, top + .4, depth + .3, depth + 1, c=line_coll)
    dim = .55 if depth > 15 else .75
    window_grid(b, x0 + .1, x1 - .1, zb + .4, top - .3, rng, lit=rng.uniform(.08, .22), w=.1, h=.14, gx=.26, gz=.34,
                y=depth - .01, palette=[tuple(ch * dim for ch in c) for c in [AMBER, AMBER, (1, .85, .6), (1, .6, .45), CYAN, PINK]])
    # big vertical neon signs (ref: tall stacked billboards)
    if rng.random() < sign_rate and w > 1.6:
        word = rng.choice(WORDS_V)
        col = rng.choice(NEONS)
        sw = .78
        sx = x0 + rng.choice([.15, w - sw - .15])
        size = .58
        st = top - rng.uniform(.6, 1.6)
        sb = st - len(word) * size * 1.05 - .3
        b.box(toon('SignBack', (0.05, 0.03, 0.08)), sx, sx + sw, sb, st, depth - .45, depth - .05)
        b.box(emit(f'Frame{col}', col), sx - .04, sx + sw + .04, sb - .04, sb + .03, depth - .5, depth - .44, c='NoLine')
        b.box(emit(f'Frame{col}', col), sx - .04, sx + sw + .04, st - .03, st + .04, depth - .5, depth - .44, c='NoLine')
        text(word, emit(f'Txt{col}', col), sx + sw / 2, st - .45, size, y=depth - .5, vertical=True, c='NoLine')
    elif rng.random() < .35 and w > 2.2:  # horizontal glowing panel
        col = rng.choice(NEONS)
        pz = rng.uniform(zb + 4, top - 1.5)
        word = rng.choice(WORDS_H)
        size = .42
        tw = min(w - .5, text_width(word, size) + .4)
        cx = x0 + w / 2
        b.box(toon('BillBack', (0.06, 0.03, 0.09)), cx - tw / 2, cx + tw / 2, pz, pz + .72, depth - .3, depth - .1)
        b.box(emit(f'Frame{col}', col), cx - tw / 2 - .04, cx + tw / 2 + .04, pz - .04, pz, depth - .32, depth - .3, c='NoLine')
        b.box(emit(f'Frame{col}', col), cx - tw / 2 - .04, cx + tw / 2 + .04, pz + .72, pz + .76, depth - .32, depth - .3, c='NoLine')
        text(word, emit(f'Txt{col}', col), cx, pz + .15, size, y=depth - .35, c='NoLine')
    return top_vis


def far():
    reset()
    W, H, Z0 = 48, 14, -2
    setup_render(W, H, PPU, 0, Z0, line=1.0)
    rng = random.Random(11)
    b = Batch()
    back = toon('FarBack', (0.17, 0.12, 0.30), steps=(.75, .88, 1.0))
    front = toon('FarFront', (0.08, 0.075, 0.17), steps=(.7, .85, 1.0))
    x = 0
    while x < W:  # back row: hazy and tall
        w = rng.uniform(1.4, 3.2)
        tower(b, rng, x, w, rng.uniform(6, 11.5), 20, back, .25)
        x += w + rng.uniform(-.2, .6)
    x = rng.uniform(-1, 0)
    while x < W:  # front row: darker, more signs
        w = rng.uniform(1.6, 3.6)
        tower(b, rng, x, w, rng.uniform(3.5, 9.5), 10, front, .55)
        x += w + rng.uniform(.4, 1.6)
    # skybridges + distant train line
    for _ in range(4):
        bx = rng.uniform(0, W)
        bz = rng.uniform(2, 6)
        b.box(front, bx, bx + rng.uniform(1.5, 3), bz, bz + .25, 14, 15)
        b.box(emit('BridgeLight', CYAN), bx, bx + 2, bz + .08, bz + .12, 13.9, 13.95, c='NoLine')
    b.box(front, -W, 2 * W, 1.0, 1.25, 8, 8.5)
    for i in range(int(W / 1.6)):
        b.box(front, i * 1.6, i * 1.6 + .18, Z0, 1.0, 8.1, 8.4)
    # atmospheric haze: the city dissolves into pink smog towards the street
    b.quad(gradient_mat('FarSmog', (0.42, 0.12, 0.38), (0.3, 0.08, 0.3), Z0, 5.5, 0.85, 0.0), -W, 2 * W, Z0, 5.5, y=9, c='NoLine')
    b.quad(gradient_mat('BackSmog', (0.36, 0.1, 0.34), (0.25, 0.06, 0.28), Z0, 9, 0.6, 0.0), -W, 2 * W, Z0, 9, y=19, c='NoLine')
    b.build(wrap_width=W)
    wrap_objects([o for o in coll('NoLine').objects if o.name.startswith('Sign_')] +
                 [o for o in coll('Main').objects if o.name.startswith('Sign_')], W)
    render('Roof_Far.png', glow_kw=dict(threshold=0.6, radii=(4, 16), strength=(.6, .45)))


# ============================================================ MID STREET (ref: cosy dense facades)
def mid():
    reset()
    W, H, Z0 = 48, 13, -2.5
    setup_render(W, H, PPU, 0, Z0, line=1.5)
    rng = random.Random(23)
    b = Batch()
    facades = [toon('Brick', (0.27, 0.10, 0.14)), toon('Teal', (0.08, 0.16, 0.21)), toon('Plum', (0.19, 0.10, 0.25)),
               toon('Slate', (0.12, 0.12, 0.20)), toon('Rust', (0.29, 0.14, 0.12))]
    trim = toon('Trim', (0.07, 0.07, 0.11))
    metal = toon('Metal', (0.3, 0.32, 0.4))
    dark_win = flat('DarkWin', (0.03, 0.04, 0.08))
    leaf = toon('Leaf', (0.12, 0.42, 0.26), steps=(.45, .72, 1.0))
    leaf2 = toon('Leaf2', (0.2, 0.55, 0.22), steps=(.45, .72, 1.0))
    flower = emit('Flower', (1, .45, .75))
    bulb = emit('Bulb', (1, .85, .55))
    tops = []
    x = 0
    while x < W - 1.5:
        w = min(rng.uniform(3.4, 6.4), W - x)
        top = rng.uniform(0.6, 5.0)
        fac = rng.choice(facades)
        d = rng.uniform(0, 1.5)
        b.box(fac, x, x + w, Z0, top, d, d + 3)
        # cornice + parapet
        b.box(trim, x - .12, x + w + .12, top - .05, top + .22, d - .25, d + 3)
        b.box(fac, x + .05, x + w - .05, top + .22, top + .55, d - .1, d + .3)
        # floor ledges
        floors = []
        fz = Z0 + .4
        while fz < top - 1.0:
            floors.append(fz)
            fz += 1.35
        for fz in floors[1:]:
            b.box(trim, x, x + w, fz - .12, fz - .02, d - .12, d + 1)
        # windows: framed, some lit with curtains / plants
        n = max(1, int((w - .4) / 1.25))
        gx = (w - .2) / n
        for fz in floors:
            for i in range(n):
                cx = x + .1 + gx * (i + .5)
                ww, wh = min(.78, gx * .62), .82
                z0w = fz + .18
                lit = rng.random() < .45
                if lit:
                    col = tuple(ch * .82 for ch in rng.choice([AMBER, AMBER, AMBER, (1, .62, .4), (1, .55, .45), CYAN, PINK, VIOLET]))
                    b.quad(emit(f'mwin{col}', col), cx - ww / 2, cx + ww / 2, z0w, z0w + wh, d - .02, c='NoLine')
                    r = rng.random()
                    if r < .3:  # curtains
                        cur = toon(f'Cur{col}', tuple(c * .45 for c in col))
                        b.box(cur, cx - ww / 2, cx - ww / 2 + ww * .28, z0w, z0w + wh, d - .06, d - .03)
                        b.box(cur, cx + ww / 2 - ww * .28, cx + ww / 2, z0w, z0w + wh, d - .06, d - .03)
                    elif r < .45:  # silhouette of a plant / cat on the sill
                        sil = flat('Sil', (0.05, 0.03, 0.08))
                        b.sphere(sil, cx + rng.uniform(-.15, .15), z0w + .12, .14, y=d - .05, sz=1.2, sub=1, c='NoLine')
                    else:  # blinds
                        for k in range(3):
                            bz = z0w + wh - .12 - k * .1
                            b.box(flat('Blind', tuple(c * .55 for c in col)), cx - ww / 2, cx + ww / 2, bz, bz + .04, d - .05, d - .04, c='NoLine')
                else:
                    b.quad(dark_win, cx - ww / 2, cx + ww / 2, z0w, z0w + wh, d - .02, c='NoLine')
                # frame + sill
                b.box(trim, cx - ww / 2 - .06, cx + ww / 2 + .06, z0w - .09, z0w, d - .14, d)
                b.box(trim, cx - ww / 2 - .06, cx + ww / 2 + .06, z0w + wh, z0w + wh + .06, d - .08, d)
                b.box(trim, cx - ww / 2 - .06, cx - ww / 2, z0w, z0w + wh, d - .08, d)
                b.box(trim, cx + ww / 2, cx + ww / 2 + .06, z0w, z0w + wh, d - .08, d)
                b.box(trim, cx - .02, cx + .02, z0w, z0w + wh, d - .05, d)
                # AC unit under some windows
                if rng.random() < .22:
                    ax = cx + rng.uniform(-.2, .2)
                    b.box(metal, ax - .3, ax + .3, z0w - .55, z0w - .14, d - .55, d, bevel=.03)
                    b.disc(trim, ax + .08, z0w - .345, .15, y=d - .56, seg=16)
                    b.disc(metal, ax + .08, z0w - .345, .05, y=d - .57, seg=10, c='NoLine')
                # flower box
                if lit and rng.random() < .25:
                    b.box(toon('Planter', (0.3, 0.16, 0.12)), cx - ww / 2, cx + ww / 2, z0w - .2, z0w - .02, d - .45, d - .14)
                    for k in range(5):
                        b.sphere(rng.choice([leaf, leaf2]), cx - ww / 2 + .08 + k * (ww - .16) / 4, z0w + .02, .12, y=d - .3, sub=1)
                    for k in range(3):
                        b.sphere(flower, cx - ww / 2 + .15 + k * (ww - .3) / 2, z0w + .1, .04, y=d - .46, sub=1, c='NoLine')
        # balcony on one floor
        if floors and rng.random() < .55:
            fz = rng.choice(floors[1:] or floors)
            bx0, bx1 = x + w * rng.uniform(.05, .3), x + w * rng.uniform(.6, .95)
            b.box(trim, bx0, bx1, fz - .08, fz + .04, d - .9, d)
            b.box(metal, bx0, bx1, fz + .5, fz + .55, d - .92, d - .86)
            for k in range(int((bx1 - bx0) / .14) + 1):
                px = bx0 + k * .14
                b.box(metal, px, px + .025, fz + .04, fz + .5, d - .9, d - .88, c='NoLine')
            # potted plant + hanging vines
            px = bx0 + .3
            b.box(toon('Pot', (0.45, 0.22, 0.16)), px - .15, px + .15, fz + .04, fz + .32, d - .75, d - .5)
            for k in range(6):
                b.sphere(rng.choice([leaf, leaf2]), px + rng.uniform(-.25, .25), fz + .45 + rng.uniform(0, .35), rng.uniform(.13, .2), y=d - .7, sub=1)
            for k in range(rng.randint(2, 5)):
                vx = rng.uniform(bx0, bx1)
                L = rng.uniform(.4, 1.2)
                for s in range(int(L / .12)):
                    b.sphere(rng.choice([leaf, leaf2]), vx + math.sin(s) * .03, fz - .1 - s * .12, .07, y=d - .95, sub=1)
            # string lights along the railing
            for k in range(int((bx1 - bx0) / .22)):
                b.sphere(bulb, bx0 + .1 + k * .22, fz + .58 - .05 * math.sin(k * .9), .035, y=d - .95, sub=1, c='NoLine')
        # shop sign (horizontal) or vertical blade sign
        r = rng.random()
        if r < .5 and floors:
            word = rng.choice(WORDS_H)
            col = rng.choice(NEONS)
            size = .5
            tw = text_width(word, size) + .5
            sx = x + rng.uniform(.3, max(.31, w - tw - .3))
            sz = rng.choice(floors) - .55 if len(floors) > 1 else top - 1.4
            b.box(toon('SignBox', (0.06, 0.04, 0.09)), sx, sx + tw, sz, sz + .7, d - .4, d - .05)
            b.box(emit(f'Edge{col}', col), sx, sx + tw, sz - .04, sz, d - .42, d - .38, c='NoLine')
            b.box(emit(f'Edge{col}', col), sx, sx + tw, sz + .7, sz + .74, d - .42, d - .38, c='NoLine')
            text(word, emit(f'T{col}', col), sx + tw / 2, sz + .12, size, y=d - .45, c='NoLine')
        elif r < .8:
            word = rng.choice(WORDS_V)
            col = rng.choice(NEONS)
            size = .42
            vx = x + rng.choice([.05, w - .05])
            vt = top - .3
            vb = vt - len(word) * size * 1.05 - .35
            b.box(trim, vx - .45, vx - .3, vt - .2, vt - .1, d - .9, d)
            b.box(trim, vx - .45, vx - .3, vb + .2, vb + .3, d - .9, d)
            b.box(toon('Blade', (0.07, 0.04, 0.1)), vx - .32, vx + .32, vb, vt, d - 1.1, d - .8)
            b.box(emit(f'Edge{col}', col), vx - .36, vx + .36, vb - .04, vt + .04, d - .82, d - .79, c='NoLine')
            text(word, emit(f'T{col}', col), vx, vt - .35, size, y=d - 1.15, vertical=True, c='NoLine')
        # rooftop furniture
        rx = x + rng.uniform(.4, w - 1.6)
        rt = top + .55
        r = rng.random()
        if r < .4:  # water tank on legs
            for lx in (rx + .15, rx + 1.05):
                b.box(metal, lx, lx + .08, rt, rt + .7, d + .5, d + .6)
            b.cyl(toon('Tank', (0.36, 0.22, 0.17)), rx + .64, rt + 1.3, .6, 1.2, y=d + .9, seg=20)
            b.cyl(toon('TankRoof', (0.25, 0.15, 0.13)), rx + .64, rt + 2.05, .66, .3, y=d + .9, seg=20, r2=.1)
            for k in range(3):
                b.box(trim, rx + .02, rx + 1.26, rt + .9 + k * .35, rt + .94 + k * .35, d + .2, d + .3)
        elif r < .7:  # antenna + dish
            b.box(metal, rx, rx + .05, rt, rt + 1.6, d + .5, d + .6)
            for k in range(3):
                b.box(metal, rx - .25 + k * .05, rx + .3 - k * .05, rt + .8 + k * .3, rt + .83 + k * .3, d + .5, d + .6)
            b.sphere(emit('Blink', RED), rx + .025, rt + 1.65, .06, y=d + .3, c='NoLine')
            b.sphere(toon('Dish', (0.6, 0.6, 0.7)), rx + .8, rt + .45, .35, y=d + .4, sx=.45, sub=2)
        else:  # rooftop shack with lit door
            b.box(fac, rx, rx + 1.3, rt, rt + 1.1, d + .2, d + 1.4)
            b.box(trim, rx - .08, rx + 1.38, rt + 1.1, rt + 1.2, d + .1, d + 1.4)
            b.quad(emit('Door', AMBER), rx + .25, rx + .65, rt, rt + .8, d + .18, c='NoLine')
        tops.append((x, x + w, top, d))
        x += w + (rng.uniform(1.0, 3.2) if rng.random() < .5 else rng.uniform(0, .3))
    # cables + string lights between roofs
    for (a0, a1, at, ad), (b0, b1, bt, bd) in zip(tops, tops[1:] + [(tops[0][0] + W, 0, tops[0][2], tops[0][3])]):
        if rng.random() < .8:
            p = catenary(a1 - .3, at + .5, b0 + .3, bt + .5, rng.uniform(.5, 1.3), n=20)
            b.tube(trim, p, .018, y=-1.3, seg=5)
            if rng.random() < .7:
                cols = [AMBER, PINK, CYAN, (1, .9, .6)]
                for k, (px, pz) in enumerate(p[1:-1]):
                    b.sphere(emit(f'SL{k % 4}', cols[k % 4] if rng.random() < .5 else AMBER), px, pz - .06, .045, y=-1.35, sub=1, c='NoLine')
    b.build(wrap_width=W)
    wrap_objects([o for c in ('Main', 'NoLine') for o in coll(c).objects if o.name.startswith('Sign_')], W)
    render('Roof_Mid.png', glow_kw=dict(threshold=0.58, radii=(4, 14), strength=(.55, .35)))


# ============================================================ FOREGROUND (top band silhouettes)
def front():
    reset()
    W, H, Z0 = 40, 5, 0
    setup_render(W, H, PPU, 0, Z0, line=0)
    rng = random.Random(5)
    b = Batch()
    sil = flat('FG', (0.02, 0.015, 0.04))
    bulb_cols = [AMBER, PINK, CYAN]
    x = 0
    while x < W:
        x1 = x + rng.uniform(5, 11)
        p = catenary(x, H + .2, x1, H + .2 - rng.uniform(0, .8), rng.uniform(1.0, 2.6), n=26)
        b.tube(sil, p, .05, y=0, seg=6)
        if rng.random() < .55:
            for k, (px, pz) in enumerate(p[2:-2:2]):
                b.box(sil, px - .01, px + .01, pz - .25, pz, 0, .02)
                b.sphere(emit(f'FB{k % 3}', bulb_cols[k % 3]), px, pz - .3, .07, y=-.1, sub=1)
        x = x1 - rng.uniform(0, 2)
    b.build(wrap_width=W)
    render('Roof_Front.png', glow_kw=dict(threshold=0.5, radii=(4, 12), strength=(.6, .3)))


if __name__ == '__main__' or True:
    import sys
    which = globals().get('ONLY', ['sky', 'far', 'mid', 'front'])
    for name in which:
        globals()[name]()
