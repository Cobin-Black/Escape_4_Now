using Escape4Now.Arena;
using UnityEditor;
using UnityEngine;

// Edit the assigned settings beside the map, like the Race and Survival map components.
[CustomEditor(typeof(ArenaMap))]
public sealed class ArenaMapInspector : Editor
{
    private readonly bool[] open =
    {
        true,
        false,
        false,
        false,
        false,
        false,
        false,
        false
    };
    private Editor obstacleEditor, roomEditor, eventEditor;
    private bool eventsOpen = true;
    private bool obstacles, visuals;
    private static readonly string[] titles =
    {
        "Generation / Room and Seed",
        "Room Layout / Seating",
        "Player Spawn Safety",
        "Obstacle Prefabs and Placement",
        "Materials and Tile Rendering",
        "Auditorium Presentation",
        "Camera / Whole Map Framing",
        "Debug Labels"
    };
    private static readonly string[][] fields =
    {
        new[]
        {
            "room",
            "size",
            "fixedSeed",
            "seed",
            "generationAttempts"
        },
        new[]
        {
            "sideRoomDepth",
            "aisleWidth",
            "stageDepth",
            "seatingBlockWidth",
            "seatingRowSpacing",
            "seatingRowsPerSection",
            "seatQuarterTurns"
        },
        new[]
        {
            "playerRadius",
            "minimumSpawnDistance",
            "minimumOpenNeighbours",
            "spawnAttempts"
        },
        new[]
        {
            "equipmentCount",
            "routeClearance",
            "equipment",
            "bench",
            "bleachers",
            "hoop",
            "crates",
            "obstacleGroups",
            "roomVisuals"
        },
        new[]
        {
            "material",
            "tileSize",
            "wood",
            "court",
            "carpet",
            "service",
            "stage",
            "wall",
            "markings",
            "floorVariation",
            "floorDepth",
            "wallHeight",
            "frontWallHeight",
            "lineWidth"
        },
        new[]
        {
            "seatColor",
            "seatSize",
            "curtainColor",
            "curtainHeight"
        },
        new[]
        {
            "frameWholeMap",
            "cameraPropHeadroom",
            "cameraSpreadZoom",
            "background",
            "minimumZoom",
            "maximumZoom",
            "cameraPadding",
            "cameraSafeViewport",
            "cameraFocusOffset",
            "cameraResponse"
        },
        new[]
        {
            "showRoomNames",
            "labelFont",
            "labelSize",
            "labelColor"
        }
    };
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var map = (ArenaMap)target;
        var settings = map.Settings;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Assign a mode-specific Map Settings asset.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox("These sections edit this map's assigned settings asset. Shared assets affect every scene that uses them.", MessageType.Info);
        var data = new SerializedObject(settings);
        data.Update();
        for (int i = 0; i < titles.Length; i++)
        {
            open[i] = EditorGUILayout.Foldout(open[i], titles[i], true, EditorStyles.foldoutHeader);
            if (!open[i])
                continue;
            EditorGUI.indentLevel++;
            foreach (string name in fields[i])
                EditorGUILayout.PropertyField(data.FindProperty(name), true);
            EditorGUI.indentLevel--;
        }

        data.ApplyModifiedProperties();
        Inline(map.EventController, "Event Spawner / Enabled Events and Weights", ref eventsOpen, ref eventEditor);
        Inline(settings.obstacleGroups, "Obstacle Groups / Density, Facing and Prefabs", ref obstacles, ref obstacleEditor);
        Inline(settings.roomVisuals, "Room Details / Prefabs, Colors and Markings", ref visuals, ref roomEditor);
        if (Application.isPlaying && map.Layout != null)
        {
            EditorGUILayout.LabelField("Current Match", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Seed", map.Seed.ToString());
            EditorGUILayout.LabelField("Layout", map.Layout.Width + " × " + map.Layout.Height);
            EditorGUILayout.LabelField("Grouped Obstacles", map.Layout.groupedProps.Count.ToString());
        }
    }

    private static void Inline(Object asset, string label, ref bool expanded, ref Editor editor)
    {
        if (asset == null)
            return;
        expanded = EditorGUILayout.Foldout(expanded, label, true, EditorStyles.foldoutHeader);
        if (!expanded)
            return;
        CreateCachedEditor(asset, null, ref editor);
        editor.OnInspectorGUI();
    }

    private void OnDisable()
    {
        if (obstacleEditor != null)
            DestroyImmediate(obstacleEditor);
        if (roomEditor != null)
            DestroyImmediate(roomEditor);
        if (eventEditor != null)
            DestroyImmediate(eventEditor);
    }
}
