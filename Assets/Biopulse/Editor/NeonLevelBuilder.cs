using System.Collections.Generic;
using System.IO;
using Biopulse.Gameplay;
using Biopulse.Input;
using Biopulse.Neon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Biopulse.EditorTools
{
    // Generates the neon level scenes from compact layout scripts. Re-run after tuning a layout:
    // Biopulse → Neon → Build All Levels. Scenes are outputs; edit the layouts here, not the scenes.
    public static class NeonLevelBuilder
    {
        private const string Art = "Assets/Biopulse/Art/Neon";
        private const string Sprites = Art + "/Sprites";
        private const string Generated = Art + "/Generated";
        private const string Scenes = "Assets/Scenes";

        // ---------- layouts ----------
        // Tokens: run N | low | mid | high | laser | pit N | rail N | cp | term | exit
        public enum PitStyle { Goo, Electric, Void }

        // One painted backdrop strip (rendered by ArtSource/Blender). Pivot is bottom-centre at world Y = Bottom.
        public sealed class Layer
        {
            public string File; public float Ppu = 80, Factor = .5f, FactorY = .3f, Bottom; public int Order;
            public Color Tint = Color.white; public bool AnchorTop; public float TopInset;
            public Layer(string file, float ppu, float factor, float factorY, float bottom, int order)
            { File = file; Ppu = ppu; Factor = factor; FactorY = factorY; Bottom = bottom; Order = order; }
        }

        public sealed class Theme
        {
            public Layer[] Layers;   // when set, replaces the legacy Far/Mid pair
            public string Floor = "Floor_Lab.png", Wall;   // floor strip (4x2, top at y=0) and optional tiled wall below it
            public string Far, Mid;
            public float FarPpu = 100, MidPpu = 80, FarFactor = .6f, MidFactor = .3f, FarY = 3.6f, MidY = 3.4f;
            public Color FarTint, MidTint, Edge, Accent, Goo, Ambient, Sky, SkyGlow;
            public Color FloorTint = Color.white, CrateTint = Color.white, Underfloor = new Color(.035f, .04f, .06f);
            public float AmbientIntensity = .8f;
            public bool Ceiling = true;
            public PitStyle Pit = PitStyle.Goo;
        }

        public sealed class LevelDef
        {
            public int Number; public string Scene, Title, Subtitle, Next;
            public float RunSpeed = 5f;
            public Theme Theme; public string Layout;
        }

        public static readonly Theme Underground = new Theme
        {
            Layers = new[]
            {
                new Layer("Lab_Back.png", 64, .78f, .5f, -1f, -100),
                new Layer("Lab_Mid.png", 80, .5f, .3f, -1f, -50),
                new Layer("Lab_Front.png", 80, -.35f, 1f, 0, 60) { AnchorTop = true, TopInset = 4.4f },
            },
            Far = "Lab_Far.png", Mid = "Lab_Mid.png",
            FarTint = new Color(.36f, .42f, .5f), MidTint = new Color(.75f, .8f, .9f),
            Edge = new Color(.2f, .95f, 1f), Accent = new Color(.3f, 1f, .5f), Goo = new Color(.35f, 1f, .4f),
            Ambient = new Color(.6f, .72f, .95f), Sky = new Color(.02f, .035f, .05f), SkyGlow = new Color(.05f, .3f, .2f, .5f),
            AmbientIntensity = .85f, Floor = "Floor_Lab.png"
        };

        public static readonly Theme SurfaceLab = new Theme
        {
            Layers = new[]
            {
                new Layer("Lab2_Back.png", 64, .78f, .5f, -1f, -100),
                new Layer("Lab2_Mid.png", 80, .5f, .3f, -1f, -50),
                new Layer("Lab2_Front.png", 80, -.35f, 1f, 0, 60) { AnchorTop = true, TopInset = 4.4f },
            },
            Far = "Lab2_Far.png", Mid = "Lab2_Mid.png",
            FarTint = new Color(.4f, .41f, .5f), MidTint = new Color(.85f, .85f, .95f),
            Edge = new Color(1f, .32f, .42f), Accent = new Color(1f, .25f, .2f), Goo = new Color(.35f, .7f, 1f),
            Ambient = new Color(.85f, .82f, .95f), Sky = new Color(.035f, .03f, .05f), SkyGlow = new Color(.4f, .05f, .08f, .45f),
            Underfloor = new Color(.05f, .045f, .06f),
            AmbientIntensity = .95f, Pit = PitStyle.Electric, Floor = "Floor_Office.png"
        };

        public static readonly Theme Rooftops = new Theme
        {
            Layers = new[]
            {
                new Layer("Roof_Sky.png", 48, .97f, 1f, -4f, -130),
                new Layer("Roof_Far.png", 80, .82f, .55f, -2f, -100),
                new Layer("Roof_Mid.png", 80, .55f, .3f, -2.5f, -50),
                new Layer("Roof_Front.png", 80, -.35f, 1f, 0, 60) { AnchorTop = true, TopInset = 4.2f },
            },
            Far = "Roof_Far.png", Mid = "Roof_Mid.png", FarPpu = 80, FarFactor = .85f, MidFactor = .5f, FarY = 3.2f, MidY = 3.0f,
            FarTint = new Color(.8f, .72f, .95f), MidTint = new Color(.9f, .82f, 1f),
            Edge = new Color(1f, .35f, .8f), Accent = new Color(1f, .6f, .2f), Goo = new Color(1f, .35f, .8f),
            Ambient = new Color(.8f, .65f, 1f), Sky = new Color(.03f, .015f, .08f), SkyGlow = new Color(.75f, .18f, .4f, .75f),
            CrateTint = new Color(.92f, .86f, 1f), Underfloor = new Color(.07f, .045f, .11f),
            AmbientIntensity = .9f, Ceiling = false, Pit = PitStyle.Void, Floor = "Floor_Roof.png", Wall = "Wall_Roof.png"
        };

        public static readonly LevelDef[] Levels =
        {
            new LevelDef
            {
                Number = 1, Scene = "Neon_L1_Underground", Next = "Neon_L2_SurfaceLab", RunSpeed = 6f,
                Title = "ПОДЗЕМНАЯ ЛАБОРАТОРИЯ", Subtitle = "Побег из испытательного блока", Theme = Underground,
                Layout = "run 9 low low run 2 mid low cp pit 2.6 low mid run 2 laser low laser cp " +
                         "rail 6 run 3 low mid rail 10 run 2 cp term run 2 high mid pit 3.6 low high laser cp " +
                         "low mid pit 3 rail 14 low high run 2 mid laser cp rail 10 low mid run 2 high pit 3 laser run 3 exit"
            },
            new LevelDef
            {
                Number = 2, Scene = "Neon_L2_SurfaceLab", Next = "Neon_L3_Rooftops", RunSpeed = 6.4f,
                Title = "НАЗЕМНАЯ ЛАБОРАТОРИЯ", Subtitle = "Тревога! Коридоры и серверные", Theme = SurfaceLab,
                Layout = "run 8 low mid low high cp pit 3 mid laser rail 6 low mid cp term run 2 high low mid " +
                         "rail 11 laser pit 3.6 cp low high mid low laser rail 7 high mid pit 3 cp " +
                         "term run 2 mid high low laser high cp rail 14 mid high laser pit 3.6 low run 3 exit"
            },
            new LevelDef
            {
                Number = 3, Scene = "Neon_L3_Rooftops", Next = "Neon_Menu", RunSpeed = 6.8f,
                Title = "КРЫШИ", Subtitle = "Последний рывок к свободе", Theme = Rooftops,
                Layout = "run 8 pit 2.6 low pit 3 mid pit 3.6 cp high pit 2.6 laser pit 3.6 low mid cp " +
                         "pit 3 rail 9 high pit 3.6 mid low cp pit 2.6 high pit 3 laser mid pit 3.6 cp " +
                         "low pit 3 high pit 2.6 mid pit 3.6 laser cp run 4 term run 2 pit 3 high pit 3.6 cp run 4 rail 10 high pit 3 mid pit 3.6 run 3 exit"
            },
        };

        [MenuItem("Biopulse/Neon/Build All Levels")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PrepareAssets();
            foreach (var level in Levels) Build(level);
            BuildMenu();
            RegisterScenes();
            EditorSceneManager.OpenScene($"{Scenes}/{MenuScene}.unity");
        }

        public const string MenuScene = "Neon_Menu";

        private static void BuildMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var c = new Ctx { Def = Levels[0], T = Underground };
            c.World = new GameObject("World").transform;
            c.Deco = Child("Decor", c.World); c.Lights = Child("Lights", c.World);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = c.T.Sky;
            camGo.transform.position = new Vector3(0, 2.6f, -10);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var vol = new GameObject("Post").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = post;
            var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global; global.intensity = c.T.AmbientIntensity; global.color = c.T.Ambient;
            Ground(c, -20, 20, true);
            Ceiling(c, -30, 30);
            Backdrop(c, -30, 30);
            AmbientLights(c, -16, 16);
            var hero = Sprite(null, "Hero", heroIdle[0], new Vector2(5.2f, 0), Vector2.one * 1.5f, Color.white, 10, unlit);
            var idle = hero.gameObject.AddComponent<NeonFlipbook>(); idle.Frames = heroIdle; idle.Fps = 6;
            var heroLight = new GameObject("Hero light").AddComponent<Light2D>(); heroLight.transform.position = new Vector3(5.2f, 1.6f);
            heroLight.lightType = Light2D.LightType.Point; heroLight.pointLightOuterRadius = 4; heroLight.intensity = 1; heroLight.color = NeonPalette.Flex;
            var bot = new GameObject("MOP-3000").transform; bot.position = new Vector3(1.6f, 2.9f);
            Sprite(bot, "Body", botBody, Vector2.zero, Vector2.one * 1.1f, Color.white, 9, unlit);
            Sprite(bot, "Mop", botMop, new Vector2(.66f, -.26f) * 1.1f, Vector2.one * 1.1f, Color.white, 8, unlit);
            Sprite(bot, "Eye", botEye, new Vector2(.3f, .12f) * 1.1f, Vector2.one * 1.1f, NeonPalette.Gold, 10, unlit);
            var botLight = new GameObject("Bot light").AddComponent<Light2D>(); botLight.transform.SetParent(bot, false);
            botLight.lightType = Light2D.LightType.Point; botLight.pointLightOuterRadius = 3.5f; botLight.intensity = 1; botLight.color = NeonPalette.Gold;
            Sprite(null, "Crates", crate, new Vector2(8.2f, 0), Vector2.one, c.T.CrateTint, 3, lit);
            Sprite(null, "Crates", crate, new Vector2(8.2f, .8f), Vector2.one, c.T.CrateTint, 3, lit);
            Sprite(null, "Crates", crate, new Vector2(9.0f, 0), Vector2.one, c.T.CrateTint, 3, lit);

            var input = new GameObject("EMG Input (emulator)").AddComponent<EmgInputDriver>();
            var so = new SerializedObject(input); so.FindProperty("showDiagnostics").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
            input.LoadSavedCalibrationOnStart = true;
            var menu = new GameObject("Menu").AddComponent<NeonMenu>();
            var menuAudio = menu.gameObject.AddComponent<NeonAudio>(); menuAudio.MusicBpm = 88; menuAudio.MusicKey = -2;
            menu.Input = input; menu.Font = font; menu.Bobbing = new[] { bot };
            menu.Items = new[]
            {
                new NeonMenu.Item { Title = "ИГРАТЬ", Subtitle = "Уровень 1 · Подземная лаборатория", Scene = Levels[0].Scene, Level = 1 },
                new NeonMenu.Item { Title = "НАСТРОЙКА И ОБУЧЕНИЕ", Subtitle = "Калибровка каналов и шаги управления", Scene = "CalibrationTutorial", Level = 0 },
                new NeonMenu.Item { Title = "УРОВЕНЬ 2", Subtitle = "Наземная лаборатория", Scene = Levels[1].Scene, Level = 2 },
                new NeonMenu.Item { Title = "УРОВЕНЬ 3", Subtitle = "Крыши", Scene = Levels[2].Scene, Level = 3 },
            };
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{MenuScene}.unity");
        }

        // ---------- assets ----------
        private static Material unlit, lit;
        private static PhysicsMaterial2D slick;
        private static Sprite white, dot, softDot, ring, gradient, crate, crate2, terminal, gate, botBody, botMop, character,
            anchor, emitter, drone, beacon, exitDoor, botEye;
        private static Font font;
        private static Sprite[] heroRun, heroIdle;
        private static Sprite heroRise, heroApex, heroFall, heroLand, heroHang, heroSwingF, heroSwingB, heroReach, heroDeath;
        private const string HeroArt = Art + "/Hero";

        private static Sprite Hero(string name) => Import($"{HeroArt}/{name}.png", 160, new Vector2(.5f, .3f / 2.4f));
        private static Sprite[] HeroSeq(string prefix, int count)
        {
            var list = new Sprite[count];
            for (int i = 0; i < count; i++) list[i] = Hero($"{prefix}_{i:00}");
            return list;
        }
        private static VolumeProfile post;

        private static void PrepareAssets()
        {
            Directory.CreateDirectory(Generated);
            WritePng("White.png", 8, (x, y, s) => 1);
            WritePng("Dot.png", 64, (x, y, s) => { float r = R(x, y, s); return Mathf.Clamp01((1 - r) * s * .25f); });
            WritePng("SoftDot.png", 128, (x, y, s) => { float r = Mathf.Clamp01(1 - R(x, y, s)); return r * r; });
            WritePng("Gradient.png", 64, (x, y, s) => Mathf.Pow(1 - y / (float)s, 1.6f));
            WritePng("Ring.png", 128, (x, y, s) => { float r = R(x, y, s); return Mathf.Clamp01((1 - r) * s * .5f) * Mathf.Clamp01((r - .8f) * s * .5f); });
            AssetDatabase.Refresh();

            white = Import($"{Generated}/White.png", 8, new Vector2(.5f, .5f), true);
            dot = Import($"{Generated}/Dot.png", 64, new Vector2(.5f, .5f));
            softDot = Import($"{Generated}/SoftDot.png", 128, new Vector2(.5f, .5f));
            ring = Import($"{Generated}/Ring.png", 128, new Vector2(.5f, .5f));
            gradient = Import($"{Generated}/Gradient.png", 64, new Vector2(.5f, .5f));
            crate = Import($"{Sprites}/Crate.png", 200, new Vector2(.5f, .05f / .9f));
            crate2 = Import($"{Sprites}/Crate2.png", 200, new Vector2(.5f, .05f / .9f));
            anchor = Import($"{Sprites}/Anchor.png", 200, new Vector2(.5f, .5f));
            emitter = Import($"{Sprites}/Emitter.png", 200, new Vector2(.5f, .5f));
            drone = Import($"{Sprites}/Drone.png", 200, new Vector2(.5f, .5f));
            beacon = Import($"{Sprites}/Beacon.png", 200, new Vector2(.5f, .05f / 2.6f));
            exitDoor = Import($"{Sprites}/ExitDoor.png", 150, new Vector2(.5f, .1f / 4.6f));
            floors.Clear();
            terminal = Import($"{Sprites}/Terminal.png", 200, new Vector2(.5f, .1f / 1.9f));
            gate = Import($"{Sprites}/Gate.png", 200, new Vector2(.5f, .1f / 4f));
            botBody = Import($"{Sprites}/Mop3000_Body.png", 220, new Vector2(.5f, .5f));
            botMop = Import($"{Sprites}/Mop3000_Mop.png", 220, new Vector2(.5f, .5f));
            botEye = Import($"{Sprites}/Mop3000_Eye.png", 220, new Vector2(.5f, .5f));
            character = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Biopulse/Art/Laboratory/MainCharacter.png");
            heroRun = HeroSeq("run", 10); heroIdle = HeroSeq("idle", 6);
            heroRise = Hero("rise_00"); heroApex = Hero("apex_00"); heroFall = Hero("fall_00"); heroLand = Hero("land_00");
            heroHang = Hero("hang_00"); heroSwingF = Hero("swingf_00"); heroSwingB = Hero("swingb_00");
            heroReach = Hero("reach_00"); heroDeath = Hero("death_00");
            font = AssetDatabase.LoadAssetAtPath<Font>($"{Art}/Fonts/RussoOne-Regular.ttf");

            const string urp = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/";
            unlit = AssetDatabase.LoadAssetAtPath<Material>(urp + "Sprite-Unlit-Default.mat");
            lit = AssetDatabase.LoadAssetAtPath<Material>(urp + "Sprite-Lit-Default.mat");

            string slickPath = $"{Generated}/Slick.physicsMaterial2D";
            slick = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(slickPath);
            if (slick == null) { slick = new PhysicsMaterial2D("Slick") { friction = 0, bounciness = 0 }; AssetDatabase.CreateAsset(slick, slickPath); }

            string postPath = $"{Generated}/NeonPost.asset";
            AssetDatabase.DeleteAsset(postPath);
            post = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(post, postPath);
            var bloom = post.Add<Bloom>(true);
            bloom.threshold.Override(.78f); bloom.intensity.Override(1.35f); bloom.scatter.Override(.72f);
            var vignette = post.Add<Vignette>(true);
            vignette.intensity.Override(.34f); vignette.smoothness.Override(.45f); vignette.color.Override(new Color(.02f, 0, .05f));
            var ca = post.Add<ChromaticAberration>(true); ca.intensity.Override(.12f);
            var color = post.Add<ColorAdjustments>(true); color.contrast.Override(14); color.saturation.Override(12);
            var grain = post.Add<FilmGrain>(true); grain.intensity.Override(.12f);
            foreach (var c in post.components) AssetDatabase.AddObjectToAsset(c, post);
            EditorUtility.SetDirty(post);
            AssetDatabase.SaveAssets();
        }

        private static float R(int x, int y, int s) => Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(s / 2f, s / 2f)) / (s / 2f);

        private static void WritePng(string name, int size, System.Func<int, int, int, float> alpha)
        {
            string path = $"{Generated}/{name}";
            if (File.Exists(path)) return;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) tex.SetPixel(x, y, new Color(1, 1, 1, alpha(x, y, size)));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        private static Sprite Import(string path, float ppu, Vector2 pivot, bool tiled = false)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = ppu;
            ti.mipmapEnabled = false; ti.alphaIsTransparency = true;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            var s = new TextureImporterSettings(); ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Custom; s.spritePivot = pivot;
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            s.spriteExtrude = 0;
            ti.SetTextureSettings(s);
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------- scene ----------
        private sealed class Ctx
        {
            public LevelDef Def; public Theme T;
            public Transform World, Deco, Hazards, Zones, Lights, Pickups;
            public float X; public int ZoneId, Lights2D;
            public readonly List<float> Checkpoints = new List<float>();
            public float FinishX;
        }

        public static void Build(LevelDef def)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var c = new Ctx { Def = def, T = def.Theme };
            c.World = new GameObject("World").transform;
            c.Deco = Child("Decor", c.World); c.Hazards = Child("Hazards", c.World);
            c.Zones = Child("Timing zones", c.World); c.Lights = Child("Lights", c.World); c.Pickups = Child("Grab points", c.World);

            // Camera + post.
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.orthographic = true; cam.orthographicSize = 6.2f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = c.T.Sky;
            camGo.transform.position = new Vector3(6, 2.6f, -10);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var neonCam = camGo.AddComponent<NeonCamera>();
            var vol = new GameObject("Post").AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = post;
            var global = new GameObject("Global Light 2D").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global; global.intensity = c.T.AmbientIntensity; global.color = c.T.Ambient;
            global.transform.SetParent(c.Lights);

            // Layout pass.
            c.X = -6;
            Ground(c, -6, 0, true);
            var tokens = def.Layout.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i];
                float arg = i + 1 < tokens.Length && float.TryParse(tokens[i + 1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : float.NaN;
                bool usedArg = !float.IsNaN(arg);
                switch (t)
                {
                    case "run": Run(c, usedArg ? arg : 4); break;
                    case "low": Crates(c, 1, "ПРЫЖОК", NeonChallenge.LowJump); usedArg = false; break;
                    case "mid": Crates(c, 2, "ПРЫЖОК СИЛЬНЕЕ", NeonChallenge.MidJump); usedArg = false; break;
                    case "high": Crates(c, 3, "МАКСИМАЛЬНЫЙ ПРЫЖОК", NeonChallenge.HighJump); usedArg = false; break;
                    case "laser": Laser(c); usedArg = false; break;
                    case "pit": Pit(c, usedArg ? arg : 2.5f); break;
                    case "rail": Rail(c, usedArg ? arg : 4.5f); break;
                    case "cp": Checkpoint(c); usedArg = false; break;
                    case "term": Terminal(c); usedArg = false; break;
                    case "exit": Exit(c); usedArg = false; break;
                    default: Debug.LogWarning("Unknown layout token " + t); usedArg = false; break;
                }
                if (usedArg) i++;
            }
            Ground(c, c.X, c.X + 30, false);
            if (c.T.Ceiling) Ceiling(c, -20, c.X + 40);
            Backdrop(c, -20, c.X + 40);
            AmbientLights(c, -6, c.X);

            // Runner.
            var runner = Runner(c, new Vector2(0, .82f), def.RunSpeed, out var grab, out var locks, out var visuals);
            // Input + routing.
            var input = new GameObject("EMG Input (emulator)").AddComponent<EmgInputDriver>();
            var so = new SerializedObject(input); so.FindProperty("showDiagnostics").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
            input.LoadSavedCalibrationOnStart = true;
            var router = new GameObject("Command Router").AddComponent<RunnerCommandRouter>();
            router.Runner = runner; router.Grab = grab; router.Terminal = locks; router.Input = input;
            router.enabled = false;

            var chaser = Chaser(runner);
            var fx = new GameObject("FX").AddComponent<NeonFx>(); fx.UnlitMaterial = unlit;
            var hud = new GameObject("HUD").AddComponent<NeonHud>(); hud.Font = font;
            var session = new GameObject("Level Session").AddComponent<NeonLevelSession>();
            session.Runner = runner; session.Grab = grab; session.Locks = locks; session.Router = router; session.Input = input;
            session.Cam = neonCam; session.Hud = hud; session.Fx = fx; session.Chaser = chaser; session.Visuals = visuals;
            session.LevelNumber = def.Number; session.Title = def.Title; session.Subtitle = def.Subtitle; session.NextScene = def.Next;
            session.FinishX = c.FinishX; session.CheckpointXs = c.Checkpoints;
            hud.Session = session;
            session.gameObject.AddComponent<NeonAutopilot>().Session = session;
            var audio = session.gameObject.AddComponent<NeonAudio>();
            audio.MusicKey = def.Number == 1 ? 0 : def.Number == 2 ? 3 : 7;
            audio.MusicBpm = def.Number == 1 ? 96 : def.Number == 2 ? 104 : 112;
            neonCam.Target = runner.transform;

            EditorSceneManager.SaveScene(scene, $"{Scenes}/{def.Scene}.unity");
            Debug.Log($"Built {def.Scene}: length {c.X:F0} units, {c.ZoneId} timing zones.");
        }

        // ---------- pieces ----------
        private static void Run(Ctx c, float length) { Ground(c, c.X, c.X + length, false); c.X += length; }

        private static void Crates(Ctx c, int stack, string hint, NeonChallenge kind)
        {
            float start = c.X; Ground(c, start, start + 7, false);
            float x = start + 4.2f;
            var root = new GameObject($"Crates x{stack}").transform; root.SetParent(c.World); root.position = new Vector3(x, 0);
            for (int i = 0; i < stack; i++)
                Sprite(root, "Crate", ((int)x + i) % 2 == 0 ? crate : crate2, new Vector2(0, i * .8f), Vector2.one, c.T.CrateTint, 3, lit);
            var col = root.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(.8f, .8f * stack); col.offset = new Vector2(0, .4f * stack); col.sharedMaterial = slick;
            Zone(c, start + .4f, x + 1.4f, kind, hint, false, x);
            c.X = start + 7;
        }

        private static void Laser(Ctx c)
        {
            float start = c.X; Ground(c, start, start + 8, false);
            float x = start + 4.6f;
            var root = new GameObject("Laser gate").transform; root.SetParent(c.Hazards); root.position = new Vector3(x, 0);
            Sprite(root, "Crate", crate, Vector2.zero, Vector2.one, Color.white, 3, lit);
            var col = root.gameObject.AddComponent<BoxCollider2D>(); col.size = new Vector2(.8f, .8f); col.offset = new Vector2(0, .4f); col.sharedMaterial = slick;
            const float beamY = 3.45f; const float half = 2.3f;
            foreach (float side in new[] { -half, half })
            {
                if (c.T.Ceiling)
                {
                    Sprite(root, "Emitter pylon", white, new Vector2(side, beamY + 2.75f), new Vector2(.16f, 5f), new Color(.1f, .1f, .15f), 2, unlit);
                    Sprite(root, "Emitter", emitter, new Vector2(side, beamY), Vector2.one, Color.white, 4, unlit);
                }
                else
                {   // open sky: the emitters are little hovering drones
                    Sprite(root, "Drone", drone, new Vector2(side, beamY + .02f), Vector2.one, Color.white, 4, unlit)
                        .gameObject.AddComponent<NeonBob>();
                }
            }
            var beam = Sprite(root, "Beam", white, new Vector2(0, beamY), new Vector2(half * 2, .09f), new Color(1, .2f, .15f), 6, unlit);
            beam.gameObject.AddComponent<NeonPulse>().Speed = 22;
            Sprite(root, "Beam glow", softDot, new Vector2(0, beamY), new Vector2(half * 2.4f, .7f), new Color(1, .15f, .1f, .35f), 5, unlit);
            var hazard = new GameObject("Laser hazard").AddComponent<BoxCollider2D>();
            hazard.transform.SetParent(root, false); hazard.transform.localPosition = new Vector3(0, beamY + .05f);
            hazard.size = new Vector2(half * 2, .16f); hazard.isTrigger = true;
            hazard.gameObject.AddComponent<NeonHazard>().Kind = NeonDeath.Laser;
            PointLight(c, new Vector2(x, beamY), new Color(1, .2f, .15f), 4, 1.2f);
            Zone(c, start + .6f, x + 1.6f, NeonChallenge.LowJump, "СЛАБЫЙ ПРЫЖОК — ЛАЗЕР!", false, x);
            c.X = start + 8;
        }

        private static void Pit(Ctx c, float width)
        {
            float lead = 4;
            Ground(c, c.X, c.X + lead, false);
            float a = c.X + lead, b = a + width;
            Goo(c, a, b);
            Zone(c, a - 3.6f, b + .6f, NeonChallenge.Gap, width > 3.2f ? "ДЛИННЫЙ ПРЫЖОК" : "ПРЫЖОК", false, a);
            c.X = b;
        }

        private static void Rail(Ctx c, float width)
        {
            float lead = 4;
            Ground(c, c.X, c.X + lead, false);
            float a = c.X + lead, b = a + width;
            Goo(c, a, b);
            var root = new GameObject("Swing chasm").transform; root.SetParent(c.Pickups); root.position = new Vector3(a, 0);
            // Hooks hang high on cables over the chasm, ~5 units apart: grab → whip round → release → next hook.
            int count = Mathf.Max(1, Mathf.RoundToInt(width / 5f));
            float span = width / count;
            for (int k = 0; k < count; k++)
            {
                float gx = span * (k + .5f);
                const float hookY = 4.6f;
                var point = new GameObject($"Hook {k + 1}").transform; point.SetParent(root, false); point.localPosition = new Vector3(gx, hookY);
                Sprite(root, "Hook cable", white, new Vector2(gx, hookY + 3.4f), new Vector2(.05f, 6f), new Color(.05f, .05f, .08f), 4, unlit);
                var target = point.gameObject.AddComponent<GrabTarget>(); target.StableId = $"L{c.Def.Number}/hook@{a + gx:F1}";
                var gp = point.gameObject.AddComponent<NeonGrabPoint>(); gp.MaxReach = 6;
                gp.Halo = Sprite(point, "Halo", softDot, Vector2.zero, Vector2.one * 2f, NeonPalette.Alpha(NeonPalette.Flex, .3f), 6, unlit);
                Sprite(point, "Anchor", anchor, Vector2.zero, Vector2.one, Color.white, 6, unlit);
                gp.Core = Sprite(point, "Core", ring, Vector2.zero, Vector2.one * .46f, NeonPalette.Flex, 7, unlit);
                Sprite(point, "Handle", dot, Vector2.zero, Vector2.one * .26f, Color.white, 8, unlit);
                PointLight(c, new Vector2(a + gx, hookY), NeonPalette.Flex, 4f, 1f);
            }
            Zone(c, a - 4.5f, b + .8f, NeonChallenge.Grab, count > 1 ? "ЗАХВАТ · Р · ЗАХВАТ" : "ЗАХВАТ · Р — ЛЕТИ!", true, a + span * .5f);
            c.X = b;
        }

        private static void Checkpoint(Ctx c)
        {
            Ground(c, c.X, c.X + 5, false);
            float x = c.X + 2.5f;
            var root = new GameObject("Checkpoint").transform; root.SetParent(c.World); root.position = new Vector3(x, 0);
            Sprite(root, "Post", NeonLevelBuilder.beacon, Vector2.zero, Vector2.one, Color.white, 3, unlit);
            var beacon = Sprite(root, "Beacon", dot, new Vector2(0, 2.25f), Vector2.one * .3f, c.T.Edge, 6, unlit);
            Sprite(root, "Beacon halo", softDot, new Vector2(0, 2.25f), Vector2.one * 1.6f, NeonPalette.Alpha(c.T.Edge, .35f), 5, unlit);
            var light = PointLight(c, new Vector2(x, 2.2f), c.T.Edge, 4.5f, 1.1f);
            var col = root.gameObject.AddComponent<BoxCollider2D>(); col.isTrigger = true; col.size = new Vector2(1, 6); col.offset = new Vector2(0, 3);
            var cp = root.gameObject.AddComponent<NeonCheckpoint>(); cp.Beacon = beacon; cp.Glow = light;
            c.Checkpoints.Add(x);
            c.X += 5;
        }

        private static void Terminal(Ctx c)
        {
            float start = c.X; Ground(c, start, start + 9, false);
            float tx = start + 3.4f, gx = tx + 2.6f;
            var root = new GameObject("C+P Terminal").transform; root.SetParent(c.World); root.position = new Vector3(tx, 0);
            Sprite(root, "Console", terminal, Vector2.zero, Vector2.one, Color.white, 4, lit);
            var screen = Sprite(root, "Screen glow", white, new Vector2(.03f, 1.3f), new Vector2(.84f, .4f), NeonPalette.Combo, 5, unlit);
            screen.transform.localRotation = Quaternion.Euler(0, 0, -4.4f);
            var zone = new GameObject("Activation zone").AddComponent<BoxCollider2D>(); zone.transform.SetParent(root, false);
            zone.isTrigger = true; zone.offset = new Vector2(.7f, 1); zone.size = new Vector2(1.6f, 2);
            var gateGo = new GameObject("Security gate"); gateGo.transform.SetParent(root, false); gateGo.transform.localPosition = new Vector3(gx - tx, 0);
            var gateSprite = gateGo.AddComponent<SpriteRenderer>(); gateSprite.sprite = gate; gateSprite.sortingOrder = 4; gateSprite.sharedMaterial = lit;
            var gateCol = gateGo.AddComponent<BoxCollider2D>(); gateCol.size = new Vector2(.6f, 3.8f); gateCol.offset = new Vector2(0, 1.9f); gateCol.sharedMaterial = slick;
            Sprite(root, "Gate lintel", white, new Vector2(gx - tx, 4.0f), new Vector2(1.4f, .25f), new Color(.1f, .1f, .15f), 3, unlit);
            var terminal_ = root.gameObject.AddComponent<LockTerminal>(); terminal_.Configure(zone, gateCol, null);
            var vis = root.gameObject.AddComponent<NeonTerminalVisual>(); vis.Terminal = terminal_; vis.Screen = screen;
            vis.Glow = PointLight(c, new Vector2(tx, 1.6f), NeonPalette.Combo, 5f, 1.4f);
            Zone(c, tx - .6f, tx + 1.6f, NeonChallenge.Lock, "", false, tx);
            c.X = start + 9;
        }

        private static void Exit(Ctx c)
        {
            Ground(c, c.X, c.X + 8, false);
            float x = c.X + 5;
            var root = new GameObject("Exit").transform; root.SetParent(c.World); root.position = new Vector3(x, 0);
            Sprite(root, "Door light", white, new Vector2(0, 1.6f), new Vector2(1.8f, 3.2f), NeonPalette.Alpha(NeonPalette.Success, .85f), 2, unlit);
            Sprite(root, "Door haze", softDot, new Vector2(0, 1.6f), new Vector2(6, 6), NeonPalette.Alpha(NeonPalette.Success, .3f), 1, unlit);
            Sprite(root, "Door frame", exitDoor, Vector2.zero, Vector2.one, Color.white, 3, unlit);
            PointLight(c, new Vector2(x, 2), NeonPalette.Success, 7, 1.6f);
            var col = root.gameObject.AddComponent<BoxCollider2D>(); col.isTrigger = true; col.size = new Vector2(.6f, 6); col.offset = new Vector2(0, 3);
            root.gameObject.AddComponent<NeonFinish>();
            c.FinishX = x;
            c.X += 8;
        }

        private static void Ground(Ctx c, float a, float b, bool first)
        {
            if (b - a < .01f) return;
            var root = new GameObject($"Ground {a:F0}..{b:F0}").transform; root.SetParent(c.World);
            root.position = new Vector3((a + b) / 2, 0);
            var col = root.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(b - a, 10); col.offset = new Vector2(0, -5); col.sharedMaterial = slick;
            var top = Sprite(root, "Floor", FloorSprite(c.T.Floor), Vector2.zero, Vector2.one, c.T.FloorTint, 1, unlit);
            top.drawMode = SpriteDrawMode.Tiled; top.size = new Vector2(b - a, 2f);
            if (c.T.Wall != null)
            {
                var wall = Sprite(root, "Wall", FloorSprite(c.T.Wall, 80), new Vector2(0, -1.9f), Vector2.one, new Color(.5f, .45f, .55f), 0, unlit);
                wall.drawMode = SpriteDrawMode.Tiled; wall.size = new Vector2(b - a, 12);
            }
            else Sprite(root, "Underfloor", white, new Vector2(0, -6.9f), new Vector2(b - a, 10), c.T.Underfloor, 0, unlit);
            Sprite(root, "Edge glow", white, new Vector2(0, 0f), new Vector2(b - a, .04f), c.T.Edge * new Color(1, 1, 1, .7f), 2, unlit);
        }

        private static readonly Dictionary<string, Sprite> floors = new Dictionary<string, Sprite>();
        private static Sprite FloorSprite(string file, float ppu = 160)
        {
            if (floors.TryGetValue(file, out var s) && s != null) return s;
            return floors[file] = Import($"{Sprites}/{file}", ppu, new Vector2(.5f, 1f), true);
        }

        private static void Goo(Ctx c, float a, float b)
        {
            float w = b - a;
            if (c.T.Pit == PitStyle.Void)
            {
                // Gap between buildings: the city shows through; falling ends the attempt.
                var gap = new GameObject($"Roof gap {a:F0}").transform; gap.SetParent(c.Hazards); gap.position = new Vector3((a + b) / 2, 0);
                foreach (float s in new[] { -w / 2, w / 2 })
                {
                    Sprite(gap, "Edge post", white, new Vector2(s, .05f), new Vector2(.1f, .9f), c.T.Edge, 4, unlit);
                    Sprite(gap, "Edge post glow", softDot, new Vector2(s, .1f), new Vector2(.9f, 1.6f), NeonPalette.Alpha(c.T.Edge, .45f), 3, unlit);
                }
                // Chasm glow rising from the street far below, so the gap reads at a glance.
                var chasm = Sprite(gap, "Chasm glow", gradient, new Vector2(0, -2.2f), new Vector2(w, 4f), NeonPalette.Alpha(c.T.Accent, .55f), -30, unlit);
                chasm.flipY = false;
                var fall = new GameObject("Fall hazard").AddComponent<BoxCollider2D>(); fall.transform.SetParent(gap, false);
                fall.transform.localPosition = new Vector3(0, -3.6f); fall.size = new Vector2(w + .2f, 2); fall.isTrigger = true;
                fall.gameObject.AddComponent<NeonHazard>().Kind = NeonDeath.Fall;
                return;
            }
            bool electric = c.T.Pit == PitStyle.Electric;
            var root = new GameObject($"{(electric ? "Electric" : "Toxic")} pit {a:F0}").transform; root.SetParent(c.Hazards); root.position = new Vector3((a + b) / 2, 0);
            var vat = Sprite(root, "Vat", FloorSprite(electric ? "Pit_Electric.png" : "Pit_Toxic.png", 120), Vector2.zero, Vector2.one, Color.white, 0, unlit);
            vat.drawMode = SpriteDrawMode.Tiled; vat.size = new Vector2(w, 4.3f);
            Sprite(root, "Vat depth", white, new Vector2(0, -9.3f), new Vector2(w, 10), electric ? new Color(.02f, .05f, .16f) : new Color(.04f, .16f, .06f), 0, unlit);
            if (electric)
                for (int i = 0; i < 3; i++)
                {
                    var arc = Sprite(root, "Arc", white, new Vector2(Random.Range(-w * .4f, w * .4f), -1.1f), new Vector2(.05f, .7f), Color.white, 2, unlit);
                    arc.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-35f, 35f));
                    var ap = arc.gameObject.AddComponent<NeonPulse>(); ap.Speed = 25 + i * 7; ap.Depth = 1; ap.Flicker = true;
                }
            Sprite(root, "Pit haze", softDot, new Vector2(0, -1.2f), new Vector2(w * 1.3f, 2.2f), NeonPalette.Alpha(c.T.Goo, .4f), 1, unlit);
            foreach (float s in new[] { -w / 2, w / 2 })
                for (int i = 0; i < 3; i++)
                    Sprite(root, "Warning", white, new Vector2(s, -.25f - i * .32f), new Vector2(.14f, .2f), NeonPalette.Gold, 3, unlit);
            var hz = new GameObject("Pit hazard").AddComponent<BoxCollider2D>(); hz.transform.SetParent(root, false);
            hz.transform.localPosition = new Vector3(0, -2.4f); hz.size = new Vector2(w + .2f, 2); hz.isTrigger = true;
            hz.gameObject.AddComponent<NeonHazard>().Kind = electric ? NeonDeath.Shock : NeonDeath.Toxic;
            PointLight(c, new Vector2((a + b) / 2, -.8f), c.T.Goo, Mathf.Max(3, w), 1.3f);
        }

        private static void Ceiling(Ctx c, float a, float b)
        {
            var root = new GameObject("Ceiling").transform; root.SetParent(c.Deco); root.position = new Vector3((a + b) / 2, 0);
            Sprite(root, "Slab", white, new Vector2(0, 13.5f), new Vector2(b - a, 10), new Color(.03f, .035f, .05f), -5, unlit);
            Sprite(root, "Lip", white, new Vector2(0, 8.55f), new Vector2(b - a, .12f), new Color(.15f, .17f, .24f), -4, unlit);
            for (float x = a; x < b; x += 12)
            {
                var lamp = Sprite(root, "Lamp", white, new Vector2(x - (a + b) / 2, 8.4f), new Vector2(2.2f, .1f), new Color(.85f, .95f, 1f), -3, unlit);
                var p = lamp.gameObject.AddComponent<NeonPulse>(); p.Depth = .05f; p.Flicker = Random.value < .3f;
            }
        }

        private static void Backdrop(Ctx c, float a, float b)
        {
            if (c.T.Layers != null) { PaintedBackdrop(c, a, b); return; }
            var bg = Child("Backdrop", c.Deco);
            var far = Import($"{Sprites}/{c.T.Far}", c.T.FarPpu, new Vector2(.5f, .5f), true);
            var mid = Import($"{Sprites}/{c.T.Mid}", c.T.MidPpu, new Vector2(.5f, .5f), true);
            // Horizon glow glued to the camera (sunset on the roofs, alarm wash in the lab).
            var glow = Sprite(bg, "Sky glow", gradient, new Vector2(6, 1.2f), new Vector2(60, 9), c.T.SkyGlow, c.T.Ceiling ? -95 : -120, unlit);
            var gp = glow.gameObject.AddComponent<NeonParallax>(); gp.Factor = 1; gp.FactorY = 1;
            float span = (b - a) * Mathf.Max(1, 1 - c.T.FarFactor) + 60;
            var farR = Sprite(bg, "Far layer", far, new Vector2((a + b) / 2, c.T.FarY), Vector2.one, c.T.FarTint, -100, unlit);
            farR.drawMode = SpriteDrawMode.Tiled; farR.size = new Vector2(span, 12);
            var fp = farR.gameObject.AddComponent<NeonParallax>(); fp.Factor = c.T.FarFactor;
            var haze = Sprite(bg, "Depth haze", white, new Vector2((a + b) / 2, 1), new Vector2(span, 14), NeonPalette.Alpha(c.T.Sky, .4f), -90, unlit);
            haze.gameObject.AddComponent<NeonParallax>().Factor = c.T.FarFactor;
            var midR = Sprite(bg, "Mid layer", mid, new Vector2((a + b) / 2, c.T.MidY), Vector2.one, c.T.MidTint, -50, unlit);
            midR.drawMode = SpriteDrawMode.Tiled; midR.size = new Vector2(b - a + 40, 12);
            midR.gameObject.AddComponent<NeonParallax>().Factor = c.T.MidFactor;
            Sprite(bg, "Floor fog", softDot, new Vector2((a + b) / 2, .2f), new Vector2(b - a + 40, 2.2f), NeonPalette.Alpha(c.T.Accent, .07f), -40, unlit);
        }

        private static void PaintedBackdrop(Ctx c, float a, float b)
        {
            var bg = Child("Backdrop", c.Deco);
            foreach (var l in c.T.Layers)
            {
                var sprite = Import($"{Sprites}/{l.File}", l.Ppu, new Vector2(.5f, 0), true);
                if (sprite == null) { Debug.LogWarning("Missing backdrop " + l.File); continue; }
                float h = sprite.rect.height / l.Ppu;
                // Enough strip to cover the level as seen through this layer's parallax, plus margins.
                float span = (b - a) * Mathf.Max(.2f, Mathf.Abs(1 - l.Factor)) + 80;
                // Top-anchored bands start where the game camera will hold them (feet ~26% up a 6.4-size view).
                float bottom = l.AnchorTop ? 3.1f + 6.4f - l.TopInset : l.Bottom;
                var r = Sprite(bg, l.File.Replace(".png", ""), sprite, new Vector2((a + b) / 2 * (1 - l.Factor) + 6 * l.Factor, bottom),
                    Vector2.one, l.Tint, l.Order, unlit);
                r.drawMode = SpriteDrawMode.Tiled; r.tileMode = SpriteTileMode.Continuous; r.size = new Vector2(span, h);
                var p = r.gameObject.AddComponent<NeonParallax>(); p.Factor = l.Factor; p.FactorY = l.FactorY;
                p.AnchorTop = l.AnchorTop; p.TopInset = l.TopInset;
            }
            Sprite(bg, "Floor fog", softDot, new Vector2((a + b) / 2, .2f), new Vector2(b - a + 40, 2.2f), NeonPalette.Alpha(c.T.Accent, .07f), -40, unlit);
        }

        private static void AmbientLights(Ctx c, float a, float b)
        {
            int i = 0;
            for (float x = a + 4; x < b; x += 9, i++)
                PointLight(c, new Vector2(x, 5.5f), i % 2 == 0 ? c.T.Edge : c.T.Accent, 8, .55f);
        }

        private static RunnerMotor2D Runner(Ctx c, Vector2 at, float speed, out RunnerGrabController grab, out RunnerLockController locks, out NeonRunnerVisuals visuals)
        {
            var go = new GameObject("Runner");
            go.transform.position = at;
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 3; body.mass = 1;
            var col = go.AddComponent<CapsuleCollider2D>(); col.size = new Vector2(.8f, 1.6f); col.sharedMaterial = slick;
            var motor = go.AddComponent<RunnerMotor2D>(); motor.RunSpeed = speed;
            // Grapple feel: the cable reels in fast (shorter rope = faster swing), release keeps the momentum
            // plus a constant, amplitude-independent fling.
            var mso = new SerializedObject(motor);
            mso.FindProperty("grabRetractSpeed").floatValue = 16;
            mso.FindProperty("grabLengthRatio").floatValue = .5f;
            mso.FindProperty("minGrabLength").floatValue = 1.8f;
            mso.FindProperty("releaseSpeedMultiplier").floatValue = 1.1f;
            mso.FindProperty("releaseMinForwardSpeed").floatValue = speed;
            mso.FindProperty("releaseUpBoost").floatValue = 3f;
            mso.ApplyModifiedPropertiesWithoutUndo();

            var pivot = new GameObject("Pivot").transform; pivot.SetParent(go.transform, false); pivot.localPosition = Vector3.zero;
            var feet = new GameObject("Feet").transform; feet.SetParent(go.transform, false); feet.localPosition = new Vector3(0, -.8f);
            pivot.SetParent(feet, false);
            var bodySprite = Sprite(pivot, "Body", heroIdle[0], Vector2.zero, Vector2.one, Color.white, 10, unlit);

            var glow = new GameObject("Rim light").AddComponent<Light2D>(); glow.transform.SetParent(go.transform, false);
            glow.lightType = Light2D.LightType.Point; glow.pointLightOuterRadius = 3; glow.pointLightInnerRadius = 0; glow.intensity = .7f; glow.color = NeonPalette.Flex;

            var trail = new GameObject("Trail").AddComponent<TrailRenderer>(); trail.transform.SetParent(go.transform, false);
            trail.time = .22f; trail.minVertexDistance = .05f; trail.sharedMaterial = unlit; trail.sortingOrder = 9;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, .55f), new Keyframe(1, 0));
            var tg = new Gradient(); tg.SetKeys(new[] { new GradientColorKey(NeonPalette.Flex, 0), new GradientColorKey(NeonPalette.Extend, 1) },
                new[] { new GradientAlphaKey(.35f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = tg;

            var cable = new GameObject("Bionic arm cable").AddComponent<LineRenderer>(); cable.transform.SetParent(go.transform, false);
            cable.useWorldSpace = true; cable.positionCount = 2; cable.widthMultiplier = .09f; cable.numCapVertices = 4;
            cable.sharedMaterial = unlit; cable.sortingOrder = 12; cable.enabled = false;
            var arc = new GameObject("Reach arc").AddComponent<LineRenderer>(); arc.transform.SetParent(go.transform, false);
            arc.useWorldSpace = true; arc.widthMultiplier = .04f; arc.sharedMaterial = unlit; arc.sortingOrder = 11; arc.enabled = false;

            grab = go.AddComponent<RunnerGrabController>(); grab.Runner = motor; grab.Arm = cable;
            var gso = new SerializedObject(grab);
            gso.FindProperty("minReach").floatValue = 1.5f;
            gso.FindProperty("maxReach").floatValue = 6f;
            gso.FindProperty("reachSeconds").floatValue = .2f;
            gso.ApplyModifiedPropertiesWithoutUndo();
            locks = go.AddComponent<RunnerLockController>(); locks.Runner = motor;

            var arm = go.AddComponent<NeonArm>(); arm.Grab = grab; arm.Cable = cable; arm.ReachArc = arc;
            arm.Claw = Sprite(null, "Claw", ring, Vector2.zero, Vector2.one * .45f, Color.white, 13, unlit);
            arm.AimRing = Sprite(null, "Aim ring", ring, Vector2.zero, Vector2.one, NeonPalette.Flex, 13, unlit);
            arm.Claw.transform.SetParent(go.transform.parent, true); arm.AimRing.transform.SetParent(go.transform.parent, true);

            visuals = go.AddComponent<NeonRunnerVisuals>();
            visuals.Motor = motor; visuals.Grab = grab; visuals.Body = bodySprite; visuals.Pivot = pivot; visuals.Trail = trail;
            var anim = go.AddComponent<NeonHeroAnimator>();
            anim.Motor = motor; anim.Grab = grab; anim.Visuals = visuals; anim.Body = bodySprite;
            anim.Run = heroRun; anim.Idle = heroIdle; anim.Rise = heroRise; anim.Apex = heroApex; anim.Fall = heroFall;
            anim.Land = heroLand; anim.Hang = heroHang; anim.SwingForward = heroSwingF; anim.SwingBack = heroSwingB;
            anim.Reach = heroReach; anim.Death = heroDeath;
            return motor;
        }

        private static NeonChaser Chaser(RunnerMotor2D runner)
        {
            var go = new GameObject("MOP-3000");
            var visual = new GameObject("Visual").transform; visual.SetParent(go.transform, false);
            Sprite(visual, "Body", botBody, Vector2.zero, Vector2.one * .85f, Color.white, 9, unlit);
            var mop = new GameObject("Mop pivot").transform; mop.SetParent(visual, false);
            mop.localPosition = new Vector3(.66f, -.26f, 0) * .85f;
            Sprite(mop, "Mop", botMop, Vector2.zero, Vector2.one * .85f, Color.white, 8, unlit);
            var eye = Sprite(visual, "Eye", botEye, new Vector2(.3f, .12f) * .85f, Vector2.one * .85f, NeonPalette.Gold, 10, unlit);
            var light = new GameObject("Glow").AddComponent<Light2D>(); light.transform.SetParent(visual, false);
            light.lightType = Light2D.LightType.Point; light.pointLightOuterRadius = 3.5f; light.intensity = .9f; light.color = NeonPalette.Gold;
            var ch = go.AddComponent<NeonChaser>(); ch.Runner = runner; ch.Visual = visual; ch.Mop = mop; ch.Eye = eye;
            return ch;
        }

        // ---------- helpers ----------
        private static Transform Child(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent); return t; }

        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector2 local, Vector2 scale, Color color, int order, Material mat)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = local; go.transform.localScale = new Vector3(scale.x, scale.y, 1);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
            if (mat != null) sr.sharedMaterial = mat;
            // White sprite is 1x1 unit at 8ppu → scale is world size.
            if (sprite == white) { sr.drawMode = SpriteDrawMode.Sliced; sr.size = scale; go.transform.localScale = Vector3.one; }
            return sr;
        }

        private static Light2D PointLight(Ctx c, Vector2 at, Color color, float radius, float intensity)
        {
            var l = new GameObject("Light").AddComponent<Light2D>(); l.transform.SetParent(c.Lights); l.transform.position = at;
            l.lightType = Light2D.LightType.Point; l.pointLightOuterRadius = radius; l.pointLightInnerRadius = radius * .1f;
            l.intensity = intensity; l.color = color; l.falloffIntensity = .6f;
            c.Lights2D++;
            return l;
        }

        private static void Zone(Ctx c, float a, float b, NeonChallenge kind, string hint, bool flex, float target)
        {
            var go = new GameObject($"Zone {++c.ZoneId:00} {kind}"); go.transform.SetParent(c.Zones);
            go.transform.position = new Vector3((a + b) / 2, 4);
            var box = go.AddComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(b - a, 16);
            var z = go.AddComponent<NeonTimingZone>();
            z.Id = $"L{c.Def.Number}-{c.ZoneId:00}"; z.Challenge = kind; z.TargetX = target; z.Hint = hint; z.HintIsFlex = flex;
            z.Points = kind == NeonChallenge.HighJump || kind == NeonChallenge.Grab ? 150 : 100;
        }

        private static void RegisterScenes()
        {
            var list = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene($"{Scenes}/{MenuScene}.unity", true) };
            foreach (var l in Levels) list.Add(new EditorBuildSettingsScene($"{Scenes}/{l.Scene}.unity", true));
            foreach (var s in EditorBuildSettings.scenes)
                if (!list.Exists(x => x.path == s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
