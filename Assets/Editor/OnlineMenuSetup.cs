using System.Collections.Generic;
using System.IO;
using System.Linq;
using Escape4Now.Menus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// Saves the new menu references without recreating the menu or any game scene.
public static class OnlineMenuSetup
{
    [MenuItem("Tools/Escape 4 Now/Relay Test/Connect Main Menu")]
    public static void Configure()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        Directory.CreateDirectory("Assets/Settings/Menus");
        AssetDatabase.Refresh();
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
        var menu = Object.FindFirstObjectByType<MainMenuScreen>();
        var data = new SerializedObject(menu);
        data.FindProperty("onlineRaceScene").stringValue = "OnlineRace";
        foreach (string name in new[] { "Navigate", "Submit", "Cancel" })
        {
            string path = "Assets/Settings/Menus/Menu " + name + ".asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
            if (reference == null)
            {
                reference = InputActionReference.Create(actions.FindAction("UI/" + name, true));
                AssetDatabase.CreateAsset(reference, path);
            }
            data.FindProperty(name.ToLowerInvariant()).objectReferenceValue = reference;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        var scenes = EditorBuildSettings.scenes.ToList();
        const string onlineScene = "Assets/Scenes/OnlineRace.unity";
        var existing = scenes.Find(scene => scene.path == onlineScene);
        if (existing == null) scenes.Add(new EditorBuildSettingsScene(onlineScene, true));
        else existing.enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("ONLINE MENU CONFIGURED: saved UI actions and enabled OnlineRace scene.");
    }
}
