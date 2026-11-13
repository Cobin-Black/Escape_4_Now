// Creates saved arena scenes and assets. Runtime scripts do not construct their menus or input bindings.
using System;
using System.Collections.Generic;
using System.IO;
using Escape4Now.Arena;
using Escape4Now.Items;
using Escape4Now.Map;
using Escape4Now.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ArenaAuthoring
{
    private static Font font;
    [MenuItem("Tools/Escape 4 Now/Author Arena Scenes")]
    public static void Build()
    {
        try
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Directory.CreateDirectory("Assets/Settings/Arena");
            Directory.CreateDirectory("Assets/Prefabs/Arena");
            var controls = CreateControls();
            var project = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
            var pause = Reference(project, "Player/Pause", "Pause");
            EditorSceneManager.OpenScene("Assets/Scenes/Race_Gamemode.unity");
            var source = Object.FindFirstObjectByType<PlayerCharacter>(FindObjectsInactive.Include);
            var skinSource = Object.FindFirstObjectByType<Skin_Handler>(FindObjectsInactive.Include);
            var skinCopy = new GameObject("Skin library").AddComponent<Skin_Handler>();
            EditorUtility.CopySerialized(skinSource, skinCopy);
            var skinPrefab = PrefabUtility.SaveAsPrefabAsset(skinCopy.gameObject, "Assets/Prefabs/Arena/Skin Library.prefab");
            var actor = Object.Instantiate(source.gameObject);
            actor.name = "Arena Player";
            actor.SetActive(true);
            var character = actor.GetComponent<PlayerCharacter>();
            var identity = new SerializedObject(character);
            Set(identity, "realTimeMovement", true);
            Set(identity, "mapTemplate", null);
            Set(identity, "gameMode", null);
            Set(identity, "turnSystem", null);
            identity.ApplyModifiedPropertiesWithoutUndo();
            character.enabled = true;
            var inventory = actor.GetComponent<PlayerInventory>();
            if (inventory != null)
                inventory.enabled = false;
            var participant = actor.AddComponent<ArenaParticipant>();
            var plate = WorldText(actor.transform, "Player name", new Vector3(0, 1.85f, 0), .06f);
            plate.GetComponent<Renderer>().sortingOrder = 2000;
            var shadow = WorldText(actor.transform, "Name shadow", new Vector3(.015f, 1.83f, 0), .06f);
            shadow.color = new Color(0, 0, 0, .9f);
            shadow.GetComponent<Renderer>().sortingOrder = 1999;
            Assign(participant, "nameLabel", plate, "nameShadow", shadow);
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(actor, "Assets/Prefabs/Arena/Arena Player.prefab").GetComponent<ArenaParticipant>();
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(skinCopy.gameObject);
            foreach (ArenaRoom room in Enum.GetValues(typeof(ArenaRoom)))
            {
                string stem = room == ArenaRoom.Gymnasium ? "FreeForAll" : "HotPotato";
                var settings = ScriptableObject.CreateInstance<ArenaMapSettings>();
                settings.room = room;
                settings.labelFont = font;
                settings.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
                settings.bench = Prop("ParkBench");
                settings.bleachers = Prop("Bleachers");
                settings.hoop = Prop("BasketballHoop");
                settings.crates = Prop("Crates");
                settings.equipment = new[]
                {
                    settings.bench,
                    settings.crates,
                    room == ArenaRoom.Gymnasium ? settings.bleachers : settings.crates
                };
                var list = new List<SurvivalPropVisual>();
                foreach (var p in settings.equipment)
                    if (p != null)
                        list.Add(p);
                settings.equipment = list.ToArray();
                settings = Save(settings, "Assets/Settings/Arena/" + stem + " Map.asset");
                var pool = CreatePool(stem, room);
                AssetDatabase.SaveAssets();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/Arena/Arena Controls.inputactions");
                playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Arena Player.prefab").GetComponent<ArenaParticipant>();
                pause = AssetDatabase.LoadAssetAtPath<InputActionReference>("Assets/Settings/Arena/Pause Input.asset");
                project = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
                settings = AssetDatabase.LoadAssetAtPath<ArenaMapSettings>("Assets/Settings/Arena/" + stem + " Map.asset");
                pool = AssetDatabase.LoadAssetAtPath<ArenaEventPool>("Assets/Settings/Arena/" + stem + " Events.asset");
                skinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Skin Library.prefab");
                if (controls == null || playerPrefab == null)
                    throw new InvalidOperationException("Arena player or input asset failed to reload after switching scenes.");
                var root = new GameObject(room == ArenaRoom.Gymnasium ? "Free For All — Gymnasium" : "Hot Potato — Auditorium");
                var mode = room == ArenaRoom.Gymnasium ? (ArenaMatch)root.AddComponent<KingOfTheHillController>() : root.AddComponent<HotPotatoController>();
                var map = new GameObject("Procedural " + room).AddComponent<ArenaMap>();
                Assign(map, "settings", settings);
                var events = new GameObject("Mode Events").AddComponent<ArenaEventDirector>();
                Assign(events, "pool", pool);
                var skins = (GameObject)PrefabUtility.InstantiatePrefab(skinPrefab);
                skins.name = "Player Skins";
                Assign(mode, "map", map, "playerPrefab", playerPrefab, "skins", skins.GetComponent<Skin_Handler>(), "events", events, "inputActions", controls, "pause", pause);
                var fields = new SerializedObject(mode);
                fields.FindProperty("modeTitle").stringValue = room == ArenaRoom.Gymnasium ? "FREE FOR ALL" : "HOT POTATO";
                fields.FindProperty("instructions").stringValue = room == ArenaRoom.Gymnasium ? "Move freely and be the last player standing. Combat is a foundation for now; attacks are not enabled. Choose separate keyboard controls or gamepads below." : "Chase the potato holder. Use your Tag button nearby to pass it before time runs out. Each explosion eliminates its holder.";
                fields.ApplyModifiedPropertiesWithoutUndo();
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 8, -30);
                camera.GetComponent<Camera>().orthographic = true;
                camera.GetComponent<Camera>().orthographicSize = 12;
                Assign(camera.AddComponent<ArenaSharedCamera>(), "match", mode);
                if (mode is HotPotatoController hot)
                    Potato(hot);
                UI(mode, project);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/" + stem + ".unity");
            }

            AddBuildScenes();
            UpdateMenu();
            AssetDatabase.SaveAssets();
            foreach (string path in new[]
            {
                "Assets/Scenes/FreeForAll.unity",
                "Assets/Scenes/HotPotato.unity"
            }

            )
            {
                EditorSceneManager.OpenScene(path);
                var mode = Object.FindFirstObjectByType<ArenaMatch>();
                var data = new SerializedObject(mode);
                foreach (string field in new[]
                {
                    "map",
                    "playerPrefab",
                    "inputActions",
                    "pause",
                    "skins",
                    "events"
                }

                )
                    if (data.FindProperty(field).objectReferenceValue == null)
                        throw new InvalidOperationException(path + " has an empty " + field + " reference.");
            }

            Debug.Log("Arena scenes and Inspector assets authored successfully.");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arenaPlay") >= 0)
            {
                ArenaChecks.Run();
                return;
            }

            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-arenaGeneration") >= 0)
            {
                ArenaChecks.Generation();
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

    private static SurvivalPropVisual Prop(string name) => AssetDatabase.LoadAssetAtPath<SurvivalPropVisual>("Assets/Prefabs/Survival/" + name + ".prefab");
    private static T Save<T>(T asset, string path)
        where T : ScriptableObject
    {
        asset.name = Path.GetFileNameWithoutExtension(path);
        var old = AssetDatabase.LoadAssetAtPath<T>(path);
        if (old != null)
        {
            EditorUtility.CopySerialized(asset, old);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(old);
            return old;
        }

        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static InputActionReference Reference(InputActionAsset asset, string action, string name)
    {
        return Save(InputActionReference.Create(asset.FindAction(action, true)), "Assets/Settings/Arena/" + name + " Input.asset");
    }

    private static InputActionAsset CreateControls()
    {
        var asset = ScriptableObject.CreateInstance<InputActionAsset>();
        asset.name = "Arena Controls";
        string[][] keys =
        {
            new[]
            {
                "w",
                "s",
                "a",
                "d",
                "f"
            },
            new[]
            {
                "upArrow",
                "downArrow",
                "leftArrow",
                "rightArrow",
                "rightCtrl"
            },
            new[]
            {
                "i",
                "k",
                "j",
                "l",
                "o"
            },
            new[]
            {
                "numpad8",
                "numpad5",
                "numpad4",
                "numpad6",
                "numpad0"
            }
        };
        for (int i = 0; i < keys.Length; i++)
        {
            var map = asset.AddActionMap("Player" + (i + 1));
            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/" + keys[i][0], groups: "Keyboard").With("Down", "<Keyboard>/" + keys[i][1], groups: "Keyboard").With("Left", "<Keyboard>/" + keys[i][2], groups: "Keyboard").With("Right", "<Keyboard>/" + keys[i][3], groups: "Keyboard");
            move.AddBinding("<Gamepad>/leftStick", groups: "Gamepad");
            var tag = map.AddAction("Tag", InputActionType.Button);
            tag.AddBinding("<Keyboard>/" + keys[i][4], groups: "Keyboard");
            tag.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
        }

        string path = "Assets/Settings/Arena/Arena Controls.inputactions";
        File.WriteAllText(path, asset.ToJson());
        Object.DestroyImmediate(asset);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }

    private static ArenaEventPool CreatePool(string stem, ArenaRoom room) => ArenaEventAuthoring.ConfigurePool(stem, room);
    private static void Potato(HotPotatoController mode)
    {
        var view = new GameObject("Potato presentation").AddComponent<PotatoHolderView>();
        var marker = new GameObject("Holder marker");
        marker.transform.SetParent(view.transform);
        var potato = new GameObject("Potato", typeof(SpriteRenderer));
        potato.transform.SetParent(marker.transform, false);
        potato.transform.localScale = new Vector3(2.5f, 1.8f, 1);
        potato.GetComponent<SpriteRenderer>().sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var glow = new GameObject("Holder glow", typeof(SpriteRenderer));
        glow.transform.SetParent(marker.transform, false);
        glow.transform.localScale = new Vector3(4.2f, 3f, 1);
        glow.GetComponent<SpriteRenderer>().sprite = potato.GetComponent<SpriteRenderer>().sprite;
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
        potato.GetComponent<SpriteRenderer>().sharedMaterial = material;
        glow.GetComponent<SpriteRenderer>().sharedMaterial = material;
        var label = WorldText(marker.transform, "Countdown", new Vector3(0, .3f, 0), .09f);
        var source = view.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;
        Assign(view, "mode", mode, "marker", marker, "potato", potato.GetComponent<SpriteRenderer>(), "glow", glow.GetComponent<SpriteRenderer>(), "countdown", label, "audioSource", source, "tick", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SurvivalInteraction/Press.wav"));
        marker.SetActive(false);
    }

    private static void UI(ArenaMatch mode, InputActionAsset project) => ArenaPresentationAuthoring.BuildSceneUI(mode, project);
    private static TextMesh WorldText(Transform parent, string label, Vector3 position, float size)
    {
        var go = new GameObject(label, typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var text = go.GetComponent<TextMesh>();
        text.font = font;
        text.fontSize = 40;
        text.characterSize = size;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.GetComponent<Renderer>().sharedMaterial = font.material;
        return text;
    }

    private static void Assign(Object target, params object[] pairs)
    {
        var data = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
            data.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(SerializedObject data, string name, bool value) => data.FindProperty(name).boolValue = value;
    private static void Set(SerializedObject data, string name, Object value) => data.FindProperty(name).objectReferenceValue = value;
    private static void AddBuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var path in new[]
        {
            "Assets/Scenes/FreeForAll.unity",
            "Assets/Scenes/HotPotato.unity"
        }

        )
            if (!scenes.Exists(s => s.path == path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
    private static void UpdateMenu()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var menu = Object.FindFirstObjectByType<Escape4Now.Menus.MainMenuScreen>();
        var data = new SerializedObject(menu);
        var scenes = data.FindProperty("modeScenes");
        scenes.arraySize = 4;
        scenes.GetArrayElementAtIndex(2).stringValue = "FreeForAll";
        scenes.GetArrayElementAtIndex(3).stringValue = "HotPotato";
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }
}
