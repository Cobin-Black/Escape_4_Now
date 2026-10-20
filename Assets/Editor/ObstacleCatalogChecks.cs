using System;
using System.Collections.Generic;
using System.Reflection;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ObstacleCatalogChecks
{
    public static void Run()
    {
        int result = 0;
        try
        {
            CheckRace();
            CheckSurvival();
            Debug.Log("AREA AND SPECIAL ROOM CATALOG CHECKS PASSED");
        }
        catch (Exception error) { Debug.LogException(error); result = 1; }
        if (Application.isBatchMode) EditorApplication.Exit(result);
    }

    private static void CheckRace()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Race_Gamemode.unity");
        try
        {
            IsometricMapTemplate map = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out IsometricMapTemplate found)) map = found;
            Require(map != null, "Race map is assigned");
            var spawner = map.GetComponent<ObstacleSpawner>();
            var seen = new HashSet<SchoolSpaceType>();
            for (int seed = 0; seed < 512 && seen.Count < 6; seed++)
            {
                map.UseNetworkLayout(seed);
                foreach (SchoolSecondarySpace space in map.SecondarySpaces)
                {
                    if (!seen.Add(space.Type)) continue;
                    SchoolFurniture[] originals = new List<SchoolFurniture>(spawner.GetSpecialRoomPrefabs(space.Type)).ToArray();
                    var replacements = new List<SchoolFurniture>();
                    try
                    {
                        var variantSection = new List<Component>();
                        foreach (SchoolFurniture original in originals)
                        {
                            SchoolFurniture variant = UnityEngine.Object.Instantiate(original);
                            variant.name = "Room-specific variant";
                            replacements.Add(variant);
                            variantSection.Add(variant);
                        }
                        typeof(ObstacleSpawner).GetMethod("SetSpecialSection", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(spawner, new object[] { space.Type, variantSection.ToArray() });
                        map.UseNetworkLayout(seed);
                        var plan = new SchoolFurniturePlan(new SchoolMapLayout(seed, map.GenerationSettings), map.FurnitureSettings);
                        var picker = typeof(IsometricMapTemplate).GetMethod("GetFurniturePrefab", BindingFlags.Instance | BindingFlags.NonPublic);
                        int checkedCount = 0;
                        foreach (SchoolFurniturePlacement placement in plan.Furniture)
                        {
                            var cell = new Vector2Int(placement.Cell % map.Width, placement.Cell / map.Width);
                            if (map.GetSpaceType(cell) != space.Type) continue;
                            var selected = (SchoolFurniture)picker.Invoke(map, new object[] { placement });
                            Require(replacements.Contains(selected), space.Type + " uses its own variants, not another room's catalog");
                            checkedCount++;
                        }
                        Require(checkedCount > 0, space.Type + " has planned obstacles");
                    }
                    finally
                    {
                        typeof(ObstacleSpawner).GetMethod("SetSpecialSection", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(spawner, new object[] { space.Type, Array.ConvertAll(originals, item => (Component)item) });
                        foreach (SchoolFurniture variant in replacements) UnityEngine.Object.DestroyImmediate(variant.gameObject);
                    }
                }
            }
            Require(seen.Count == 6, "All six special-room types generate from saved scene settings");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void CheckSurvival()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/SurvivalMode.unity");
        try
        {
            SurvivalMap map = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.TryGetComponent(out SurvivalMap found)) map = found;
            Require(map != null && map.GenerateWithSeed(42), "Survival uses its saved area catalog");
            var spawner = map.GetComponent<SurvivalObstacleSpawner>();
            string original = EditorJsonUtility.ToJson(spawner);
            var choices = typeof(SurvivalObstacleSpawner).GetMethod("Choices", BindingFlags.Instance | BindingFlags.NonPublic);
            var validate = typeof(SurvivalObstacleSpawner).GetMethod("ValidateForLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            var variant = UnityEngine.Object.Instantiate(spawner.GetAreaPrefabs(OutdoorAreaKind.Garden)[0]);
            try
            {
                var settings = new SerializedObject(spawner);
                var garden = settings.FindProperty("gardenPrefabs");
                garden.arraySize = 1;
                garden.GetArrayElementAtIndex(0).objectReferenceValue = variant;
                settings.ApplyModifiedPropertiesWithoutUndo();
                var selected = (SurvivalPropVisual[])choices.Invoke(spawner, new object[] { variant.Kind, OutdoorAreaKind.Garden });
                Require(selected.Length == 1 && selected[0] == variant, "Garden selection uses its own prefab variant");
                var paths = (SurvivalPropVisual[])choices.Invoke(spawner, new object[] { variant.Kind, OutdoorAreaKind.None });
                Require(Array.IndexOf(paths, variant) < 0, "An area variant does not leak into path placement");
                EditorJsonUtility.FromJsonOverwrite(original, spawner);
                OutdoorPropPlacement placement = map.Layout.Props[0];
                OutdoorAreaKind area = map.Layout.GetArea(placement.X, placement.Y);
                typeof(SurvivalObstacleSpawner).GetMethod("SetSection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(spawner, new object[] { area, Array.Empty<SurvivalPropVisual>() });
                bool rejected = false;
                try { validate.Invoke(spawner, new object[] { map.Layout }); }
                catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { rejected = true; }
                Require(rejected && spawner.GetAreaPrefabs(area).Count == 0, "Missing area prefabs are rejected, never silently refilled or substituted");
            }
            finally
            {
                EditorJsonUtility.FromJsonOverwrite(original, spawner);
                UnityEngine.Object.DestroyImmediate(variant.gameObject);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
        Debug.Log("PASS: " + message);
    }
}
