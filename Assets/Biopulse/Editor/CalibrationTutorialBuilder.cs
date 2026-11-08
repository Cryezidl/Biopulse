using System;
using System.Collections.Generic;
using Biopulse.Calibration;
using Biopulse.Gameplay;
using Biopulse.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Biopulse.Editor
{
    public static class CalibrationTutorialBuilder
    {
        public const string ScenePath = "Assets/Scenes/CalibrationTutorial.unity";
        [MenuItem("Biopulse/Scenes/Open Calibration and Tutorial")]
        public static void OpenOrCreate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { ConfigureBuildScenes(); EditorSceneManager.OpenScene(ScenePath); return; }

            // Keep existing content; opt the mechanics scene into the explicitly saved profile.
            EditorSceneManager.OpenScene(RunnerSandboxBuilder.ScenePath);
            UnityEngine.Object.FindAnyObjectByType<EmgInputDriver>().LoadSavedCalibrationOnStart = true;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Biopulse/Art/UnitSquare.png");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Biopulse/Art/SandboxUnlit.mat");
            var friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Biopulse/Art/RunnerFrictionless.physicsMaterial2D");
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(1.5f,2.5f,-10);
            var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5.4f;
            camera.backgroundColor = new Color(.025f,.035f,.065f); camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional;
            var floor = Rect("Safe practice floor",new Vector2(0,-.5f),new Vector2(25,1),new Color(.1f,.17f,.22f),sprite,material);
            floor.AddComponent<BoxCollider2D>().sharedMaterial = friction;
            Rect("Floor edge",new Vector2(0,.025f),new Vector2(25,.05f),Color.cyan,sprite,material);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Biopulse/Prefabs/Runner.prefab"));
            player.transform.position = new Vector2(0,.82f);
            var motor = player.GetComponent<RunnerMotor2D>(); motor.RunSpeed = 0;
            var grab = player.AddComponent<RunnerGrabController>(); grab.Runner = motor;
            var armObject = new GameObject("Reach Arm"); armObject.transform.SetParent(player.transform,false);
            var arm = armObject.AddComponent<LineRenderer>(); arm.useWorldSpace = true; arm.positionCount = 2;
            arm.startWidth = arm.endWidth = .06f; arm.sharedMaterial = material; arm.sortingOrder = 5;
            arm.startColor = arm.endColor = new Color(1,.7f,.2f); arm.enabled = false; grab.Arm = arm;
            var near = Rect("Near practice target",new Vector2(1.2f,2.2f),new Vector2(.35f,.35f),Color.yellow,sprite,material).AddComponent<GrabTarget>();
            near.StableId = "tutorial-near"; near.gameObject.SetActive(false);
            var far = Rect("Far practice target",new Vector2(3.2f,3.2f),new Vector2(.35f,.35f),Color.yellow,sprite,material).AddComponent<GrabTarget>();
            far.StableId = "tutorial-far"; far.gameObject.SetActive(false);

            var terminalRoot = new GameObject("Practice terminal");
            var zone = terminalRoot.AddComponent<BoxCollider2D>(); zone.isTrigger = true; zone.size = new Vector2(2,3); zone.offset = Vector2.up;
            var gate = Rect("Practice gate",new Vector2(2.5f,1.5f),new Vector2(.2f,3),Color.cyan,sprite,material);
            gate.transform.SetParent(terminalRoot.transform,true); var barrier = gate.AddComponent<BoxCollider2D>();
            var badge = Rect("Terminal display",new Vector2(1,2.5f),new Vector2(.5f,.7f),Color.cyan,sprite,material);
            badge.transform.SetParent(terminalRoot.transform,true);
            var terminal = terminalRoot.AddComponent<LockTerminal>(); terminal.Configure(zone,barrier,badge.GetComponent<SpriteRenderer>());
            terminalRoot.SetActive(false);
            var locks = player.AddComponent<RunnerLockController>(); locks.Runner = motor;
            var services = new GameObject("Calibration and Tutorial"); var input = services.AddComponent<EmgInputDriver>();
            input.ShowDiagnostics = false;
            var router = services.AddComponent<RunnerCommandRouter>(); router.Runner = motor; router.Input = input; router.Grab = grab; router.Terminal = locks;
            var wizard = services.AddComponent<CalibrationTutorial>(); wizard.Configure(input,motor,router,grab,locks,near,far,terminal);
            router.enabled = false;
            EditorSceneManager.SaveScene(scene,ScenePath);
            ConfigureBuildScenes(); AssetDatabase.SaveAssets();
        }
        private static void ConfigureBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.globalScenes);
            foreach (var active in EditorBuildSettings.scenes)
                if (!scenes.Exists(s => s.path == active.path)) scenes.Add(active);
            scenes.RemoveAll(s => s.path == ScenePath || s.path == RunnerSandboxBuilder.ScenePath);
            scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
            // Start directly in gameplay during prototyping; calibration is opened explicitly.
            scenes.Insert(0,new EditorBuildSettingsScene(RunnerSandboxBuilder.ScenePath,true));
            EditorBuildSettings.globalScenes = scenes.ToArray();
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        private static GameObject Rect(string name, Vector2 position, Vector2 size, Color color, Sprite sprite, Material material)
        {
            var go = new GameObject(name); go.transform.position = position; go.transform.localScale = new Vector3(size.x,size.y,1);
            var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.color = color;
            return go;
        }
    }
}
