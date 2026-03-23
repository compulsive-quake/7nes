using UnityEditor;
using UnityEngine;
using System.IO;

public class SetupNESPrefab
{
    [MenuItem("7nes/Setup NES Console Prefab")]
    public static void Setup()
    {
        // Find the imported FBX
        string fbxPath = "Assets/NESModel/NintendoNes.fbx";
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError($"FBX not found at {fbxPath}. Make sure the model is imported.");
            return;
        }

        // Configure FBX import settings
        ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            importer.globalScale = 100f; // Adjust scale - NES console should be ~1 block (1m)
            importer.useFileUnits = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.SaveAndReimport();
        }

        // Instantiate the model
        GameObject instance = Object.Instantiate(fbxAsset);
        instance.name = "NESConsolePrefab";

        // Set layer and tag for 7DTD
        instance.isStatic = true;

        // Add a box collider if none exists
        if (instance.GetComponent<Collider>() == null)
        {
            // Add colliders to all mesh children
            foreach (var meshFilter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.GetComponent<Collider>() == null)
                {
                    meshFilter.gameObject.AddComponent<MeshCollider>();
                }
            }
        }

        // Setup materials from the Arnold texture set
        SetupMaterials(instance);

        // Save as prefab
        string prefabPath = "Assets/NESModel/NESConsolePrefab.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log($"NES Console prefab created at {prefabPath}");
        Debug.Log("Review the prefab scale and materials, then run '7nes > Build Asset Bundle'");
    }

    static void SetupMaterials(GameObject go)
    {
        string texturePath = "Assets/NESModel/Textures/Arnold";

        // Find all renderers and set up materials
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                // Create a new Standard material for each submesh
                int udimTile = 1001 + i; // First submesh = 1001, second = 1002
                Material mat = new Material(Shader.Find("Standard"));
                mat.name = $"NES_Mat_{udimTile}";

                // Try to load textures for this UDIM tile
                string baseColorPath = $"{texturePath}/BaseColor.{udimTile}.exr";
                string normalPath = $"{texturePath}/Normal.{udimTile}.exr";
                string metallicPath = $"{texturePath}/Metalness.{udimTile}.exr";
                string roughnessPath = $"{texturePath}/Roughness.{udimTile}.exr";

                Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorPath);
                if (baseColor != null)
                {
                    mat.mainTexture = baseColor;
                }

                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal != null)
                {
                    // Set normal map import settings
                    TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                    if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
                    {
                        normalImporter.textureType = TextureImporterType.NormalMap;
                        normalImporter.SaveAndReimport();
                        normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                    }
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }

                Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath);
                if (metallic != null)
                {
                    mat.SetTexture("_MetallicGlossMap", metallic);
                    mat.EnableKeyword("_METALLICGLOSSMAP");
                }

                // Save material as asset
                string matPath = $"Assets/NESModel/Materials/NES_Mat_{udimTile}.mat";
                AssetDatabase.CreateAsset(mat, matPath);

                mats[i] = mat;
            }
            renderer.sharedMaterials = mats;
        }
    }
}
