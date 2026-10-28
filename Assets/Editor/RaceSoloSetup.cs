// Editor setup tool for the Race lobby's 1–4 player buttons. It saves scene changes when run.

using System;
using Escape4Now.GameModes;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class RaceSoloSetup
{
    public static void Apply()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Race_Gamemode.unity");
            var mode = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<Race_Gamemode>());
            mode.FindProperty("allowSoloPlay").boolValue = true;
            mode.ApplyModifiedPropertiesWithoutUndo();
            var lobby = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<LobbyUI>(FindObjectsInactive.Include));
            var counts = lobby.FindProperty("playerCounts");
            var template = (Toggle)counts.GetArrayElementAtIndex(0).objectReferenceValue;
            var single = (Toggle)lobby.FindProperty("singlePlayer").objectReferenceValue;
            if (single == null) single = UnityEngine.Object.Instantiate(template, template.transform.parent);
            single.name = "1";
            single.GetComponentInChildren<Text>(true).text = "1";
            single.SetIsOnWithoutNotify(false);
            lobby.FindProperty("singlePlayer").objectReferenceValue = single;
            lobby.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < 4; i++)
            {
                var toggle = i == 0 ? single : (Toggle)counts.GetArrayElementAtIndex(i - 1).objectReferenceValue;
                var rect = (RectTransform)toggle.transform;
                var min = rect.anchorMin; var max = rect.anchorMax;
                min.x = i / 4f; max.x = (i + 1) / 4f;
                rect.anchorMin = min; rect.anchorMax = max;
                var left = rect.offsetMin; var right = rect.offsetMax;
                left.x = 3; right.x = -3; rect.offsetMin = left; rect.offsetMax = right;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                foreach(var child in toggle.GetComponentsInChildren<RectTransform>(true))
                {
                    if(child==rect)continue;
                    child.anchorMin=Vector2.zero;child.anchorMax=Vector2.one;
                    child.offsetMin=child.offsetMax=Vector2.zero;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                }
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(lobby.targetObject);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RACE SOLO SETUP APPLIED: Inspector option and native 1/2/3/4 lobby controls.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
