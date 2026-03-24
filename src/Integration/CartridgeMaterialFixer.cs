using UnityEngine;

namespace SevenNes.Integration
{
    /// <summary>
    /// Fixes cartridge model materials so they respond to scene lighting,
    /// and adjusts colliders on dropped cartridges so they lay flat.
    /// </summary>
    public static class CartridgeMaterialFixer
    {
        private static bool _loggedOnce;
        /// <summary>
        /// Switches all renderers under the given transform to the Standard shader
        /// so the cartridge model is properly lit by the scene.
        /// </summary>
        public static void FixMaterials(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                foreach (var mat in r.materials)
                {
                    var standardShader = Shader.Find("Standard");
                    if (standardShader != null && mat.shader != standardShader)
                    {
                        var mainTex = mat.mainTexture;
                        var mainColor = mat.HasProperty("_Color") ? mat.color : Color.white;
                        mat.shader = standardShader;
                        mat.mainTexture = mainTex;
                        mat.color = mainColor;
                        mat.SetFloat("_Mode", 0); // Opaque
                        mat.SetFloat("_Metallic", 0f);
                        mat.SetFloat("_Glossiness", 0.3f);
                        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                        mat.SetInt("_ZWrite", 1);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.DisableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                        mat.renderQueue = -1;
                    }
                }
            }
            if (!_loggedOnce)
            {
                Log.Out($"[7nes] Fixed cartridge material on {renderers.Length} renderer(s)");
                _loggedOnce = true;
            }
        }

        /// <summary>
        /// Replaces colliders on the cartridge model with a flat box collider
        /// so dropped cartridges lay flat on the ground instead of balancing on edge.
        /// </summary>
        public static void FixColliderForDrop(Transform root)
        {
            // Remove existing colliders on the model
            var existingColliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var col in existingColliders)
                Object.Destroy(col);

            // Add a flat box collider matching cartridge proportions (wide & flat)
            var boxCol = root.gameObject.AddComponent<BoxCollider>();
            // NES cartridge is roughly: width > height > depth (flat rectangular shape)
            boxCol.size = new Vector3(1f, 0.15f, 0.8f);
            boxCol.center = Vector3.zero;
        }
    }
}
