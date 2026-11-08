using System.Collections.Generic;
using System.IO;
using Biopulse.Neon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Biopulse.EditorTools
{
    // Edit-mode screenshots of a level as the game camera would frame it (parallax applied), for art review
    // without entering Play mode. Restores every transform afterwards; the scene is not saved.
    public static class NeonCapture
    {
        public static string[] Capture(string scenePath, float[] runnerXs, string outPrefix, int width = 1600, int height = 900, float runnerY = .82f, string heroFrame = null)
        {
            EditorSceneManager.OpenScene(scenePath);
            var cam = Camera.main;
            var ncam = cam.GetComponent<NeonCamera>();
            float size = ncam != null ? ncam.BaseSize : cam.orthographicSize;
            float feet = .26f;
            var layers = Object.FindObjectsByType<NeonParallax>(FindObjectsInactive.Exclude);
            var saved = new Dictionary<Transform, Vector3>();
            foreach (var p in layers) saved[p.transform] = p.transform.position;
            var camSaved = cam.transform.position;
            var runner = GameObject.Find("Runner");
            var runnerSaved = runner != null ? runner.transform.position : Vector3.zero;
            if (runner != null && heroFrame != null)
            {
                var body = runner.GetComponentInChildren<SpriteRenderer>();
                var frame = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Biopulse/Art/Neon/Hero/{heroFrame}.png");
                if (body != null && frame != null) body.sprite = frame;
            }
            float camStartX = 0 + 5.2f, camStartY = runnerY - .8f + size * (1 - 2 * feet);
            var paths = new List<string>();
            Directory.CreateDirectory(Path.GetDirectoryName(outPrefix));
            foreach (float rx in runnerXs)
            {
                var camPos = new Vector3(rx + 5.2f, runnerY - .8f + size * (1 - 2 * feet), -10);
                cam.transform.position = camPos; cam.orthographicSize = size;
                if (runner != null) runner.transform.position = new Vector3(rx, runnerY, 0);
                foreach (var p in layers)
                {
                    var s = saved[p.transform];
                    float y = p.AnchorTop ? camPos.y + size - p.TopInset : s.y + (camPos.y - camStartY) * p.FactorY;
                    p.transform.position = new Vector3(s.x + (camPos.x - camStartX) * p.Factor, y, s.z);
                }
                var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                string path = $"{outPrefix}_{rx:0}.png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                paths.Add(path);
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            }
            foreach (var kv in saved) kv.Key.position = kv.Value;
            cam.transform.position = camSaved;
            if (runner != null) runner.transform.position = runnerSaved;
            return paths.ToArray();
        }
    }
}
