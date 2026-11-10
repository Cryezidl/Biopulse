# Biopulse "NII-punk" art kit (replaces the Neon Noir look). Builds on neon_kit.py (camera, batching, render, publish).
# Style rules (see ART_DIRECTION.md):
#   * Flat fills, 3 tones decided by face orientation only: bevel tops catch light, undersides go to shadow.
#   * Light comes from things in the frame (screens, tanks, lamps): LIGHTS tint nearby surfaces with their colour.
#   * Atmospheric perspective: surfaces fade to the HAZE colour with depth (world Y).
#   * Ink line = dark shade of the location hue, calligraphic width; no line on far layers.
# Usage: exec(open(SOLS_KIT, encoding="utf-8").read(), globals()); then style(...) before creating materials.
import bpy, math, random, colorsys
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\neon_kit.py", encoding="utf-8").read(), globals())

STYLE = dict(
    haze=(0.18, 0.35, 0.32), haze_near=0.0, haze_far=40.0, haze_max=0.0,
    ink=(0.02, 0.07, 0.07), light_dir=(-0.28, -0.55, 0.79),
    shade_k=0.62, light_k=1.28,
)
LIGHTS = []   # (x, z, radius, colour, strength)

# ---------------------------------------------------------------- animation mask pass
# Second render of the same layer where every surface is black except animated categories:
#   R = screens / glyphs (flicker + scanline scroll), G = liquid (wobble + rising caustics), B = light shafts / lamps (pulse + drift).
# The Unity shader Biopulse/LivingBackdrop reads <layer>_mask.png.
MASK_MODE = False
CATCOL = dict(none=(0, 0, 0), screen=(1, 0, 0), liquid=(0, 1, 0), shaft=(0, 0, 1), lamp=(0, 0, 1))


def mask_mat(name, color, alpha=1.0):
    key = ('mask', name, alpha)
    if key in MAT:
        return MAT[key]
    m = _flat0(f'{name}{alpha:.3f}', color, alpha)
    MAT[key] = m
    return m


_flat0, _grad0, _render0 = flat, gradient_mat, render


def flat(name, color, alpha=1.0, cat=None):
    if MASK_MODE:
        return mask_mat('mask_' + (cat or 'none'), CATCOL[cat or 'none'], alpha)
    return _flat0(name, color, alpha)


def gradient_mat(name, bottom, top, z0, z1, alpha_bottom=1.0, alpha_top=1.0, cat=None):
    if MASK_MODE:
        if (cat or 'none') == 'none' and alpha_bottom >= 1 and alpha_top >= 1:
            return mask_mat('mask_none', (0, 0, 0))
        c = CATCOL[cat or 'none']
        return _grad0(f'mask_{cat}_{alpha_bottom:.2f}_{alpha_top:.2f}_{z0:.1f}_{z1:.1f}', c, c, z0, z1, alpha_bottom, alpha_top)
    return _grad0(name, bottom, top, z0, z1, alpha_bottom, alpha_top)


def render(name, glow_kw=None, folder=None):
    if MASK_MODE:
        bpy.context.scene.render.use_freestyle = False
        return _render0(name.replace('.png', '_mask.png'), None, folder)
    return _render0(name, glow_kw, folder)


def with_mask(fn):
    """Render a layer function twice: colour, then the animation mask."""
    global MASK_MODE
    MASK_MODE = False
    fn()
    MASK_MODE = True
    try:
        fn()
    finally:
        MASK_MODE = False


def style(**kw):
    STYLE.update(kw)
    MAT.clear()


def light(x, z, radius, color, strength=1.0):
    LIGHTS.append((x, z, radius, color, strength))


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def shade(c, k):
    """Darken/lighten while shifting hue slightly (shadows cooler, lights warmer) — avoids grey mud."""
    h, l, s = colorsys.rgb_to_hls(*c)
    if k < 1:
        h = h + (0.52 - h) * 0.08     # shadows lean teal-blue
        s = min(1, s * 1.1)
    else:
        h = h + (0.12 - h) * 0.04     # lights lean warm
    return colorsys.hls_to_rgb(h % 1, max(0, min(1, l * k)), s)


def light_field():
    """Shared node group: Position -> summed light colour. Filled by finalize_lights() after the scene is built,
    so screens/tanks added during construction still light every material."""
    ng = bpy.data.node_groups.get('LightField')
    if ng is None:
        ng = bpy.data.node_groups.new('LightField', 'ShaderNodeTree')
        ng.interface.new_socket(name='Pos', in_out='INPUT', socket_type='NodeSocketVector')
        ng.interface.new_socket(name='Light', in_out='OUTPUT', socket_type='NodeSocketVector')
        _fill_light_field(ng, [])
    return ng


