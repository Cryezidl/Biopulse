using System.Collections.Generic;
using Biopulse.Neon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Biopulse.EditorTools
{
    public static class NeonCoinBuilder
    {
        public const string PrefabPath = "Assets/Biopulse/Prefabs/Coin_NII.prefab";

        [MenuItem("Biopulse/Neon/Install Coins In All Levels")]
        public static void InstallAllLevels()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            foreach (var level in NeonLevelBuilder.Levels)
            {
                var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{level.Scene}.unity");
                ConfigureScene(Object.FindFirstObjectByType<NeonLevelSession>());
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
        }

        public static void ConfigureScene(NeonLevelSession session)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new System.InvalidOperationException("Missing coin prefab: " + PrefabPath);
            var director = session.GetComponent<NeonCoinDirector>() ?? session.gameObject.AddComponent<NeonCoinDirector>();
            if (session.IsEndless)
            {
                director.LevelField = null;
                foreach (var chunk in Object.FindFirstObjectByType<NeonEndlessRooftops>().Templates)
                    chunk.Coins = ConfigureField(chunk.transform, prefab, session.Runner.RunSpeed);
            }
            else
                director.LevelField = ConfigureField(GameObject.Find("World").transform, prefab, session.Runner.RunSpeed, session.FinishX - 1);
            EditorUtility.SetDirty(director);
        }

        private static NeonCoinField ConfigureField(Transform root, GameObject prefab, float speed, float maxX = float.PositiveInfinity)
        {
            var fieldRoot = root.Find("Coin field");
            if (fieldRoot == null)
            {
                fieldRoot = new GameObject("Coin field").transform;
                fieldRoot.SetParent(root, false);
            }
            var field = fieldRoot.GetComponent<NeonCoinField>() ?? fieldRoot.gameObject.AddComponent<NeonCoinField>();
            field.CoinPrefab = prefab;
            var routes = new List<NeonCoinRoute>();
            var solids = new List<Bounds>();
            foreach (var collider in root.GetComponentsInChildren<BoxCollider2D>(true))
                if (!collider.isTrigger || collider.GetComponentInParent<NeonHazard>() != null) solids.Add(BoxBounds(collider));

            // Ground rows are above actual supporting colliders, never arbitrarily over empty space.
            foreach (var floor in root.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (floor.isTrigger || !floor.name.StartsWith("Ground ")) continue;
                Bounds b = BoxBounds(floor);
                for (float x = b.min.x + 1.6f; x <= b.max.x - 1.6f; x += 5)
                    AddRoute(routes, root, solids, "run", new Vector2(x - .85f, b.max.y + .95f),
                        new Vector2(x, b.max.y + .95f), new Vector2(x + .85f, b.max.y + .95f), maxX);
            }

            foreach (var zone in root.GetComponentsInChildren<NeonTimingZone>(true))
            {
                bool gap = zone.Challenge == NeonChallenge.Gap;
                bool jump = zone.Challenge == NeonChallenge.LowJump || zone.Challenge == NeonChallenge.MidJump || zone.Challenge == NeonChallenge.HighJump;
                if (!gap && !jump) continue;
                float amplitude = zone.Challenge == NeonChallenge.LowJump ? .36f : zone.Challenge == NeonChallenge.MidJump ? .64f : .97f;
                float centre = zone.TargetX;
                float takeoff;
                if (gap)
                {
                    float width = BoxBounds(zone.GetComponent<BoxCollider2D>()).max.x - .6f - zone.TargetX;
                    amplitude = width > 3.2f ? .85f : .5f;
                    centre += width / 2;
                    takeoff = zone.TargetX - .45f - speed * .05f;
                }
                else takeoff = centre - speed * (Mathf.Lerp(6, 14, amplitude) / 29.43f) * .9f - speed * .05f;
                float impulse = Mathf.Lerp(6, 14, amplitude);
                var points = new Vector2[3];
                for (int i = 0; i < 3; i++)
                {
                    float x = centre + (i - 1) * .85f;
                    float t = (x - takeoff) / speed;
                    points[i] = new Vector2(x, .82f + impulse * t - 14.715f * t * t);
                }
                AddRoute(routes, root, solids, gap ? "gap" : "jump", points[0], points[1], points[2], maxX);
            }

            foreach (var target in root.GetComponentsInChildren<Biopulse.Gameplay.GrabTarget>(true))
            {
                Vector2 centre = (Vector2)target.transform.position + new Vector2(.35f, -2.1f);
                AddRoute(routes, root, solids, "grab", centre + new Vector2(-.75f, -.2f), centre,
                    centre + new Vector2(.75f, .2f), maxX);
            }
            routes.Sort((a, b) => a.Points[0].x.CompareTo(b.Points[0].x));
            field.Routes = routes.ToArray();
            EditorUtility.SetDirty(field);
            return field;
        }

        private static void AddRoute(List<NeonCoinRoute> routes, Transform root, List<Bounds> solids, string kind,
            Vector2 a, Vector2 b, Vector2 c, float maxX)
        {
            var points = new List<Vector2>(3);
            foreach (var p in new[] { a, b, c })
            {
                if (p.x < 3 || p.x > maxX || p.y < .3f) continue;
                // Reserve room for the player's capsule as well as the coin, including bobbing margin.
                var body = new Bounds(p, new Vector3(.9f, 1.74f, 1));
                bool safe = true;
                foreach (var solid in solids) if (body.Intersects(solid)) { safe = false; break; }
                if (safe) points.Add(root.InverseTransformPoint(p));
                else if (points.Count > 0) break; // Keep a contiguous collectible row.
            }
            if (points.Count > 0) routes.Add(new NeonCoinRoute { Kind = kind, Points = points.ToArray() });
        }

        private static Bounds BoxBounds(BoxCollider2D box)
        {
            Vector2 half = box.size / 2;
            var bounds = new Bounds(box.transform.TransformPoint(box.offset - half), Vector3.zero);
            bounds.Encapsulate(box.transform.TransformPoint(box.offset + half));
            bounds.Encapsulate(box.transform.TransformPoint(box.offset + new Vector2(-half.x, half.y)));
            bounds.Encapsulate(box.transform.TransformPoint(box.offset + new Vector2(half.x, -half.y)));
            bounds.size = new Vector3(bounds.size.x, bounds.size.y, 1);
            return bounds;
        }
    }
}
