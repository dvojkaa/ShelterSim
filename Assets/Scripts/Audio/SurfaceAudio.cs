using System.Collections.Generic;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Audio
{
    /// <summary>Which footstep the ground under a point makes. Reads the look of the thing walked on, so new content needs no tagging.</summary>
    public static class SurfaceAudio
    {
        private static readonly Dictionary<Collider, string> cache = new Dictionary<Collider, string>();

        public static string FootstepAt(Vector3 feet)
        {
            if (!Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return NaturalGroundAt(feet); // Terrain not streamed in under this point yet.
            }

            if (!cache.TryGetValue(hit.collider, out string surface))
            {
                // Streamed chunks come and go, so never let this grow without bound.
                if (cache.Count > 4000) cache.Clear();
                surface = Classify(hit.collider);
                cache[hit.collider] = surface;
            }
            return surface ?? NaturalGroundAt(feet);
        }

        /// <summary>Null means natural ground, which depends on the biome at the exact point rather than the object.</summary>
        private static string Classify(Collider collider)
        {
            string objectName = collider.name;
            if (objectName.Contains("Car")) return Sfx.FootMetal;
            if (objectName.Contains("Crate") || objectName.Contains("Bed") || objectName.Contains("Workbench")) return Sfx.FootWood;

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null) renderer = collider.GetComponentInChildren<Renderer>();
            string material = renderer != null && renderer.sharedMaterial != null ? renderer.sharedMaterial.name : string.Empty;
            if (material.Contains("Concrete") || material.Contains("Brick") || material.Contains("Plaster") || material.Contains("RoofTar")) return Sfx.FootConcrete;
            if (material.Contains("Rust")) return Sfx.FootMetal;
            if (objectName.Contains("FloorTile")) return Sfx.FootConcrete;
            return null;
        }

        private static string NaturalGroundAt(Vector3 position)
        {
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) return Sfx.FootDirt;
            // Only reached on natural ground, so a bridge over a river stays concrete.
            if (WorldLayout.WaterDepthAt(position.x, position.z, world.Seed) > 0.05f) return Sfx.FootWater;
            switch (WorldLayout.BiomeIndexAt(position.x, position.z, world.Seed))
            {
                case 3: return Sfx.FootSnow;
                case 4: return Sfx.FootMud;
                default: return Sfx.FootDirt;
            }
        }
    }
}
