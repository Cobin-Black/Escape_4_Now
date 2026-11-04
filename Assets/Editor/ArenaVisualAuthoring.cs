using System;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Saves the compact room palette and editable prop models. It does not rebuild either scene.
public static class ArenaVisualAuthoring
{
    private const string Folder = "Assets/Prefabs/Arena/Room Details/";
    internal struct Part
    {
        public Vector2 offset;
        public float width, depth, bottom, height;
        public Color color;
        public Part(float x, float y, float width, float depth, float bottom, float height, Color color)
        {
            offset = new Vector2(x, y);
            this.width = width;
            this.depth = depth;
            this.bottom = bottom;
            this.height = height;
            this.color = color;
        }
    }

    private static Color Metal => new Color(.14f, .18f, .23f);
    private static Color Wood => new Color(.58f, .35f, .15f);
    private static Color Red => new Color(.43f, .035f, .08f);

    [MenuItem("Tools/Escape 4 Now/Polish Compact Arena Rooms")]
    public static void Apply()
    {
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Arena/Room Details"))
                AssetDatabase.CreateFolder("Assets/Prefabs/Arena", "Room Details");
            var style = AssetDatabase.LoadAssetAtPath<ArenaRoomVisualSettings>("Assets/Settings/Arena/Room Visuals.asset");
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<ArenaRoomVisualSettings>();
                style.name = "Room Visuals";
                AssetDatabase.CreateAsset(style, "Assets/Settings/Arena/Room Visuals.asset");
            }

