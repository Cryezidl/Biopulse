using System;
using System.IO;
using Biopulse.Gameplay;
using Biopulse.Input;
using Biopulse.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Biopulse.Editor
{
    public static class RunnerSandboxBuilder
    {
        public const string ScenePath = "Assets/Scenes/MovementTest.unity";
        private const string ArtPath = "Assets/Biopulse/Art";
        private const string PrefabPath = "Assets/Biopulse/Prefabs/Runner.prefab";
        public const string FinalHurdlePath = "Assets/Biopulse/Prefabs/FinalHurdle.prefab";

        [MenuItem("Biopulse/Scenes/Open Movement Test")]
        public static void OpenOrCreate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before opening the scene.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the current scene before opening Movement Test.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { EditorSceneManager.OpenScene(ScenePath); return; }

            EnsureFolder(ArtPath); EnsureFolder("Assets/Biopulse/Prefabs"); EnsureFolder("Assets/Scenes");
            var sprite = GetSquare();
            var material = GetMaterial();
            var physicsMaterial = GetPhysicsMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 4.8f;
            camera.backgroundColor = new Color(.025f, .035f, .065f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.transform.position = new Vector3(4, 2, -10);
            cameraObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = .4f;

            var environment = new GameObject("Environment");
            // Solid runway ends beyond the collision wall; useful for later jump/fall sections.
            var floor = Rectangle("Runway", new Vector2(7, -.5f), new Vector2(30, 1),
                new Color(.11f, .15f, .22f), sprite, material, environment.transform);
            floor.AddComponent<BoxCollider2D>().sharedMaterial = physicsMaterial;
            Rectangle("Runway edge", new Vector2(7, .025f), new Vector2(30, .05f),
                new Color(.1f, .85f, .9f), sprite, material, environment.transform);
            var wall = Rectangle("Collision wall", new Vector2(15, 1.5f), new Vector2(.8f, 3),
                new Color(.75f, .2f, .4f), sprite, material, environment.transform);
            wall.AddComponent<BoxCollider2D>().sharedMaterial = physicsMaterial;
            // All decoration has no collider and sits behind gameplay.
            var background = new GameObject("Background");
            for (int i = -3; i < 16; i++)
            {
                var pillar = Rectangle("Backdrop pillar " + i, new Vector2(i * 3, 2), new Vector2(.08f, 8),
                    new Color(.065f, .11f, .17f), sprite, material, background.transform);
                pillar.GetComponent<SpriteRenderer>().sortingOrder = -10;
            }
            for (int i = -1; i < 7; i++)
                Rectangle("Runway marker " + i, new Vector2(i * 3, -.25f), new Vector2(.3f, .08f),
                    new Color(.07f, .45f, .54f), sprite, material, environment.transform);

            GameObject runner;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                runner = new GameObject("Runner");
                var body = runner.AddComponent<Rigidbody2D>();
                body.gravityScale = 3; body.constraints = RigidbodyConstraints2D.FreezeRotation;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                var collider = runner.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(.8f, 1.6f); collider.sharedMaterial = physicsMaterial;
                runner.AddComponent<RunnerMotor2D>();
                Rectangle("Body placeholder", Vector2.zero, new Vector2(.8f, 1.6f),
                    new Color(.1f, .8f, .85f), sprite, material, runner.transform);
                Rectangle("Visor", new Vector2(.08f, .4f), new Vector2(.62f, .15f),
                    new Color(.015f, .055f, .1f), sprite, material, runner.transform).GetComponent<SpriteRenderer>().sortingOrder = 1;
                Rectangle("Bionic arm placeholder", new Vector2(.3f, -.05f), new Vector2(.2f, .65f),
                    new Color(1, .67f, .2f), sprite, material, runner.transform).GetComponent<SpriteRenderer>().sortingOrder = 1;
                PrefabUtility.SaveAsPrefabAssetAndConnect(runner, PrefabPath, InteractionMode.AutomatedAction);
            }
            else runner = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            runner.transform.position = new Vector3(0, 2.5f, 0);
            cameraObject.AddComponent<RunnerCamera>().Target = runner.transform;

            var services = new GameObject("Sandbox Services");
            var input = services.AddComponent<EmgInputDriver>(); input.ShowDiagnostics = false;
            var sandbox = services.AddComponent<RunnerSandbox>();
            sandbox.Runner = runner.GetComponent<RunnerMotor2D>(); sandbox.Input = input;
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = runner;
            AssetDatabase.SaveAssets();
            AddJumpTestSection();
        }

        [MenuItem("Biopulse/Scenes/Add Jump Test Section")]
        public static void AddJumpTestSection()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath)
                throw new InvalidOperationException("Open MovementTest in Edit Mode first.");
            var sandbox = UnityEngine.Object.FindAnyObjectByType<RunnerSandbox>();
            if (sandbox == null) throw new InvalidOperationException("MovementTest needs Sandbox Services.");
            var router = sandbox.GetComponent<RunnerCommandRouter>();
            if (router == null) router = sandbox.gameObject.AddComponent<RunnerCommandRouter>();
            router.Runner = sandbox.Runner; router.Input = sandbox.Input;
            if (GameObject.Find("Jump Test Section") == null)
            {
                var root = new GameObject("Jump Test Section");
                var sprite = GetSquare(); var material = GetMaterial(); var physicsMaterial = GetPhysicsMaterial();
                float[] heights = { .8f, 1.6f, 2.4f }; float[] positions = { 6, 12, 19 };
                for (int i = 0; i < heights.Length; i++)
                {
                    var hurdle = Rectangle("Hurdle " + (i + 1), new Vector2(positions[i], heights[i] / 2),
                        new Vector2(.6f, heights[i]), new Color(.95f, .55f - i * .13f, .2f + i * .1f), sprite, material, root.transform);
                    hurdle.AddComponent<BoxCollider2D>().sharedMaterial = physicsMaterial;
                }
                var floor = GameObject.Find("Environment/Runway");
                floor.transform.position = new Vector3(15, -.5f, 0); floor.transform.localScale = new Vector3(46, 1, 1);
                var edge = GameObject.Find("Environment/Runway edge");
                edge.transform.position = new Vector3(15, .025f, 0); edge.transform.localScale = new Vector3(46, .05f, 1);
                var wall = GameObject.Find("Environment/Collision wall");
                wall.transform.position = new Vector3(30, 1.3f, 0); wall.transform.localScale = new Vector3(.8f, 2.6f, 1);
            }
            // Keep the final obstacle as an asset so traversal tests use the actual geometry.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FinalHurdlePath) == null)
            {
                var wall = GameObject.Find("Environment/Collision wall");
                wall.transform.position = new Vector3(30, 1.3f, 0);
                wall.transform.localScale = new Vector3(.8f, 2.6f, 1);
                PrefabUtility.SaveAsPrefabAssetAndConnect(wall, FinalHurdlePath, InteractionMode.AutomatedAction);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AddGrabTestSection();
        }

        [MenuItem("Biopulse/Scenes/Add Grab Test Section")]
        public static void AddGrabTestSection()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open MovementTest in Edit Mode first.");
            var sandbox = UnityEngine.Object.FindAnyObjectByType<RunnerSandbox>();
            var grab = sandbox.Runner.GetComponent<RunnerGrabController>();
            if (grab == null) grab = sandbox.Runner.gameObject.AddComponent<RunnerGrabController>();
            grab.Runner = sandbox.Runner;
            if (grab.Arm == null)
            {
                var go = new GameObject("Reach Arm"); go.transform.SetParent(sandbox.Runner.transform, false);
                var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 2;
                line.startWidth = line.endWidth = .06f; line.sharedMaterial = GetMaterial();
                line.startColor = line.endColor = new Color(1, .7f, .2f); line.sortingOrder = 5;
                line.enabled = false; grab.Arm = line;
            }
            sandbox.GetComponent<RunnerCommandRouter>().Grab = grab;
            if (GameObject.Find("Grab Test Section") == null)
            {
                var root = new GameObject("Grab Test Section");
                Vector2[] positions = { new Vector2(4, 3.2f), new Vector2(10, 4.5f), new Vector2(25, 3.5f) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var target = Rectangle("Grab Target " + (i + 1), positions[i], new Vector2(.35f, .35f),
                        new Color(1, .8f, .15f), GetSquare(), GetMaterial(), root.transform);
                    target.AddComponent<CircleCollider2D>().isTrigger = true;
                    target.AddComponent<GrabTarget>().StableId = "sandbox-target-" + i;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AddLockTestSection();
        }

        [MenuItem("Biopulse/Scenes/Add Lock Test Section")]
        public static void AddLockTestSection()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != ScenePath) throw new InvalidOperationException("Open MovementTest in Edit Mode first.");
            var sandbox = UnityEngine.Object.FindAnyObjectByType<RunnerSandbox>();
            var controller = sandbox.Runner.GetComponent<RunnerLockController>();
            if (controller == null) controller = sandbox.Runner.gameObject.AddComponent<RunnerLockController>();
            controller.Runner = sandbox.Runner; sandbox.GetComponent<RunnerCommandRouter>().Terminal = controller;
            if (GameObject.Find("Lock Test Section") == null)
            {
                var root = new GameObject("Lock Test Section");
                var zone = new GameObject("Terminal activation zone"); zone.transform.SetParent(root.transform);
                zone.transform.position = new Vector2(2, 1.3f);
                var trigger = zone.AddComponent<BoxCollider2D>(); trigger.isTrigger = true; trigger.size = new Vector2(2, 2.6f);
                var badge = Rectangle("Blue terminal", new Vector2(2, 2.5f), new Vector2(.5f, .7f),
                    new Color(.2f, .6f, 1), GetSquare(), GetMaterial(), root.transform);
                var gate = Rectangle("Locked gate", new Vector2(3, 1.5f), new Vector2(.2f, 3),
                    new Color(.2f, .6f, 1), GetSquare(), GetMaterial(), root.transform);
                var collider = gate.AddComponent<BoxCollider2D>(); collider.sharedMaterial = GetPhysicsMaterial();
                root.AddComponent<LockTerminal>().Configure(trigger, collider, badge.GetComponent<SpriteRenderer>());
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static GameObject Rectangle(string name, Vector2 position, Vector2 size, Color color,
            Sprite sprite, Material material, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.color = color; renderer.sharedMaterial = material;
            return go;
        }
        private static Sprite GetSquare()
        {
            const string path = ArtPath + "/UnitSquare.png";
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color32[256];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Material GetMaterial()
        {
            const string path = ArtPath + "/SandboxUnlit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("URP 2D unlit sprite shader is unavailable.");
            material = new Material(shader); AssetDatabase.CreateAsset(material, path); return material;
        }
        private static PhysicsMaterial2D GetPhysicsMaterial()
        {
            const string path = ArtPath + "/RunnerFrictionless.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null) return material;
            material = new PhysicsMaterial2D("RunnerFrictionless") { friction = 0, bounciness = 0 };
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
    }
}
