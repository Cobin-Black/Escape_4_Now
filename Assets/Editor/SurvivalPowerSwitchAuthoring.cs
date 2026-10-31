//Saves the distinct horizontal switch panel. Breaker controls are left unchanged.
using System;
using Escape4Now.Puzzles;
using Escape4Now.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SurvivalPowerSwitchAuthoring
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
                var overlay=Rect(pieces,"Power switch indicator panel");overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;
                overlay.offsetMin=overlay.offsetMax=Vector2.zero;Add<LayoutElement>(overlay.gameObject).ignoreLayout=true;
                var board=Add<SurvivalPowerSwitchBoard>(overlay.gameObject);var boardFields=new SerializedObject(board);
                boardFields.FindProperty("view").objectReferenceValue=view;
                var lamps=boardFields.FindProperty("lights");var rings=boardFields.FindProperty("focusRings");
                var labels=boardFields.FindProperty("lightLabels");var links=boardFields.FindProperty("linkedLabels");
                lamps.arraySize=rings.arraySize=labels.arraySize=links.arraySize=6;
                var font=((Text)fields.FindProperty("status").objectReferenceValue).font;
                for(int i=0;i<6;i++)
                {
                    var lamp=Picture(overlay,"Output light "+(i+1),new Vector2(28,28),Vector2.zero,new Color(.3f,1,.6f));
                    lamp.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                    var ring=Add<Outline>(lamp.gameObject);ring.effectColor=Color.cyan;ring.effectDistance=new Vector2(3,-3);ring.enabled=false;
                    lamps.GetArrayElementAtIndex(i).objectReferenceValue=lamp;rings.GetArrayElementAtIndex(i).objectReferenceValue=ring;
                    labels.GetArrayElementAtIndex(i).objectReferenceValue=Caption(overlay,"Light caption "+i,font,new Vector2(88,18));
                    links.GetArrayElementAtIndex(i).objectReferenceValue=Caption(overlay,"Linked lights "+i,font,new Vector2(100,18));
                }
                boardFields.ApplyModifiedPropertiesWithoutUndo();
                var cells=fields.FindProperty("cells");
                for(int i=0;i<cells.arraySize;i++)
                {
                    var token=(PuzzleTokenUI)cells.GetArrayElementAtIndex(i).objectReferenceValue;
                    var tokenFields=new SerializedObject(token);var motion=new SerializedObject(token.Motion);
                    var art=token.Motion.Artwork;var handle=Add<SurvivalPowerSwitchHandle>(token.gameObject);
                    var housing=Rect(art,"Horizontal power switch");housing.anchorMin=Vector2.zero;housing.anchorMax=Vector2.one;
                    housing.offsetMin=housing.offsetMax=Vector2.zero;
                    var track=Picture(housing,"Slide track",new Vector2(62,24),new Vector2(0,-10),new Color(.15f,.24f,.29f));
                    track.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    var knob=Picture(track.transform,"Sliding handle",new Vector2(22,20),new Vector2(-18,0),new Color(.8f,.9f,.94f));
                    knob.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                    Picture(knob.transform,"Grip",new Vector2(3,10),Vector2.zero,new Color(.3f,.45f,.5f));
                    var data=new SerializedObject(handle);
                    data.FindProperty("housing").objectReferenceValue=housing.gameObject;
                    data.FindProperty("knob").objectReferenceValue=knob.rectTransform;data.FindProperty("track").objectReferenceValue=track;
                    data.FindProperty("caption").objectReferenceValue=tokenFields.FindProperty("label").objectReferenceValue;
                    data.FindProperty("oldHandle").objectReferenceValue=((RectTransform)motion.FindProperty("lever").objectReferenceValue).parent.gameObject;
                    data.FindProperty("oldLamp").objectReferenceValue=((Image)motion.FindProperty("lamp").objectReferenceValue).gameObject;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    tokenFields.FindProperty("powerSwitch").objectReferenceValue=handle;tokenFields.ApplyModifiedPropertiesWithoutUndo();
                    housing.gameObject.SetActive(false);
                }
                fields.FindProperty("powerSwitchBoard").objectReferenceValue=board;fields.ApplyModifiedPropertiesWithoutUndo();
                overlay.gameObject.SetActive(false);PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var definition=AssetDatabase.LoadAssetAtPath<SurvivalPuzzleDefinition>("Assets/Settings/Survival/PowerSwitches.asset");
            definition.instruction="Slide or click a switch to flip its two linked lights. The labels show which lights it affects. Turn every light green.";
            EditorUtility.SetDirty(definition);AssetDatabase.SaveAssets();
            Debug.Log("POWER SWITCH PANEL AUTHORED: horizontal sliders, separate linked output lights and hover preview; breaker preserved.");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static RectTransform Rect(Transform parent,string name)
    {var found=parent.Find(name);var root=found!=null?found.gameObject:new GameObject(name,typeof(RectTransform));root.transform.SetParent(parent,false);return (RectTransform)root.transform;}
    private static Image Picture(Transform parent,string name,Vector2 size,Vector2 position,Color color)
    {
        var rect=Rect(parent,name);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
        var image=Add<Image>(rect.gameObject);image.color=color;image.raycastTarget=false;return image;
    }
    private static Text Caption(Transform parent,string name,Font font,Vector2 size)
    {var rect=Rect(parent,name);rect.sizeDelta=size;var text=Add<Text>(rect.gameObject);text.font=font;text.fontSize=10;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(.6f,.8f,.86f);text.raycastTarget=false;return text;}
    private static T Add<T>(GameObject root)where T:Component
    {var component=root.GetComponent<T>();return component!=null?component:root.AddComponent<T>();}
}
