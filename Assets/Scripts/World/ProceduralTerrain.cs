using System;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Builds one deterministic, walkable terrain tile as the player explores.</summary>
    public sealed class ProceduralTerrain : MonoBehaviour
    {
        private const int SampleSpacing = 16;
        private const int BiomeCount = 6;
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
            var triangles = new List<int>[BiomeCount];
            for (int i = 0; i < BiomeCount; i++) triangles[i] = new List<int>();

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
                }
            }

            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    float centerX = originX + (x + 0.5f) * SampleSpacing;
                    float centerZ = originZ + (z + 0.5f) * SampleSpacing;
                    if (WorldLayout.IsTerrainHoleCell(centerX, centerZ)) continue;
                    int biome = Mathf.Clamp(WorldLayout.BiomeIndexAt(centerX, centerZ, seed), 0, BiomeCount - 1);
                    List<int> indices = triangles[biome];
                    int lowerLeft = z * row + x;
                    int upperLeft = lowerLeft + row;
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
            mesh.subMeshCount = BiomeCount;
            bool hasGround = false;
            for (int i = 0; i < BiomeCount; i++)
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
            var materials = new Material[BiomeCount];
            Array.Copy(biomeMaterials, materials, BiomeCount);
            renderer.sharedMaterials = materials;
            if (hasGround) tile.AddComponent<MeshCollider>().sharedMesh = mesh;
            return tile;
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
