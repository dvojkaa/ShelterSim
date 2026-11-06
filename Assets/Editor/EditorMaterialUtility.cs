using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Grey-box primitives all default to the same flat white/grey material, making the
    /// scene hard to read at a glance (every wall, resource node, and enemy looks identical).
    /// This is a shared helper for the setup tools to give each a distinct flat color —
    /// still primitive shapes (per the project's art direction), just not all the same color.
    /// </summary>
    public static class EditorMaterialUtility
    {
        public static Material CreateColor(Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            return material;
        }

        public static void Apply(GameObject target, Color color)
        {
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateColor(color);
            }
        }
    }
}