            style.auditoriumSeat = Model("Padded Auditorium Seat", new Part(0, 0, .66f, .50f, .12f, .13f, Red), new Part(0, .21f, .66f, .11f, .23f, .36f, Red), new Part(-.31f, 0, .07f, .46f, .27f, .07f, Metal), new Part(.31f, 0, .07f, .46f, .27f, .07f, Metal), new Part(-.23f, -.15f, .07f, .07f, 0, .15f, Metal), new Part(.23f, -.15f, .07f, .07f, 0, .15f, Metal), new Part(-.23f, .16f, .07f, .07f, 0, .15f, Metal), new Part(.23f, .16f, .07f, .07f, 0, .15f, Metal));
            style.lockers = Model("School Lockers", new Part(0, 0, .88f, .40f, 0, .08f, Metal), new Part(-.3f, 0, .27f, .37f, .08f, .66f, new Color(.18f, .39f, .56f)), new Part(0, 0, .27f, .37f, .08f, .66f, new Color(.22f, .46f, .64f)), new Part(.3f, 0, .27f, .37f, .08f, .66f, new Color(.18f, .39f, .56f)), new Part(-.22f, -.2f, .04f, .025f, .39f, .08f, Metal), new Part(.08f, -.2f, .04f, .025f, .39f, .08f, Metal), new Part(.38f, -.2f, .04f, .025f, .39f, .08f, Metal));
            style.controlDesk = Model("Control Booth Desk", new Part(0, 0, .84f, .44f, 0, .22f, Metal), new Part(0, 0, .9f, .50f, .22f, .06f, Wood), new Part(-.2f, .08f, .27f, .09f, .3f, .28f, new Color(.04f, .2f, .28f)), new Part(.2f, .08f, .27f, .09f, .3f, .28f, new Color(.05f, .28f, .35f)), new Part(0, -.15f, .5f, .12f, .28f, .025f, new Color(.65f, .71f, .71f)));
            style.equipmentRack = Model("Sports Equipment Cart", new Part(0, 0, .78f, .48f, .12f, .07f, Metal), new Part(-.36f, 0, .06f, .48f, .16f, .35f, Metal), new Part(.36f, 0, .06f, .48f, .16f, .35f, Metal), new Part(-.2f, 0, .2f, .24f, .2f, .2f, new Color(.95f, .43f, .06f)), new Part(.1f, 0, .2f, .24f, .2f, .2f, new Color(.9f, .37f, .04f)), new Part(-.3f, -.18f, .12f, .1f, 0, .12f, Metal), new Part(.3f, -.18f, .12f, .1f, 0, .12f, Metal));
            style.scoreboard = Model("Gym Scoreboard", new Part(0, 0, 3.2f, .22f, .10f, 1.58f, Metal), new Part(0, -.13f, 3.0f, .03f, .18f, 1.4f, new Color(.035f, .05f, .07f)), new Part(-.82f, -.15f, 1.15f, .02f, .40f, .50f, new Color(.004f, .008f, .013f)), new Part(.82f, -.15f, 1.15f, .02f, .40f, .50f, new Color(.004f, .008f, .013f)));
            style.window = Model("High Gym Window", new Part(0, 0, .9f, .16f, .15f, .70f, new Color(.79f, .78f, .7f)), new Part(-.21f, -.09f, .36f, .025f, .25f, .5f, new Color(.45f, .73f, .86f)), new Part(.21f, -.09f, .36f, .025f, .25f, .5f, new Color(.6f, .83f, .9f)));
            style.curtainPleats = Model("Stage Curtain Pleats", new Part(-.32f, 0, .17f, .18f, 0, 1.1f, Red), new Part(-.16f, .025f, .16f, .16f, 0, 1.1f, Red * .7f), new Part(0, 0, .17f, .18f, 0, 1.1f, Red), new Part(.16f, .025f, .16f, .16f, 0, 1.1f, Red * .7f), new Part(.32f, 0, .17f, .18f, 0, 1.1f, Red));
            style.speaker = Model("Stage Speaker", new Part(0, 0, .58f, .4f, 0, .75f, Metal), new Part(0, -.22f, .44f, .03f, .12f, .42f, new Color(.035f, .045f, .055f)), new Part(0, -.24f, .28f, .025f, .2f, .25f, new Color(.08f, .09f, .11f)), new Part(0, -.24f, .18f, .025f, .57f, .1f, new Color(.3f, .34f, .36f)));
            style.displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EditorUtility.SetDirty(style);
            style.courtKeyColor = new Color(.18f, .34f, .59f, .9f);
            style.awayKeyColor = new Color(.66f, .13f, .15f, .85f);
            style.playerSortingOffset = 60;
            var bleachers = BlueBleachers();
            foreach (string stem in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                var settings = AssetDatabase.LoadAssetAtPath<ArenaMapSettings>("Assets/Settings/Arena/" + stem + " Map.asset");
                settings.size = new Vector2Int(20, 20);
                settings.sideRoomDepth = 3;
                settings.stageDepth = 4;
                settings.seatingRowSpacing = 2;
                settings.equipmentCount = new Vector2Int(4, 7);
                settings.minimumSpawnDistance = 4.5f;
                settings.roomVisuals = style;
                settings.floorDepth = .001f;
                settings.floorVariation = .018f;
                settings.labelSize = .05f;
                settings.frontWallHeight = .18f;
                settings.wall = new Color(.66f, .63f, .54f);
                settings.service = new Color(.31f, .36f, .39f);
                settings.court = new Color(.78f, .57f, .31f);
                settings.wood = new Color(.69f, .47f, .25f);
                settings.carpet = new Color(.24f, .08f, .13f);
                settings.stage = new Color(.55f, .34f, .18f);
                settings.curtainColor = Red;
                settings.curtainHeight = .95f;
                settings.cameraPadding = new Vector2(2.2f, 2);
                settings.minimumZoom = 4;
                settings.bleachers = bleachers;
                if (stem == "FreeForAll")
                    settings.equipment = new[]
                    {
                        style.equipmentRack,
                        settings.bench,
                        settings.crates
                    };
                else
                    settings.equipment = new[]
                    {
                        style.controlDesk,
                        settings.crates,
                        settings.bench
                    };
                EditorUtility.SetDirty(settings);
            }

