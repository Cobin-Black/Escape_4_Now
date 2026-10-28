// Editor tool that saves the outdoor effects and task UI into prefabs; it does not run during matches.

using System;
using System.Linq;
using Escape4Now.Map;
using Escape4Now.Puzzles;
using Escape4Now.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

//One-time prefab authoring. All resulting geometry, particles and references remain editable in Unity.
public static class SurvivalPresentationAuthoring
{
    private const string Folder = "Assets/Prefabs/Survival/Gameplay/";
    public static void Apply()
    {
        try
        {
            Edit("Sprinkler Water", root =>
            {
                var spray = root.GetComponentInChildren<ParticleSystem>();
                ConfigureParticles(spray, 26, .7f, .035f, new Color(.55f, .85f, 1f, .7f));
                var main = spray.main; main.startSpeed = .85f; main.gravityModifier = .15f;
                var shape = spray.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 65; shape.radius = .035f;
                spray.transform.localPosition = new Vector3(0, .08f, 0);
                spray.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var motion = Component<SurvivalEffectVisual>(root);
                SetObject(motion, "rotatingSpray", spray.transform);
                var geometry = new SerializedObject(root.GetComponentInChildren<SurvivalPropVisual>());
                var waterMaterial = (Material)geometry.FindProperty("material").objectReferenceValue;
                for (int side = -1; side <= 1; side += 2)
                {
                    var arc = root.transform.Find("Water arc " + side);
                    var lineObject = arc != null ? arc.gameObject : new GameObject("Water arc " + side);
                    lineObject.transform.SetParent(root.transform, false);
                    var line = Component<LineRenderer>(lineObject); line.useWorldSpace = false;
                    line.sharedMaterial = waterMaterial; line.startWidth = line.endWidth = .012f;
                    line.startColor = new Color(.5f,.85f,1,.65f); line.endColor = new Color(.7f,.9f,1,.15f);
                    line.sortingOrder = 505; line.positionCount = 12;
                    for (int i = 0; i < line.positionCount; i++)
                    {
                        float t = i / (float)(line.positionCount - 1);
                        line.SetPosition(i, new Vector3(side * t * .36f, .07f + .65f * t * (1-t), -.01f));
                    }
                }
            });
            Edit("Rain Weather", root =>
            {
                var rain = root.GetComponentInChildren<ParticleSystem>();
                ConfigureParticles(rain, 260, .65f, .025f, new Color(.7f, .85f, .95f, .45f));
                var main = rain.main; main.startSpeed = 0;
                var velocity = rain.velocityOverLifetime; velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World; velocity.x = -.6f; velocity.y = -4f; velocity.z = 0;
                var renderer = rain.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3; renderer.velocityScale = .05f;
            });
            Edit("Car Alarm", root =>
            {
                var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                var lights = new[] { Sprite(root, "Left alarm light", square, new Vector3(-.23f, .13f, 0), new Vector3(.14f, .07f, 1), new Color(1, .55f, .1f)),
                    Sprite(root, "Right alarm light", square, new Vector3(.23f, .13f, 0), new Vector3(.14f, .07f, 1), new Color(1, .55f, .1f)) };
                var motion = Component<SurvivalEffectVisual>(root);
                var fields = new SerializedObject(motion); var refs = fields.FindProperty("highlights"); refs.arraySize = lights.Length;
                for (int i = 0; i < lights.Length; i++)
                {
                    lights[i].transform.localScale = new Vector3(.16f / square.bounds.size.x, .07f / square.bounds.size.y, 1);
                    refs.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                }
                fields.FindProperty("pulseSeconds").floatValue = 1.5f; fields.ApplyModifiedPropertiesWithoutUndo();
            });
            Edit("Fallen Branch", root =>
            {
                var material = root.GetComponentInChildren<SurvivalPropVisual>();
                var fields = new SerializedObject(material);
                var parts = fields.FindProperty("parts");
                //A forked trunk, smaller twigs and foliage; no additional colliders.
                var strokes = fields.FindProperty("strokes"); strokes.arraySize = 4;
                Stroke(strokes.GetArrayElementAtIndex(0), new[] { new Vector2(-.42f,-.14f), new Vector2(.04f,.08f), new Vector2(.43f,.13f) }, .09f, new Color(.3f,.16f,.07f));
                Stroke(strokes.GetArrayElementAtIndex(1), new[] { new Vector2(-.04f,.04f), new Vector2(.05f,.28f), new Vector2(.25f,.38f) }, .045f, new Color(.37f,.22f,.1f));
                Stroke(strokes.GetArrayElementAtIndex(2), new[] { new Vector2(.13f,.1f), new Vector2(.38f,-.08f) }, .04f, new Color(.37f,.22f,.1f));
                Stroke(strokes.GetArrayElementAtIndex(3), new[] { new Vector2(-.23f,-.04f), new Vector2(-.35f,.18f) }, .035f, new Color(.37f,.22f,.1f));
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            foreach (string name in new[] { "Mud Patch", "Wet Pool Deck" })
                Edit(name, root =>
                {
                    var source = root.GetComponentInChildren<SpriteRenderer>();
                    var glint = Sprite(root, "Surface highlight", source.sprite, new Vector3(.08f,.015f,-.01f),
                        source.transform.localScale * .45f, name == "Mud Patch" ? new Color(.55f,.38f,.2f,.22f) : new Color(.7f,.95f,1,.35f));
                    glint.sortingOrder = source.sortingOrder + 1;
                    var motion = Component<SurvivalEffectVisual>(root);
                    var fields = new SerializedObject(motion); var refs = fields.FindProperty("highlights"); refs.arraySize = 1;
                    refs.GetArrayElementAtIndex(0).objectReferenceValue = glint;
                    fields.FindProperty("pulseSeconds").floatValue = 4;
                    fields.FindProperty("minimumOpacity").floatValue = .12f;
                    fields.FindProperty("maximumOpacity").floatValue = .3f;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                });
            Edit("Track Boost Marking", root =>
            {
                var fields = new SerializedObject(root.GetComponentInChildren<SurvivalPropVisual>());
                var strokes = fields.FindProperty("strokes"); int start = 0; strokes.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    float x = -.22f + i * .2f;
                    Stroke(strokes.GetArrayElementAtIndex(start+i), new[] { new Vector2(x-.08f,-.13f), new Vector2(x+.05f,0), new Vector2(x-.08f,.13f) }, .035f, new Color(.95f,.95f,.65f));
                }
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { Folder.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(" Station.prefab") && !path.EndsWith("Puzzle Station.prefab")))
                Edit(System.IO.Path.GetFileNameWithoutExtension(path), root =>
                {
                    var indicator = root.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(item => item.name == "Tile Indicator");
                    if (indicator == null) throw new InvalidOperationException("Station has no authored indicator: " + path);
                    var ring = Sprite(root, "Nearby interaction ring", indicator.sprite, indicator.transform.localPosition,
                        indicator.transform.localScale * 1.25f, Color.cyan);
                    ring.sortingOrder = indicator.sortingOrder + 1; ring.enabled = false;
                    var beacon = Component<SurvivalTaskBeacon>(root);
                    SetObject(beacon, "ring", ring);
                });
            Edit("Survival Interface", root =>
            {
                var ui = root.GetComponentInChildren<SurvivalPuzzleUI>(true);
                var fields = new SerializedObject(ui);
                fields.FindProperty("interactFormat").stringValue = "{0}  [{1}]\nInteract ends your turn";
                var interact = (Button)fields.FindProperty("interact").objectReferenceValue;
                var label = (Text)fields.FindProperty("interactLabel").objectReferenceValue;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 10; label.resizeTextMaxSize = 15;
                ((RectTransform)interact.transform).sizeDelta = new Vector2(280, 56);
                var colors = interact.colors; colors.normalColor = new Color(.08f,.4f,.5f); colors.highlightedColor = new Color(.12f,.6f,.7f); interact.colors = colors;
                var title = (Text)fields.FindProperty("title").objectReferenceValue;
                title.color = new Color(.8f,.95f,1); title.fontStyle = FontStyle.Bold;
                var status = (Text)fields.FindProperty("status").objectReferenceValue;
                status.color = new Color(.55f,.92f,.85f);
                fields.ApplyModifiedPropertiesWithoutUndo();
            });
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVAL PRESENTATION ASSETS AUTHORED: native particles, local environmental detail, nine nearby task beacons and binding-aware prompt.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Edit(string name, Action<GameObject> edit)
    {
        string path = Folder + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static void SetObject(UnityEngine.Object owner, string field, UnityEngine.Object reference)
    { var fields = new SerializedObject(owner); fields.FindProperty(field).objectReferenceValue = reference; fields.ApplyModifiedPropertiesWithoutUndo(); }
    private static SpriteRenderer Sprite(GameObject root, string name, Sprite sprite, Vector3 position, Vector3 scale, Color color)
    {
        var existing = root.transform.Find(name);
        var child = existing != null ? existing.gameObject : new GameObject(name);
        child.transform.SetParent(root.transform, false); child.transform.localPosition = position; child.transform.localScale = scale;
        var renderer = Component<SpriteRenderer>(child); renderer.sprite = sprite;
        renderer.color = color; renderer.sortingOrder = 510; return renderer;
    }
    private static void Stroke(SerializedProperty stroke, Vector2[] points, float width, Color color)
    {
        var positions = stroke.FindPropertyRelative("points"); positions.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++) positions.GetArrayElementAtIndex(i).vector2Value = points[i];
        stroke.FindPropertyRelative("height").floatValue = .06f; stroke.FindPropertyRelative("width").floatValue = width;
        stroke.FindPropertyRelative("color").colorValue = color; stroke.FindPropertyRelative("loop").boolValue = false;
    }
    private static T Component<T>(GameObject owner) where T : Component
    { var component = owner.GetComponent<T>(); return component != null ? component : owner.AddComponent<T>(); }
    private static void ConfigureParticles(ParticleSystem system, float rate, float lifetime, float size, Color color)
    {
        var main = system.main; main.startLifetime = lifetime; main.startSize = size; main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 1500;
        var emission = system.emission; emission.rateOverTime = rate;
        var fade = system.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
            new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.15f), new GradientAlphaKey(0,1) });
        fade.color = gradient;
    }
}
