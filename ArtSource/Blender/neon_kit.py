# Biopulse "Neon Noir" art kit for Blender 5.x.
# Style: flat cel shading (2-3 tones) + dark Freestyle outline + emissive neon with a soft glow pass.
# Every location/prop script does:  exec(open(KIT).read(), globals())  and then builds + renders.
# Coordinates: X = screen right, Z = screen up, Y = depth (camera at -Y looking +Y). 1 Blender unit = 1 Unity unit.
import bpy, bmesh, math, random, os
import numpy as np
from mathutils import Vector, Matrix

ROOT = r"C:\Users\80709\Desktop\Biopulse"
SPRITES = ROOT + r"\ArtSource\Renders"  # staging; publish() copies finished art into Unity
UNITY_SPRITES = ROOT + r"\Assets\Biopulse\Art\Neon\Sprites"
FONT_PATH = ROOT + r"\Assets\Biopulse\Art\Neon\Fonts\RussoOne-Regular.ttf"
INK = (0.015, 0.012, 0.035)


# ---------------------------------------------------------------- scene
def reset():
    for coll in (bpy.data.objects, bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.lights,
                 bpy.data.cameras, bpy.data.images, bpy.data.node_groups):
        bpy.data.batch_remove([x for x in coll if x.users == 0 or coll is bpy.data.objects])
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)
    for coll in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.images):
        bpy.data.batch_remove([x for x in coll if x.users == 0])
    MAT.clear()


