// Checks generated outdoor maps, prefab references and camera framing, then saves map previews.

using System;
using System.IO;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SurvivalMapChecks
{
    [MenuItem("Tools/Survival/Validate Scene and Render Previews")]
    public static void Run()
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = default;
        int result = 0;
        try
        {
            scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SurvivalMode.unity");
            SurvivalMap map = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.TryGetComponent(out SurvivalMap found)) map = found;
            Require(map != null && map.GenerateWithSeed(42), "Survival scene generates from its serialized settings and references");
            Require(map.Layout.Width == 32 && map.Layout.Height == 32 && map.Layout.Validate(out _), "Scene uses a validated 32 x 32 map");
            var spawner = map.GetComponent<SurvivalObstacleSpawner>();
            Require(spawner != null && spawner.enabled && spawner.Map == map, "Map has its own enabled obstacle spawner");
            var serializedSpawner = new SerializedObject(spawner);
            var kinds = new System.Collections.Generic.HashSet<OutdoorPropKind>();
            foreach (SurvivalPropVisual prefab in spawner.GetAllPrefabs())
            {
                Require(prefab != null && kinds.Add(prefab.Kind), "Each obstacle kind has a valid distinct prefab reference");
                if (prefab.Kind == OutdoorPropKind.BasketballHoop) CheckMaterialOverride(prefab, map);
            }
            Require(kinds.Count == Enum.GetValues(typeof(OutdoorPropKind)).Length, "All outdoor kinds are assigned in the area and boundary sections");
            foreach (OutdoorPropRule rule in spawner.PlacementSettings.Rules)
                foreach (OutdoorAreaKind area in rule.AllowedAreas)
                {
                    bool found = false;
                    foreach (SurvivalPropVisual prefab in spawner.GetAreaPrefabs(area)) found |= prefab != null && prefab.Kind == rule.Prop;
                    Require(found, $"The {area} section contains {rule.Prop}");
                }
            Require(spawner.HasSpawnedObstacles && spawner.SpawnedObstacleCount == map.Layout.Props.Count,
                "The obstacle component spawns the validated placements");
            spawner.enabled = false;
            Require(map.GenerateWithSeed(42) && map.Layout.Props.Count == 0, "Disabling obstacle placement updates both visuals and walkability");
            spawner.enabled = true;
            Require(map.GenerateWithSeed(42) && map.Layout.Props.Count > 0, "Re-enabling the component restores seeded placement");
            string original = EditorJsonUtility.ToJson(spawner);
            serializedSpawner.Update();
            serializedSpawner.FindProperty("gardenPrefabs").ClearArray();
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            Require(spawner.GetAreaPrefabs(OutdoorAreaKind.Garden).Count == 0,
                "An intentionally empty area section is not refilled or replaced by another area's prefabs");
            EditorJsonUtility.FromJsonOverwrite(original, spawner);
            serializedSpawner.Update();
            serializedSpawner.FindProperty("placement.rules").ClearArray();
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            Require(map.GenerateWithSeed(42) && map.Layout.Props.Count == 0, "An intentionally empty rule list is not repopulated");
            EditorJsonUtility.FromJsonOverwrite(original, spawner);
            Require(map.GenerateWithSeed(42), "Inspector rules can be restored");
            int blocked = 0;
            for (int y = 0; y < map.Layout.Height; y++) for (int x = 0; x < map.Layout.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                Require(map.WorldToGrid(map.GridToWorld(cell)) == cell, "Grid/world conversion round trip");
                if (!map.IsWalkable(cell)) blocked++;
            }
            Require(map.GetComponentsInChildren<PolygonCollider2D>().Length == blocked, "Every blocked tile has a matching collider");
            for (int i = 0; i < 4; i++) Require(map.TryGetPlayerSpawn(i, out Vector2Int cell) && map.IsWalkable(cell), "Player reservation is walkable");
            foreach (Vector2Int candidate in map.GetSpawnCandidates(8)) Require(map.IsWalkable(candidate), "Future spawn queries use the same walkability grid");
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.TryGetComponent(out Camera found)) camera = found;
            Require(camera != null, "Scene camera is assigned");
            camera.scene = scene;
            Capture(map, camera, 1280, 720);
            Capture(map, camera, 960, 720);
            Capture(map, camera, 720, 960);
            //Review both court styles and a fully featured campus, without fixing the actual match seed.
            bool basketball = false, tennis = false;
            for (int seed = 0; seed < 128 && (!basketball || !tennis); seed++)
            {
                Require(map.GenerateWithSeed(seed), "Additional seeded scene layout generates");
                bool pool = false, maintenance = false;
                OutdoorCourtSport sport = OutdoorCourtSport.Any;
                foreach (OutdoorArea area in map.Layout.Areas)
                {
                    if (area.Kind == OutdoorAreaKind.Pool) pool = true;
                    if (area.Kind == OutdoorAreaKind.Maintenance) maintenance = true;
                    if (area.Kind == OutdoorAreaKind.Courts) sport = area.CourtSport;
                }
                if (!pool || !maintenance) continue;
                if (sport == OutdoorCourtSport.Basketball && !basketball || sport == OutdoorCourtSport.Tennis && !tennis)
                {
                    Capture(map, camera, 1600, 1000, seed, "-" + sport.ToString().ToLowerInvariant());
                    if (sport == OutdoorCourtSport.Basketball) basketball = true; else tennis = true;
                }
            }
            Require(basketball && tennis, "Both sports coexist with pool and maintenance layouts across seeds");
            Debug.Log("SURVIVAL SCENE CHECKS PASSED");
        }
        catch (Exception error) { Debug.LogException(error); result = 1; }
        finally
        {
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
        if (Application.isBatchMode) EditorApplication.Exit(result);
    }

    private static void CheckMaterialOverride(SurvivalPropVisual prefab, SurvivalMap map)
    {
        var mapSettings = new SerializedObject(map);
        var fallback = (Material)mapSettings.FindProperty("mapMaterial").objectReferenceValue;
        Vector2 tileSize = mapSettings.FindProperty("tileSize").vector2Value;
        SurvivalPropVisual prop = UnityEngine.Object.Instantiate(prefab, map.transform);
        var customMaterial = new Material(fallback) { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var settings = new SerializedObject(prop);
            settings.FindProperty("materialOverride").objectReferenceValue = customMaterial;
            settings.ApplyModifiedPropertiesWithoutUndo();
            prop.Build(tileSize, Vector2.one, fallback, 0);
            Renderer[] renderers = prop.GetComponentsInChildren<Renderer>();
            Require(prop.GetComponentInChildren<MeshRenderer>() != null && prop.GetComponentInChildren<LineRenderer>() != null,
                "Material override fixture covers both geometry and detail lines");
            foreach (Renderer renderer in renderers)
                Require(renderer.sharedMaterial == customMaterial, "Every prop part honors the Inspector material override");

            settings.Update();
            settings.FindProperty("materialOverride").objectReferenceValue = null;
            settings.ApplyModifiedPropertiesWithoutUndo();
            prop.Build(tileSize, Vector2.one, fallback, 0);
            foreach (Renderer renderer in prop.GetComponentsInChildren<Renderer>())
                Require(renderer.sharedMaterial == fallback, "Clearing the override restores the map material");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(prop.gameObject);
            UnityEngine.Object.DestroyImmediate(customMaterial);
        }
    }

    private static void Capture(SurvivalMap map, Camera camera, int width, int height, int seed = 42, string suffix = "")
    {
        Directory.CreateDirectory("Logs/SurvivalPreviews");
        RenderTexture previous = RenderTexture.active;
        var target = new RenderTexture(width, height, 24);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.aspect = width / (float)height;
            Require(map.GenerateWithSeed(seed), "Repeated generation releases and rebuilds the preview");
            camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            int visible = 0, pink = 0;
            Color background = image.GetPixel(0, 0);
            foreach (Color color in image.GetPixels())
            {
                if (Mathf.Abs(color.r - background.r) + Mathf.Abs(color.g - background.g)
                    + Mathf.Abs(color.b - background.b) > .12f) visible++;
                if (color.r > .9f && color.b > .9f && color.g < .1f) pink++;
            }
            Require(visible > width * height / 10 && pink < width * height / 100, "Rendered map is nonblank without missing-material pink");
            foreach (Renderer renderer in map.GetComponentsInChildren<Renderer>())
            {
                Vector3 min = camera.WorldToViewportPoint(renderer.bounds.min), max = camera.WorldToViewportPoint(renderer.bounds.max);
                Require(min.x >= -.01f && max.x <= 1.01f && min.y >= -.01f && max.y <= 1.01f, "All generated geometry stays inside the camera frame");
            }
            File.WriteAllBytes($"Logs/SurvivalPreviews/survival-{width}x{height}{suffix}.png", image.EncodeToPNG());
            Debug.Log($"SURVIVAL RENDER: seed {seed}, {width}x{height}{suffix}, {map.Layout.Areas.Count} areas, {map.Layout.Props.Count} props");
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