            ImproveEventVisuals(style);
            AssetDatabase.SaveAssets();
            PolishSavedHUDs();
            Debug.Log("Compact 20x20 gym and auditorium visuals saved.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-checkCompactArenas") >= 0)
            {
                ArenaEventChecks.Run();
                return;
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
        }
    }

    internal static SurvivalPropVisual Model(string name, params Part[] parts)
    {
        var root = new GameObject(name);
        var visual = root.AddComponent<SurvivalPropVisual>();
        var fields = new SerializedObject(visual);
        fields.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
        var entries = fields.FindProperty("parts");
        entries.arraySize = parts.Length;
        for (int i = 0; i < parts.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("offset").vector2Value = parts[i].offset;
            entry.FindPropertyRelative("width").floatValue = parts[i].width;
            entry.FindPropertyRelative("depth").floatValue = parts[i].depth;
            entry.FindPropertyRelative("bottom").floatValue = parts[i].bottom;
            entry.FindPropertyRelative("height").floatValue = parts[i].height;
            entry.FindPropertyRelative("color").colorValue = parts[i].color;
        }

        fields.ApplyModifiedPropertiesWithoutUndo();
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab").GetComponent<SurvivalPropVisual>();
        Object.DestroyImmediate(root);
        return saved;
    }

    private static SurvivalPropVisual BlueBleachers()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Survival/Bleachers.prefab"));
        root.name = "Blue Gym Bleachers";
        var visual = root.GetComponent<SurvivalPropVisual>();
        var fields = new SerializedObject(visual);
        var parts = fields.FindProperty("parts");
        for (int i = 0; i < parts.arraySize; i++)
            if (i % 2 == 0)
                parts.GetArrayElementAtIndex(i).FindPropertyRelative("color").colorValue = new Color(.17f, .33f, .57f);
        fields.ApplyModifiedPropertiesWithoutUndo();
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + "Blue Gym Bleachers.prefab").GetComponent<SurvivalPropVisual>();
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void ImproveEventVisuals(ArenaRoomVisualSettings style)
    {
        ReplaceFallingModel("Seat Barrier", style.auditoriumSeat.gameObject);
        ReplaceFallingModel("Falling Curtain", style.curtainPleats.gameObject);
        var visuals = AssetDatabase.LoadAssetAtPath<ArenaEventVisualSettings>("Assets/Settings/Arena/Event Visuals.asset");
        visuals.speakerPrefab = style.speaker.gameObject;
        visuals.beamOpacity = .32f;
        visuals.lightPoolOpacity = .4f;
        EditorUtility.SetDirty(visuals);
        var wet = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Arena/Events/Slippery Gym Floor.prefab");
        try
        {
            var fields = new SerializedObject(wet.GetComponent<SurvivalEnvironmentalSurface>());
            fields.FindProperty("groundColor").colorValue = new Color(.28f, .62f, .75f, .2f);
            fields.FindProperty("variationColor").colorValue = new Color(.55f, .8f, .9f, .25f);
            fields.FindProperty("puddleChance").floatValue = .8f;
            fields.FindProperty("edgeRoundness").floatValue = .47f;
            fields.FindProperty("puddleColor").colorValue = new Color(.8f, .95f, 1, .55f);
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(wet, "Assets/Prefabs/Arena/Events/Slippery Gym Floor.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(wet);
        }
    }

    private static void ReplaceFallingModel(string name, GameObject prefab)
    {
        string path = "Assets/Prefabs/Arena/Events/" + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            while (root.transform.childCount > 0)
                Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
            prop.transform.SetParent(root.transform, false);
            var fields = new SerializedObject(root.GetComponent<SurvivalBranchFall>());
            fields.FindProperty("branch").objectReferenceValue = prop.transform;
            fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PolishSavedHUDs()
    {
        foreach (string stem in new[]
        {
            "FreeForAll",
            "HotPotato"
        }

        )
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + stem + ".unity");
            var ui = Object.FindFirstObjectByType<ArenaUI>();
            var canvas = ui.GetComponent<Canvas>();
            if (canvas.GetComponent<Escape4Now.UI.MatchCanvasVisibility>() == null)
                canvas.gameObject.AddComponent<Escape4Now.UI.MatchCanvasVisibility>();
            var hud = ui.transform.Find("Match HUD");
            var layout = hud.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = hud.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 7;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = hud.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = hud.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var text in hud.GetComponentsInChildren<Text>(true))
                if (text.gameObject.name == "Title")
                    text.fontStyle = FontStyle.Bold;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
    }
}
