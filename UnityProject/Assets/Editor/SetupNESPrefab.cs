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
            importer.globalScale = 1.5f; // Scale so NES console fills ~1 block (1m)
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

        // Remove the nesGamepad - we only want the console body
        Transform gamepad = instance.transform.Find("nesGamepad");
        if (gamepad != null)
        {
            Object.DestroyImmediate(gamepad.gameObject);
            Debug.Log("Removed nesGamepad from prefab");
        }

        // T_Block tag is REQUIRED for 7DTD's interaction raycast in normal gameplay.
        // Without it, the block is invisible to the raycast and "Press E" never appears.
        instance.tag = "T_Block";
        instance.isStatic = true;

        // Remove any colliders on child objects (MeshColliders break interaction and cause lag)
        foreach (var col in instance.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(col);
        }

        // Position model so its bottom sits at ground level (y=0)
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool boundsInit = false;
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (!boundsInit)
            {
                bounds = renderer.bounds;
                boundsInit = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        if (boundsInit)
        {
            // Shift so bottom of model is at y=0 and centered on x/z
            Vector3 offset = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            foreach (Transform child in instance.transform)
            {
                child.position += offset;
            }
            Debug.Log($"Model bounds: {bounds.size}, shifted by {offset}");
        }

        // Add a BoxCollider sized to a full block (1x1x1) for 7DTD's voxel raycast.
        // In normal gameplay, the interaction raycast steps through voxels — if the
        // collider is smaller than a block, the raycast won't register the block.
        // The prefab editor uses a different system so it works there regardless.
        var box = instance.GetComponent<BoxCollider>();
        if (box == null)
            box = instance.AddComponent<BoxCollider>();
        box.center = new Vector3(0, 0.5f, 0);
        box.size = new Vector3(1f, 1f, 1f);
        Debug.Log($"BoxCollider set to full block size: center={box.center}, size={box.size}");

        // Apply existing materials
        SetupMaterials(instance);

        // Save as prefab
        string prefabPath = "Assets/NESModel/NESConsolePrefab.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log($"NES Console prefab created at {prefabPath}");
        Debug.Log("Run '7nes > Build Asset Bundle' to build.");
    }

    static void SetupMaterials(GameObject go)
    {
        Material nesMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/NESModel/Materials/nes.mat");
        if (nesMat == null)
        {
            Debug.LogError("Could not find Assets/NESModel/Materials/nes.mat");
            return;
        }

        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterial = nesMat;
        }
    }
}
