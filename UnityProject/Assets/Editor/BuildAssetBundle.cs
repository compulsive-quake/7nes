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

        var contents = PrefabUtility.LoadPrefabContents(prefabPath);

        // Remove all MeshColliders - non-convex ones cause lag and break 7DTD interaction
        foreach (var mc in contents.GetComponentsInChildren<MeshCollider>())
        {
            Object.DestroyImmediate(mc);
            Debug.Log("Removed MeshCollider from " + mc.gameObject.name);
        }

        // Verify BoxCollider exists (should be set by Setup script to match model bounds)
        var box = contents.GetComponent<BoxCollider>();
        if (box == null)
        {
            Debug.LogWarning("No BoxCollider on NESConsolePrefab root - run Setup NES Console Prefab first!");
        }
        else
        {
            Debug.Log($"BoxCollider OK: center={box.center}, size={box.size}");
        }

        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);

        Debug.Log("NESConsolePrefab collider setup complete: single BoxCollider, no MeshColliders");
    }
}
