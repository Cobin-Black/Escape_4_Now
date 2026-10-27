// Draws one event's spawn chance with its name so the list is easier to edit.

using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(MapEventSpawnChance))]
public sealed class MapEventSpawnChanceDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * (property.isExpanded ? 3f : 1f)
            + (property.isExpanded ? EditorGUIUtility.standardVerticalSpacing * 2f : 0f);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var type = property.FindPropertyRelative("eventType");
        var chance = property.FindPropertyRelative("chance");
        string title = ObjectNames.NicifyVariableName(((MapEventType)type.enumValueIndex).ToString());
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(row, property.isExpanded,
            new GUIContent(title + " (" + chance.floatValue.ToString("P0") + ")"), true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(row, type);
            row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(row, chance);
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
