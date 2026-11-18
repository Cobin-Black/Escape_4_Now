using System.Linq;
using Escape4Now.Arena;
using Escape4Now.Multiplayer;
using Escape4Now.Multiplayer.RelayTesting;
using Escape4Now.Player;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Updates saved references without rebuilding a scene or replacing its existing layout.
public static class PlayerPresentationAuthoring
{
    public static void Apply()
    {
        const string prefabPath = "Assets/Prefabs/Interface/MatchInterface.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ConfigureDice(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }

        foreach (string scene in new[] { "Race_Gamemode", "SurvivalMode", "FreeForAll", "HotPotato",
            "OnlineRace", "OnlineSurvival", "OnlineFreeForAll", "OnlineHotPotato" })
        {
            var opened = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
            var local = Object.FindFirstObjectByType<LocalMultiplayerSession>();
            var arena = Object.FindFirstObjectByType<ArenaMatch>();
            if (local != null) ConfigureColors(local);
            if (arena != null) ConfigureColors(arena);
            foreach (var root in opened.GetRootGameObjects()) ConfigureDice(root);
            var online = Object.FindFirstObjectByType<OnlineRaceLobbyView>(FindObjectsInactive.Include);
            if (online != null)
            {
                var rows = new SerializedObject(online).FindProperty("rows");
                for (int slot = 0; slot < rows.arraySize; slot++)
                {
                    var card = (GameObject)rows.GetArrayElementAtIndex(slot).FindPropertyRelative("root").objectReferenceValue;
                    ConfigureLocker(card.transform, arena != null ? arena.GetPlayerColor(slot) : local.GetPlayerColor(slot));
                }
            }
            var setup = Object.FindFirstObjectByType<ArenaLobbyView>(FindObjectsInactive.Include);
            if (setup != null)
            {
                var rows = new SerializedObject(setup).FindProperty("rows");
                for (int slot = 0; slot < rows.arraySize; slot++)
                    ConfigureLocker(((GameObject)rows.GetArrayElementAtIndex(slot).objectReferenceValue).transform, arena.GetPlayerColor(slot));
            }
            EditorSceneManager.SaveScene(opened);

        }
        Debug.Log("PLAYER PRESENTATION REFERENCES SAVED: shared colors, locker accents and HUD dice.");
    }

    private static void ConfigureDice(GameObject root)
    {
        foreach (var battle in root.GetComponentsInChildren<RaceBattleUI>(true))
        {
            var hud = battle.GetComponentInParent<Canvas>().GetComponentInChildren<MatchHudUI>(true);
            var template = (DiceDisplay)new SerializedObject(hud).FindProperty("dice").objectReferenceValue;
            var data = new SerializedObject(battle);
            data.FindProperty("diceTemplate").objectReferenceValue = template;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static void ConfigureColors(Component owner)
    {
        var data = new SerializedObject(owner);
        data.FindProperty("playerPalette").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<PlayerColorPalette>("Assets/Settings/Player Colors.asset");
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ConfigureLocker(Transform root, Color color)
    {
        var band = root.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Locker Band");
        if (band == null) return;
        var view = root.GetComponent<PlayerColorView>();
        if (view == null) view = root.gameObject.AddComponent<PlayerColorView>();
        var data = new SerializedObject(view);
        var accents = data.FindProperty("accents");
        // Record the existing player-colored pieces once. Later palette edits use these saved references.
        if (accents.arraySize == 0)
        {
            var graphics = root.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.color == band.color).ToArray();
            accents.arraySize = graphics.Length;
            for (int i = 0; i < graphics.Length; i++) accents.GetArrayElementAtIndex(i).objectReferenceValue = graphics[i];
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        view.SetColor(color);
        EditorUtility.SetDirty(view);
    }
}