def _fill_light_field(ng, lights):
    nt = ng
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    N, L = nt.nodes.new, nt.links.new
    gi = N('NodeGroupInput'); go = N('NodeGroupOutput')
    flat_pos = N('ShaderNodeVectorMath'); flat_pos.operation = 'MULTIPLY'
    flat_pos.inputs[1].default_value = (1, 0, 1)
    L(gi.outputs[0], flat_pos.inputs[0])
    total = None
    for (lx, lz, r, lc, k) in lights:
        sub = N('ShaderNodeVectorMath'); sub.operation = 'SUBTRACT'
        sub.inputs[1].default_value = (lx, 0, lz)
        L(flat_pos.outputs[0], sub.inputs[0])
        ln = N('ShaderNodeVectorMath'); ln.operation = 'LENGTH'
        L(sub.outputs[0], ln.inputs[0])
        f = N('ShaderNodeMapRange'); f.inputs['From Min'].default_value = 0; f.inputs['From Max'].default_value = r
        f.inputs['To Min'].default_value = 1; f.inputs['To Max'].default_value = 0; f.clamp = True
        L(ln.outputs['Value'], f.inputs['Value'])
        sq = N('ShaderNodeMath'); sq.operation = 'POWER'; sq.inputs[1].default_value = 1.8
        L(f.outputs['Result'], sq.inputs[0])
        sc = N('ShaderNodeVectorMath'); sc.operation = 'SCALE'
        sc.inputs[0].default_value = tuple(v * k for v in lin(lc))
        L(sq.outputs[0], sc.inputs['Scale'])
        if total is None:
            total = sc.outputs[0]
        else:
            add = N('ShaderNodeVectorMath'); add.operation = 'ADD'
            L(total, add.inputs[0]); L(sc.outputs[0], add.inputs[1]); total = add.outputs[0]
    if total is None:
        z = N('ShaderNodeVectorMath'); z.operation = 'SCALE'; z.inputs['Scale'].default_value = 0
        L(gi.outputs[0], z.inputs[0]); total = z.outputs[0]
    L(total, go.inputs[0])


def finalize_lights(wrap=None):
    """Bake LIGHTS into the shared group (with copies at +-wrap so tiling strips stay seamless)."""
    ls = list(LIGHTS)
    if wrap:
        ls += [(x + o, z, r, c, k) for (x, z, r, c, k) in LIGHTS for o in (-wrap, wrap)]
    _fill_light_field(light_field(), ls)


def new_scene():
    reset()
    LIGHTS.clear()
    ng = bpy.data.node_groups.get('LightField')
    if ng:
        bpy.data.node_groups.remove(ng)


