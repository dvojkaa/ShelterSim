using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Core;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run with --procedural-save-probe. Saves and reloads changed generated resource/furniture
    /// state through SaveManager, using a unique temporary path rather than the player's save slot.
    /// </summary>
    public sealed class ProceduralSaveProbe : MonoBehaviour
    {
        private int failures;
        private bool finished;
        private FirstPersonController player;
        private ProceduralWorld world;
        private SaveManager save;
        private string tempSave;
        private int oldLoadRadius;
        private float oldTimeScale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--procedural-save-probe") >= 0)
                new GameObject(nameof(ProceduralSaveProbe)).AddComponent<ProceduralSaveProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            oldTimeScale = Time.timeScale;
            tempSave = Path.Combine(Application.temporaryCachePath,
                "sheltersim_procedural_save_probe_" + Guid.NewGuid().ToString("N") + ".json");

            yield return null;
            yield return null;
            if (ModalUI.IsOpen) ModalUI.DismissCurrent();
            Time.timeScale = 1f;

            player = FindFirstObjectByType<FirstPersonController>();
            world = ProceduralWorld.Active;
            save = FindFirstObjectByType<SaveManager>();
            if (save == null) save = new GameObject("ProceduralSaveProbeManager").AddComponent<SaveManager>();
            if (player == null || world == null || save == null)
            {
                Fail("required player, procedural world, or SaveManager is missing");
                Finish();
                yield break;
            }

            oldLoadRadius = world.loadRadius;
            world.loadRadius = 1;
            HomeBase home = FindFirstObjectByType<HomeBase>();
            if (!world.Ruined && home != null) home.DebugStartSurvival();
            yield return new WaitForSecondsRealtime(.5f);
            Check(world.Ruined, "survival has switched procedural generation to its ruined state");
            if (!world.Ruined)
            {
                Finish();
                yield break;
            }

            if (!TryFindCandidate(out Vector2Int coordinate, out GameObject originalChunk,
                out ResourceNode originalNode, out LootContainer originalContainer))
            {
                Fail("could not find a deterministic suburb chunk containing both a charged ResourceNode and an unsearched LootContainer");
                Finish();
                yield break;
            }

            string nodeId = StableHierarchyId(originalChunk.transform, originalNode.transform);
            string containerId = StableHierarchyId(originalChunk.transform, originalContainer.transform);
            int resourceBefore = player.GetComponent<Inventory>().GetCount(originalNode.resourceType.id);
            string lootResourceId = originalContainer.entries.First(entry => entry.resource != null && entry.amount > 0).resource.id;
            int lootBefore = player.GetComponent<Inventory>().GetCount(lootResourceId);

            // Drive the real gameplay paths: use one remaining charge so the actual harvest
            // depletes the node, then search a non-empty generated container exactly once.
            originalNode.RestoreState(1, false);
            originalNode.Interact(player.gameObject);
            Check(originalNode.IsDepleted && originalNode.remainingCharges == 0,
                $"harvesting the generated node depletes its final charge ({nodeId})");
            Check(player.GetComponent<Inventory>().GetCount(originalNode.resourceType.id) > resourceBefore,
                "the generated harvest transfers its resource into the player inventory");
            originalContainer.Interact(player.gameObject);
            Check(originalContainer.Searched, $"searching the generated LootContainer marks it searched ({containerId})");
            Check(player.GetComponent<Inventory>().GetCount(lootResourceId) > lootBefore,
                "searching the generated container transfers its rolled loot into the player inventory");

            yield return new WaitForSecondsRealtime(.1f);
            float expectedRegrowthElapsed = originalNode.RegrowthElapsed;
            Check(expectedRegrowthElapsed > 0f, "the depleted generated node begins accumulating regrowth time");

            // Force the world streamer to unload the changed chunk, then visit it again.
            // This tests the in-memory chunk history before we test F5/F9 persistence.
            PlacePlayerInChunk(WorldLayout.Coordinate(player.transform.position) + new Vector2Int(20, 20));
            float unloadDeadline = Time.realtimeSinceStartup + 15f;
            Vector3 chunkCenter = ChunkCenter(coordinate);
            while (Time.realtimeSinceStartup < unloadDeadline && world.HasGroundAt(chunkCenter)) yield return null;
            Check(!world.HasGroundAt(chunkCenter), "streaming unloads the chunk containing the harvested node and searched loot");
            GameObject streamedChunk = world.EnsureChunk(coordinate);
            ResourceNode streamedNode = streamedChunk != null ? FindNode(streamedChunk, nodeId) : null;
            LootContainer streamedContainer = streamedChunk != null ? FindContainer(streamedChunk, containerId) : null;
            Check(streamedNode != null && streamedNode.IsDepleted,
                "chunk revisit restores the harvested node's depleted state");
            Check(streamedNode != null && streamedNode.RegrowthElapsed >= expectedRegrowthElapsed - .05f,
                "chunk revisit preserves the node's regrowth timer");
            Check(streamedContainer != null && streamedContainer.Searched,
                "chunk revisit preserves the searched container state");
            if (streamedChunk == null || streamedNode == null || streamedContainer == null)
            {
                Finish();
                yield break;
            }

            yield return null;
            bool saved = InvokePath(save, "TrySaveToPath", tempSave);
            Check(saved && File.Exists(tempSave), "F5 writes harvested and searched generated-object state to the unique temporary save");
            if (!saved)
            {
                Finish();
                yield break;
            }

            // Change the runtime objects after F5. Re-searching must grant a duplicate only
            // temporarily; F9 must restore the saved inventory along with world state.
            int lootAfterSave = player.GetComponent<Inventory>().GetCount(lootResourceId);
            streamedNode.RestoreState(1, false);
            streamedContainer.RestoreSearchedState(false);
            streamedContainer.Interact(player.gameObject);
            Check(!streamedNode.IsDepleted && streamedNode.remainingCharges == 1 && streamedContainer.Searched &&
                  player.GetComponent<Inventory>().GetCount(lootResourceId) > lootAfterSave,
                "the loaded test fixture is demonstrably changed after the temporary save");

            bool loaded = InvokePath(save, "TryLoadFromPath", tempSave);
            Check(loaded, "F9 reads the temporary procedural-world save through SaveManager");
            if (!loaded)
            {
                Finish();
                yield break;
            }

            // RestoreWorld destroys the old chunk and retains its serialized delta in history.
            yield return null;
            Check(streamedChunk == null, "F9 discarded the generated chunk before it was rebuilt");
            Check(!world.HasGroundAt(chunkCenter), "the selected distant chunk is absent after the world reload");
            GameObject rebuiltChunk = world.EnsureChunk(coordinate);
            Check(rebuiltChunk != null, "EnsureChunk rebuilt the selected procedural coordinate after F9");
            if (rebuiltChunk != null)
            {
                ResourceNode restoredNode = FindNode(rebuiltChunk, nodeId);
                LootContainer restoredContainer = FindContainer(rebuiltChunk, containerId);
                Check(restoredNode != null, $"rebuilt generated ResourceNode matches stable hierarchy id {nodeId}");
                Check(restoredContainer != null, $"rebuilt generated LootContainer matches stable hierarchy id {containerId}");
                Check(restoredNode != null && restoredNode.IsDepleted && restoredNode.remainingCharges == 0,
                    "F9 restores the generated node's depleted state");
                Check(restoredNode != null && restoredNode.RegrowthElapsed >= expectedRegrowthElapsed - .1f,
                    "F9 restores the generated node's regrowth progress");
                Check(restoredContainer != null && restoredContainer.Searched,
                    "F9 restores the generated LootContainer searched state by hierarchy id");
                int restoredLoot = player.GetComponent<Inventory>().GetCount(lootResourceId);
                Check(restoredLoot == lootAfterSave,
                    "F9 restores inventory to the saved value without retaining the post-save duplicate loot");
            }

            Finish();
        }

        private bool TryFindCandidate(out Vector2Int coordinate, out GameObject chunk,
            out ResourceNode node, out LootContainer container)
        {
            coordinate = default;
            chunk = null;
            node = null;
            container = null;
            Vector2Int center = WorldLayout.Coordinate(player.transform.position);
            // Search a deterministic square spiral, outside the guaranteed 3x3 post-load area.
            // Restricting to suburbs gives both family-house loot and harvestable grass/trees.
            for (int radius = 2; radius <= 12; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dz) != radius) continue;
                    Vector2Int candidate = center + new Vector2Int(dx, dz);
                    if (!WorldLayout.IsSuburbChunk(candidate, world.Seed)) continue;

                    GameObject generated = world.EnsureChunk(candidate);
                    if (generated == null) continue;
                    ResourceNode candidateNode = generated.GetComponentsInChildren<ResourceNode>(true)
                        .FirstOrDefault(value => value != null && !value.IsDepleted && value.remainingCharges > 1);
                    LootContainer candidateContainer = generated.GetComponentsInChildren<LootContainer>(true)
                        .FirstOrDefault(value => value != null && !value.Searched && value.hasLoot && value.entries != null &&
                            value.entries.Any(entry => entry.resource != null && entry.amount > 0));
                    if (candidateNode == null || candidateContainer == null) continue;

                    coordinate = candidate;
                    chunk = generated;
                    node = candidateNode;
                    container = candidateContainer;
                    Debug.Log($"[ProceduralSaveProbe] selected suburb chunk {coordinate}, node={StableHierarchyId(generated.transform, candidateNode.transform)}, container={StableHierarchyId(generated.transform, candidateContainer.transform)}");
                    return true;
                }
            }
            return false;
        }

        private void PlacePlayerInChunk(Vector2Int coordinate)
        {
            CharacterController body = player.GetComponent<CharacterController>();
            bool enabled = body != null && body.enabled;
            if (enabled) body.enabled = false;
            Vector3 position = new Vector3((coordinate.x + .5f) * WorldLayout.ChunkSize, 0f,
                (coordinate.y + .5f) * WorldLayout.ChunkSize);
            position.y = WorldLayout.HeightAt(position.x, position.z, world.Seed) + 1.1f;
            player.transform.position = position;
            if (enabled) body.enabled = true;
            Physics.SyncTransforms();
        }

        private static Vector3 ChunkCenter(Vector2Int coordinate) =>
            new Vector3((coordinate.x + .5f) * WorldLayout.ChunkSize, 0f,
                (coordinate.y + .5f) * WorldLayout.ChunkSize);

        private static ResourceNode FindNode(GameObject chunk, string id) => chunk.GetComponentsInChildren<ResourceNode>(true)
            .FirstOrDefault(node => StableHierarchyId(chunk.transform, node.transform) == id);

        private static LootContainer FindContainer(GameObject chunk, string id) => chunk.GetComponentsInChildren<LootContainer>(true)
            .FirstOrDefault(container => StableHierarchyId(chunk.transform, container.transform) == id);

        /// <summary>Matches ProceduralWorldState's saved path format, including same-name sibling ordinals.</summary>
        private static string StableHierarchyId(Transform root, Transform target)
        {
            var segments = new System.Collections.Generic.List<string>();
            for (Transform current = target; current != root; current = current.parent)
            {
                if (current == null) throw new ArgumentException("The target must belong to the generated chunk.");
                int ordinal = 0;
                for (int i = 0; i < current.GetSiblingIndex(); i++)
                    if (current.parent.GetChild(i).name == current.name) ordinal++;
                segments.Add(Uri.EscapeDataString(current.name) + "[" + ordinal + "]");
            }
            segments.Reverse();
            return segments.Count == 0 ? "." : string.Join("/", segments);
        }

        private static bool InvokePath(SaveManager manager, string methodName, string path)
        {
            MethodInfo method = typeof(SaveManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(typeof(SaveManager).FullName, methodName);
            return (bool)method.Invoke(manager, new object[] { path });
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            DeleteTemp(tempSave);
            if (world != null && oldLoadRadius > 0) world.loadRadius = oldLoadRadius;
            Time.timeScale = oldTimeScale;
            Debug.Log(failures == 0 ? "[ProceduralSaveProbe] ALL CHECKS PASSED" : $"[ProceduralSaveProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private static void DeleteTemp(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            foreach (string candidate in new[] { path, path + ".bak", path + ".tmp" })
            {
                try { if (File.Exists(candidate)) File.Delete(candidate); }
                catch (Exception error) { Debug.LogWarning("[ProceduralSaveProbe] temp save cleanup: " + error.Message); }
            }
        }

        private void Check(bool condition, string message)
        {
            Debug.Log($"[ProceduralSaveProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("[ProceduralSaveProbe] FAIL: " + message);
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(180f);
            if (finished) yield break;
            Fail("timed out");
            Finish();
        }
    }
}
