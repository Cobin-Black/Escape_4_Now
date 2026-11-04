using Escape4Now.Arena;
using UnityEditor;
using UnityEngine;

// Show each mode's enabled events directly, without requiring teammates to open a separate asset.
[CustomEditor(typeof(ArenaEventDirector))]
public sealed class ArenaEventInspector : Editor
{
    private bool details, additional;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var director = (ArenaEventDirector)target;
        if (director.Pool == null)
            return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Event Spawner Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Edits the assigned mode-specific event pool. Global events do not need floor prefabs.", MessageType.Info);
        var data = new SerializedObject(director.Pool);
        data.Update();
        foreach (string name in new[]
        {
            "enableEvents",
            "count"
        }

        )
            EditorGUILayout.PropertyField(data.FindProperty(name));
        EditorGUILayout.LabelField("Enabled Events / Spawn Weights", EditorStyles.boldLabel);
        var entries = data.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("enabled"), GUIContent.none, GUILayout.Width(18));
                EditorGUILayout.LabelField(entry.FindPropertyRelative("label").stringValue);
                EditorGUILayout.PropertyField(entry.FindPropertyRelative("weight"), GUIContent.none, GUILayout.Width(65));
            }
        }

        details = EditorGUILayout.Foldout(details, "Event Details / Add or Remove Events", true);
        if (details)
            EditorGUILayout.PropertyField(entries, new GUIContent("Event Definitions"), true);
        additional = EditorGUILayout.Foldout(additional, "Additional Event Settings", true);
        if (additional)
            foreach (string name in new[]
            {
                "initialDelay",
                "activationInterval",
                "maximumSimultaneous",
                "retryDelay",
                "noticeSeconds",
                "playerClearance",
                "safePlacementAttempts",
                "minimumPotatoTimeAfterCut",
                "visuals",
                "volume"
            }

            )
                EditorGUILayout.PropertyField(data.FindProperty(name), true);
        data.ApplyModifiedProperties();
        foreach (var entry in director.Pool.entries)
            if (entry != null && entry.enabled && entry.DisabledReason.Length > 0)
                EditorGUILayout.HelpBox(entry.label + ": " + entry.DisabledReason, MessageType.Info);
        if (!Application.isPlaying)
            return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("This Match", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Next event attempt", director.NextEventSeconds.ToString("0.0") + "s");
        foreach (var entry in director.Selected)
            EditorGUILayout.LabelField(entry.label);
        foreach (var state in director.Active)
            EditorGUILayout.LabelField(state.Rule.label, state.Warning ? "Warning" : "Active");
        Repaint();
    }
}
