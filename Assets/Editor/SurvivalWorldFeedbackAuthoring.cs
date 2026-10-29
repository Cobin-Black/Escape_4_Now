//Assigns native particles and sound to the existing outdoor rules; it does not create another event system.
using System;
using Escape4Now.Map;
using Escape4Now.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SurvivalWorldFeedbackAuthoring
{
    private const string Folder = "Assets/Prefabs/Survival/Gameplay/";
    public static void Apply()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SurvivalMode.unity");
            var events = UnityEngine.Object.FindFirstObjectByType<SurvivalEventController>();
            var map = UnityEngine.Object.FindFirstObjectByType<SurvivalMap>();
            var feedback = Add<SurvivalWorldFeedback>(events.gameObject);
            Set(events,"worldFeedback",feedback);
            var lights = Add<SurvivalLightingFeedback>(map.gameObject); Set(lights,"events",events); Set(lights,"map",map);
            var settings = new SerializedObject(feedback); var cues = settings.FindProperty("cues"); cues.arraySize = 5;
            var types = new[]{SurvivalEventType.Mud,SurvivalEventType.Sprinklers,SurvivalEventType.TrackBoost,SurvivalEventType.PoolsideSlip,SurvivalEventType.EnergyDrink};
            var names = new[]{"Mud step feedback","Wet step feedback","Track energy feedback","Pool slip feedback","Drink pickup feedback"};
            var clips = new[]{"Mud Footstep","Water Splash","Track Boost","Water Splash","Drink Pickup"};
            var colors = new[]{new Color(.4f,.28f,.14f,.7f),new Color(.5f,.8f,1,.7f),new Color(.2f,.9f,1,.9f),new Color(.6f,.9f,1,.85f),new Color(.7f,1,.35f,.9f)};
            for (int i = 0; i < types.Length; i++)
            {
                var cue = cues.GetArrayElementAtIndex(i);
                cue.FindPropertyRelative("type").enumValueIndex = (int)types[i];
                cue.FindPropertyRelative("prefab").objectReferenceValue = Burst(names[i],clips[i],colors[i],i >= 2,i == 4);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Edit("Fallen Branch",root =>
            {
                var falling = Add<SurvivalBranchFall>(root);
                var geometry = root.GetComponentInChildren<SurvivalPropVisual>(true);
                Set(falling,"branch",geometry.transform);
                var source = Add<AudioSource>(root); source.playOnAwake = false; source.spatialBlend = 0; source.volume = .45f;
                source.clip = Clip("Branch Impact"); Set(falling,"impact",source);
            });
            Edit("Rain Weather",root =>
            {
                var particles = root.GetComponentInChildren<ParticleSystem>(true);
                var main = particles.main; main.startSize = .045f; main.startColor = new Color(.7f,.85f,.95f,.65f);
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
                var ambient = root.GetComponent<SurvivalAmbientEffect>();
                var fields = new SerializedObject(ambient); fields.FindProperty("particlesPerTile").floatValue = .5f;
                fields.FindProperty("maximumEmission").intValue = 500; fields.ApplyModifiedPropertiesWithoutUndo();
                var source = Add<AudioSource>(root); source.clip = Clip("Rain Loop"); source.loop = true;
                source.playOnAwake = true; source.volume = .18f; source.spatialBlend = 0;
                Set(root.GetComponent<SurvivalAmbientEffect>(),"sound",source);
            });
            Edit("Survival Interface",root =>
            {
                var existing = root.transform.Find("Rain shading");
                var layer = existing != null ? existing.gameObject : new GameObject("Rain shading",typeof(RectTransform),typeof(Canvas),typeof(Image));
                layer.transform.SetParent(root.transform,false); layer.transform.SetAsFirstSibling();
                var rect = (RectTransform)layer.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var canvas = layer.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = -1;
                var image = layer.GetComponent<Image>(); image.color = Color.clear; image.raycastTarget = false;
                var weather = Add<SurvivalWeatherUI>(root);
                Set(weather,"view",root.GetComponentInChildren<SurvivalPuzzleUI>(true)); Set(weather,"rainShade",image);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVAL WORLD FEEDBACK AUTHORED: footsteps, boost/slip/pickup cues, falling branch, rain sound/shade and lamp outages.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static SurvivalWorldBurst Burst(string name,string sound,Color color,bool follow,bool pickup)
    {
        var root = new GameObject(name);
        try
        {
            var burst = root.AddComponent<SurvivalWorldBurst>();
            var particles = new GameObject("Editable particles").AddComponent<ParticleSystem>(); particles.transform.SetParent(root.transform,false);
            particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = false; main.duration = .6f; main.startLifetime = .65f;
            main.startSize = .055f; main.startSpeed = .45f; main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = .05f;
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .16f;
            var emission = particles.emission; emission.rateOverTime = follow ? 12 : 0; emission.SetBursts(new[]{new ParticleSystem.Burst(0,12)});
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat"); renderer.sortingOrder = 800;
            var audio = root.AddComponent<AudioSource>(); audio.clip = Clip(sound); audio.playOnAwake = true; audio.spatialBlend = 0; audio.volume = .3f;
            Set(burst,"particles",particles); Set(burst,"sound",audio);
            var fields = new SerializedObject(burst); fields.FindProperty("followPlayer").boolValue = follow;
            fields.ApplyModifiedPropertiesWithoutUndo();
            if (pickup)
            {
                var sprite = new GameObject("Pickup can").AddComponent<SpriteRenderer>(); sprite.transform.SetParent(root.transform,false);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(Folder+"Energy Drink Can.png")) if (asset is Sprite found) { sprite.sprite = found; break; }
                sprite.sortingOrder = 801;
                Set(burst,"pickupVisual",sprite.transform);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,Folder+name+".prefab"); return prefab.GetComponent<SurvivalWorldBurst>();
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(SurvivalInteractionAuthoring.AudioFolder+"/"+name+".wav");
    private static void Edit(string name,Action<GameObject> edit)
    {
        string path = Folder+name+".prefab"; var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,path); } finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    private static T Add<T>(GameObject owner) where T : Component
    { var component = owner.GetComponent<T>(); return component != null ? component : owner.AddComponent<T>(); }
    private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object reference)
    { var data = new SerializedObject(owner); data.FindProperty(field).objectReferenceValue = reference; data.ApplyModifiedPropertiesWithoutUndo(); }
}