def coll(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    return c


def setup_render(width_units, height_units, ppu, x0=0.0, z0=0.0, line=1.4, samples=16):
    """Orthographic camera framing x0..x0+W, z0..z0+H. ppu = pixels per unit."""
    s = bpy.context.scene
    r = s.render
    try:
        r.engine = 'BLENDER_EEVEE'
    except TypeError as e:
        print('engine', e)
    r.resolution_x = int(round(width_units * ppu))
    r.resolution_y = int(round(height_units * ppu))
    r.resolution_percentage = 100
    r.film_transparent = True
    r.image_settings.file_format = 'PNG'
    r.image_settings.color_mode = 'RGBA'
    r.image_settings.color_depth = '8'
    s.view_settings.view_transform = 'Standard'
    s.view_settings.look = 'None'
    s.view_settings.exposure = 0
    s.view_settings.gamma = 1
    try:
        s.eevee.taa_render_samples = samples
    except Exception:
        pass
    r.use_freestyle = line > 0
    r.line_thickness_mode = 'ABSOLUTE'
    r.line_thickness = max(line, 0.1)
    fs = s.view_layers[0].freestyle_settings
    fs.crease_angle = math.radians(120)
    for ls in list(fs.linesets):
        fs.linesets.remove(ls)
    ls = fs.linesets.new('Ink')
    ls.select_by_visibility = True
    ls.select_by_edge_types = True
    ls.select_silhouette = True
    ls.select_border = True
    ls.select_crease = True
    ls.select_contour = True
    ls.select_external_contour = True
    nl = coll('NoLine')
    ls.select_by_collection = True
    ls.collection = nl
    ls.collection_negation = 'EXCLUSIVE'
    ls.linestyle.color = lin(INK)
    ls.linestyle.thickness = max(line, 0.1)
    ls.linestyle.alpha = 1.0

    cam_data = bpy.data.cameras.new('Cam')
    cam_data.type = 'ORTHO'
    aspect = width_units / height_units
    cam_data.ortho_scale = max(width_units, height_units)
    cam_data.sensor_fit = 'AUTO'
    cam_data.clip_start = 0.1
    cam_data.clip_end = 400
    cam = bpy.data.objects.new('Cam', cam_data)
    s.collection.objects.link(cam)
    cam.location = (x0 + width_units / 2, -150, z0 + height_units / 2)
    cam.rotation_euler = (math.pi / 2, 0, 0)
    s.camera = cam

    sun_data = bpy.data.lights.new('Key', 'SUN')
    sun_data.energy = 3.2
    sun_data.angle = 0.02
    sun = bpy.data.objects.new('Key', sun_data)
    s.collection.objects.link(sun)
    # from upper-left-front: front faces mid, tops bright, right sides dark
    sun.rotation_euler = (math.radians(52), math.radians(-28), math.radians(-18))
    w = s.world or bpy.data.worlds.new('World')
    s.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs[0].default_value = (0, 0, 0, 1)
    bg.inputs[1].default_value = 0
    return cam


# ---------------------------------------------------------------- materials
MAT = {}


def lin(c):
    """Palette values are authored as display (sRGB) colours; shaders want linear."""
    def f(x):
        return x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4
    return tuple(f(max(0.0, min(1.0, x))) for x in c[:3])


def _new_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        bpy.data.materials.remove(m)
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    return m, nt


def toon(name, color, steps=(0.5, 0.74, 1.0), cuts=(0.16, 0.42)):
    """Flat cel shading: 3 bands of the base colour driven by the key light."""
    key = ('toon', name)
    if key in MAT:
        return MAT[key]
    m, nt = _new_mat(name)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    dif = nt.nodes.new('ShaderNodeBsdfDiffuse')
    dif.inputs['Color'].default_value = (1, 1, 1, 1)
    s2r = nt.nodes.new('ShaderNodeShaderToRGB')
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    el = ramp.color_ramp.elements
    el[0].position = 0.0
    el[0].color = (lin((steps[0],) * 3)[0],) * 3 + (1,)
    el[1].position = cuts[0]
    el[1].color = (lin((steps[1],) * 3)[0],) * 3 + (1,)
    e3 = el.new(cuts[1])
    e3.color = (lin((steps[2],) * 3)[0],) * 3 + (1,)
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = lin(color) + (1,)
    nt.links.new(dif.outputs[0], s2r.inputs[0])
    nt.links.new(s2r.outputs['Color'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], em.inputs['Strength'])
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    MAT[key] = m
    return m


def emit(name, color, strength=1.0):
    key = ('emit', name)
    if key in MAT:
        return MAT[key]
    m, nt = _new_mat(name)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = lin(color) + (1,)
    em.inputs['Strength'].default_value = strength
    nt.links.new(em.outputs[0], out.inputs['Surface'])
    MAT[key] = m
    return m


def flat(name, color, alpha=1.0):
    """Unshaded colour, optional alpha (blended)."""
    key = ('flat', name)
    if key in MAT:
        return MAT[key]
    m, nt = _new_mat(name)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    em = nt.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = lin(color) + (1,)
    if alpha < 1:
        tr = nt.nodes.new('ShaderNodeBsdfTransparent')
        mix = nt.nodes.new('ShaderNodeMixShader')
        mix.inputs['Fac'].default_value = alpha
        nt.links.new(tr.outputs[0], mix.inputs[1])
        nt.links.new(em.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs['Surface'])
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            m.blend_method = 'BLEND'
    else:
        nt.links.new(em.outputs[0], out.inputs['Surface'])
    MAT[key] = m
    return m


def gradient_mat(name, bottom, top, z0, z1, alpha_bottom=1.0, alpha_top=1.0):
    """Vertical gradient in world Z (for skies, haze, liquid)."""
    if ('grad', name) in MAT:
        return MAT[('grad', name)]
    m, nt = _new_mat(name)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    geo = nt.nodes.new('ShaderNodeNewGeometry')
    sep = nt.nodes.new('ShaderNodeSeparateXYZ')
    mr = nt.nodes.new('ShaderNodeMapRange')
    mr.inputs['From Min'].default_value = z0
    mr.inputs['From Max'].default_value = z1
    ramp = nt.nodes.new('ShaderNodeValToRGB')
    el = ramp.color_ramp.elements
    el[0].color = lin(bottom) + (alpha_bottom,)
    el[1].color = lin(top) + (alpha_top,)
    em = nt.nodes.new('ShaderNodeEmission')
    nt.links.new(geo.outputs['Position'], sep.inputs[0])
    nt.links.new(sep.outputs['Z'], mr.inputs['Value'])
    nt.links.new(mr.outputs['Result'], ramp.inputs['Fac'])
    nt.links.new(ramp.outputs['Color'], em.inputs['Color'])
    if alpha_bottom < 1 or alpha_top < 1:
        tr = nt.nodes.new('ShaderNodeBsdfTransparent')
        mix = nt.nodes.new('ShaderNodeMixShader')
        nt.links.new(ramp.outputs['Alpha'], mix.inputs['Fac'])
        nt.links.new(tr.outputs[0], mix.inputs[1])
        nt.links.new(em.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs['Surface'])
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            m.blend_method = 'BLEND'
    else:
        nt.links.new(em.outputs[0], out.inputs['Surface'])
    MAT[('grad', name)] = m
    return m


def hsv(h, s, v):
    import colorsys
    return colorsys.hsv_to_rgb(h % 1.0, s, v)


# ---------------------------------------------------------------- batched geometry
class Batch:
    """Collects primitives into one bmesh per (collection, material) -> few objects, fast Freestyle."""

    def __init__(self):
        self.groups = {}

    def _bm(self, mat, c):
        key = (c, mat.name)
        if key not in self.groups:
            self.groups[key] = (bmesh.new(), mat)
        return self.groups[key][0]

    def box(self, mat, x0, x1, z0, z1, y0=0.0, y1=None, c='Main', bevel=0.0):
        """Axis-aligned box. y is depth (smaller = closer to camera)."""
        if y1 is None:
            y1 = y0 + 0.4
        if x1 < x0: x0, x1 = x1, x0
        if z1 < z0: z0, z1 = z1, z0
        bm = self._bm(mat, c)
        M = Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2)) @ Matrix.Diagonal(
            (max(x1 - x0, 1e-4), max(y1 - y0, 1e-4), max(z1 - z0, 1e-4), 1))
        res = bmesh.ops.create_cube(bm, size=1.0, matrix=M)
        if bevel > 0:
            verts = res['verts']
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=verts + edges, offset=bevel, segments=1, affect='EDGES', clamp_overlap=True)
        return res

    def quad(self, mat, x0, x1, z0, z1, y=0.0, c='Main'):
        bm = self._bm(mat, c)
        vs = [bm.verts.new(p) for p in ((x0, y, z0), (x1, y, z0), (x1, y, z1), (x0, y, z1))]
        bm.faces.new(vs)

    def poly(self, mat, pts, y=0.0, c='Main'):
        """Flat polygon facing camera; pts are (x, z)."""
        bm = self._bm(mat, c)
        vs = [bm.verts.new((p[0], y, p[1])) for p in pts]
        bm.faces.new(vs)

    def prism(self, mat, pts, y0, y1, c='Main'):
        """2D polygon (x, z) extruded along depth from y0 (front) to y1 (back)."""
        bm = self._bm(mat, c)
        front = [bm.verts.new((p[0], y0, p[1])) for p in pts]
        back = [bm.verts.new((p[0], y1, p[1])) for p in pts]
        n = len(pts)
        # winding so normals face outwards (front face towards -Y / camera)
        area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
        order = list(range(n)) if area > 0 else list(range(n))[::-1]
        bm.faces.new([front[i] for i in order])
        bm.faces.new([back[i] for i in order[::-1]])
        for k in range(n):
            i, j = order[k], order[(k + 1) % n]
            bm.faces.new([front[i], back[i], back[j], front[j]])

    def bar(self, mat, ax, az, bx, bz, thick, y0, y1, c='Main'):
        """Straight flat bar between two points (braces, struts, stripes)."""
        d = Vector((bx - ax, bz - az))
        if d.length < 1e-6:
            return
        nrm = Vector((-d.y, d.x)).normalized() * thick / 2
        self.prism(mat, [(ax + nrm.x, az + nrm.y), (bx + nrm.x, bz + nrm.y), (bx - nrm.x, bz - nrm.y), (ax - nrm.x, az - nrm.y)], y0, y1, c)

    def ngon(self, mat, cx, cz, r, n, y0, y1, rot=0.0, c='Main'):
        self.prism(mat, [(cx + r * math.cos(rot + i * 2 * math.pi / n), cz + r * math.sin(rot + i * 2 * math.pi / n)) for i in range(n)], y0, y1, c)

    def cyl(self, mat, cx, cz, r, length, axis='Z', y=0.0, seg=16, c='Main', r2=None):
        bm = self._bm(mat, c)
        rot = {'Z': Matrix.Identity(4), 'X': Matrix.Rotation(math.pi / 2, 4, 'Y'),
               'Y': Matrix.Rotation(math.pi / 2, 4, 'X')}[axis]
        M = Matrix.Translation((cx, y, cz)) @ rot
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r,
                              radius2=r if r2 is None else r2, depth=length, matrix=M)

    def sphere(self, mat, cx, cz, r, y=0.0, sx=1.0, sz=1.0, sy=1.0, sub=2, c='Main'):
        bm = self._bm(mat, c)
        M = Matrix.Translation((cx, y, cz)) @ Matrix.Diagonal((sx, sy, sz, 1))
        bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r, matrix=M)

    def disc(self, mat, cx, cz, r, y=0.0, seg=32, c='Main'):
        bm = self._bm(mat, c)
        vs = [bm.verts.new((cx + r * math.cos(a), y, cz + r * math.sin(a)))
              for a in [i * 2 * math.pi / seg for i in range(seg)]]
        bm.faces.new(vs)

    def tube(self, mat, pts, r, y=0.0, seg=6, c='Main'):
        """Polyline tube through (x, z) points (cables, pipes)."""
        for (ax, az), (bx, bz) in zip(pts[:-1], pts[1:]):
            d = Vector((bx - ax, 0, bz - az))
            L = d.length
            if L < 1e-5:
                continue
            ang = math.atan2(d.x, d.z)
            M = Matrix.Translation(((ax + bx) / 2, y, (az + bz) / 2)) @ Matrix.Rotation(ang, 4, 'Y')
            bm = self._bm(mat, c)
            bmesh.ops.create_cone(bm, cap_ends=False, segments=seg, radius1=r, radius2=r, depth=L + r * 0.6, matrix=M)

    def build(self, wrap_width=None):
        objs = []
        for (c, _), (bm, mat) in self.groups.items():
            me = bpy.data.meshes.new(f'{c}_{mat.name}')
            bm.to_mesh(me)
            bm.free()
            me.materials.append(mat)
            ob = bpy.data.objects.new(me.name, me)
            coll(c).objects.link(ob)
            objs.append(ob)
            if wrap_width:
                for off in (-wrap_width, wrap_width):
                    d = bpy.data.objects.new(me.name + ('_L' if off < 0 else '_R'), me)
                    d.location.x = off
                    coll(c).objects.link(d)
        self.groups = {}
        return objs


