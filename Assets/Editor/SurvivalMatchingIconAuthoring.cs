//Saves small native UI objects. Designers can edit every shape or replace the icon prefabs in Unity.
using Escape4Now.Puzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SurvivalMatchingIconAuthoring
{
    private const string Folder = "Assets/Prefabs/Survival/Puzzle Pieces";
    private static readonly string[] Supplies = { "Ball", "Towel", "Book", "Cone", "Bottle", "Tool" };
    public static void Create()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/Survival","Puzzle Pieces");
        for (int i = 0; i < Supplies.Length; i++)
        {
            Save(Supplies[i],root => Supply(root,i));
            Save("Battery "+(i+1),root => Battery(root,i));
        }
    }
    public static GameObject[] Load(SurvivalPuzzleType type)
    {
        var icons = new GameObject[Supplies.Length];
        for (int i = 0; i < icons.Length; i++) icons[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"
            +(type == SurvivalPuzzleType.Batteries ? "Battery "+(i+1) : Supplies[i])+".prefab");
        return icons;
    }
    private static void Save(string name,System.Action<Transform> build)
    {
        string path = Folder+"/"+name+".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject(name,typeof(RectTransform));
        try { ((RectTransform)root.transform).sizeDelta = new Vector2(30,30); build(root.transform); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { Object.DestroyImmediate(root); }
    }
    private static Image Part(Transform parent,string name,Color color,Vector2 position,Vector2 size,bool round=false)
    {
        var image = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent,false); var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        image.color = color; image.raycastTarget = false;
        if (round) image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        return image;
    }
    private static void Supply(Transform root,int index)
    {
        var dark = new Color(.08f,.12f,.16f); var pale = new Color(.88f,.95f,.94f);
        switch (index)
        {
            case 0:
                Part(root,"Ball",new Color(1,.5f,.13f),Vector2.zero,new Vector2(24,24),true);
                Part(root,"Seam vertical",dark,Vector2.zero,new Vector2(2,23));
                Part(root,"Seam horizontal",dark,Vector2.zero,new Vector2(23,2)); break;
            case 1:
                Part(root,"Folded towel",pale,Vector2.zero,new Vector2(24,18));
                Part(root,"Fold",new Color(.45f,.7f,.8f),new Vector2(0,-6),new Vector2(24,3));
                Part(root,"Stripe",new Color(.15f,.45f,.7f),new Vector2(-7,0),new Vector2(3,18)); break;
            case 2:
                Part(root,"Book cover",new Color(.15f,.35f,.7f),Vector2.zero,new Vector2(23,26));
                Part(root,"Pages",pale,new Vector2(2,0),new Vector2(17,21));
                Part(root,"Spine",new Color(.06f,.15f,.4f),new Vector2(-9,0),new Vector2(4,26)); break;
            case 3:
                Part(root,"Base",dark,new Vector2(0,-10),new Vector2(26,5));
                for(int step=0;step<4;step++) Part(root,"Cone section "+step,new Color(1,.5f,.1f),new Vector2(0,-6+step*6),new Vector2(20-step*5,6));
                Part(root,"Reflector",pale,new Vector2(0,0),new Vector2(14,3)); break;
            case 4:
                Part(root,"Bottle",new Color(.2f,.75f,.7f),new Vector2(0,-2),new Vector2(16,21));
                Part(root,"Neck",new Color(.2f,.75f,.7f),new Vector2(0,10),new Vector2(10,6));
                Part(root,"Cap",dark,new Vector2(0,14),new Vector2(11,3));
                Part(root,"Label",pale,new Vector2(0,-1),new Vector2(16,7)); break;
            default:
                Part(root,"Handle",new Color(.7f,.8f,.85f),new Vector2(0,-5),new Vector2(7,22));
                Part(root,"Jaw",new Color(.7f,.8f,.85f),new Vector2(0,7),new Vector2(18,17),true);
                Part(root,"Jaw opening",dark,new Vector2(0,12),new Vector2(8,14)); break;
        }
    }
    private static void Battery(Transform root,int index)
    {
        var colors = new[]{new Color(.9f,.25f,.3f),new Color(.15f,.65f,.95f),new Color(.3f,.75f,.4f),new Color(.95f,.7f,.15f),new Color(.7f,.4f,.85f),new Color(.1f,.8f,.8f)};
        Part(root,"Casing",new Color(.05f,.09f,.12f),Vector2.zero,new Vector2(18,26));
        Part(root,"Charge color",colors[index],Vector2.zero,new Vector2(13,19));
        Part(root,"Contact",Color.white,new Vector2(0,14),new Vector2(8,3));
        Part(root,"Plus horizontal",Color.white,new Vector2(0,5),new Vector2(7,2));
        Part(root,"Plus vertical",Color.white,new Vector2(0,5),new Vector2(2,7));
    }
}
