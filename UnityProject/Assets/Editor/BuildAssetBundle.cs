using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildAssetBundle
{
    [MenuItem("7nes/Build Asset Bundle")]
    public static void Build()
    {
        string outputPath = Path.Combine(Application.dataPath, "../../Resources");
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        var builds = new AssetBundleBuild[1];
        builds[0].assetBundleName = "nesmodel.unity3d";
        builds[0].assetNames = new string[] { "Assets/NESModel/NESConsolePrefab.prefab" };

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

        Debug.Log("7nes asset bundle build complete!");
    }
}
