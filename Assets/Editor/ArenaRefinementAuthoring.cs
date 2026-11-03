using System;
using Escape4Now.Arena;
using UnityEditor;
using UnityEngine;

// Apply this presentation pass to the two existing arena scenes; never rebuild their match systems.
public static class ArenaRefinementAuthoring
{
    public static void Apply()
    {
        try
        {
            var style = AssetDatabase.LoadAssetAtPath<ArenaInterfaceStyle>("Assets/Settings/Arena/Interface Style.asset");
            style.hudWidth = 252;
            style.hudPadding = 9;
            style.healthRowHeight = 38;
            style.hudGap = 3;
            style.hudScale = 1;
            EditorUtility.SetDirty(style);
            foreach (string stem in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                var map = AssetDatabase.LoadAssetAtPath<ArenaMapSettings>("Assets/Settings/Arena/" + stem + " Map.asset");
                map.frameWholeMap = true;
                map.seatQuarterTurns = 2;
                map.seatingBlockWidth = 5;
                map.seatingRowSpacing = 1;
                map.seatingRowsPerSection = 2;
                map.cameraSafeViewport = new Rect(.025f, .035f, .95f, .93f);
                map.cameraPadding = new Vector2(.55f, .55f);
                map.cameraPropHeadroom = 1.4f;
                map.cameraSpreadZoom = .025f;
                map.minimumZoom = 4;
                map.maximumZoom = 16;
                var groups = map.obstacleGroups;
                groups.groupCount = stem == "FreeForAll" ? new Vector2Int(5, 6) : new Vector2Int(4, 5);
                groups.maximumBlockedTiles = stem == "FreeForAll" ? 14 : 12;
                groups.maximumFloorCoverage = .10f;
                groups.placementAttempts = 320;
                foreach (var pattern in groups.patterns)
                {
                    if (stem == "FreeForAll" && pattern.label == "Storage corner")
                    {
                        pattern.zone = ArenaObstacleZone.Service;
                        pattern.clearance = 0;
                    }

                    pattern.edgeOnly = true;
                    pattern.edgeDepth = 3;
                    pattern.facing = stem == "FreeForAll" ? ArenaPropFacing.TowardRoom : pattern.zone == ArenaObstacleZone.Stage ? ArenaPropFacing.TowardAudience : ArenaPropFacing.TowardStage;
                }

                EditorUtility.SetDirty(groups);
                EditorUtility.SetDirty(map);
            }

            const string prefabPath = "Assets/Prefabs/Arena/Arena Player.prefab";
            var player = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (player.GetComponent<ArenaPlayerIndicator>() == null)
                    player.AddComponent<ArenaPlayerIndicator>();
                var data = new SerializedObject(player.GetComponent<ArenaParticipant>());
                data.FindProperty("speed").floatValue = 2.2f;
                data.FindProperty("showNameLabels").boolValue = false;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(player);
            }

            AssetDatabase.SaveAssets();
            ArenaPresentationAuthoring.Apply();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }
}
