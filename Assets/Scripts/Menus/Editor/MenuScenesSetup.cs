using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Escape4Now.Menus.EditorTools
{
    //Adds the start screen and main menu to Build Settings, start screen first so the game opens on it.
    public static class MenuScenesSetup
    {
        private const string StartScene = "Assets/Scenes/StartScreen.unity";
        private const string MainMenuScene = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Escape 4 Now/Add Menu Scenes To Build Settings")]
        public static void AddMenuScenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != StartScene && s.path != MainMenuScene).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(MainMenuScene, true));
            scenes.Insert(0, new EditorBuildSettingsScene(StartScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("Escape 4 Now: StartScreen and MainMenu are now the first scenes in Build Settings.");
        }
    }
}
