// Editor tool that prepares the ground-effect prefabs. The saved appearance stays editable in Unity.

using System;
using Escape4Now.Map;
using UnityEditor;
using UnityEngine;

public static class SurvivalGroundAuthoring
{
    public static void Apply()
    {
        try
        {
            foreach(string name in new[]{"Mud Patch","Wet Pool Deck","Track Boost Marking","Sprinkler Water"})
            {
                string path="Assets/Prefabs/Survival/Gameplay/"+name+".prefab";
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var surface=root.GetComponent<SurvivalEnvironmentalSurface>();
                    if(surface==null)surface=root.AddComponent<SurvivalEnvironmentalSurface>();
                    var fields=new SerializedObject(surface);
                    fields.FindProperty("chevronSize").floatValue=.6f;
                    fields.FindProperty("paintWidth").floatValue=.04f;
                    fields.FindProperty("style").enumValueIndex=name=="Mud Patch"?0:name=="Track Boost Marking"?2:1;
                    var replaced=fields.FindProperty("replacedVisuals");
                    replaced.arraySize=name=="Sprinkler Water"?0:root.transform.childCount;
                    for(int i=0;i<replaced.arraySize;i++)
                    {
                        var child=root.transform.GetChild(i).gameObject;
                        replaced.GetArrayElementAtIndex(i).objectReferenceValue=child;
                        child.SetActive(false);
                    }
                    if(name=="Wet Pool Deck"||name=="Sprinkler Water")
                    {
                        fields.FindProperty("groundColor").colorValue=new Color(.25f,.43f,.5f,.3f);
                        fields.FindProperty("variationColor").colorValue=new Color(.4f,.63f,.7f,.4f);
                        fields.FindProperty("puddleColor").colorValue=new Color(.65f,.85f,.9f,.45f);
                    }
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            Debug.Log("SURVIVAL GROUND ASSETS AUTHORED: merged soil/wet regions, lane-aligned paint and grouped sprinklers.");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
