// Organizes Race event prefabs, chances and extra settings in the Inspector.

using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapEventController))]
public sealed class MapEventControllerEditor : Editor
{
    private bool showDetails, showAdditional;
    private int selected;
    private Editor prefabEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.Update();
        var prefabs = serializedObject.FindProperty("eventPrefabs");
        EditorGUILayout.Space();
        showDetails = EditorGUILayout.Foldout(showDetails, "Event Details / Add or Remove Events", true);
        if (showDetails)
        {
            EditorGUILayout.HelpBox("Add or remove event types in Event Prefabs and Spawn Chances above. Select an event here to edit its matching entry.", MessageType.Info);
            if (prefabs.arraySize > 0)
            {
                var names = new string[prefabs.arraySize];
                for (int i = 0; i < names.Length; i++)
                {
                    var marker = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as EventTileMarker;
                    names[i] = marker != null ? ObjectNames.NicifyVariableName(marker.EventType.ToString()) : "Unassigned prefab";
                }
                selected = Mathf.Clamp(selected, 0, names.Length - 1);
                selected = EditorGUILayout.Popup("Event", selected, names);
                EditorGUILayout.PropertyField(prefabs.GetArrayElementAtIndex(selected), new GUIContent("Prefab"));
                var current = prefabs.GetArrayElementAtIndex(selected).objectReferenceValue as EventTileMarker;
                var chances = serializedObject.FindProperty("spawnChances");
                if (current != null)
                    for (int i = 0; i < chances.arraySize; i++)
                    {
                        var rule = chances.GetArrayElementAtIndex(i);
                        if (rule.FindPropertyRelative("eventType").enumValueIndex == (int)current.EventType)
                            EditorGUILayout.PropertyField(rule.FindPropertyRelative("chance"), new GUIContent("Spawn Chance"));
                    }
            }
        }
        showAdditional = EditorGUILayout.Foldout(showAdditional, "Additional Event Settings", true);
        if (showAdditional && prefabs.arraySize > 0)
        {
            selected = Mathf.Clamp(selected, 0, prefabs.arraySize - 1);
            var marker = prefabs.GetArrayElementAtIndex(selected).objectReferenceValue;
            EditorGUILayout.HelpBox("Colors, labels and dimensions below edit the selected event prefab asset.", MessageType.Info);
            if (marker != null)
            {
                CreateCachedEditor(marker, null, ref prefabEditor);
                prefabEditor.OnInspectorGUI();
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
    private void OnDisable() { if (prefabEditor != null) DestroyImmediate(prefabEditor); }
}