def catenary(x0, z0, x1, z1, sag, n=24):
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((x0 + (x1 - x0) * t, z0 + (z1 - z0) * t - sag * 4 * t * (1 - t)))
    return pts


# ---------------------------------------------------------------- text
_FONT = None


def font():
    global _FONT
    if _FONT is None or _FONT.name not in bpy.data.fonts:
        _FONT = bpy.data.fonts.load(FONT_PATH, check_existing=True)
    return _FONT


def text(body, mat, x, z, size, y=0.0, align='CENTER', c='Main', vertical=False, extrude=0.0, spacing=1.0):
    """Neon sign text as a mesh. vertical=True stacks letters top-to-bottom (x,z = top centre)."""
    objs = []
    chars = list(body) if vertical else [body]
    for i, ch in enumerate(chars):
        cu = bpy.data.curves.new('T', 'FONT')
        cu.body = ch
        cu.font = font()
        cu.size = size
        cu.align_x = align if not vertical else 'CENTER'
        cu.align_y = 'CENTER' if vertical else 'BOTTOM'
        cu.extrude = extrude
        cu.space_character = spacing
        ob = bpy.data.objects.new('Text', cu)
        bpy.context.scene.collection.objects.link(ob)
        ob.rotation_euler = (math.pi / 2, 0, 0)
        ob.location = (x, y, z - i * size * 1.05) if vertical else (x, y, z)
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        me.transform(ob.matrix_world)
        bpy.data.objects.remove(ob)
        bpy.data.curves.remove(cu)
        me.materials.clear()
        me.materials.append(mat)
        o2 = bpy.data.objects.new('Sign_' + body[:8], me)
        coll(c).objects.link(o2)
        objs.append(o2)
    return objs


