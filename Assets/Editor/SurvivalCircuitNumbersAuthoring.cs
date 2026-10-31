//Adds editable circuit wires and number-slot guides to the existing puzzle prefab.
using System;
using Escape4Now.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SurvivalCircuitNumbersAuthoring
{
    public static void Apply()
    {
        try
        {
            const string path="Assets/Prefabs/Survival/Gameplay/Survival Interface.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponentInChildren<SurvivalPuzzleUI>(true);var fields=new SerializedObject(view);
                var pieces=((GameObject)fields.FindProperty("pieces").objectReferenceValue).transform;
                var flowRoot=Rect(pieces,"Circuit power overlay"); Stretch(flowRoot);Add<LayoutElement>(flowRoot.gameObject).ignoreLayout=true;
                var flow=Add<SurvivalCircuitFlowView>(flowRoot.gameObject);var flowFields=new SerializedObject(flow);
                var bridges=flowFields.FindProperty("bridges");var pulses=flowFields.FindProperty("pulses");
                //A four-by-four circuit has 24 neighboring pairs and two external terminals.
                bridges.arraySize=pulses.arraySize=26;
                for(int i=0;i<bridges.arraySize;i++)
                {
                    var bridge=Add<Image>(Rect(flowRoot,"Connection "+i).gameObject);bridge.raycastTarget=false;
                    bridges.GetArrayElementAtIndex(i).objectReferenceValue=bridge;
                }
                for(int i=0;i<pulses.arraySize;i++)
                {
                    var pulse=Add<Image>(Rect(flowRoot,"Power pulse "+i).gameObject);pulse.raycastTarget=false;
                    pulse.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                    pulses.GetArrayElementAtIndex(i).objectReferenceValue=pulse;
                }
                flowFields.ApplyModifiedPropertiesWithoutUndo();
                var numberRoot=Rect(pieces,"Number slot guides");Stretch(numberRoot);Add<LayoutElement>(numberRoot.gameObject).ignoreLayout=true;
                var numbers=Add<SurvivalNumberRowView>(numberRoot.gameObject);var numberFields=new SerializedObject(numbers);
                var labels=numberFields.FindProperty("targets");labels.arraySize=6;
                var font=((Text)fields.FindProperty("status").objectReferenceValue).font;
                for(int i=0;i<labels.arraySize;i++)
                {
                    var rect=Rect(numberRoot,"Target slot "+(i+1));rect.sizeDelta=new Vector2(72,18);
                    var label=Add<Text>(rect.gameObject);label.font=font;label.fontSize=10;label.fontStyle=FontStyle.Bold;
                    label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;labels.GetArrayElementAtIndex(i).objectReferenceValue=label;
                }
                numberFields.ApplyModifiedPropertiesWithoutUndo();
                fields.FindProperty("circuitFlow").objectReferenceValue=flow;
                fields.FindProperty("numberRow").objectReferenceValue=numbers;
                fields.ApplyModifiedPropertiesWithoutUndo();
                flowRoot.gameObject.SetActive(false);numberRoot.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();Debug.Log("CIRCUIT AND NUMBER VISUALS AUTHORED: connected bridges, power pulses, slot guides and stable tile animation.");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static T Add<T>(GameObject root)where T:Component
    {var component=root.GetComponent<T>();return component!=null?component:root.AddComponent<T>();}
    private static RectTransform Rect(Transform parent,string name)
    {
        var found=parent.Find(name);var root=found!=null?found.gameObject:new GameObject(name,typeof(RectTransform));
        root.transform.SetParent(parent,false);return (RectTransform)root.transform;
    }
    private static void Stretch(RectTransform rect)
    {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
}
