using System;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Builds one deterministic, walkable terrain tile as the player explores.</summary>
    public sealed class ProceduralTerrain : MonoBehaviour
    {
        private const int SampleSpacing = 8;
        private const int BiomeCount = 6;
        private static readonly Dictionary<int, Material> BlendedMaterials = new Dictionary<int, Material>();
        private Mesh ownedMesh;

        public static GameObject Build(Transform parent, Vector2Int coordinate, int seed,
            Material[] biomeMaterials)
        {
            if (biomeMaterials == null || biomeMaterials.Length < BiomeCount)
                throw new ArgumentException("Terrain requires six shared biome materials.", nameof(biomeMaterials));
            for (int i = 0; i < BiomeCount; i++)
                if (biomeMaterials[i] == null)
                    throw new ArgumentException("Terrain biome materials must not be null.", nameof(biomeMaterials));

            int cells = WorldLayout.ChunkSize / SampleSpacing;
            int row = cells + 1;
            float originX = (float)coordinate.x * WorldLayout.ChunkSize;
            float originZ = (float)coordinate.y * WorldLayout.ChunkSize;
            var vertices = new Vector3[row * row];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var vertexBiomes = new int[vertices.Length];
            var triangles = new List<List<int>>(BiomeCount);
            for (int i = 0; i < BiomeCount; i++) triangles.Add(new List<int>());
            var tileBlends = new List<Material>();
            var blendSubmesh = new Dictionary<int, int>();

            for (int z = 0; z <= cells; z++)
            {
                for (int x = 0; x <= cells; x++)
                {
                    int index = z * row + x;
                    float worldX = originX + x * SampleSpacing;
                    float worldZ = originZ + z * SampleSpacing;
                    vertices[index] = new Vector3(x * SampleSpacing,
                        WorldLayout.HeightAt(worldX, worldZ, seed), z * SampleSpacing);
                    // Sampling the same world positions on both sides of a border keeps
                    // lighting continuous even when the adjacent tile has not loaded yet.
                    float dx = WorldLayout.HeightAt(worldX + 1f, worldZ, seed)
                        - WorldLayout.HeightAt(worldX - 1f, worldZ, seed);
                    float dz = WorldLayout.HeightAt(worldX, worldZ + 1f, seed)
                        - WorldLayout.HeightAt(worldX, worldZ - 1f, seed);
                    normals[index] = new Vector3(-dx, 2f, -dz).normalized;
                    uv[index] = new Vector2(worldX / 11f, worldZ / 11f);
                    vertexBiomes[index] = Mathf.Clamp(WorldLayout.BiomeIndexAt(worldX, worldZ, seed), 0, BiomeCount - 1);
                }
            }

            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float centerX = originX + (x + 0.5f) * SampleSpacing;
                    float centerZ = originZ + (z + 0.5f) * SampleSpacing;
                    if (WorldLayout.IsTerrainHoleCell(centerX, centerZ)) continue;
                    int lowerLeft = z * row + x;
                    int upperLeft = lowerLeft + row;
                    int[] corners = { vertexBiomes[lowerLeft], vertexBiomes[upperLeft], vertexBiomes[upperLeft + 1], vertexBiomes[lowerLeft + 1] };
                    int[] counts = new int[BiomeCount];
                    foreach (int cornerBiome in corners) counts[cornerBiome]++;
                    int primary = 0, secondary = -1;
                    for (int biome = 1; biome < BiomeCount; biome++)
                        if (counts[biome] > counts[primary]) primary = biome;
                    for (int biome = 0; biome < BiomeCount; biome++)
                        if (biome != primary && (secondary < 0 || counts[biome] > counts[secondary])) secondary = biome;

                    int submesh = primary;
                    if (secondary >= 0 && counts[secondary] > 0)
                    {
                        int low = Mathf.Min(primary, secondary), high = Mathf.Max(primary, secondary);
                        int highCorners = counts[high];
                        // One boundary cell receives a blended tint based on its corner mix.
                        // Quantized quarter steps keep the shared material palette compact.
                        if (primary > secondary) highCorners = counts[primary];
                        int blendStep = Mathf.Clamp(Mathf.RoundToInt(highCorners), 1, 3);
                        int key = low * 100 + high * 10 + blendStep;
                        if (!blendSubmesh.TryGetValue(key, out submesh))
                        {
                            submesh = BiomeCount + tileBlends.Count;
                            blendSubmesh.Add(key, submesh);
                            tileBlends.Add(GetBlendedMaterial(low, high, blendStep, biomeMaterials));
                            triangles.Add(new List<int>());
                        }
                    }
                    List<int> indices = triangles[submesh];
                    indices.Add(lowerLeft);
                    indices.Add(upperLeft);
                    indices.Add(upperLeft + 1);
                    indices.Add(lowerLeft);
                    indices.Add(upperLeft + 1);
                    indices.Add(lowerLeft + 1);
                }
            }

            var mesh = new Mesh { name = $"Terrain_{coordinate.x}_{coordinate.y}" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.subMeshCount = triangles.Count;
            bool hasGround = false;
            for (int i = 0; i < triangles.Count; i++)
            {
                mesh.SetTriangles(triangles[i], i, false);
                hasGround |= triangles[i].Count > 0;
            }
            mesh.RecalculateBounds();

            var tile = new GameObject(mesh.name);
            tile.transform.SetParent(parent, false);
            tile.transform.localPosition = new Vector3(originX, 0f, originZ);
            tile.AddComponent<ProceduralTerrain>().ownedMesh = mesh;
            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = tile.AddComponent<MeshRenderer>();
            // Never instantiate per-tile materials; loaded tiles share this small palette.
            var materials = new Material[BiomeCount + tileBlends.Count];
            Array.Copy(biomeMaterials, materials, BiomeCount);
            for (int i = 0; i < tileBlends.Count; i++) materials[BiomeCount + i] = tileBlends[i];
            renderer.sharedMaterials = materials;
            if (hasGround) tile.AddComponent<MeshCollider>().sharedMesh = mesh;
            return tile;
        }

        private static Material GetBlendedMaterial(int first, int second, int blendStep, Material[] biomeMaterials)
        {
            int key = first * 100 + second * 10 + blendStep;
            if (BlendedMaterials.TryGetValue(key, out Material cached) && cached != null) return cached;
            Material blended = new Material(biomeMaterials[first])
            {
                name = $"BiomeBlend_{first}_{second}_{blendStep}",
                hideFlags = HideFlags.HideAndDontSave
            };
            blended.color = Color.Lerp(biomeMaterials[first].color, biomeMaterials[second].color, blendStep * 0.25f);
            BlendedMaterials[key] = blended;
            return blended;
        }

        private void OnDestroy()
        {
            // Meshes created at runtime are not unloaded with scene assets automatically.
            if (ownedMesh == null) return;
            if (Application.isPlaying) Destroy(ownedMesh);
            else DestroyImmediate(ownedMesh);
        }
    }
}
