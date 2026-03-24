using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildAssetBundle
{
    [MenuItem("7nes/Build Asset Bundle")]
    public static void Build()
    {
        // Ensure NES console prefab has a root BoxCollider before building
        EnsureConsoleCollider();

        string outputPath = Path.Combine(Application.dataPath, "../../Resources");
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        var builds = new AssetBundleBuild[2];
        builds[0].assetBundleName = "nesmodel.unity3d";
        builds[0].assetNames = new string[] { "Assets/NESModel/NESConsolePrefab.prefab" };
        builds[1].assetBundleName = "nescartridge.unity3d";
        builds[1].assetNames = new string[] { "Assets/NESCartridge/NESCartridgePrefab.prefab" };

        BuildPipeline.BuildAssetBundles(outputPath, builds,
            BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);

        // Copy to mod Resources folder
        string modResourcesPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Resources"));
        if (!Directory.Exists(modResourcesPath))
            Directory.CreateDirectory(modResourcesPath);

        string srcBundle = Path.Combine(outputPath, "nesmodel.unity3d");
        string dstBundle = Path.Combine(modResourcesPath, "nesmodel.unity3d");
        if (File.Exists(srcBundle))
        {
            File.Copy(srcBundle, dstBundle, true);
            Debug.Log($"Asset bundle copied to: {dstBundle}");
        }

        string srcCartridge = Path.Combine(outputPath, "nescartridge.unity3d");
        string dstCartridge = Path.Combine(modResourcesPath, "nescartridge.unity3d");
        if (File.Exists(srcCartridge))
        {
            File.Copy(srcCartridge, dstCartridge, true);
            Debug.Log($"Cartridge asset bundle copied to: {dstCartridge}");
        }

        Debug.Log("7nes asset bundle build complete!");
    }

    static void EnsureConsoleCollider()
    {
        string prefabPath = "Assets/NESModel/NESConsolePrefab.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("NESConsolePrefab not found, skipping collider setup");
            return;
        }

        if (prefab.GetComponent<BoxCollider>() != null)
        {
            Debug.Log("NESConsolePrefab already has root BoxCollider");
            return;
        }

        // Edit prefab contents through Unity's proper API
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);

        // Calculate bounds from all renderers
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool init = false;
        foreach (var r in contents.GetComponentsInChildren<Renderer>())
        {
            if (!init) { bounds = r.bounds; init = true; }
            else bounds.Encapsulate(r.bounds);
        }

        var box = contents.AddComponent<BoxCollider>();
        box.center = contents.transform.InverseTransformPoint(bounds.center);
        box.size = bounds.size;

        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);

        Debug.Log($"Added root BoxCollider to NESConsolePrefab: center={box.center}, size={box.size}");
    }
}
