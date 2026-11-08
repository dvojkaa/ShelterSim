using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Triangle-conforming shallow streams, lake shores, and ramped highway crossings.</summary>
    public static class WorldWaterways
    {
        private struct Point
        {
            public Vector3 position;
            public float depth;
            public Point(float x, float z, int seed)
            {
                position = new Vector3(x, WorldLayout.HeightAt(x, z, seed), z);
                depth = WorldLayout.WaterDepthAt(x, z, seed);
            }
            public static Point Lerp(Point a, Point b, float t) => new Point
            {
                position = Vector3.Lerp(a.position, b.position, t),
                depth = Mathf.Lerp(a.depth, b.depth, t)
            };
        }

        public static GameObject Build(Transform parent, Vector2Int coordinate, int seed,
            Material waterMaterial, Material bridgeMaterial)
        {
            if (waterMaterial == null) return null;
            float ox = (float)coordinate.x * WorldLayout.ChunkSize;
            float oz = (float)coordinate.y * WorldLayout.ChunkSize;
            var waterVertices = new List<Vector3>();
            var waterTriangles = new List<int>();
            var bridgeVertices = new List<Vector3>();
            var bridgeTriangles = new List<int>();
            // Cache the exact terrain nodes; both meshes then use affine interpolation only.
            var nodes = new Point[9, 9];
            bool wet = false;
            for (int z = 0; z <= 8; z++)
                for (int x = 0; x <= 8; x++)
                {
                    nodes[x, z] = new Point(ox + x * 16f, oz + z * 16f, seed);
                    wet |= nodes[x, z].depth > 0.015f;
                }
            if (!wet) return null;
            for (int z = 0; z < 8; z++)
                for (int x = 0; x < 8; x++)
                {
                    AddTriangle(nodes[x, z], nodes[x, z + 1], nodes[x + 1, z + 1],
                        waterVertices, waterTriangles, bridgeVertices, bridgeTriangles);
                    AddTriangle(nodes[x, z], nodes[x + 1, z + 1], nodes[x + 1, z],
                        waterVertices, waterTriangles, bridgeVertices, bridgeTriangles);
                }
            if (waterVertices.Count == 0) return null;
            var root = new GameObject("Shallow waterways");
            root.transform.SetParent(parent, false);
            CreateMesh(root.transform, "Streams and lakes", waterVertices, waterTriangles, waterMaterial, false);
            if (bridgeVertices.Count > 0 && bridgeMaterial != null)
                CreateMesh(root.transform, "Ramped highway crossings", bridgeVertices, bridgeTriangles, bridgeMaterial, true);
            return root;
        }

        private static void AddTriangle(Point a, Point b, Point c, List<Vector3> waterVertices,
            List<int> waterTriangles, List<Vector3> bridgeVertices, List<int> bridgeTriangles)
        {
            if (Mathf.Max(a.depth, Mathf.Max(b.depth, c.depth)) <= 0.015f) return;
            var triangle = new List<Point> { a, b, c };
            AddPolygon(Clip(triangle, -1, 0.015f, true), waterVertices, waterTriangles, false);
            float minX = Mathf.Min(a.position.x, Mathf.Min(b.position.x, c.position.x));
            float maxX = Mathf.Max(a.position.x, Mathf.Max(b.position.x, c.position.x));
            float minZ = Mathf.Min(a.position.z, Mathf.Min(b.position.z, c.position.z));
            float maxZ = Mathf.Max(a.position.z, Mathf.Max(b.position.z, c.position.z));
            float roadX = Mathf.Round((minX + maxX) * 0.5f / 512f) * 512f;
            float roadZ = Mathf.Round((minZ + maxZ) * 0.5f / 512f) * 512f;
            // Both sides of highway boundaries belong to their own terrain chunk, so the
            // crossing stays complete regardless of which adjacent chunk is loaded first.
            if (minX <= roadX + 6f && maxX >= roadX - 6f)
                AddPolygon(Clip(Clip(triangle, 0, roadX - 6f, true), 0, roadX + 6f, false),
                    bridgeVertices, bridgeTriangles, true);
            if (minZ <= roadZ + 6f && maxZ >= roadZ - 6f)
            {
                var horizontal = Clip(Clip(triangle, 2, roadZ - 6f, true), 2, roadZ + 6f, false);
                // Vertical crossings own intersections, exactly as the highway visual does.
                AddPolygon(Clip(horizontal, 0, roadX - 6f, false), bridgeVertices, bridgeTriangles, true);
                AddPolygon(Clip(horizontal, 0, roadX + 6f, true), bridgeVertices, bridgeTriangles, true);
            }
        }

        private static float Value(Point p, int axis) => axis < 0 ? p.depth : p.position[axis];

        private static List<Point> Clip(List<Point> input, int axis, float boundary, bool greater)
        {
            var output = new List<Point>();
            if (input.Count == 0) return output;
            Point previous = input[input.Count - 1];
            float previousValue = Value(previous, axis);
            bool wasInside = greater ? previousValue >= boundary : previousValue <= boundary;
            foreach (Point point in input)
            {
                float value = Value(point, axis);
                bool inside = greater ? value >= boundary : value <= boundary;
                if (inside != wasInside)
                    output.Add(Point.Lerp(previous, point, (boundary - previousValue) / (value - previousValue)));
                if (inside) output.Add(point);
                previous = point;
                previousValue = value;
                wasInside = inside;
            }
            return output;
        }

        private static void AddPolygon(List<Point> polygon, List<Vector3> vertices, List<int> indices, bool bridge)
        {
            if (polygon.Count < 3) return;
            int first = vertices.Count;
            foreach (Point point in polygon)
            {
                Vector3 position = point.position;
                // A dry deck ends at the road overlay height. Its maximum lift is 0.62m,
                // distributed across the same 16m shoreline triangles as the shallow bed.
                position.y += bridge ? 0.04f + point.depth * 1.4f : point.depth;
                vertices.Add(position);
            }
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                indices.Add(first);
                indices.Add(first + i);
                indices.Add(first + i + 1);
            }
        }

        private static void CreateMesh(Transform parent, string name, List<Vector3> vertices,
            List<int> indices, Material material, bool collider)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < vertices.Count; i++) uv[i] = new Vector2(vertices[i].x / 12f, vertices[i].z / 12f);
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var surface = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(RuntimeWorldMesh));
            surface.transform.SetParent(parent, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            surface.GetComponent<MeshRenderer>().sharedMaterial = material;
            surface.GetComponent<RuntimeWorldMesh>().mesh = mesh;
            if (collider) surface.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }
}
