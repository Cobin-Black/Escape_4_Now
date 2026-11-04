using System;
using Escape4Now.Arena;
using Escape4Now.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Save editable particles/audio and wire them to the existing Hot Potato elimination signal.
public static class ArenaExplosionAuthoring
{
    public static void Apply()
    {
        try
        {
            const string materialPath = "Assets/Settings/Arena/Potato Burst.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var shader = Shader.Find("Escape4Now/Arena/SoftParticle");
            if (shader == null)
                throw new Exception("Missing arena soft-particle shader.");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            material.shader = shader;
            material.SetFloat("_Softness", 1.8f);
            EditorUtility.SetDirty(material);
            var root = new GameObject("Potato Explosion");
            SurvivalWorldBurst prefab;
            try
            {
                var cue = root.AddComponent<SurvivalWorldBurst>();
                var spark = Particles(root.transform, "Sparks", material, 24, .55f, .10f, 2.4f, new Color(1, .65f, .1f), 2102);
                var core = Particles(spark.transform, "Warm expanding core", material, 6, .38f, .26f, .45f, new Color(1, .46f, .08f, .85f), 2101);
                var sparkRenderer = spark.GetComponent<ParticleSystemRenderer>();
                sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
                sparkRenderer.lengthScale = 2;
                sparkRenderer.velocityScale = .06f;
                var coreGrowth = core.sizeOverLifetime;
                coreGrowth.enabled = true;
                coreGrowth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .35f, 1, 1.4f));
                var animation = root.AddComponent<ArenaExplosionAnimation>();
                var flashObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
                flashObject.name = "Soft flash";
                flashObject.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(flashObject.GetComponent<Collider>());
                var flash = flashObject.GetComponent<MeshRenderer>();
                flash.sharedMaterial = material;
                flash.sortingOrder = 2103;
                var ring = new GameObject("Round ground shockwave").AddComponent<LineRenderer>();
                ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = new Vector3(0, -.35f, 0);
                ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/FloorTileMaterial.mat");
                ring.sortingOrder = 2099;
                Set(animation, "flash", flash, "shockwave", ring);
                var smoke = Particles(spark.transform, "Soft smoke", material, 9, .95f, .35f, .55f, new Color(.58f, .55f, .52f, .65f), 2100);
                var growth = smoke.sizeOverLifetime;
                growth.enabled = true;
                growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .4f, 1, 1.6f));
                var source = root.AddComponent<AudioSource>();
                source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SurvivalInteraction/Branch Impact.wav");
                source.volume = .08f;
                source.pitch = .7f;
                source.playOnAwake = true;
                source.spatialBlend = 0;
                Set(cue, "particles", spark, "sound", source);
                var data = new SerializedObject(cue);
                data.FindProperty("lifetime").floatValue = 1.35f;
                data.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Arena/Potato Explosion.prefab").GetComponent<SurvivalWorldBurst>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            const string actorPath = "Assets/Prefabs/Arena/Arena Player.prefab";
            var actor = PrefabUtility.LoadPrefabContents(actorPath);
            try
            {
                if (actor.GetComponent<ArenaLocomotionView>() == null)
                    actor.AddComponent<ArenaLocomotionView>();
                var motion = new SerializedObject(actor.GetComponent<ArenaParticipant>());
                motion.FindProperty("acceleration").floatValue = 48;
                motion.FindProperty("inputDeadzone").floatValue = .12f;
                motion.FindProperty("slideContacts").intValue = 3;
                motion.FindProperty("contactSearchSteps").intValue = 7;
                motion.FindProperty("contactSkin").floatValue = .001f;
                motion.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(actor, actorPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }

            var style = AssetDatabase.LoadAssetAtPath<ArenaRoomVisualSettings>("Assets/Settings/Arena/Room Visuals.asset");
            style.gymBackWallHeight = 1.15f;
            style.windowWallInset = new Vector2(0, -.46f);
            style.windowMountHeight = .28f;
            style.scoreboardWallInset = new Vector2(-.46f, 0);
            style.scoreboardMountHeight = .30f;
            style.scoreboardFacing = 3;
            style.showScoreboardText = true;
            EditorUtility.SetDirty(style);
            var theater = AssetDatabase.LoadAssetAtPath<ArenaObstacleSettings>("Assets/Settings/Arena/HotPotato Obstacle Groups.asset");
            foreach (var pattern in theater.patterns)
                if (pattern.label == "Side partition" || pattern.label == "Aisle furniture")
                {
                    pattern.zone = ArenaObstacleZone.Service;
                    pattern.facing = ArenaPropFacing.TowardRoom;
                }

            EditorUtility.SetDirty(theater);
            AssetDatabase.SaveAssets();
            foreach (string stem in new[]
            {
                "FreeForAll",
                "HotPotato"
            }

            )
            {
                EditorSceneManager.OpenScene("Assets/Scenes/" + stem + ".unity");
                var match = Object.FindFirstObjectByType<ArenaMatch>();
                Set(match.Map, "eventController", match.Events);
                if (stem == "HotPotato")
                {
                    var mode = (HotPotatoController)match;
                    var view = mode.GetComponent<PotatoExplosionView>();
                    if (view == null)
                        view = mode.gameObject.AddComponent<PotatoExplosionView>();
                    // Scene opening may unload unreferenced assets; resolve the prefab after opening it.
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Arena/Potato Explosion.prefab").GetComponent<SurvivalWorldBurst>();
                    if (prefab == null)
                        throw new Exception("Explosion prefab has no shared burst component.");
                    Set(view, "mode", mode, "explosionPrefab", prefab);
                    var ui = new SerializedObject(Object.FindFirstObjectByType<ArenaUI>());
                    ui.FindProperty("resultRevealDelay").floatValue = .55f;
                    ui.ApplyModifiedPropertiesWithoutUndo();
                }

                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            AssetDatabase.SaveAssets();
            if (System.Array.IndexOf(Environment.GetCommandLineArgs(), "-checkArenaMotion") >= 0)
                ArenaMotionChecks.Run();
            else
                ArenaFeedbackChecks.Run();
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    private static ParticleSystem Particles(Transform parent, string label, Material material, int count, float lifetime, float size, float speed, Color color, int order)
    {
        var system = new GameObject(label).AddComponent<ParticleSystem>();
        system.transform.SetParent(parent, false);
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = false;
        main.duration = .15f;
        main.startLifetime = lifetime;
        main.startSize = size;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .65f, speed);
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        // A native radial emitter gives every spark its own direction; flatten only depth motion.
        var velocity = system.velocityOverLifetime;
        velocity.enabled = label == "Soft smoke";
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = 0;
        velocity.y = .45f;
        velocity.z = 0;
        var limit = system.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.separateAxes = true;
        limit.space = ParticleSystemSimulationSpace.World;
        limit.limitX = speed * 2;
        limit.limitY = speed * 2;
        limit.limitZ = 0;
        limit.dampen = 1;
        var sheet = system.textureSheetAnimation;
        sheet.enabled = false;
        main.startColor = color;
        main.maxParticles = 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = .04f;
        var emission = system.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, count) });
        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        fade.color = gradient;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        return system;
    }

    private static void Set(Object target, params object[] values)
    {
        var data = new SerializedObject(target);
        for (int i = 0; i < values.Length; i += 2)
            data.FindProperty((string)values[i]).objectReferenceValue = (Object)values[i + 1];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
