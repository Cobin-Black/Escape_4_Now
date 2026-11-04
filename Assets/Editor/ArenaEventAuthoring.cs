using System;
using System.Collections.Generic;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Edits only arena event settings and event presentation in the existing scenes.
public static class ArenaEventAuthoring
{
    private const string Folder = "Assets/Prefabs/Arena/Events/";
    [MenuItem("Tools/Escape 4 Now/Configure Arena Events")]
    public static void Apply()
    {
        try
        {
            foreach (var room in new[]
            {
                ArenaRoom.Gymnasium,
                ArenaRoom.Auditorium
            }

            )
            {
                string stem = room == ArenaRoom.Gymnasium ? "FreeForAll" : "HotPotato";
                ConfigurePool(stem, room);
                AssetDatabase.SaveAssets();
                EditorSceneManager.OpenScene("Assets/Scenes/" + stem + ".unity");
                var director = Object.FindFirstObjectByType<ArenaEventDirector>();
                var pool = AssetDatabase.LoadAssetAtPath<ArenaEventPool>("Assets/Settings/Arena/" + stem + " Events.asset");
                Set(director, "pool", pool);
                var audio = director.GetComponent<AudioSource>();
                if (audio == null)
                    audio = director.gameObject.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0;
                var ui = Object.FindFirstObjectByType<ArenaUI>();
                var canvas = ui.GetComponent<Canvas>();
                var old = canvas.transform.Find("Environmental lighting");
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                var shade = new GameObject("Environmental lighting", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                shade.transform.SetParent(canvas.transform, false);
                shade.transform.SetAsFirstSibling();
                var rect = (RectTransform)shade.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var image = shade.GetComponent<RawImage>();
                image.color = Color.clear;
                image.raycastTarget = false;
                var lighting = shade.AddComponent<ArenaEventLightingUI>();
                Set(lighting, "events", director, "shade", image);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Arena event pools and presentation saved. No movement, combat or item settings replaced.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-checkArenaEvents") >= 0)
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

    public static ArenaEventPool ConfigurePool(string stem, ArenaRoom room)
    {
        EnsureFolders();
        var visuals = Visuals();
        var pool = LoadOrCreate<ArenaEventPool>("Assets/Settings/Arena/" + stem + " Events.asset");
        pool.enableEvents = true;
        pool.count = new Vector2Int(3, 5);
        pool.initialDelay = new Vector2(5, 8);
        pool.activationInterval = new Vector2(9, 15);
        pool.maximumSimultaneous = 2;
        pool.visuals = visuals;
        var entries = new List<ArenaEventEntry>();
        ArenaEventKind[] kinds = room == ArenaRoom.Gymnasium ? new[]
        {
            ArenaEventKind.SlipperyFloor,
            ArenaEventKind.BallBarrage,
            ArenaEventKind.BleachersMove,
            ArenaEventKind.LightsOut,
            ArenaEventKind.HazardZone,
            ArenaEventKind.FallingEquipment,
            ArenaEventKind.PlayerSwap,
            ArenaEventKind.KnockbackWave,
            ArenaEventKind.CourtShuffle,
            ArenaEventKind.LockerRoomOpens
        }

        : new[]
        {
            ArenaEventKind.Spotlight,
            ArenaEventKind.CurtainDrop,
            ArenaEventKind.Blackout,
            ArenaEventKind.StageDoorOpens,
            ArenaEventKind.SeatBlock,
            ArenaEventKind.SoundBlast,
            ArenaEventKind.PlayerSwap,
            ArenaEventKind.PotatoShuffle,
            ArenaEventKind.ExtraTime,
            ArenaEventKind.TimeCut,
            ArenaEventKind.FallingProp,
            ArenaEventKind.MovingStageLights
        };
        foreach (var kind in kinds)
        {
            var rule = new ArenaEventEntry
            {
                kind = kind,
                label = Title(kind),
                duration = 6,
                warningSeconds = 2.5f,
                maximumPerMatch = 2,
                cooldown = 20
            };
            rule.enabled = rule.DisabledReason.Length == 0;
            rule.warningSound = Clip("Hover");
            rule.activationSound = Clip("Connect");
            if (rule.IsBlackout)
            {
                rule.weight = .3f;
                rule.duration = 5;
                rule.color = new Color(.55f, .4f, 1, .8f);
            }

            if (rule.IsWave)
            {
                rule.weight = .4f;
                rule.amount = 1.5f;
                rule.radius = 4;
                rule.duration = 1.5f;
                rule.color = new Color(.4f, .7f, 1, .9f);
                rule.activationSound = Clip("Branch Impact");
            }

            if (kind == ArenaEventKind.PlayerSwap || kind == ArenaEventKind.PotatoShuffle)
            {
                rule.weight = kind == ArenaEventKind.PlayerSwap ? .25f : .12f;
                rule.duration = 1;
                rule.color = new Color(.85f, .4f, 1, .9f);
            }

            if (kind == ArenaEventKind.TimeCut)
            {
                rule.weight = .18f;
                rule.amount = 2;
                rule.warningSeconds = 3;
                rule.duration = 1;
                rule.color = new Color(1, .25f, .2f, .9f);
            }

            if (kind == ArenaEventKind.ExtraTime)
            {
                rule.weight = .6f;
                rule.amount = 3;
                rule.duration = 1;
                rule.color = new Color(.3f, 1, .6f, .9f);
            }

            if (kind == ArenaEventKind.HazardZone)
            {
                rule.damage = 1;
                rule.amount = .8f;
                rule.hitInterval = 2;
                rule.color = new Color(1, .3f, .05f, .85f);
            }

            if (kind == ArenaEventKind.SlipperyFloor)
            {
                rule.color = new Color(.3f, .85f, 1, .7f);
                rule.steeringResponse = 2;
                rule.zoneTiles = 7;
            }

            if (kind == ArenaEventKind.BallBarrage)
            {
                rule.color = new Color(1, .6f, .12f, .85f);
                rule.activationSound = Clip("Branch Impact");
            }

            if (kind == ArenaEventKind.Spotlight || kind == ArenaEventKind.MovingStageLights)
                rule.color = new Color(1, .85f, .35f, .75f);
            if (rule.IsBlock && kind != ArenaEventKind.BleachersMove)
                rule.obstaclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + (kind == ArenaEventKind.CurtainDrop ? "Falling Curtain" : kind == ArenaEventKind.SeatBlock ? "Seat Barrier" : "Falling Equipment") + ".prefab");
            entries.Add(rule);
        }

        pool.entries = entries.ToArray();
        EditorUtility.SetDirty(pool);
        return pool;
    }

    private static string Title(ArenaEventKind kind) => System.Text.RegularExpressions.Regex.Replace(kind.ToString(), "([a-z])([A-Z])", "$1 $2");
    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Arena/Events"))
            AssetDatabase.CreateFolder("Assets/Prefabs/Arena", "Events");
    }

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SurvivalInteraction/" + name + ".wav");
    private static T LoadOrCreate<T>(string path)
        where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static ArenaEventVisualSettings Visuals()
    {
        var settings = LoadOrCreate<ArenaEventVisualSettings>("Assets/Settings/Arena/Event Visuals.asset");
        settings.lineMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
        var shade = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Arena/Event Blackout.mat");
        if (shade == null)
        {
            shade = new Material(Shader.Find("Escape 4 Now/UI/Blackout"));
            AssetDatabase.CreateAsset(shade, "Assets/Settings/Arena/Event Blackout.mat");
        }

        settings.blackoutMaterial = shade;
        settings.wetFloorPrefab = Surface("Slippery Gym Floor", new Color(.3f, .65f, .8f, .45f), new Color(.75f, .94f, 1, .6f));
        settings.hazardFloorPrefab = Surface("Hazard Ground", new Color(.85f, .12f, .02f, .3f), new Color(1, .65f, .1f, .4f));
        settings.basketballPrefab = Ball(settings.lineMaterial);
        Falling("Falling Equipment", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Survival/Crates.prefab"));
        Falling("Seat Barrier", Prop("Seat Barrier Model", new Color(.5f, .1f, .15f), false));
        Falling("Falling Curtain", Prop("Curtain Model", new Color(.55f, .06f, .13f), true));
        settings.speakerPrefab = Prop("Event Speaker", new Color(.08f, .12f, .16f), true);
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static GameObject Surface(string name, Color ground, Color puddle)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Survival/Gameplay/Wet Deck Water.prefab");
        // Some projects name the visible region Wet Pool Deck; use that saved renderer if necessary.
        if (source == null || source.GetComponent<SurvivalEnvironmentalSurface>() == null)
            source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Survival/Gameplay/Wet Pool Deck.prefab");
        var root = Object.Instantiate(source);
        root.name = name;
        var renderer = root.GetComponent<SurvivalEnvironmentalSurface>();
        var data = new SerializedObject(renderer);
        data.FindProperty("groundColor").colorValue = ground;
        data.FindProperty("variationColor").colorValue = ground;
        data.FindProperty("puddleColor").colorValue = puddle;
        data.FindProperty("groundSortingOrder").intValue = -800;
        data.ApplyModifiedPropertiesWithoutUndo();
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject Falling(string name, GameObject source)
    {
        var root = new GameObject(name);
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(source);
        prop.transform.SetParent(root.transform, false);
        var fall = root.AddComponent<SurvivalBranchFall>();
        Set(fall, "branch", prop.transform);
        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = Clip("Branch Impact");
        audio.volume = .1f;
        Set(fall, "impact", audio);
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject Prop(string name, Color color, bool tall)
    {
        var root = new GameObject(name);
        var visual = root.AddComponent<SurvivalPropVisual>();
        var data = new SerializedObject(visual);
        data.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
        var parts = data.FindProperty("parts");
        parts.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var part = parts.GetArrayElementAtIndex(i);
            part.FindPropertyRelative("offset").vector2Value = new Vector2((i - 1) * .25f, 0);
            part.FindPropertyRelative("width").floatValue = .27f;
            part.FindPropertyRelative("depth").floatValue = .4f;
            part.FindPropertyRelative("height").floatValue = tall ? .9f : .45f;
            part.FindPropertyRelative("bottom").floatValue = 0;
            part.FindPropertyRelative("color").colorValue = i == 1 ? color : color * .7f;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + name + ".prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    private static GameObject Ball(Material material)
    {
        var root = new GameObject("Basketball");
        var group = root.AddComponent<UnityEngine.Rendering.SortingGroup>();
        group.sortingOrder = 900;
        var body = new GameObject("Orange leather", typeof(SpriteRenderer));
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * 2.3f;
        var sprite = body.GetComponent<SpriteRenderer>();
        sprite.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        sprite.sharedMaterial = material;
        sprite.color = new Color(.95f, .38f, .06f);
        for (int axis = 0; axis < 2; axis++)
        {
            var line = new GameObject("Ball seam").AddComponent<LineRenderer>();
            line.transform.SetParent(root.transform, false);
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.startWidth = line.endWidth = .015f;
            line.startColor = line.endColor = new Color(.18f, .08f, .02f);
            line.sortingOrder = 1;
            line.positionCount = 2;
            line.SetPosition(0, axis == 0 ? Vector3.left * .2f : Vector3.down * .2f);
            line.SetPosition(1, axis == 0 ? Vector3.right * .2f : Vector3.up * .2f);
        }

        var saved = PrefabUtility.SaveAsPrefabAsset(root, Folder + "Basketball.prefab");
        Object.DestroyImmediate(root);
        return saved;
    }

    private static void Set(Object target, params object[] fields)
    {
        var data = new SerializedObject(target);
        for (int i = 0; i < fields.Length; i += 2)
            data.FindProperty((string)fields[i]).objectReferenceValue = (Object)fields[i + 1];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
