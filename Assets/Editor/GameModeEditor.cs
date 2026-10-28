// Shows the game mode's Inspector fields in the Unity Editor.

using Escape4Now.GameModes;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameModeController), true)]
public sealed class GameModeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var mode = (GameModeController)target;
        if (!mode.ValidateConfiguration(out string error))
            EditorGUILayout.HelpBox(error, MessageType.Error);
        else
            EditorGUILayout.HelpBox("Match references and Input System action references are assigned. Edit controls in the linked Input Actions asset. Pause is owned by the shared menu, not this mode.", MessageType.Info);
    }
}
