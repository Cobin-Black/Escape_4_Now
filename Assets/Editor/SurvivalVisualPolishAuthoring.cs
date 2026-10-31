//Saves the final board layout, quieter alarm and sprinkler artwork into editable Unity prefabs.
using System;
using Escape4Now.Map;
using Escape4Now.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SurvivalVisualPolishAuthoring
{
    private const string Folder="Assets/Prefabs/Survival/Gameplay/";
    public static void Apply()
    {
        try
        {
            Edit("Car Alarm",root=>
            {
                foreach(var sound in root.GetComponentsInChildren<AudioSource>(true)) { sound.volume=.055f; sound.priority=180; }
                var motion=new SerializedObject(root.GetComponent<SurvivalEffectVisual>());
                motion.FindProperty("minimumOpacity").floatValue=.08f; motion.FindProperty("maximumOpacity").floatValue=.7f;
                motion.ApplyModifiedPropertiesWithoutUndo();
            });
            Edit("Sprinkler Water",root=>
            {
                foreach(var legacy in root.GetComponentsInChildren<ParticleSystem>(true)) legacy.gameObject.SetActive(false);
                foreach(string name in new[]{"Water arc -1","Water arc 1"})
                { var old=root.transform.Find(name); if(old!=null) old.gameObject.SetActive(false); }
                var oldMotion=new SerializedObject(root.GetComponent<SurvivalEffectVisual>());
                oldMotion.FindProperty("rotatingSpray").objectReferenceValue=null; oldMotion.ApplyModifiedPropertiesWithoutUndo();
                var spray=Add<SurvivalSprinklerSpray>(root); var fields=new SerializedObject(spray);
                var streams=fields.FindProperty("streams"); streams.arraySize=9;
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
                for(int i=0;i<streams.arraySize;i++)
                {
                    var found=root.transform.Find("Water stream "+i);
                    var jet=found!=null?found.gameObject:new GameObject("Water stream "+i);
                    jet.transform.SetParent(root.transform,false);
                    var line=Add<LineRenderer>(jet); line.useWorldSpace=false; line.sharedMaterial=material;
                    line.positionCount=12; line.startWidth=.028f; line.endWidth=.016f;
                    line.startColor=new Color(.55f,.85f,1,.75f); line.endColor=new Color(.7f,.95f,1,.25f);
                    line.numCapVertices=3; line.sortingOrder=1200;
                    streams.GetArrayElementAtIndex(i).objectReferenceValue=line;
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
                var source=Add<AudioSource>(root); source.playOnAwake=true; source.loop=true; source.spatialBlend=0; source.volume=.035f;
                source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SurvivalInteraction/Rain Loop.wav");
                foreach(var legacy in root.GetComponentsInChildren<SurvivalAmbientEffect>(true))
                    if(legacy.gameObject!=root) legacy.enabled=false;
                var ambient=new SerializedObject(Add<SurvivalAmbientEffect>(root));
                ambient.FindProperty("particles").objectReferenceValue=null; ambient.FindProperty("sound").objectReferenceValue=source;
                ambient.ApplyModifiedPropertiesWithoutUndo();
                var surface=new SerializedObject(root.GetComponent<SurvivalEnvironmentalSurface>());
                surface.FindProperty("groundColor").colorValue=new Color(.17f,.35f,.43f,.28f);
                surface.FindProperty("variationColor").colorValue=new Color(.3f,.53f,.6f,.3f);
                surface.FindProperty("puddleColor").colorValue=new Color(.55f,.8f,.86f,.45f);
                surface.FindProperty("edgeRoundness").floatValue=.42f; surface.ApplyModifiedPropertiesWithoutUndo();
                Nozzle(root.transform,"Sprinkler base",new Color(.08f,.13f,.15f),new Vector3(0,.035f,-.05f),new Vector2(.16f,.08f));
                Nozzle(root.transform,"Sprinkler cap",new Color(.5f,.65f,.69f),new Vector3(0,.075f,-.05f),new Vector2(.09f,.045f));
            });
            Edit("Survival Interface",root=>
            {
                var view=root.GetComponentInChildren<SurvivalPuzzleUI>(true); var data=new SerializedObject(view);
                var board=Add<SurvivalPuzzleBoardView>(view.gameObject);
                var pairs=((GameObject)data.FindProperty("pairs").objectReferenceValue).GetComponent<RectTransform>();
                var title=(Text)data.FindProperty("title").objectReferenceValue;
                var boardFields=new SerializedObject(board);
                boardFields.FindProperty("matchingArea").objectReferenceValue=pairs;
                boardFields.FindProperty("leftHeading").objectReferenceValue=Heading(pairs,"Source heading",title.font,.2f);
                boardFields.FindProperty("rightHeading").objectReferenceValue=Heading(pairs,"Target heading",title.font,.8f);
                boardFields.ApplyModifiedPropertiesWithoutUndo();
                data.FindProperty("board").objectReferenceValue=board; data.ApplyModifiedPropertiesWithoutUndo();
                foreach(string name in new[]{"pairs","pieces","codePanel"})
                {
                    var area=((GameObject)data.FindProperty(name).objectReferenceValue).GetComponent<RectTransform>();
                    var panel=Add<Image>(area.gameObject); panel.color=new Color(.045f,.08f,.105f,.95f); panel.raycastTarget=false;
                    var shadow=Add<Shadow>(area.gameObject); shadow.effectColor=new Color(0,0,0,.3f); shadow.effectDistance=new Vector2(0,-4);
                    for(int corner=0;corner<4;corner++)
                    {
                        var anchor=new Vector2(corner%2,corner/2);
                        var bolt=Image(area,"Housing screw "+corner,new Color(.28f,.38f,.42f),anchor,new Vector2(anchor.x==0?7:-7,anchor.y==0?7:-7),new Vector2(4,4));
                        bolt.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                        Add<LayoutElement>(bolt.gameObject).ignoreLayout=true;
                    }
                }
                foreach(var token in root.GetComponentsInChildren<PuzzleTokenUI>(true))
                {
                    var tokenFields=new SerializedObject(token); var face=(Image)tokenFields.FindProperty("face").objectReferenceValue;
                    var shadow=Add<Shadow>(face.gameObject); shadow.effectColor=new Color(0,0,0,.4f); shadow.effectDistance=new Vector2(0,-3);
                }
                var instruction=(Text)data.FindProperty("instruction").objectReferenceValue;
                instruction.color=new Color(.79f,.87f,.9f); instruction.fontSize=14;
            });
            AssetDatabase.SaveAssets(); Debug.Log("SURVIVAL VISUAL POLISH AUTHORED: quieter alarm, visible flowing sprinklers, readable task boards and controls.");
            EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    private static Text Heading(RectTransform parent,string name,Font font,float x)
    {
        var found=parent.Find(name); var item=found!=null?found.gameObject:new GameObject(name,typeof(RectTransform));
        item.transform.SetParent(parent,false); var rect=(RectTransform)item.transform;
        rect.anchorMin=rect.anchorMax=new Vector2(x,1); rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=new Vector2(0,-10); rect.sizeDelta=new Vector2(180,18);
        var text=Add<Text>(item); text.font=font; text.fontSize=11; text.fontStyle=FontStyle.Bold;
        text.alignment=TextAnchor.MiddleCenter; text.color=new Color(.55f,.78f,.84f); text.raycastTarget=false;
        return text;
    }
    private static Image Image(Transform parent,string name,Color color,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var found=parent.Find(name); var item=found!=null?found.gameObject:new GameObject(name,typeof(RectTransform));
        item.transform.SetParent(parent,false); var rect=(RectTransform)item.transform;
        rect.anchorMin=rect.anchorMax=anchor; rect.pivot=new Vector2(.5f,.5f); rect.anchoredPosition=position; rect.sizeDelta=size;
        var image=Add<Image>(item); image.color=color; image.raycastTarget=false; return image;
    }
    private static void Nozzle(Transform parent,string name,Color tint,Vector3 position,Vector2 size)
    {
        var found=parent.Find(name); var item=found!=null?found.gameObject:new GameObject(name);
        item.transform.SetParent(parent,false); item.transform.localPosition=position;
        var image=Add<SpriteRenderer>(item); image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        image.color=tint; image.sortingOrder=1210; item.transform.localScale=new Vector3(size.x/image.sprite.bounds.size.x,size.y/image.sprite.bounds.size.y,1);
    }
    private static T Add<T>(GameObject owner)where T:Component
    { var component=owner.GetComponent<T>(); return component!=null?component:owner.AddComponent<T>(); }
    private static void Edit(string name,Action<GameObject> edit)
    {
        var path=Folder+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
