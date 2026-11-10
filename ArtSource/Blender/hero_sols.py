# Hero restyle for the NII-punk look: same rig and poses as hero.py, new materials and colours.
# The runner is the warm spot on cold locations: rust-orange cloak, steel + brass bionic arm, yellow eye.
# Run: HERO_SET = None (all) or ['run_00', ...]; exec(open(r"...\hero_sols.py", encoding="utf-8").read())
import shutil
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\sols_kit.py", encoding="utf-8").read(), globals())
_kit_sol, _kit_ink, _kit_style, _kit_new_scene = sol, ink, style, new_scene
HERO_ONLY = []
exec(open(r"C:\Users\80709\Desktop\Biopulse\ArtSource\Blender\hero.py", encoding="utf-8").read(), globals())
sol, ink, style, new_scene = _kit_sol, _kit_ink, _kit_style, _kit_new_scene

CLOTH = (0.8, 0.38, 0.19)
CLOTH_D = (0.55, 0.22, 0.13)
SUIT = (0.17, 0.16, 0.19)
METAL = (0.66, 0.68, 0.66)
METAL_D = (0.38, 0.39, 0.4)
MASK = (0.07, 0.07, 0.08)
YELLOW = (1.0, 0.84, 0.2)
HERO_INK = (0.06, 0.025, 0.02)


def toon(name, color, steps=None, cuts=None):
    return sol(name, color, lights=False)


def hero_light():
    style(haze_max=0.0, dark_top=None, light_gain=0.0, shade_k=0.56, light_k=1.25, light_dir=(-0.35, -0.5, 0.8))
    ink(2.6, color=HERO_INK)
    bpy.context.scene.view_layers[0].freestyle_settings.crease_angle = math.radians(140)


_build = build
def build():
    new_scene()
    style(haze_max=0.0, dark_top=None, light_gain=0.0, shade_k=0.56, light_k=1.25, light_dir=(-0.35, -0.5, 0.8))
    return _build()


def run_hero(names=None):
    out = render_frames(names, sheet=False)
    dst = ROOT + r"\Assets\Biopulse\Art\Neon\Hero"
    for p in out:
        shutil.copyfile(p, os.path.join(dst, os.path.basename(p)))
    contact_sheet([(r'Hero\\' + os.path.basename(p), HERO_PPU) for p in out], SPRITES + r'\pv_hero.png', W=1800, H=760, scale=2.2,
                  bg=(0.09, 0.17, 0.17))
    return out


run_hero(globals().get('HERO_SET'))