def text_width(body, size):
    cu = bpy.data.curves.new('T', 'FONT')
    cu.body = body
    cu.font = font()
    cu.size = size
    ob = bpy.data.objects.new('Tw', cu)
    bpy.context.scene.collection.objects.link(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    xs = [v.co.x for v in me.vertices]
    w = (max(xs) - min(xs)) if xs else 0
    bpy.data.objects.remove(ob)
    bpy.data.curves.remove(cu)
    bpy.data.meshes.remove(me)
    return w


def wrap_objects(objs, width):
    """Linked copies at +-width so the strip tiles seamlessly."""
    for ob in objs:
        for off in (-width, width):
            d = ob.copy()
            d.location.x += off
            for cl in ob.users_collection:
                cl.objects.link(d)


# ---------------------------------------------------------------- render + glow
def _blur_axis(a, r, axis, wrap):
    if r < 1:
        return a
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r + 1, r)
    p = np.pad(a, pad, mode='wrap' if wrap else 'edge')
    cs = np.cumsum(p, axis=axis, dtype=np.float32)
    n = a.shape[axis]
    hi = np.take(cs, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
    lo = np.take(cs, np.arange(0, n), axis=axis)
    return (hi - lo) / (2 * r + 1)


def blur(a, r, wrap_x):
    for _ in range(3):
        a = _blur_axis(a, r, 1, wrap_x)
        a = _blur_axis(a, r, 0, False)
    return a


def glow(path, threshold=0.62, radii=(6, 22), strength=(0.55, 0.45), wrap_x=True, opaque=False):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    a = a.reshape(h, w, 4)
    rgb, al = a[..., :3], a[..., 3:4]
    pre = rgb * al
    lum = pre.max(axis=2, keepdims=True)
    mask = np.clip((lum - threshold) / max(1e-3, 1 - threshold), 0, 1)
    src = pre * mask
    # work at half resolution for speed
    small = src[::2, ::2]
    g = np.zeros_like(small)
    for r, k in zip(radii, strength):
        g += blur(small, max(1, r // 2), wrap_x) * k
    g = np.repeat(np.repeat(g, 2, axis=0), 2, axis=1)[:h, :w]
    C = pre + g
    ga = np.clip(g.max(axis=2, keepdims=True), 0, 1)
    A = np.ones_like(al) if opaque else np.clip(al + ga * (1 - al), 0, 1)
    out = np.where(A > 1e-4, C / np.maximum(A, 1e-4), 0)
    out = np.clip(out, 0, 1)
    res = np.concatenate([out, A], axis=2).astype(np.float32)
    img.pixels.foreach_set(res.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def render(name, glow_kw=None, folder=None):
    folder = folder or SPRITES
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name)
    s = bpy.context.scene
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    if glow_kw is not None:
        glow(path, **glow_kw)
    print('rendered', path, s.render.resolution_x, s.render.resolution_y)
    return path


# ---------------------------------------------------------------- preview composite
def _load_np(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


def preview(layers, out, x0=0.0, z0=-3.3, w=23.0, h=12.9, ppu=60, bg=(0, 0, 0)):
    """Alpha-composite world-space layers into one opaque image, as the game camera would see them.
    layers: list of (path, layer_ppu, layer_x0, layer_z0) in back-to-front order; layers tile in X."""
    W, H = int(w * ppu), int(h * ppu)
    acc = np.zeros((H, W, 3), np.float32)
    acc[:] = bg
    xs = x0 + (np.arange(W) + .5) / ppu
    zs = z0 + (np.arange(H) + .5) / ppu
    for path, lppu, lx0, lz0 in layers:
        a = _load_np(path)
        lh, lw = a.shape[:2]
        px = (np.floor((xs - lx0) * lppu).astype(int)) % lw
        pz = np.floor((zs - lz0) * lppu).astype(int)
        valid = (pz >= 0) & (pz < lh)
        pz = np.clip(pz, 0, lh - 1)
        s = a[pz][:, px]
        al = s[..., 3:4] * valid[:, None, None]
        acc = acc * (1 - al) + s[..., :3] * al
    res = np.concatenate([acc, np.ones((H, W, 1), np.float32)], axis=2)
    img = bpy.data.images.new('preview', W, H, alpha=True)
    img.pixels.foreach_set(res.ravel())
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)
    return out


def publish(names, sub=''):
    """Copy finished renders from the staging folder into the Unity project."""
    import shutil
    dst = os.path.join(UNITY_SPRITES, sub) if sub else UNITY_SPRITES
    os.makedirs(dst, exist_ok=True)
    for n in names:
        shutil.copyfile(os.path.join(SPRITES, n), os.path.join(dst, n))
    print('published', names)
