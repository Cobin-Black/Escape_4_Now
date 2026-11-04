using System;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Part = ArenaVisualAuthoring.Part;

// Saves themed obstacle patterns. Every piece and offset can be edited in the resulting Unity assets.
public static class ArenaObstacleAuthoring
{
    [MenuItem("Tools/Escape 4 Now/Configure Arena Obstacle Groups")]
    public static void Apply()
    {
        try
        {
            Color blue = new Color(.13f, .30f, .53f), metal = new Color(.13f, .18f, .23f), orange = new Color(1, .4f, .05f);
            var mats = ArenaVisualAuthoring.Model("Stacked Gym Mats", new Part(0, 0, .84f, .70f, 0, .12f, blue), new Part(0, 0, .84f, .70f, .13f, .12f, blue * .85f), new Part(0, 0, .84f, .70f, .26f, .12f, blue));
            var cone = ArenaVisualAuthoring.Model("Gym Cone", new Part(0, 0, .45f, .45f, 0, .05f, metal), new Part(0, 0, .26f, .26f, .05f, .13f, orange), new Part(0, 0, .17f, .17f, .18f, .13f, orange), new Part(0, 0, .09f, .09f, .31f, .11f, orange));
            var divider = ArenaVisualAuthoring.Model("Folding Gym Divider", new Part(-.36f, 0, .06f, .26f, 0, .60f, metal), new Part(.36f, 0, .06f, .26f, 0, .60f, metal), new Part(0, 0, .66f, .11f, .12f, .43f, blue));
            var shelf = SchoolProp("StorageShelf", "Arena Storage Shelf");
            var room = AssetDatabase.LoadAssetAtPath<ArenaRoomVisualSettings>("Assets/Settings/Arena/Room Visuals.asset");
            var crate = AssetDatabase.LoadAssetAtPath<SurvivalPropVisual>("Assets/Prefabs/Survival/Crates.prefab");
            var bench = AssetDatabase.LoadAssetAtPath<SurvivalPropVisual>("Assets/Prefabs/Survival/ParkBench.prefab");
            var gym = Save("FreeForAll Obstacle Groups", new[] { Pattern("Mat stack and safety cone", ArenaObstacleZone.MainFloor, 1, Piece(mats, 0, 0), Piece(cone, 1, 0)), Pattern("Folding cover", ArenaObstacleZone.MainFloor, 1, Piece(divider, 0, 0), Piece(divider, 0, 1)), Pattern("Ball cart and bench", ArenaObstacleZone.MainFloor, 1, Piece(room.equipmentRack, 0, 0), Piece(bench, 1, 0)), Pattern("Storage corner", ArenaObstacleZone.MainFloor, 1, Piece(shelf, 0, 0), Piece(crate, 1, 0)) });
            var auditorium = Save("HotPotato Obstacle Groups", new[] { Pattern("Stage speaker stack", ArenaObstacleZone.Stage, 0, Piece(room.speaker, 0, 0), Piece(crate, 1, 0)), Pattern("Backstage equipment", ArenaObstacleZone.Stage, 0, Piece(room.controlDesk, 0, 0), Piece(crate, 1, 0)), Pattern("Aisle furniture", ArenaObstacleZone.MainFloor, 0, Piece(bench, 0, 0), Piece(crate, 1, 0)), Pattern("Side partition", ArenaObstacleZone.MainFloor, 0, Piece(divider, 0, 0), Piece(shelf, 0, 1)) });
            gym.groupCount = new Vector2Int(3, 4);
            gym.maximumBlockedTiles = 10;
            gym.maximumFloorCoverage = .08f;
            gym.groupSpacing = 3.5f;
            auditorium.groupCount = new Vector2Int(3, 4);
            auditorium.maximumBlockedTiles = 8;
            auditorium.maximumFloorCoverage = .07f;
            auditorium.groupSpacing = 3;
            foreach (string name in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                var settings = AssetDatabase.LoadAssetAtPath<ArenaMapSettings>("Assets/Settings/Arena/" + name + " Map.asset");
                settings.obstacleGroups = name == "FreeForAll" ? gym : auditorium;
                settings.showRoomNames = false;
                room.showScoreboardText = false;
                EditorUtility.SetDirty(settings);
            }

            EditorUtility.SetDirty(room);
            EditorUtility.SetDirty(gym);
            EditorUtility.SetDirty(auditorium);
            AssetDatabase.SaveAssets();
            Debug.Log("Arena obstacle groups saved; room labels disabled. No escape doors added.");
            if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "-polishArenaUI") >= 0)
            {
                ArenaPresentationAuthoring.Apply();
                return;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    private static ArenaObstaclePiece Piece(SurvivalPropVisual prefab, int x, int y) => new ArenaObstaclePiece
    {
        prefab = prefab,
        offset = new Vector2Int(x, y)
    };
    private static ArenaObstaclePattern Pattern(string label, ArenaObstacleZone zone, int clearance, params ArenaObstaclePiece[] pieces) => new ArenaObstaclePattern
    {
        label = label,
        zone = zone,
        clearance = clearance,
        pieces = pieces
    };
    private static ArenaObstacleSettings Save(string name, ArenaObstaclePattern[] patterns)
    {
        string path = "Assets/Settings/Arena/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<ArenaObstacleSettings>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<ArenaObstacleSettings>();
            asset.name = name;
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.patterns = patterns;
        return asset;
    }

    // The school shelf already has editable block parts. Copy that model data, not its map-specific behavior.
    private static SurvivalPropVisual SchoolProp(string sourceName, string name)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacles/Resources/SchoolObstacles/" + sourceName + ".prefab");
        var original = new SerializedObject(source.GetComponent<SchoolFurniture>());
        var parts = original.FindProperty("parts");
        var values = new Part[parts.arraySize];
        for (int i = 0; i < values.Length; i++)
        {
            var part = parts.GetArrayElementAtIndex(i);
            var offset = part.FindPropertyRelative("offset").vector2Value;
            values[i] = new Part(offset.x, offset.y, part.FindPropertyRelative("width").floatValue, part.FindPropertyRelative("depth").floatValue, part.FindPropertyRelative("bottom").floatValue, part.FindPropertyRelative("height").floatValue, part.FindPropertyRelative("color").colorValue);
        }

        return ArenaVisualAuthoring.Model(name, values);
    }
}
