// Shows the assigned Survival event settings directly below the event controller in the Inspector.

using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SurvivalEventController))]
public sealed class SurvivalEventControllerEditor : Editor
{
    private Editor settingsEditor;
    private bool showMatchPlan;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.Update();
        var settings = serializedObject.FindProperty("settings").objectReferenceValue;
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Assign an Event Settings asset to configure Survival events.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Event Spawner Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These fields edit the assigned Event Settings asset. Changes apply wherever that asset is used.", MessageType.Info);
        CreateCachedEditor(settings, null, ref settingsEditor);
        settingsEditor.OnInspectorGUI();
        var plan = ((SurvivalEventController)target).MatchPlan;
        if (Application.isPlaying && plan != null)
        {
            showMatchPlan = EditorGUILayout.Foldout(showMatchPlan, "Current Match / Selected Events", true);
            if (showMatchPlan)
            {
                EditorGUILayout.LabelField("Floor tile budget", plan.TileBudget.ToString());
                foreach (var rule in plan.SelectedRules)
                    EditorGUILayout.LabelField(rule.title + " [" + rule.Trigger + "]",
                        (plan.Limit(rule.type) - plan.Remaining(rule.type)) + " / " + plan.Limit(rule.type) + " used");
            }
        }
    }

    private void OnDisable()
    {
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
    }
}
