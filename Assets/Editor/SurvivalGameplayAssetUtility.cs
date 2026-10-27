// Editor repair tool for saved prefab references that have not loaded correctly. It keeps the existing assignments.

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Escape4Now.Map;
using Escape4Now.Puzzles;
using UnityEditor;
using UnityEngine;

//Rebinds existing serialized prefab assignments after imports, without supplying gameplay defaults.
public static class SurvivalGameplayAssetUtility
{
    private const string Menu = "Tools/Survival/Refresh Gameplay Asset References";
    [InitializeOnLoadMethod]
    private static void ScheduleImportRecovery() => EditorApplication.delayCall += RecoverAfterImport;
    //Wait for imports to settle and repair only references that still have an assigned saved GUID.
    private static void RecoverAfterImport()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += RecoverAfterImport; return; }
        foreach (string guid in AssetDatabase.FindAssets("t:SurvivalPuzzleDefinition", new[] { "Assets/Settings/Survival" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var puzzle = AssetDatabase.LoadAssetAtPath<SurvivalPuzzleDefinition>(path);
            if (puzzle == null || puzzle.stationPrefab != null ||
                !Regex.IsMatch(File.ReadAllText(path), @"stationPrefab:\s*\{[^}]*guid:")) continue;
            try { RefreshReferences(); }
            catch (Exception error) { Debug.LogException(error); }
            break;
        }
    }
    [MenuItem(Menu, true)] private static bool Available() => !EditorApplication.isPlayingOrWillChangePlaymode;
    [MenuItem(Menu)]
    //Reload the already assigned prefab components; do not replace missing choices with gameplay defaults.
    public static void RefreshReferences()
    {
        var report = new StringBuilder();
        int repaired = 0;
        string backup = "Logs/GameplayAssetRefresh-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        try
        {
            foreach (string type in new[] { "SurvivalPuzzleDefinition", "SurvivalRulesSettings", "SurvivalEventSettings" })
                foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets/Settings/Survival" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string source = File.ReadAllText(path);
                    //Read the original saved references before Unity saves a temporarily unresolved field as empty.
                    var references = Regex.Matches(source, @"(?m)^\s+(stationPrefab|exitPrefab|prefab):\s*\{([^}]+)\}");
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    var fields = new SerializedObject(asset);
                    var iterator = fields.GetIterator();
                    int index = 0;
                    bool changed = false;
                    while (iterator.Next(true))
                    {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                            iterator.name != "stationPrefab" && iterator.name != "exitPrefab" && iterator.name != "prefab") continue;
                        if (index >= references.Count || references[index].Groups[1].Value != iterator.name)
                            throw new InvalidOperationException("Prefab field order does not match " + path + "; no guessed assignments were made.");
                        string prefabGuid = Regex.Match(references[index++].Groups[2].Value, @"guid:\s*([0-9a-fA-F]{32})").Groups[1].Value;
                        if (string.IsNullOrEmpty(prefabGuid)) continue;
                        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                        bool unresolved = iterator.objectReferenceValue == null;
                        if (unresolved) AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        var marker = prefab != null ? prefab.GetComponent<SurvivalWorldMarker>() : null;
                        report.AppendLine(path + " / " + iterator.propertyPath + ": " + (unresolved ? "unresolved" : "loaded") + " -> " + prefabPath);
                        if (marker == null) throw new InvalidOperationException("Assigned prefab has no Survival World Marker: " + prefabPath);
                        if (iterator.objectReferenceValue == marker) continue;
                        if (!changed)
                        {
                            //Keep the original asset before saving its first repaired reference.
                            string target = Path.Combine(backup, path);
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            File.Copy(path, target);
                        }
                        iterator.objectReferenceValue = marker;
                        changed = true; repaired++;
                    }
                    if (changed)
                    {
                        fields.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(asset);
                        AssetDatabase.SaveAssetIfDirty(asset);
                    }
                    if (asset is SurvivalPuzzleDefinition puzzle) puzzle.Validate();
                    if (asset is SurvivalEventSettings events) events.Validate();
                }
            report.AppendLine("SURVIVAL ASSET REFERENCES VALIDATED; reassigned " + repaired + " existing references.");
            Debug.Log(report.ToString());
        }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SurvivalAssetReferences.log", report.ToString());
        }
    }
    public static void BatchCheck()
    {
        try { RefreshReferences(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
