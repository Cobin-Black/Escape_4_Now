//Saves editable interaction controls and sound clips into the existing Survival UI prefab.
using System;
using System.IO;
using System.Linq;
using Escape4Now.Puzzles;
using Escape4Now.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SurvivalInteractionAuthoring
{
    public const string AudioFolder = "Assets/Audio/SurvivalInteraction";
    public static void Apply()
    {
        try
        {
            Directory.CreateDirectory(AudioFolder);
            SurvivalMatchingIconAuthoring.Create();
            var hover = Sound("Hover",.06f,0);
            var press = Sound("Press",.1f,1);
            var connect = Sound("Connect",.22f,2);
            var success = Sound("Complete",.45f,3);
            var failure = Sound("Incorrect",.28f,4);
            Sound("Mud Footstep",.16f,5); Sound("Water Splash",.3f,6);
            Sound("Branch Impact",.5f,7); Sound("Track Boost",.4f,8); Sound("Drink Pickup",.4f,3);
            Sound("Rain Loop",4f,9);
            const string path = "Assets/Prefabs/Survival/Gameplay/Survival Interface.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponentInChildren<SurvivalPuzzleUI>(true);
                var fields = new SerializedObject(view);
                var panel = (GameObject)fields.FindProperty("panel").objectReferenceValue;
                var title = (Text)fields.FindProperty("title").objectReferenceValue;
                var card = (RectTransform)title.transform.parent;
                var font = title.font;
                var feedback = Add<SurvivalPuzzleFeedback>(panel);
                var sound = Add<AudioSource>(panel); sound.playOnAwake = false; sound.spatialBlend = 0;
                Set(feedback,"sound",sound);
                Set(feedback,"hoverSound",hover); Set(feedback,"pressSound",press); Set(feedback,"connectionSound",connect);
                Set(feedback,"successSound",success); Set(feedback,"failureSound",failure);
                var timer = Text(card,"Task countdown",font,14,new Vector2(1,1),new Vector2(-24,-28),new Vector2(130,22));
                timer.alignment = TextAnchor.MiddleRight; timer.rectTransform.pivot = new Vector2(1,1);
                var timerBack = Image(card,"Task countdown track",new Color(.12f,.2f,.25f),new Vector2(1,1),new Vector2(-24,-54),new Vector2(120,5));
                timerBack.rectTransform.pivot = new Vector2(1,1);
                var fill = Image(timerBack.transform,"Time remaining",Color.cyan,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(fill.rectTransform);
                fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                var lamp = Image(card,"Task state lamp",Color.cyan,new Vector2(1,1),new Vector2(-164,-30),new Vector2(10,10));
                lamp.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                var result = Text(card,"Attempt result",font,13,Vector2.zero,new Vector2(24,20),new Vector2(340,30));
                result.rectTransform.pivot = Vector2.zero; result.gameObject.SetActive(false);
                var glow = Image(card,"Completion light",Color.clear,Vector2.zero,Vector2.zero,Vector2.zero);
                Stretch(glow.rectTransform); glow.transform.SetAsFirstSibling();
                Set(feedback,"timeLabel",timer); Set(feedback,"timeFill",fill); Set(feedback,"stateLamp",lamp);
                Set(feedback,"resultLabel",result); Set(feedback,"completionGlow",glow);
                var titleRect = title.rectTransform;
                titleRect.anchorMin = new Vector2(0,1); titleRect.anchorMax = Vector2.one;
                titleRect.pivot = new Vector2(0,1); titleRect.offsetMin = new Vector2(24,-60); titleRect.offsetMax = new Vector2(-170,-24);
                title.resizeTextForBestFit = true; title.resizeTextMinSize = 16; title.resizeTextMaxSize = 25;
                var pairs = (GameObject)fields.FindProperty("pairs").objectReferenceValue;
                var matching = Add<SurvivalMatchingUI>(pairs);
                var layer = Rect(pairs.transform,"Wire layer"); Stretch(layer); layer.SetAsFirstSibling();
                var preview = Add<PuzzleWireGraphic>(Rect(layer,"Loose wire").gameObject); Add<CanvasRenderer>(preview.gameObject); Stretch(preview.rectTransform); preview.raycastTarget = false;
                var tip = Image(layer,"Wire tip",Color.white,new Vector2(.5f,.5f),Vector2.zero,new Vector2(10,10));
                tip.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                Set(matching,"preview",preview); Set(matching,"pointerTip",tip);
                var lines = fields.FindProperty("connections");
                for (int i = 0; i < lines.arraySize; i++)
                {
                    var line = (RectTransform)lines.GetArrayElementAtIndex(i).objectReferenceValue;
                    line.SetParent(layer,false); Stretch(line); line.gameObject.SetActive(true);
                    var oldImage = line.GetComponent<Image>(); if (oldImage != null) oldImage.enabled = false;
                    var cable = Add<PuzzleWireGraphic>(Rect(line,"Cable").gameObject); Add<CanvasRenderer>(cable.gameObject); Stretch(cable.rectTransform); cable.raycastTarget = false;
                    cable.gameObject.SetActive(false);
                }
                preview.transform.SetAsLastSibling(); tip.transform.SetAsLastSibling();
                preview.gameObject.SetActive(false); tip.gameObject.SetActive(false);
                foreach (string list in new[]{"left","right","cells"})
                {
                    var tokens = fields.FindProperty(list);
                    for (int i = 0; i < tokens.arraySize; i++)
                        PrepareToken((PuzzleTokenUI)tokens.GetArrayElementAtIndex(i).objectReferenceValue,list == "right" ? 1 : list == "left" ? 0 : 2);
                }
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var motion = button.GetComponent<PuzzlePieceMotion>();
                    if (motion == null) { motion = Add<PuzzlePieceMotion>(button.gameObject); Set(motion,"artwork",button.transform); }
                    var response = Add<PuzzleButtonFeedback>(button.gameObject);
                    Set(response,"feedback",feedback); Set(response,"motion",motion);
                }
                fields.FindProperty("matching").objectReferenceValue = matching;
                fields.FindProperty("feedback").objectReferenceValue = feedback;
                fields.FindProperty("wireThickness").floatValue = 5;
                fields.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (string guid in AssetDatabase.FindAssets("t:SurvivalPuzzleDefinition",new[]{"Assets/Settings/Survival"}))
            {
                var puzzle = AssetDatabase.LoadAssetAtPath<SurvivalPuzzleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (puzzle.timeLimitSeconds <= 0) puzzle.timeLimitSeconds = puzzle.type == SurvivalPuzzleType.Circuit ? 20 : 15;
                if (puzzle.type == SurvivalPuzzleType.MatchWires)
                    puzzle.instruction = "Drag a wire between matching sockets, or click both ends in either order. Match color and symbol. Drop on empty space to cancel.";
                if (puzzle.type == SurvivalPuzzleType.SortSupplies || puzzle.type == SurvivalPuzzleType.Batteries)
                {
                    puzzle.matchingIconPrefabs = SurvivalMatchingIconAuthoring.Load(puzzle.type);
                    puzzle.instruction = puzzle.type == SurvivalPuzzleType.SortSupplies
                        ? "Drag each supply into the matching container on the right. Match the picture, name and color. You can also select an item, then its container."
                        : "Drag each battery into its matching device slot. Match color and symbol. You can also select a battery, then its slot.";
                }
                EditorUtility.SetDirty(puzzle);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVAL INTERACTION ASSETS AUTHORED: live cables, physical pieces, sounds, indicators and task deadlines.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void PrepareToken(PuzzleTokenUI token,int side)
    {
        var fields = new SerializedObject(token);
        var originalFace = (Image)fields.FindProperty("face").objectReferenceValue;
        var artwork = Rect(token.transform,"Physical piece"); Stretch(artwork);
        var face = Add<Image>(artwork.gameObject); face.sprite = originalFace.sprite; face.type = originalFace.type;
        face.color = originalFace.color; face.raycastTarget = false;
        var rootImage = token.GetComponent<Image>(); if (rootImage != null) { rootImage.color = Color.clear; rootImage.raycastTarget = true; }
        var label = (Text)fields.FindProperty("label").objectReferenceValue; label.transform.SetParent(artwork,false);
        var mechanism = Rect(artwork,"Rotating circuit"); Stretch(mechanism);
        var ports = fields.FindProperty("circuitPorts");
        for (int i = 0; i < ports.arraySize; i++) ((Image)ports.GetArrayElementAtIndex(i).objectReferenceValue).transform.SetParent(mechanism,false);
        var leverBack = Image(artwork,"Switch track",new Color(.04f,.08f,.1f),new Vector2(.5f,.5f),new Vector2(-32,0),new Vector2(16,36));
        var lever = Image(leverBack.transform,"Switch handle",new Color(.65f,.73f,.77f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(12,14));
        var lamp = Image(artwork,"Indicator lamp",new Color(.2f,.3f,.3f),new Vector2(1,1),new Vector2(-12,-12),new Vector2(8,8));
        lamp.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var battery = Image(artwork,"Battery body",new Color(.06f,.1f,.13f),new Vector2(.5f,.5f),new Vector2(-30,0),new Vector2(20,32));
        Image(battery.transform,"Battery cap",Color.white,new Vector2(.5f,1),new Vector2(0,2),new Vector2(8,4));
        Image(battery.transform,"Battery charge",new Color(.8f,.9f,.55f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(12,23));
        var supply = Image(artwork,"Supply crate",new Color(.7f,.53f,.29f),new Vector2(.5f,.5f),new Vector2(-30,0),new Vector2(25,24));
        Image(supply.transform,"Crate strap",new Color(.32f,.25f,.16f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(5,24));
        var motion = Add<PuzzlePieceMotion>(token.gameObject);
        Set(motion,"artwork",artwork); Set(motion,"mechanism",mechanism); Set(motion,"lever",lever.rectTransform);
        Set(motion,"lamp",lamp); Set(motion,"artworkGroup",Add<CanvasGroup>(artwork.gameObject));
        Set(motion,"batteryShape",battery.gameObject); Set(motion,"supplyShape",supply.gameObject);
        //Hide the switch's full track, not only its handle, when showing another puzzle type.
        leverBack.gameObject.SetActive(false);
        var outline = Add<Outline>(artwork.gameObject); outline.effectDistance = new Vector2(2,-2); outline.enabled = false;
        if (side < 2)
        {
            var socket = Image(artwork,"Wire socket",Color.white,new Vector2(side == 0 ? 1 : 0,.5f),new Vector2(side == 0 ? -6 : 6,0),new Vector2(12,12));
            socket.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            fields.FindProperty("connector").objectReferenceValue = socket;
        }
        fields.FindProperty("face").objectReferenceValue = face;
        fields.FindProperty("motion").objectReferenceValue = motion;
        var presentation = Add<PuzzleMatchingPieceView>(token.gameObject);
        var mount = Rect(artwork,"Item icon");
        var frame = Rect(artwork,"Receiving slot frame"); Stretch(frame); frame.offsetMin = new Vector2(3,3); frame.offsetMax = new Vector2(-3,-3);
        var frameColor = new Color(.6f,.85f,.95f,.4f);
        var top = Image(frame,"Top",frameColor,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(top.rectTransform); top.rectTransform.anchorMin = new Vector2(0,1); top.rectTransform.sizeDelta = new Vector2(0,1);
        var bottom = Image(frame,"Bottom",frameColor,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(bottom.rectTransform); bottom.rectTransform.anchorMax = new Vector2(1,0); bottom.rectTransform.sizeDelta = new Vector2(0,1);
        var edgeA = Image(frame,"Left",frameColor,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(edgeA.rectTransform); edgeA.rectTransform.anchorMax = new Vector2(0,1); edgeA.rectTransform.sizeDelta = new Vector2(1,0);
        var edgeB = Image(frame,"Right",frameColor,Vector2.zero,Vector2.zero,Vector2.zero); Stretch(edgeB.rectTransform); edgeB.rectTransform.anchorMin = new Vector2(1,0); edgeB.rectTransform.sizeDelta = new Vector2(1,0);
        Set(presentation,"caption",label.rectTransform); Set(presentation,"label",label); Set(presentation,"face",face);
        Set(presentation,"socket",fields.FindProperty("connector").objectReferenceValue);
        Set(presentation,"iconMount",mount); Set(presentation,"iconOpacity",Add<CanvasGroup>(mount.gameObject)); Set(presentation,"slotFrame",frame.gameObject);
        fields.FindProperty("matchingView").objectReferenceValue = presentation;
        label.transform.SetAsLastSibling();
        fields.FindProperty("interactionOutline").objectReferenceValue = outline;
        fields.ApplyModifiedPropertiesWithoutUndo();
        var button = token.GetComponent<Button>(); button.targetGraphic = face;
        var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
        foreach (var graphic in artwork.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    public static AudioClip Sound(string name,float seconds,int kind)
    {
        string path = AudioFolder+"/"+name+".wav";
        if (!File.Exists(path))
        {
            int rate = 22050, count = (int)(rate*seconds);
            var random = new System.Random(91+kind); float filtered = 0;
            using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36+count*2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate*2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count*2);
                for (int i = 0; i < count; i++)
                {
                    float t = i/(float)rate, fade = kind == 9 ? 1 : Mathf.Pow(1-i/(float)count,2);
                    float noise = (float)random.NextDouble()*2-1;
                    filtered = Mathf.Lerp(filtered,noise,kind == 5 ? .1f : .35f);
                    float frequency = kind == 4 ? 170 : kind == 3 ? 650+Mathf.Floor(t*9)*160 : kind == 8 ? 300+t*1500 : 450+kind*110;
                    float value = kind >= 5 && kind != 8 ? filtered : Mathf.Sin(t*frequency*Mathf.PI*2)*.5f;
                    if (kind == 7) value += Mathf.Sin(t*(95-t*100)*Mathf.PI*2)*.4f;
                    writer.Write((short)(Mathf.Clamp(value*fade,-1,1)*12000));
                }
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    private static T Add<T>(GameObject owner) where T : Component
    { var value = owner.GetComponent<T>(); return value != null ? value : owner.AddComponent<T>(); }
    private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
    { var data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); }
    private static RectTransform Rect(Transform parent,string name)
    {
        var existing = parent.Find(name);
        var result = existing != null ? (RectTransform)existing : (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        result.SetParent(parent,false); return result;
    }
    private static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one; }
    private static Image Image(Transform parent,string name,Color color,Vector2 anchor,Vector2 position,Vector2 size)
    { var rect = Rect(parent,name); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = Add<Image>(rect.gameObject); image.color = color; image.raycastTarget = false; return image; }
    private static Text Text(Transform parent,string name,Font font,int size,Vector2 anchor,Vector2 position,Vector2 dimensions)
    { var rect = Rect(parent,name); rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        var text = Add<Text>(rect.gameObject); text.font = font; text.fontSize = size; text.color = Color.white; text.raycastTarget = false; return text; }
}
