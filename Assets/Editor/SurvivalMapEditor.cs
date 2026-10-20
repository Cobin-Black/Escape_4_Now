using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SurvivalMap))]
public sealed class SurvivalMapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var map = (SurvivalMap)target;
        EditorGUILayout.Space();
        if (GUILayout.Button("Regenerate Map Preview")) map.Generate();
        if (map.Layout != null)
        {
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.IntField("Generated Seed", map.CurrentSeed);
            EditorGUILayout.LabelField("Validated Layout", $"{map.Layout.Width} x {map.Layout.Height}, {map.Layout.Areas.Count} areas, {map.Layout.AttemptsUsed} attempts");
            EditorGUILayout.LabelField("Spawn Reservations", map.Layout.PlayerSpawns.Count.ToString());
            EditorGUILayout.LabelField("Outdoor Props", map.Layout.Props.Count.ToString());
            EditorGUILayout.LabelField("Parking Bays", map.Layout.ParkingBays.Count.ToString());
            int pockets = 0;
            foreach (OutdoorArea area in map.Layout.Areas)
            {
                if (area.IsPocket) pockets++;
                if (area.Kind == OutdoorAreaKind.Pool)
                    EditorGUILayout.LabelField("Pool Water", $"{area.WaterWidth} x {area.WaterHeight} blocked tiles");
                if (area.Kind == OutdoorAreaKind.Courts)
                    EditorGUILayout.LabelField("Court Sport", area.CourtSport.ToString());
            }
            EditorGUILayout.LabelField("Courtyard Pockets", pockets.ToString());
        }
        if (!string.IsNullOrEmpty(map.LastGenerationError)) EditorGUILayout.HelpBox(map.LastGenerationError, MessageType.Error);
    }
}
