// Checks obstacle drawings and saves preview images without starting a normal match.

using System;
using System.IO;
using Escape4Now.Map;
using Escape4Now.Obstacles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

//Checks previews in a separate test editor without saving the original prefabs.
public static class ObstaclePreviewChecks
{
    //Run with -executeMethod ObstaclePreviewChecks.Run in the isolated test project.
    public static void Run()
    {
        const string savedCopy = "Assets/ObstaclePreviewCheckTemporary.prefab";
        try
        {
            Require(!File.Exists(savedCopy), "Temporary test path is unused");
            SchoolFurniture[] templates = Resources.LoadAll<SchoolFurniture>("SchoolObstacles");
            Require(templates.Length == 22, "All obstacle templates are present");
            foreach (SchoolFurniture template in templates)
            {
                string path = AssetDatabase.GetAssetPath(template);
                string original = File.ReadAllText(path);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SchoolFurniture obstacle = root.GetComponent<SchoolFurniture>();
                    var contents = root.GetComponent<ObstacleStorage>().StoredItemPrefab;
                    bool dirtyBefore = root.scene.isDirty;
                    UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
                    Refresh(obstacle);
                    int count = root.GetComponentsInChildren<MeshRenderer>().Length;
                    Require(count > 0, "Preview has visible parts: " + template.name);
                    Require(root.GetComponent<Collider2D>() == null
                        && root.GetComponent<ObstacleStorage>().StoredItemPrefab == contents
                        && UnityEngine.Random.state.Equals(randomBefore), "Preview does not run gameplay");
                    Require(root.scene.isDirty == dirtyBefore, "Preview does not dirty its scene");
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
                        Require((filter.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave
                            && (filter.sharedMesh.hideFlags & HideFlags.DontSave) == HideFlags.DontSave,
                            "Preview geometry is temporary");
                    Refresh(obstacle);
                    Require(root.GetComponentsInChildren<MeshRenderer>().Length == count, "Refresh does not duplicate parts");
                    if (template.Kind == SchoolFurnitureKind.BookCart && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                        Capture(root);
                    PrefabUtility.SaveAsPrefabAsset(root, savedCopy);
                    string saved = File.ReadAllText(savedCopy);
                    Require(!saved.Contains("Obstacle preview") && !saved.Contains("Preview part")
                        && !saved.Contains("MeshRenderer:"), "Saving a prefab excludes its preview");
                    Require(File.ReadAllText(path) == original, "Original prefab stays unchanged");
                    obstacle.enabled = false;
                    Require(root.GetComponentsInChildren<MeshRenderer>().Length == 0, "Disabling releases preview parts");
                    obstacle.enabled = true;
                    Refresh(obstacle);
                    Require(root.GetComponentsInChildren<MeshRenderer>().Length == count, "Reopening restores the preview");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    AssetDatabase.DeleteAsset(savedCopy);
                }
            }
            Debug.Log("OBSTACLE PREVIEW CHECKS PASSED: 22 prefabs, refresh, cleanup, and no saved geometry");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    //Renders the same BookCart preview shown when its prefab is opened in Unity.
    private static void Capture(GameObject root)
    {
        var cameraObject = new GameObject("Preview test camera");
        SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.scene = root.scene;
        camera.orthographic = true;
        camera.orthographicSize = 0.8f;
        camera.transform.position = root.transform.position + new Vector3(0, 0.25f, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.1f, 0.14f, 0.17f);
        var target = new RenderTexture(512, 512, 24);
        var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            target.Create();
            camera.targetTexture = target;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
            image.Apply();
            var colors = new System.Collections.Generic.HashSet<Color32>(image.GetPixels32());
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/obstacle-preview.png", image.EncodeToPNG());
            Require(colors.Count > 3, "Prefab preview image is not blank");
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    //Runs deferred editor work directly so the batch check does not need a window repaint.
    private static void Refresh(SchoolFurniture obstacle)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(SchoolFurniture).GetMethod("OnValidate", flags).Invoke(obstacle, null);
        typeof(SchoolFurniture).GetMethod("Update", flags).Invoke(obstacle, null);
    }

    //Stops on the first failed check and names the problem in the editor log.
    private static void Require(bool passed, string message)
    {
        if (!passed) throw new Exception("FAIL: " + message);
        Debug.Log("PASS: " + message);
    }
}
