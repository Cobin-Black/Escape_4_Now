// Groups Survival event choices, prefabs, chances and extra settings into readable Inspector sections.

using Escape4Now.Map;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(SurvivalEventSettings))]
public sealed class SurvivalEventSettingsEditor : Editor
{
    private ReorderableList events;
    private bool showPrefabs;
    private bool showChances;
    private bool showDetails;
    private bool showAdditionalSettings;
    private bool showEnvironment;

    private void OnEnable()
    {
        events = new ReorderableList(serializedObject, serializedObject.FindProperty("events"), true, true, true, true);
        events.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Events: expand a name to edit");
        events.elementHeightCallback = index => EditorGUI.GetPropertyHeight(events.serializedProperty.GetArrayElementAtIndex(index), true) + 6f;
        events.drawElementCallback = (rect, index, active, focused) =>
        {
            var rule = events.serializedProperty.GetArrayElementAtIndex(index);
            string title = rule.FindPropertyRelative("title").stringValue;
            if (string.IsNullOrWhiteSpace(title))
                title = ObjectNames.NicifyVariableName(((SurvivalEventType)rule.FindPropertyRelative("type").enumValueIndex).ToString());
            if (!rule.FindPropertyRelative("enabled").boolValue) title += " (disabled)";
            title += " [" + SurvivalEventRule.TriggerFor((SurvivalEventType)rule.FindPropertyRelative("type").enumValueIndex) + "]";
            rect.y += 2f;
            rect.height = EditorGUI.GetPropertyHeight(rule, true);
            EditorGUI.PropertyField(rect, rule, new GUIContent(title), true);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eventCount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showEventLocations"));
        var rules = events.serializedProperty;
        int tileCount = 0;
        for (int i = 0; i < rules.arraySize; i++)
            if (SurvivalEventRule.TriggerFor((SurvivalEventType)rules.GetArrayElementAtIndex(i).FindPropertyRelative("type").enumValueIndex) == SurvivalEventTrigger.Tile) tileCount++;
        EditorGUILayout.Space();
        showPrefabs = EditorGUILayout.Foldout(showPrefabs, "Event Prefabs (" + tileCount + " standard tiles)", true);
        if (showPrefabs)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                var trigger = SurvivalEventRule.TriggerFor((SurvivalEventType)rule.FindPropertyRelative("type").enumValueIndex);
                if (trigger == SurvivalEventTrigger.Tile)
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("prefab"), new GUIContent(EventName(rule)));
            }
            EditorGUI.indentLevel--;
        }
        showEnvironment = EditorGUILayout.Foldout(showEnvironment, "Environmental / Pickup Prefabs", true);
        if (showEnvironment)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                var type = (SurvivalEventType)rule.FindPropertyRelative("type").enumValueIndex;
                var trigger = SurvivalEventRule.TriggerFor(type);
                if (type == SurvivalEventType.CarAlarm || type == SurvivalEventType.OpenGate || trigger == SurvivalEventTrigger.Global || trigger == SurvivalEventTrigger.Tile) continue;
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("prefab"), new GUIContent(EventName(rule)));
            }
            EditorGUILayout.HelpBox("Poolside Slip uses visible wet patches on the generated deck. Car Alarm uses a generated car and its effect prefab. Open Gate remains unavailable until a functional shortcut gate is supplied.", MessageType.Info);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Event Spawn Chances", EditorStyles.boldLabel);
        showChances = EditorGUILayout.Foldout(showChances, "Spawn Chances (" + rules.arraySize + ")", true);
        if (showChances)
        {
            EditorGUILayout.HelpBox("Weights select the match's event types, then their placements or activations within each trigger group. Per-match counts, density and turn timing are in Additional Event Settings. Expand an event's details to set its occurrence limit.", MessageType.Info);
            EditorGUI.indentLevel++;
            for (int i = 0; i < rules.arraySize; i++)
            {
                var rule = rules.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("enabled"), GUIContent.none, GUILayout.Width(32f));
                    EditorGUILayout.PropertyField(rule.FindPropertyRelative("weight"), new GUIContent(EventName(rule)));
                }
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();
        showDetails = EditorGUILayout.Foldout(showDetails, "Event Details / Add or Remove Events", true);
        if (showDetails) events.DoLayoutList();
        showAdditionalSettings = EditorGUILayout.Foldout(showAdditionalSettings, "Additional Event Settings", true);
        if (showAdditionalSettings) DrawAdditionalSettings();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawAdditionalSettings()
    {
        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script" || property.name == "events" || property.name == "eventCount" || property.name == "showEventLocations") continue;
            EditorGUILayout.PropertyField(property, true);
        }
    }

    private static string EventName(SerializedProperty rule)
    {
        string title = rule.FindPropertyRelative("title").stringValue;
        return string.IsNullOrWhiteSpace(title)
            ? ObjectNames.NicifyVariableName(((SurvivalEventType)rule.FindPropertyRelative("type").enumValueIndex).ToString())
            : title;
    }
}