# ---------------------------------------------------------------- micro-detail patterns (procedural tiles)
def _glyph_tile(n=512, cell=32, seed=1):
    """Ornamental tech-glyph wall pattern (our own vocabulary: stencil strokes, brackets, dots, circuit hooks)."""
    rng = np.random.default_rng(seed)
    a = np.zeros((n, n), np.float32)
    s = 4                                     # stroke px
    def hl(x0, x1, y):
        a[y:y + s, min(x0, x1):max(x0, x1) + s] = 1
    def vl(x, y0, y1):
        a[min(y0, y1):max(y0, y1) + s, x:x + s] = 1
    g = cell // 4
    for cy in range(n // cell):
        for cx in range(n // cell):
            ox, oy = cx * cell + 3, cy * cell + 3
            P = lambda i, j: (ox + i * (g - 1), oy + j * (g - 1))
            kind = rng.integers(0, 9)
            if kind == 0:
                continue                               # rest cell
            if (cy % 4) == 0 and kind < 4:            # band rows: repeated bracket motif
                x0, y0 = P(0, 1); x1, y1 = P(3, 1); hl(x0, x1, y0); vl(x0, y0, P(0, 3)[1]); vl(x1, y0, P(3, 3)[1])
                continue
            for _ in range(rng.integers(2, 5)):
                i, j = rng.integers(0, 4, 2)
                if rng.random() < 0.5:
                    i2 = rng.integers(0, 4); x0, y0 = P(i, j); x1, _ = P(i2, j); hl(x0, x1, y0)
                else:
                    j2 = rng.integers(0, 4); x0, y0 = P(i, j); _, y1 = P(i, j2); vl(x0, y0, y1)
            if rng.random() < 0.35:
                x0, y0 = P(rng.integers(1, 3), rng.integers(1, 3)); a[y0:y0 + 5, x0:x0 + 5] = 1
    for y in range(0, n, cell * 4):          # faint horizontal register lines between bands
        a[y:y + 1, :] = 0.6
    return a


def _hatch_tile(n=256, period=8, w=2):
    a = np.zeros((n, n), np.float32)
    for y in range(0, n, period):
        a[y:y + w, :] = 1
    return a


def _seam_tile(n=512, seed=2):
    """Panel seams: irregular rectangles of thin grooves + rivet dots."""
    rng = np.random.default_rng(seed)
    a = np.zeros((n, n), np.float32)
    def split(x0, y0, x1, y1, d):
        if d > 3 or (x1 - x0) < 70 or (y1 - y0) < 70 or (d > 1 and rng.random() < 0.3):
            a[y0:y0 + 4, x0:x1] = 1; a[y0:y1, x0:x0 + 4] = 1
            for (x, y) in ((x0 + 12, y0 + 12), (x1 - 16, y0 + 12), (x0 + 12, y1 - 16), (x1 - 16, y1 - 16)):
                a[y:y + 6, x:x + 6] = 0.9
            return
        if (x1 - x0) > (y1 - y0):
            m = int(x0 + (x1 - x0) * rng.choice([0.33, 0.5, 0.66])); split(x0, y0, m, y1, d + 1); split(m, y0, x1, y1, d + 1)
        else:
            m = int(y0 + (y1 - y0) * rng.choice([0.33, 0.5, 0.66])); split(x0, y0, x1, m, d + 1); split(x0, m, x1, y1, d + 1)
    split(0, 0, n, n, 0)
    return a


def _noise_tile(n=256, seed=3):
    rng = np.random.default_rng(seed)
    a = rng.random((n // 16, n // 16)).astype(np.float32)
    a = np.kron(a, np.ones((16, 16), np.float32))
    for _ in range(3):
        a = (a + np.roll(a, 4, 0) + np.roll(a, -4, 0) + np.roll(a, 4, 1) + np.roll(a, -4, 1)) / 5
    return (a - a.min()) / max(1e-6, a.max() - a.min())


PATTERNS = dict(glyph=_glyph_tile, hatch=_hatch_tile, seam=_seam_tile, noise=_noise_tile)


def pattern_image(kind):
    name = 'PAT_' + kind
    img = bpy.data.images.get(name)
    if img is not None:
        return img
    path = os.path.join(SPRITES, 'tex', f'pat_{kind}.png')
    if not os.path.exists(path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        a = PATTERNS[kind]()
        h, w = a.shape
        rgba = np.stack([a, a, a, np.ones_like(a)], axis=2).astype(np.float32)
        tmp = bpy.data.images.new('tmp_' + name, w, h, alpha=False)
        tmp.pixels.foreach_set(rgba.ravel())
        tmp.filepath_raw = path
        tmp.file_format = 'PNG'
        tmp.save()
        bpy.data.images.remove(tmp)
    img = bpy.data.images.load(path, check_existing=True)
    img.name = name
    img.colorspace_settings.name = 'Non-Color'
    img.use_fake_user = True
    return img


def sol(name, base, lit=None, dark=None, haze=True, lights=True, glow=0.0, pat=None, pat_scale=1.0, pat_k=0.3, pat_light=False):
    """Main surface material. base is the colour of a face pointing at the camera.
    pat: micro-detail tile ('glyph' | 'hatch' | 'seam' | 'noise') mapped in world X/Z; pat_scale = tiles per unit;
    pat_k = strength; pat_light = pattern lightens (engraved light lines) instead of darkening."""
    key = ('sol', name)
    if key in MAT:
        return MAT[key]
    if MASK_MODE:
        return mask_mat('mask_none', (0, 0, 0))
    m, nt = _new_mat(name)
    N = nt.nodes.new
    L = nt.links.new
    out = N('ShaderNodeOutputMaterial')
    geo = N('ShaderNodeNewGeometry')
    dot = N('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'
    d = Vector(STYLE['light_dir']).normalized()
    dot.inputs[1].default_value = d
    L(geo.outputs['Normal'], dot.inputs[0])
    ramp = N('ShaderNodeValToRGB'); ramp.color_ramp.interpolation = 'CONSTANT'
    el = ramp.color_ramp.elements
    el[0].position = 0.0; el[0].color = lin(dark or shade(base, STYLE['shade_k'])) + (1,)
    el[1].position = 0.5; el[1].color = lin(base) + (1,)
    e3 = el.new(0.76); e3.color = lin(lit or shade(base, STYLE['light_k'])) + (1,)
    mr = N('ShaderNodeMapRange'); mr.inputs['From Min'].default_value = -1; mr.inputs['From Max'].default_value = 1
    L(dot.outputs['Value'], mr.inputs['Value'])
    L(mr.outputs['Result'], ramp.inputs['Fac'])
    col = ramp.outputs['Color']

    pos = geo.outputs['Position']
    if pat:
        sp = N('ShaderNodeSeparateXYZ'); L(pos, sp.inputs[0])
        cb = N('ShaderNodeCombineXYZ'); L(sp.outputs['X'], cb.inputs['X']); L(sp.outputs['Z'], cb.inputs['Y'])
        sc = N('ShaderNodeVectorMath'); sc.operation = 'SCALE'; sc.inputs['Scale'].default_value = pat_scale
        L(cb.outputs[0], sc.inputs[0])
        tex = N('ShaderNodeTexImage'); tex.image = pattern_image(pat); tex.extension = 'REPEAT'; tex.interpolation = 'Linear'
        L(sc.outputs[0], tex.inputs['Vector'])
        fk = N('ShaderNodeMath'); fk.operation = 'MULTIPLY'; fk.inputs[1].default_value = pat_k
        L(tex.outputs['Color'], fk.inputs[0])
        mxp = N('ShaderNodeMix'); mxp.data_type = 'RGBA'
        L(fk.outputs[0], mxp.inputs[0]); L(col, mxp.inputs[6])
        tgt = shade(base, 1.9) if pat_light else shade(base, 0.45)
        mxp.inputs[7].default_value = lin(tgt) + (1,)
        col = mxp.outputs[2]
    sepz = N('ShaderNodeSeparateXYZ'); L(pos, sepz.inputs[0])
    if STYLE.get('dark_top'):
        # value falls off towards the top of the frame (and below dark_bottom): light pools sit in a dark mass
        z_a, z_b, k_top = STYLE['dark_top']
        vf = N('ShaderNodeMapRange'); vf.clamp = True
        vf.inputs['From Min'].default_value = z_a; vf.inputs['From Max'].default_value = z_b
        vf.inputs['To Min'].default_value = 1.0; vf.inputs['To Max'].default_value = k_top
        L(sepz.outputs['Z'], vf.inputs['Value'])
        vm = N('ShaderNodeVectorMath'); vm.operation = 'SCALE'
        L(col, vm.inputs[0]); L(vf.outputs['Result'], vm.inputs['Scale'])
        col = vm.outputs[0]
    if lights:
        grp = N('ShaderNodeGroup'); grp.node_tree = light_field()
        L(pos, grp.inputs[0])
        total = grp.outputs[0]
        # surface * (1 + L*gain) + L*0.05  : light tints the material, barely adds to blacks
        k1 = N('ShaderNodeVectorMath'); k1.operation = 'MULTIPLY_ADD'
        g_ = STYLE.get('light_gain', 1.6)
        k1.inputs[1].default_value = (g_, g_, g_); k1.inputs[2].default_value = (1, 1, 1)
        L(total, k1.inputs[0])
        mul = N('ShaderNodeVectorMath'); mul.operation = 'MULTIPLY'
        L(col, mul.inputs[0]); L(k1.outputs[0], mul.inputs[1])
        add2 = N('ShaderNodeVectorMath'); add2.operation = 'MULTIPLY_ADD'
        L(total, add2.inputs[0]); add2.inputs[1].default_value = (.05, .05, .05); L(mul.outputs[0], add2.inputs[2])
        col = add2.outputs[0]

    if haze and STYLE['haze_max'] > 0:
        sep = N('ShaderNodeSeparateXYZ'); L(pos, sep.inputs[0])
        hf = N('ShaderNodeMapRange'); hf.clamp = True
        hf.inputs['From Min'].default_value = STYLE['haze_near']; hf.inputs['From Max'].default_value = STYLE['haze_far']
        hf.inputs['To Max'].default_value = STYLE['haze_max']
        L(sep.outputs['Y'], hf.inputs['Value'])
        mx = N('ShaderNodeMix'); mx.data_type = 'RGBA'
        L(hf.outputs['Result'], mx.inputs[0])
        L(col, mx.inputs[6]); mx.inputs[7].default_value = lin(STYLE['haze']) + (1,)
        col = mx.outputs[2]
    em = N('ShaderNodeEmission')
    L(col, em.inputs['Color'])
    em.inputs['Strength'].default_value = 1.0 + glow
    L(em.outputs[0], out.inputs['Surface'])
    MAT[key] = m
    return m


def glowm(name, color, strength=1.0, cat='screen'):
    """Self-lit (screens, lamps, liquid). Not hazed: light cuts through the air. cat = animation mask category."""
    if MASK_MODE:
        return mask_mat('mask_' + cat, CATCOL[cat])
    return emit(name, color, strength)


def ink(thickness, color=None, calligraphy=True):
    """Configure the Freestyle line after setup_render()."""
    s = bpy.context.scene
    fs = s.view_layers[0].freestyle_settings
    fs.crease_angle = math.radians(100)
    s.render.use_freestyle = thickness > 0 and not MASK_MODE
    for ls in fs.linesets:
        st = ls.linestyle
        st.color = lin(color or STYLE['ink'])
        st.thickness = max(thickness, 0.1)
        st.caps = 'ROUND'
        for mdf in list(st.thickness_modifiers):
            st.thickness_modifiers.remove(mdf)
        if calligraphy and thickness > 0:
            cm = st.thickness_modifiers.new('Hand', 'CALLIGRAPHY')
            cm.orientation = math.radians(60)
            cm.thickness_min = thickness * 0.45
            cm.thickness_max = thickness * 1.35
            cm.blend = 'MIX'; cm.influence = 1.0
        ls.select_crease = True
        ls.select_border = False


# ---------------------------------------------------------------- shape vocabulary
class Pal:
    """A location palette: wall tones + metal + accent + glow colours (display sRGB)."""
    def __init__(self, **kw):
        self.__dict__.update(kw)


def plate(b, mat, x0, x1, z0, z1, y=0.0, d=0.3, bevel=0.06, c='Main', cut=0.0, corners=(1, 1, 1, 1)):
    """Machined plate. cut>0 chamfers the chosen corners (bl, br, tr, tl) — the signature Nine Sols-style silhouette;
    the front rim is bevelled so chamfers catch light/shadow like the straight edges."""
    if cut <= 0:
        b.box(mat, x0, x1, z0, z1, y, y + d, bevel=bevel, c=c)
        return
    if x1 < x0: x0, x1 = x1, x0
    if z1 < z0: z0, z1 = z1, z0
    k = min(cut, (x1 - x0) * 0.45, (z1 - z0) * 0.45)
    bl, br, tr, tl = corners
    pts = []
    pts += [(x0, z0 + k), (x0 + k, z0)] if bl else [(x0, z0)]
    pts += [(x1 - k, z0), (x1, z0 + k)] if br else [(x1, z0)]
    pts += [(x1, z1 - k), (x1 - k, z1)] if tr else [(x1, z1)]
    pts += [(x0 + k, z1), (x0, z1 - k)] if tl else [(x0, z1)]
    bm = b._bm(mat, c)
    front = [bm.verts.new((p[0], y, p[1])) for p in pts]
    back = [bm.verts.new((p[0], y + d, p[1])) for p in pts]
    n = len(pts)
    bm.faces.new(front)                # pts are CCW on screen (seen from -Y) -> normal faces the camera
    bm.faces.new(back[::-1])
    rim = []
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new([front[i], back[i], back[j], front[j]])
    if bevel > 0:
        bm.edges.ensure_lookup_table()
        fs = set(front)
        rim = [e for e in bm.edges if e.verts[0] in fs and e.verts[1] in fs]
        bmesh.ops.bevel(bm, geom=rim + front, offset=min(bevel, k * 0.6 if k else bevel), segments=1, affect='EDGES', clamp_overlap=True)


def chamfer_corners(rng):
    """Asymmetric chamfers read hand-designed; all-four reads generic."""
    return rng.choice([(0, 0, 1, 0), (0, 0, 1, 1), (1, 0, 1, 0), (0, 1, 0, 1), (1, 1, 1, 1), (0, 0, 0, 1), (1, 0, 0, 0)])


def slats(b, mat, x0, x1, z0, z1, y, n=None, gap=0.09, c='Main'):
    """Horizontal louvres (vents)."""
    h = z1 - z0
    n = n or max(2, int(h / 0.16))
    step = h / n
    for i in range(n):
        za = z0 + i * step + gap * 0.5
        b.box(mat, x0, x1, za, za + step - gap, y, y + 0.08, bevel=min(0.03, (step - gap) * 0.3), c=c)


def bolts(b, mat, x0, x1, z0, z1, y, inset=0.12, r=0.035, c='NoLine'):
    for x in (x0 + inset, x1 - inset):
        for z in (z0 + inset, z1 - inset):
            b.ngon(mat, x, z, r, 6, y - 0.02, y, c=c)


def pipe_h(b, mat, x0, x1, z, r, y, collar=None, every=3.0, c='Main'):
    b.cyl(mat, (x0 + x1) / 2, z, r, x1 - x0, axis='X', y=y, seg=12, c=c)
    if collar:
        x = x0 + every / 2
        while x < x1:
            b.cyl(collar, x, z, r * 1.3, 0.16, axis='X', y=y, seg=12, c=c)
            x += every


def pipe_v(b, mat, x, z0, z1, r, y, collar=None, every=2.0, c='Main'):
    b.cyl(mat, x, (z0 + z1) / 2, r, z1 - z0, axis='Z', y=y, seg=12, c=c)
    if collar:
        z = z0 + every / 2
        while z < z1:
            b.cyl(collar, x, z, r * 1.3, 0.14, axis='Z', y=y, seg=12, c=c)
            z += every


def cable(b, mat, x0, z0, x1, z1, sag, r, y, c='Main'):
    b.tube(mat, catenary(x0, z0, x1, z1, sag, n=18), r, y=y, seg=6, c=c)


def screen_glyphs(b, x0, x1, z0, z1, y, col, rng, kind=None):
    """Content of a monitor — EMG traces, bar meters, a hand schematic, text rows. Our own motifs."""
    g = glowm(f'Glyph{col}', col, 1.0)
    w, h = x1 - x0, z1 - z0
    kind = kind or rng.choice(['emg', 'bars', 'rows', 'hand', 'emg2'])
    t = max(0.018, min(w, h) * 0.035)
    if kind in ('emg', 'emg2'):
        lanes = 2 if kind == 'emg2' else 1
        for ln in range(lanes):
            zc = z0 + h * (ln + 0.5) / lanes
            pts, x = [], x0 + w * 0.06
            while x < x1 - w * 0.06:
                burst = abs(math.sin(x * 1.3 + ln)) > 0.55
                amp = (h / lanes) * (0.38 if burst else 0.06) * rng.uniform(0.4, 1)
                pts.append((x, zc + rng.uniform(-amp, amp)))
                x += w * 0.018
            for (ax, az), (bx, bz) in zip(pts[:-1], pts[1:]):
                b.bar(g, ax, az, bx, bz, t, y, y + 0.01, c='NoLine')
    elif kind == 'bars':
        n = rng.randint(6, 12)
        for i in range(n):
            bx = x0 + w * (0.08 + 0.84 * i / n)
            bh = h * rng.uniform(0.15, 0.8)
            b.quad(g, bx, bx + w * 0.84 / n * 0.6, z0 + h * 0.1, z0 + h * 0.1 + bh, y=y, c='NoLine')
    elif kind == 'rows':
        z = z1 - h * 0.15
        while z > z0 + h * 0.1:
            b.quad(g, x0 + w * 0.08, x0 + w * rng.uniform(0.3, 0.9), z - t * 1.6, z, y=y, c='NoLine')
            z -= t * 4
    elif kind == 'hand':
        cx, cz, s = (x0 + x1) / 2, z0 + h * 0.2, min(w, h) * 0.12
        b.quad(g, cx - s * 1.6, cx + s * 1.6, cz, cz + s * 2.2, y=y, c='NoLine')       # palm
        for i, L in enumerate((2.6, 3.0, 2.8, 2.2)):
            fx = cx - s * 1.5 + i * s * 0.95
            b.quad(g, fx, fx + s * 0.6, cz + s * 2.4, cz + s * (2.4 + L), y=y, c='NoLine')
        b.bar(g, cx - s * 1.6, cz + s * 1.2, cx - s * 3.0, cz + s * 2.8, s * 0.6, y, y + 0.01, c='NoLine')
    # frame ticks
    b.quad(g, x0 + w * 0.04, x0 + w * 0.18, z1 - t * 2.2, z1 - t * 1.2, y=y, c='NoLine')


def screen(b, pal, x0, x1, z0, z1, y, rng, col=None, kind=None, lit=True, power=1.0):
    """Monitor: bezel + glowing face + glyphs, and it lights its surroundings."""
    col = col or pal.screen
    plate(b, pal.m_dark, x0 - 0.1, x1 + 0.1, z0 - 0.1, z1 + 0.1, y, 0.2, 0.05)
    face = mix(col, (0, 0, 0), 0.62)
    b.quad(glowm(f'Face{face}', face), x0, x1, z0, z1, y=y - 0.01, c='NoLine')
    screen_glyphs(b, x0, x1, z0, z1, y - 0.02, col, rng, kind)
    if lit:
        light((x0 + x1) / 2, (z0 + z1) / 2, max(x1 - x0, z1 - z0) * 1.1 + 0.6, col, 0.5 * power)


# ---------------------------------------------------------------- modules (fill a rectangle)
LABELS = ['Т-7', 'ВЕНТ', '380В', '№14', 'ЛИНИЯ 2', 'К-12', 'НЕ ВКЛ.', 'СЕКЦ. 3', 'Б-4', 'ОТСЕК 9', 'ФИЛЬТР', 'ВОДА']


def _m(pal, name, fallback='m_wall'):
    return getattr(pal, name, None) or getattr(pal, fallback)


def label(b, pal, x, z, rng, y, size=0.11):
    word = rng.choice(LABELS)
    w = len(word) * size * 0.62
    plate(b, pal.m_light, x, x + w + 0.12, z, z + size + 0.1, y, 0.04, 0.012)
    text(word, sol('LabelInk', mix(pal.wall, (0, 0, 0), 0.75), lights=False, haze=False), x + 0.06 + w / 2, z + 0.05, size, y=y - 0.01, c='NoLine')


def rivets(b, pal, x0, x1, z, y, step=0.16, r=0.022):
    x = x0
    while x <= x1:
        b.ngon(pal.m_dark, x, z, r, 6, y - 0.01, y, c='NoLine')
        x += step


def framed(b, pal, r, y, rng, inner=None, cut=None):
    """Dark chamfered frame + inset plate (double border). Returns the inner rect."""
    x0, x1, z0, z1 = r
    cor = chamfer_corners(rng)
    cut = 0.2 if cut is None else cut
    plate(b, pal.m_dark, x0, x1, z0, z1, y + 0.05, 0.3, 0.05, cut=cut, corners=cor)
    m = 0.07
    plate(b, inner or _m(pal, rng.choice(['m_seam', 'm_wall', 'm_wall2', 'm_hatch'])), x0 + m, x1 - m, z0 + m, z1 - m, y, 0.3, 0.05,
          cut=max(0.0, cut - 0.04), corners=cor)
    return (x0 + m, x1 - m, z0 + m, z1 - m)


def mod_plate(b, pal, r, y, rng):
    x0, x1, z0, z1 = framed(b, pal, r, y, rng)
    w, h = x1 - x0, z1 - z0
    if w > 0.9 and h > 0.7 and rng.random() < 0.55:
        rivets(b, pal, x0 + 0.12, x1 - 0.12, z1 - 0.1, y - 0.01)
    if w > 1.1 and rng.random() < 0.4:
        label(b, pal, x0 + 0.15, z0 + 0.15, rng, y - 0.03)
    if h > 1.2 and rng.random() < 0.35:    # raised service hatch
        hw = min(0.8, w * 0.5)
        cx = x0 + w * rng.uniform(0.3, 0.7)
        plate(b, pal.m_wall2, cx - hw / 2, cx + hw / 2, z0 + h * 0.35, z0 + h * 0.75, y - 0.05, 0.1, 0.03, cut=0.08, corners=(1, 1, 1, 1))
        b.box(pal.m_dark, cx - 0.12, cx + 0.12, z0 + h * 0.5, z0 + h * 0.5 + 0.05, y - 0.09, y - 0.05)


def mod_vent(b, pal, r, y, rng):
    x0, x1, z0, z1 = framed(b, pal, r, y + 0.05, rng, inner=pal.m_dark, cut=0.12)
    slats(b, pal.m_wall2, x0 + 0.06, x1 - 0.06, z0 + 0.06, z1 - 0.06, y - 0.02, gap=0.07)


def mod_recess(b, pal, r, y, rng):
    """Dark niche with an engraved glyph wall and pipes — rest area + depth."""
    x0, x1, z0, z1 = r
    plate(b, _m(pal, 'm_glyph', 'm_deep'), x0, x1, z0, z1, y + 0.5, 0.2, 0.0)
    plate(b, pal.m_wall2, x0, x1, z1 - 0.16, z1, y - 0.05, 0.5, 0.03, cut=0.06, corners=(1, 1, 0, 0))
    for i in range(rng.randint(1, 3)):
        px = rng.uniform(x0 + 0.2, x1 - 0.2)
        pipe_v(b, pal.m_metal, px, z0, z1 - 0.16, rng.uniform(0.05, 0.12), y + 0.3, pal.m_dark, 0.9)


def mod_glyph(b, pal, r, y, rng):
    """Big engraved ornament panel (the dense wall texture of the reference)."""
    framed(b, pal, r, y, rng, inner=_m(pal, 'm_glyph', 'm_deep'), cut=0.25)


def mod_ribs(b, pal, r, y, rng):
    x0, x1, z0, z1 = framed(b, pal, r, y + 0.05, rng, inner=pal.m_deep, cut=0.15)
    n = max(3, int((x1 - x0) / 0.16))
    for i in range(n):
        cx = x0 + (i + 0.5) * (x1 - x0) / n
        plate(b, pal.m_wall, cx - 0.045, cx + 0.045, z0 + 0.05, z1 - 0.05, y - 0.02, 0.15, 0.02)


def mod_pipes(b, pal, r, y, rng):
    x0, x1, z0, z1 = r
    plate(b, pal.m_deep, x0, x1, z0, z1, y + 0.4, 0.1, 0.0)
    horiz = (x1 - x0) > (z1 - z0)
    n = rng.randint(3, 5)
    span = (z1 - z0) if horiz else (x1 - x0)
    rr = min(0.13, span / n * 0.38)
    for i in range(n):
        c_ = (z0 if horiz else x0) + span * (i + 0.5) / n
        if horiz:
            pipe_h(b, rng.choice([pal.m_metal, pal.m_wall2]), x0, x1, c_, rr, y + 0.1)
        else:
            pipe_v(b, rng.choice([pal.m_metal, pal.m_wall2]), c_, z0, z1, rr, y + 0.1)
    t = (x0 if horiz else z0) + 0.3
    while t < (x1 if horiz else z1) - 0.2:   # clamp brackets across the bundle
        if horiz:
            plate(b, pal.m_dark, t, t + 0.12, z0 + 0.02, z1 - 0.02, y - 0.05, 0.1, 0.02)
        else:
            plate(b, pal.m_dark, x0 + 0.02, x1 - 0.02, t, t + 0.12, y - 0.05, 0.1, 0.02)
        t += 0.9


def mod_stepped(b, pal, r, y, rng):
    """Stepped machine housing: three receding chamfered blocks."""
    x0, x1, z0, z1 = r
    cor = chamfer_corners(rng)
    for i, (mat, dz) in enumerate(((pal.m_dark, 0.0), (pal.m_wall, 0.12), (_m(pal, 'm_seam'), 0.26))):
        m = 0.08 + i * 0.16
        if x1 - x0 - 2 * m < 0.2 or z1 - z0 - 1.6 * m < 0.2:
            break
        plate(b, mat, x0 + m, x1 - m, z0 + m * 0.6, z1 - m, y + 0.1 - dz, 0.3, 0.05, cut=0.22 - i * 0.04, corners=cor)
    if (x1 - x0) > 1.2:
        b.box(glowm(f'Strip{pal.lamp}', mix(pal.lamp, (0, 0, 0), 0.15), cat='lamp'), x0 + 0.5, x1 - 0.5, z0 + 0.22, z0 + 0.26, y - 0.2, y - 0.17, c='NoLine')


def mod_screen(b, pal, r, y, rng):
    x0, x1, z0, z1 = framed(b, pal, r, y, rng, cut=0.14)
    m = min(x1 - x0, z1 - z0) * 0.14
    screen(b, pal, x0 + m, x1 - m - 0.12, z0 + m, z1 - m, y - 0.06, rng, power=0.8)
    for i in range(3):   # side buttons
        bz = z0 + m + i * 0.13
        b.box(pal.m_light, x1 - m - 0.08, x1 - m, bz, bz + 0.08, y - 0.08, y - 0.04, bevel=0.01)


def mod_gauges(b, pal, r, y, rng):
    x0, x1, z0, z1 = framed(b, pal, r, y, rng, cut=0.12)
    n = max(1, int((x1 - x0) / 0.6))
    for i in range(n):
        cx = x0 + (i + 0.5) * (x1 - x0) / n
        cz = (z0 + z1) / 2
        rr = min(0.22, (x1 - x0) / n * 0.35, (z1 - z0) * 0.3)
        b.ngon(pal.m_dark, cx, cz, rr, 16, y - 0.06, y)
        b.disc(glowm(f'Dial{pal.lamp}', mix(pal.lamp, (0, 0, 0), 0.3), cat='lamp'), cx, cz, rr * 0.75, y=y - 0.07, c='NoLine')
        a = rng.uniform(0.3, 2.8)
        b.bar(pal.m_deep, cx, cz, cx + math.cos(a) * rr * 0.7, cz + math.sin(a) * rr * 0.7, rr * 0.12, y - 0.09, y - 0.08, c='NoLine')


def mod_lamps(b, pal, r, y, rng):
    """Row of small indicator lamps — the warm 'tube-era' accent."""
    x0, x1, z0, z1 = framed(b, pal, r, y, rng, inner=pal.m_wall2, cut=0.1)
    cols = max(2, int((x1 - x0) / 0.3)); rows = max(1, int((z1 - z0) / 0.45))
    for i in range(cols):
        for j in range(rows):
            cx = x0 + (i + 0.5) * (x1 - x0) / cols
            cz = z0 + (j + 0.5) * (z1 - z0) / rows
            on = rng.random() < 0.45
            colr = pal.lamp if on else mix(pal.wall, (0, 0, 0), 0.5)
            b.disc(glowm(f'Lamp{colr}', colr, cat='screen') if on else sol('LampOff', colr, lights=False), cx, cz, 0.06, y=y - 0.03, seg=10, c='NoLine')


MODULES = [(mod_plate, 4), (mod_vent, 1.4), (mod_recess, 1.0), (mod_screen, 0.7), (mod_gauges, 0.6), (mod_lamps, 0.6),
           (mod_glyph, 1.2), (mod_ribs, 1.0), (mod_pipes, 1.0), (mod_stepped, 1.0)]


def pick(rng, table):
    tot = sum(w for _, w in table)
    v = rng.uniform(0, tot)
    for f, w in table:
        v -= w
        if v <= 0:
            return f
    return table[-1][0]


def machine_wall(b, pal, x0, x1, z0, z1, y, rng, min_size=0.9, gap=0.05, table=None, quiet=0.0):
    """BSP-subdivide a rectangle into machined panels. quiet>0 = more plain plates (rest zone)."""
    table = table or MODULES
    def rec(r, depth):
        ax, bx, az, bz = r
        w, h = bx - ax, bz - az
        stop = (w < min_size * 2 and h < min_size * 2) or (depth > 2 and rng.random() < 0.24 + quiet)
        if stop:
            f = mod_plate if rng.random() < quiet else pick(rng, table)
            f(b, pal, (ax + gap, bx - gap, az + gap, bz - gap), y + rng.choice([0, 0, 0.08, -0.06]), rng)
            return
        if (w > h * 1.15 or h < min_size * 2) and w >= min_size * 2:
            t = rng.choice([0.33, 0.5, 0.5, 0.62, 0.4, 0.25])
            sx = ax + w * t
            rec((ax, sx, az, bz), depth + 1); rec((sx, bx, az, bz), depth + 1)
        else:
            t = rng.choice([0.3, 0.5, 0.65, 0.45, 0.2])
            sz = az + h * t
            rec((ax, bx, az, sz), depth + 1); rec((ax, bx, sz, bz), depth + 1)
    rec((x0, x1, z0, z1), 0)
    b.box(_m(pal, 'm_glyph', 'm_deep'), x0, x1, z0, z1, y + 0.35, y + 0.45, c='NoLine')


def buttress(b, pal, x, z_top, z_foot, reach, y, side=1, w=0.9):
    """Huge diagonal strut from the ceiling down to a pillar (cathedral-like hall shape of the reference)."""
    def P(pts):
        return pts if side > 0 else pts[::-1]
    b.prism(_m(pal, 'm_hatch'), P([(x, z_foot), (x + side * w * 0.6, z_foot + w), (x + side * reach, z_top), (x, z_top)]), y, y + 0.6)
    b.prism(pal.m_dark, P([(x + side * 0.18, z_foot + 0.2), (x + side * w * 0.45, z_foot + w * 0.9),
                           (x + side * (reach - 0.45), z_top - 0.18), (x + side * 0.18, z_top - 0.18)]), y - 0.05, y)
    b.prism(pal.m_light, P([(x + side * w * 0.6, z_foot + w), (x + side * (w * 0.6 + 0.2), z_foot + w - 0.1),
                            (x + side * (reach + 0.25), z_top), (x + side * reach, z_top)]), y - 0.02, y + 0.5)


def light_shaft(b, x_top, z_top, x_bot, z_bot, w_top, w_bot, col, alpha, y, n=6):
    """Soft volumetric beam: stacked translucent trapezoids, densest near the source."""
    for i in range(n):
        t = i / n
        zb = z_top + (z_bot - z_top) * (1 - t * 0.7)
        k = (zb - z_top) / (z_bot - z_top)
        xb = x_top + (x_bot - x_top) * k
        wb = w_top + (w_bot - w_top) * k
        s = 1 - 0.55 * t
        b.prism(gradient_mat(f'Shaft{col}{alpha:.3f}{z_top:.1f}{z_bot:.1f}', col, col, z_bot, z_top, 0.0, alpha / n * 2, cat='shaft'),
                [(x_top - w_top * s / 2, z_top), (x_top + w_top * s / 2, z_top), (xb + wb * s / 2, zb), (xb - wb * s / 2, zb)],
                y - i * 0.01, y - i * 0.01 + 0.001, c='NoLine')


def sign(b, word, x, z, size, plate_mat, text_mat, y, pad=0.14, c='Main'):
    """Enamel plaque with lettering (Soviet institute signage)."""
    w = text_width(word, size)
    plate(b, plate_mat, x - w / 2 - pad, x + w / 2 + pad, z - pad * 0.8, z + size * 0.75 + pad * 0.8, y, 0.1, 0.03, c=c)
    text(word, text_mat, x, z, size, y=y - 0.02, c='NoLine')


def contact_sheet(items, out, W=2000, H=1300, scale=1.4, bg=(0.08, 0.15, 0.15)):
    """items: [(file in SPRITES, ppu)] -> one opaque preview image at a common world scale."""
    acc = np.zeros((H, W, 3), np.float32)
    acc[:] = bg
    x, y0, rowh = 10, 10, 0
    for n, ppu in items:
        a = _load_np(os.path.join(SPRITES, n))
        h, w = a.shape[:2]
        s = 100 / ppu * scale
        hh, ww = int(h * s), int(w * s)
        if x + ww > W:
            x = 10; y0 += rowh + 10; rowh = 0
        hh = min(hh, H - y0); ww = min(ww, W - x)
        ys = (np.arange(hh) / s).astype(int); xs = (np.arange(ww) / s).astype(int)
        a = a[::-1][ys][:, xs]; al = a[..., 3:4]
        acc[y0:y0 + hh, x:x + ww] = acc[y0:y0 + hh, x:x + ww] * (1 - al) + a[..., :3] * al
        x += ww + 10; rowh = max(rowh, hh)
    acc = acc[::-1]
    res = np.concatenate([acc, np.ones((H, W, 1), np.float32)], axis=2)
    img = bpy.data.images.new('cs', W, H, alpha=True)
    img.pixels.foreach_set(res.ravel())
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    return out
