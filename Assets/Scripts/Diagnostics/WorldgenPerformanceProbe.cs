using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Measures streamed chunk frames for a dense town block and a suburban
    /// house chunk. Town centers intentionally place blocks in every town chunk. Run in an
    /// isolated player with --worldgen-performance-probe.
    /// </summary>
    public sealed class WorldgenPerformanceProbe : MonoBehaviour
    {
        private ProceduralWorld world;
        private FirstPersonController player;
        private CharacterController body;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private bool savedPlayerEnabled;
        private bool savedBodyEnabled;
        private int savedLoadRadius;
        private float savedTimeScale;
        private string expectedChunk;
        private int targetBuiltFrame = -1;
        private float measuredFrameMs = -1f;
        private float peakStreamFrameMs;
        private int builtCount;
        private string blockFingerprint;
        private string stateCratePath;
        private bool stateCrateWasSearched;
        private readonly Dictionary<string, List<float>> samples = new Dictionary<string, List<float>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--worldgen-performance-probe") >= 0)
                new GameObject(nameof(WorldgenPerformanceProbe)).AddComponent<WorldgenPerformanceProbe>();
        }

        private void OnEnable() => ProceduralWorld.ChunkBuilt += OnChunkBuilt;
        private void OnDisable() => ProceduralWorld.ChunkBuilt -= OnChunkBuilt;

        private void OnChunkBuilt(Transform chunk)
        {
            builtCount++;
            if (chunk != null && chunk.name == expectedChunk) targetBuiltFrame = Time.frameCount;
        }

        private void LateUpdate()
        {
            if (!string.IsNullOrEmpty(expectedChunk) && targetBuiltFrame < 0)
                peakStreamFrameMs = Mathf.Max(peakStreamFrameMs, Time.unscaledDeltaTime * 1000f);
            // Include the frame that completes delayed building work.
            if (targetBuiltFrame >= 0 && measuredFrameMs < 0f && Time.frameCount > targetBuiltFrame)
                measuredFrameMs = Mathf.Max(peakStreamFrameMs, Time.unscaledDeltaTime * 1000f);
        }

        private IEnumerator Start()
        {
            world = ProceduralWorld.Active;
            player = FindFirstObjectByType<FirstPersonController>();
            if (world == null || player == null || world.assets == null)
            {
                Fail("procedural world, player, or world assets missing");
                yield break;
            }

            body = player.GetComponent<CharacterController>();
            savedPosition = player.transform.position;
            savedRotation = player.transform.rotation;
            savedPlayerEnabled = player.enabled;
            savedBodyEnabled = body != null && body.enabled;
            savedLoadRadius = world.loadRadius;
            savedTimeScale = Time.timeScale;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Time.timeScale = 1f;
            player.enabled = false;
            if (body != null) body.enabled = false;

            // Match the post-bomb world used during normal exploration. Wait for startup and the
            // ruined replacement queue to settle before selecting already-generated test chunks.
            yield return WaitForStableWorld(120);
            world.loadRadius = 1;
            world.SetRuined(true);
            yield return WaitForStableWorld(180);

            Vector2Int block;
            Vector2Int suburbHouse;
            if (!FindSamples(out block, out suburbHouse))
            {
                Fail("could not find both a generated multi-storey block chunk and a generated suburban house chunk");
                Restore();
                yield break;
            }

            Debug.Log($"[WorldgenPerformanceProbe] block chunk={block}; suburb-house chunk={suburbHouse}; seed={world.Seed}; loadRadius={world.loadRadius}; samples=3 each");
            Transform blockChunk = world.transform.Find($"Chunk_{block.x}_{block.y}");
            Transform blockContent = blockChunk != null ? blockChunk.Find("Content") : null;
            if (blockContent == null) Fail("selected block has no Content root for layout/loot invariant fingerprint");
            else
            {
                LootContainer crate = blockContent.GetComponentsInChildren<LootContainer>(true).FirstOrDefault();
                if (crate == null) Fail("selected ruined block has no loot container for deferred-state restoration check");
                else
                {
                    stateCratePath = HierarchyPath(crate.transform, blockContent);
                    stateCrateWasSearched = crate.Searched;
                    crate.RestoreSearchedState(true);
                    blockFingerprint = ContentFingerprint(blockContent);
                    Debug.Log($"[WorldgenPerformanceProbe] block invariants after setting saved crate state: {blockFingerprint}");
                }
            }
            samples["block"] = new List<float>();
            samples["suburb_house"] = new List<float>();
            Vector2Int[] targets = { block, suburbHouse };
            string[] labels = { "block", "suburb_house" };

            for (int group = 0; group < targets.Length; group++)
            {
                for (int sample = 0; sample < 3; sample++)
                {
                    yield return StageAway(targets[group]);
                    yield return StreamTarget(targets[group], labels[group], sample);
                }
            }

            Report();
            if (!string.IsNullOrEmpty(stateCratePath))
            {
                GameObject restoredBlock = world.EnsureChunk(block);
                Transform restoredContent = restoredBlock != null ? restoredBlock.transform.Find("Content") : null;
                LootContainer restoredCrate = FindCrate(restoredContent, stateCratePath);
                if (restoredCrate == null) Fail("could not restore the state-test crate after performance samples");
                else
                {
                    restoredCrate.RestoreSearchedState(stateCrateWasSearched);
                    world.CaptureWorld();
                    Debug.Log("[WorldgenPerformanceProbe] state-test crate returned to its original searched value and chunk history was refreshed");
                }
            }
            Restore();
            Debug.Log("[WorldgenPerformanceProbe] ALL CHECKS PASSED");
            Application.Quit(0);
        }

        private IEnumerator WaitForStableWorld(int timeoutFrames)
        {
            int lastCount = -1;
            int steady = 0;
            for (int i = 0; i < timeoutFrames; i++)
            {
                int count = builtCount;
                if (count == lastCount) steady++;
                else { steady = 0; lastCount = count; }
                if (steady >= 6 && world.PendingBuildingChunkCount == 0) yield break;
                yield return null;
            }
            Debug.LogWarning("[WorldgenPerformanceProbe] world did not fully settle before timeout; continuing with current loaded chunks");
        }

        private bool FindSamples(out Vector2Int block, out Vector2Int suburbHouse)
        {
            block = default;
            suburbHouse = default;
            bool foundBlock = false, foundHouse = false;
            int loadedTownChunks = 0, loadedBlockChunks = 0, loadedSuburbChunks = 0, searchedTownChunks = 0, searchedBlockChunks = 0, searchedSuburbChunks = 0;
            foreach (Transform chunk in world.transform)
            {
                if (chunk == null || !TryCoordinate(chunk.name, out Vector2Int coordinate)) continue;
                if (ProceduralLandmarks.HasLandmark(coordinate, world.Seed)) continue;
                if (WorldLayout.IsTownChunk(coordinate, world.Seed))
                {
                    loadedTownChunks++;
                    if (HasBlock(chunk)) { loadedBlockChunks++; if (!foundBlock) { block = coordinate; foundBlock = true; } }
                }
                else if (WorldLayout.IsSuburbChunk(coordinate, world.Seed))
                {
                    loadedSuburbChunks++;
                    if (!foundHouse && HasHouse(chunk)) { suburbHouse = coordinate; foundHouse = true; }
                }
            }
            Debug.Log($"[WorldgenPerformanceProbe] loaded town chunks={loadedTownChunks}, with blocks={loadedBlockChunks}; loaded suburb chunks={loadedSuburbChunks}");
            if (foundBlock && foundHouse) return true;

            // The starter region may not have both types loaded. Search deterministic town macros
            // using throwaway roots; these are removed immediately and do not enter world streaming.
            Vector2Int center = WorldLayout.Coordinate(player.transform.position);
            int macroX = Mathf.FloorToInt(center.x / 4f), macroZ = Mathf.FloorToInt(center.y / 4f);
            for (int radius = 0; radius <= 16 && !(foundBlock && foundHouse); radius++)
            {
                for (int dz = -radius; dz <= radius && !(foundBlock && foundHouse); dz++)
                for (int dx = -radius; dx <= radius && !(foundBlock && foundHouse); dx++)
                {
                    if (radius > 0 && Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                    int mx = macroX + dx, mz = macroZ + dz;
                    for (int lz = 0; lz <= 3 && !(foundBlock && foundHouse); lz++)
                    for (int lx = 0; lx <= 3 && !(foundBlock && foundHouse); lx++)
                    {
                        Vector2Int coordinate = new Vector2Int(mx * 4 + lx, mz * 4 + lz);
                        if (ProceduralLandmarks.HasLandmark(coordinate, world.Seed)) continue;
                        bool town = WorldLayout.IsTownChunk(coordinate, world.Seed);
                        bool suburb = WorldLayout.IsSuburbChunk(coordinate, world.Seed);
                        if (!town && !suburb) continue;
                        if ((town && foundBlock) || (suburb && foundHouse)) continue;
                        if (town) searchedTownChunks++; else searchedSuburbChunks++;
                        GameObject scratch = new GameObject("WorldgenProbeLayoutSearch");
                        GameObject content = ProceduralContent.Build(scratch.transform, coordinate, world.Seed, world.assets, true);
                        if (town)
                        {
                            bool hasBlock = HasBlock(content.transform);
                            if (hasBlock) searchedBlockChunks++;
                            if (hasBlock) { block = coordinate; foundBlock = true; }
                        }
                        else if (HasHouse(content.transform))
                        {
                            suburbHouse = coordinate;
                            foundHouse = true;
                        }
                        DestroyImmediate(scratch);
                    }
                }
            }
            Debug.Log($"[WorldgenPerformanceProbe] throwaway chunks={searchedTownChunks + searchedSuburbChunks} (town={searchedTownChunks}, suburb={searchedSuburbChunks}), with blocks={searchedBlockChunks}; selected block={block}, suburb house={suburbHouse}");
            return foundBlock && foundHouse;
        }

        private static bool HasBlock(Transform chunk)
        {
            foreach (BuildingInterior building in chunk.GetComponentsInChildren<BuildingInterior>(true))
            {
                int floors = building.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Floor_", StringComparison.Ordinal));
                if (floors >= 4) return true;
            }
            return false;
        }

        private static bool HasHouse(Transform chunk)
        {
            foreach (BuildingInterior building in chunk.GetComponentsInChildren<BuildingInterior>(true))
                if (building.name.StartsWith("House_", StringComparison.Ordinal)) return true;
            return false;
        }

        private static string ContentFingerprint(Transform root)
        {
            var layout = new StringBuilder();
            var loot = new StringBuilder();
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true)
                .OrderBy(t => HierarchyPath(t, root), StringComparer.Ordinal).ToArray();
            int colliderCount = 0, meshCount = 0, lootCount = 0;
            foreach (Transform t in transforms)
            {
                string path = HierarchyPath(t, root);
                layout.Append(path).Append('|').Append(t.gameObject.activeSelf ? '1' : '0').Append('|')
                    .Append(Format(t.localPosition)).Append('|').Append(Format(t.localRotation)).Append('|').Append(Format(t.localScale)).Append('\n');
                foreach (Collider collider in t.GetComponents<Collider>())
                {
                    colliderCount++;
                    layout.Append("C|").Append(path).Append('|').Append(collider.GetType().Name).Append('|')
                        .Append(collider.enabled ? '1' : '0').Append('|').Append(collider.isTrigger ? '1' : '0');
                    if (collider is BoxCollider box)
                        layout.Append('|').Append(Format(box.center)).Append('|').Append(Format(box.size));
                    else if (collider is CapsuleCollider capsule)
                        layout.Append('|').Append(Format(capsule.center)).Append('|').Append(capsule.radius.ToString("R", CultureInfo.InvariantCulture))
                            .Append('|').Append(capsule.height.ToString("R", CultureInfo.InvariantCulture)).Append('|').Append(capsule.direction);
                    else if (collider is MeshCollider meshCollider)
                        layout.Append('|').Append(meshCollider.sharedMesh != null ? meshCollider.sharedMesh.name : "null")
                            .Append('|').Append(meshCollider.convex ? '1' : '0');
                    layout.Append('\n');
                }
                foreach (MeshFilter filter in t.GetComponents<MeshFilter>())
                {
                    meshCount++;
                    Mesh mesh = filter.sharedMesh;
                    layout.Append("M|").Append(path).Append('|').Append(mesh != null ? mesh.name : "null");
                    if (mesh != null)
                    {
                        ulong indexCount = 0;
                        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++) indexCount += mesh.GetIndexCount(subMesh);
                        layout.Append('|').Append(mesh.vertexCount).Append('|').Append(indexCount);
                    }
                    layout.Append('\n');
                }
                foreach (Renderer renderer in t.GetComponents<Renderer>())
                    layout.Append("R|").Append(path).Append('|').Append(renderer.enabled ? '1' : '0')
                        .Append('|').Append(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null").Append('\n');
                LootContainer container = t.GetComponent<LootContainer>();
                if (container != null)
                {
                    lootCount++;
                    loot.Append(path).Append('|').Append(container.hasLoot ? '1' : '0');
                    if (container.entries != null)
                        foreach (LootEntry entry in container.entries)
                            loot.Append('|').Append(entry.resource != null ? entry.resource.id : "null").Append('x').Append(entry.amount);
                    loot.Append('\n');
                }
            }
            return $"objects={transforms.Length} colliders={colliderCount} meshes={meshCount} lootContainers={lootCount} layoutHash={Hash(layout)} lootHash={Hash(loot)}";
        }

        private static string HierarchyPath(Transform t, Transform root)
        {
            if (t == root) return root.name;
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent)
                path = p.name + "/" + path;
            return root.name + "/" + path;
        }

        private static string Format(Vector3 value) => string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}", value.x, value.y, value.z);
        private static string Format(Quaternion value) => string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}", value.x, value.y, value.z, value.w);

        private static string Hash(StringBuilder text)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                foreach (char character in text.ToString())
                    hash = (hash ^ character) * 1099511628211UL;
                return hash.ToString("X16", CultureInfo.InvariantCulture);
            }
        }

        private IEnumerator StageAway(Vector2Int target)
        {
            Vector2Int staging = target + new Vector2Int(8, 8);
            Teleport(staging);
            yield return WaitForStableWorld(90);
            // Ensure the previous test chunk is outside the new load radius before returning to it.
            if (Mathf.Abs(target.x - staging.x) <= world.loadRadius + 1 || Mathf.Abs(target.y - staging.y) <= world.loadRadius + 1)
                throw new InvalidOperationException("Worldgen benchmark staging coordinate is too close to its target.");
        }

        private IEnumerator StreamTarget(Vector2Int target, string label, int sample)
        {
            expectedChunk = $"Chunk_{target.x}_{target.y}";
            world.performanceTimingTarget = target;
            world.lastPerformanceTiming = null;
            targetBuiltFrame = -1;
            measuredFrameMs = -1f;
            peakStreamFrameMs = 0f;
            Teleport(target);
            for (int frame = 0; frame < 90 && measuredFrameMs < 0f; frame++) yield return null;
            if (measuredFrameMs < 0f)
            {
                Fail($"{label} sample {sample + 1}: target chunk was not generated during streaming");
                world.performanceTimingTarget = null;
                yield break;
            }
            samples[label].Add(measuredFrameMs);
            Debug.Log($"[WorldgenPerformanceProbe] {label} sample {sample + 1}/3: peak streamed generation frame {measuredFrameMs:F2} ms; stages: {world.lastPerformanceTiming ?? "not captured"}; Box()={ProceduralContent.LastBoxMs:F2} ms/{ProceduralContent.LastBoxCount} calls");
            if (label == "block")
            {
                Transform chunk = world.transform.Find(expectedChunk);
                Transform content = chunk != null ? chunk.Find("Content") : null;
                string currentFingerprint = content != null ? ContentFingerprint(content) : "missing";
                if (currentFingerprint != blockFingerprint)
                {
                    Fail($"block layout/loot fingerprint changed after streamed rebuild: expected {blockFingerprint}, got {currentFingerprint}");
                    yield break;
                }
                LootContainer restoredCrate = FindCrate(content, stateCratePath);
                if (restoredCrate == null || !restoredCrate.Searched)
                {
                    Fail("saved searched state was not applied after deferred town building completed");
                    yield break;
                }
            }
            world.performanceTimingTarget = null;
        }

        private static LootContainer FindCrate(Transform content, string path)
        {
            if (content == null || string.IsNullOrEmpty(path)) return null;
            return content.GetComponentsInChildren<LootContainer>(true)
                .FirstOrDefault(crate => HierarchyPath(crate.transform, content) == path);
        }

        private void Teleport(Vector2Int coordinate)
        {
            Vector3 position = new Vector3(coordinate.x * (float)WorldLayout.ChunkSize + 64f, 0f,
                coordinate.y * (float)WorldLayout.ChunkSize + 64f);
            position.y = WorldLayout.HeightAt(position.x, position.z, world.Seed) + 2f;
            if (body != null) body.enabled = false;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            Physics.SyncTransforms();
        }

        private static bool TryCoordinate(string name, out Vector2Int coordinate)
        {
            coordinate = default;
            if (!name.StartsWith("Chunk_", StringComparison.Ordinal)) return false;
            string[] parts = name.Substring(6).Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) return false;
            coordinate = new Vector2Int(x, z);
            return true;
        }

        private void Report()
        {
            foreach (var entry in samples)
            {
                if (entry.Value.Count == 0) continue;
                float[] ordered = entry.Value.OrderBy(v => v).ToArray();
                float average = ordered.Average();
                Debug.Log($"[WorldgenPerformanceProbe] SUMMARY {entry.Key}: n={ordered.Length}, median={ordered[ordered.Length / 2]:F2} ms, mean={average:F2} ms, max={ordered[ordered.Length - 1]:F2} ms");
            }
        }

        private void Restore()
        {
            expectedChunk = null;
            if (world != null) world.loadRadius = savedLoadRadius;
            if (body != null) body.enabled = false;
            if (player != null)
            {
                player.transform.SetPositionAndRotation(savedPosition, savedRotation);
                if (body != null) body.enabled = savedBodyEnabled;
                player.enabled = savedPlayerEnabled;
            }
            Time.timeScale = savedTimeScale;
            Physics.SyncTransforms();
        }

        private void Fail(string message)
        {
            Debug.LogError("[WorldgenPerformanceProbe] FAIL: " + message);
            Restore();
            Application.Quit(1);
        }
    }
}
