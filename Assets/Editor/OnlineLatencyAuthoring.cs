using Escape4Now.Multiplayer.RelayTesting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds saved Inspector settings to the existing scenes without rebuilding their maps or UI.
public static class OnlineLatencyAuthoring
{
    public static void Apply()
    {
        foreach (string scene in new[] { "OnlineFreeForAll", "OnlineHotPotato" })
        {
            var opened = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity");
            var adapter = Object.FindFirstObjectByType<OnlineArenaMatch>();
            var view = adapter.GetComponent<OnlineArenaPresentation>();
            if (view == null) view = adapter.gameObject.AddComponent<OnlineArenaPresentation>();
            var fields = new SerializedObject(adapter);
            fields.FindProperty("presentation").objectReferenceValue = view;
            fields.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(opened);
        }
        Debug.Log("ONLINE LATENCY SETTINGS SAVED");
    }
}
