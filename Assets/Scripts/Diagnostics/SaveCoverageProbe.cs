using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run with --save-coverage-probe in an isolated player. Exercises a real temporary F5/F9
    /// round-trip for searched furniture, an open door, a kit-placed piece, and vehicle cargo/pose.
    /// Never reads or overwrites the player's default save slot.
    /// </summary>
    public sealed class SaveCoverageProbe : MonoBehaviour
    {
        private int failures;
        private FirstPersonController player;
        private Inventory inventory;
        private ProceduralWorld world;
        private GameObject probeRoot;
        private string tempSave;
        private string tempAutosave;
        private int oldLoadRadius;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--save-coverage-probe") >= 0)
                new GameObject(nameof(SaveCoverageProbe)).AddComponent<SaveCoverageProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;

            player = FindFirstObjectByType<FirstPersonController>();
            inventory = player != null ? player.GetComponent<Inventory>() : null;
            world = ProceduralWorld.Active;
            BuildModeController build = player != null ? player.GetComponentInChildren<BuildModeController>() : null;
            SaveManager save = FindFirstObjectByType<SaveManager>();
            if (save == null) save = new GameObject("SaveCoverageProbeManager").AddComponent<SaveManager>();
            if (player == null || inventory == null || build == null || save == null)
            {
                Fail("required player, inventory, build system, or SaveManager is missing");
                Finish();
                yield break;
            }

            tempSave = Path.Combine(Application.temporaryCachePath, "sheltersim_save_coverage_probe.json");
            tempAutosave = Path.Combine(Application.temporaryCachePath, "sheltersim_save_coverage_autosave.json");
            probeRoot = new GameObject("SaveCoverageProbeObjects");
            Vector3 anchor = player.transform.position + new Vector3(18f, 0f, 12f);
            ResourceDefinition wood = world != null && world.assets != null ? world.assets.wood : null;
            if (wood == null)
            {
                Fail("procedural-world wood resource is missing");
                Finish();
                yield break;
            }

            // A drawer with actual loot checks both persisted open pose and no double-looting.
            var furnitureObject = new GameObject("DrawerFurniture");
            furnitureObject.transform.SetParent(probeRoot.transform, false);
            furnitureObject.transform.position = anchor;
            var drawer = new GameObject("Drawer_SaveProbe").transform;
            drawer.SetParent(furnitureObject.transform, false);
            drawer.localPosition = new Vector3(0f, 0.8f, 0f);
            Vector3 closedDrawerPosition = drawer.localPosition;
            LootContainer furniture = furnitureObject.AddComponent<LootContainer>();
            furniture.entries.Add(new LootEntry { resource = wood, amount = 3 });
            int woodBefore = inventory.GetCount(wood.id);
            furniture.Interact(player.gameObject);
            Check(furniture.Searched && inventory.GetCount(wood.id) == woodBefore + 3,
                "searching the drawer transfers its three wood into the player inventory");

            var doorObject = new GameObject("OpenDoor");
            doorObject.transform.SetParent(probeRoot.transform, false);
            doorObject.transform.position = anchor + Vector3.right * 3f;
            BoxCollider doorCollider = doorObject.AddComponent<BoxCollider>();
            doorCollider.size = new Vector3(1.8f, 2f, 0.2f);
            InteractableDoor door = doorObject.AddComponent<InteractableDoor>();
            door.RestoreState(true);

            GameObject kit = InstantiateDefinition(build, "workbench", anchor + Vector3.right * 6f, out string kitError);
            if (kit == null) Fail("could not prepare a kit-placed workbench: " + kitError);
            else
            {
                PlacedPiece tag = kit.GetComponent<PlacedPiece>();
                tag.wasPlacedFromKit = true;
            }

            GameObject vehicleObject = InstantiateDefinition(build, "piece_scout_buggy", anchor + Vector3.right * 10f, out string vehicleError);
            RideableScoutBuggy buggy = vehicleObject != null ? vehicleObject.GetComponent<RideableScoutBuggy>() : null;
            if (buggy == null) Fail("could not prepare a placed Scout Buggy: " + vehicleError);
            else
            {
                vehicleObject.GetComponent<PlacedPiece>().wasPlacedFromKit = true;
                buggy.Add(wood.id, 7);
            }

            yield return null; // allow active objects to register before snapshot capture
            bool saved = InvokePath(save, "TrySaveToPath", tempSave);
            Check(saved, "F5 serialization succeeds through SaveManager using a temporary path");
            if (!saved) { Finish(); yield break; }

            // Mutate all expected restored values after F5.
            inventory.SetCount(wood.id, woodBefore);
            furniture.RestoreSearchedState(false);
            door.RestoreState(false);
            kit.transform.position += Vector3.one * 40f;
            if (kit != null) kit.GetComponent<PlacedPiece>().wasPlacedFromKit = false;
            vehicleObject.transform.position += Vector3.one * 50f;
            if (buggy != null) buggy.TryTake(wood.id, 7);

            bool loaded = InvokePath(save, "TryLoadFromPath", tempSave);
            Check(loaded, "F9 deserialization succeeds through SaveManager using the temporary path");
            if (!loaded) { Finish(); yield break; }
            yield return null;
            yield return null;

            Check(furniture.Searched && inventory.GetCount(wood.id) == woodBefore + 3,
                "F9 restores the searched drawer and the one-time loot in inventory");
            Check(Vector3.Distance(drawer.localPosition, closedDrawerPosition) > 0.1f,
                "F9 restores the searched furniture's open drawer pose");
            furniture.Interact(player.gameObject);
            Check(inventory.GetCount(wood.id) == woodBefore + 3,
                "an already-looted drawer cannot duplicate its item after F9");
            Check(door.IsOpen && !doorCollider.enabled, "F9 restores an open door and its nonblocking collider state");

            PlacedPiece restoredKit = FindPlaced("workbench", anchor + Vector3.right * 6f);
            Check(restoredKit != null && restoredKit.wasPlacedFromKit,
                "F9 restores the placed workbench at its saved pose with kit origin preserved");
            PlacedPiece restoredVehiclePiece = FindPlaced("piece_scout_buggy", anchor + Vector3.right * 10f);
            RideableScoutBuggy restoredBuggy = restoredVehiclePiece != null
                ? restoredVehiclePiece.GetComponent<RideableScoutBuggy>() : null;
            Check(restoredVehiclePiece != null && restoredVehiclePiece.wasPlacedFromKit &&
                  Vector3.Distance(restoredVehiclePiece.transform.position, anchor + Vector3.right * 10f) < 0.2f,
                "F9 restores the kit-placed Scout Buggy at its saved world pose");
            Check(restoredBuggy != null && restoredBuggy.GetCount(wood.id) == 7,
                "F9 restores the Scout Buggy trunk contents");

            yield return CheckKitSurvivesChunkUnload(world, restoredKit, restoredVehiclePiece);
            yield return CheckMountedSaveIsRefused(save, restoredBuggy);
            CheckAutosaveSlotIsolationAndRecovery(save);
            Finish();
        }

        private IEnumerator CheckMountedSaveIsRefused(SaveManager save, RideableScoutBuggy buggy)
        {
            if (save == null || buggy == null || !File.Exists(tempSave))
            {
                Fail("cannot check mounted-save behavior without the restored buggy and baseline save");
                yield break;
            }

            byte[] baseline = File.ReadAllBytes(tempSave);
            buggy.Interact(player.gameObject);
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < deadline && !player.IsRiding)
                yield return null;

            Check(player.IsRiding && buggy.IsMounted,
                "test rider mounts the restored buggy before attempting F5");
            bool savedWhileMounted = InvokePath(save, "TrySaveToPath", tempSave);
            Check(!savedWhileMounted, "F5 refuses to replace the save while the player is mounted");
            Check(File.Exists(tempSave) && File.ReadAllBytes(tempSave).SequenceEqual(baseline),
                "refused mounted F5 leaves the previous save byte-for-byte intact");
            Check(player.IsRiding && buggy.IsMounted,
                "refused mounted F5 leaves the player and vehicle mounted in the current session");

            MethodInfo dismount = typeof(RideableScoutBuggy).GetMethod("Dismount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (dismount != null) dismount.Invoke(buggy, new object[] { true });
            yield return null;
            Check(!player.IsRiding, "probe cleanly dismounts after the mounted-save regression check");
        }

        private IEnumerator CheckKitSurvivesChunkUnload(ProceduralWorld activeWorld, PlacedPiece kit, PlacedPiece vehicle)
        {
            if (activeWorld == null || kit == null || vehicle == null)
            {
                Fail("cannot run chunk-unload persistence check without the restored world and both placed pieces");
                yield break;
            }

            Vector2Int originalChunk = WorldLayout.Coordinate(kit.transform.position);
            activeWorld.EnsureChunk(originalChunk);
            oldLoadRadius = activeWorld.loadRadius;
            activeWorld.loadRadius = 1;
            Vector3 originalChunkCenter = new Vector3((originalChunk.x + .5f) * WorldLayout.ChunkSize, 0f,
                (originalChunk.y + .5f) * WorldLayout.ChunkSize);

            CharacterController body = player.GetComponent<CharacterController>();
            bool enabled = body != null && body.enabled;
            if (enabled) body.enabled = false;
            Vector2Int farChunk = originalChunk + new Vector2Int(10, 0);
            Vector3 farPosition = new Vector3((farChunk.x + .5f) * WorldLayout.ChunkSize, 0f,
                (farChunk.y + .5f) * WorldLayout.ChunkSize);
            farPosition.y = WorldLayout.HeightAt(farPosition.x, farPosition.z, activeWorld.Seed) + 1.1f;
            player.transform.position = farPosition;
            if (enabled) body.enabled = true;

            float deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline && activeWorld.HasGroundAt(originalChunkCenter))
                yield return null;

            bool unloaded = !activeWorld.HasGroundAt(originalChunkCenter);
            Check(unloaded, "streaming unloads the original chunk after the player travels away");
            Check(kit != null && kit.gameObject.activeInHierarchy && vehicle != null && vehicle.gameObject.activeInHierarchy,
                "placed kit and vehicle remain alive when their placement chunk unloads");
            Check(kit != null && kit.wasPlacedFromKit && vehicle != null && vehicle.wasPlacedFromKit,
                "kit-origin flags survive the chunk unload");
        }

        private void CheckAutosaveSlotIsolationAndRecovery(SaveManager save)
        {
            if (save == null || string.IsNullOrEmpty(tempAutosave))
            {
                Fail("cannot check autosave rotation without a manager and temporary slot");
                return;
            }
            bool manualSaved = InvokePath(save, "TrySaveToPath", tempSave);
            byte[] manual = manualSaved && File.Exists(tempSave) ? File.ReadAllBytes(tempSave) : null;
            bool first = InvokePath(save, "TryAutosaveToPath", tempAutosave);
            bool second = InvokePath(save, "TryAutosaveToPath", tempAutosave);
            Check(first && second && File.Exists(tempAutosave + ".bak"),
                "autosave writes a distinct rotating slot with a recoverable previous copy");
            Check(manual != null && File.Exists(tempSave) && File.ReadAllBytes(tempSave).SequenceEqual(manual),
                "autosave rotation leaves the manual F5 slot byte-for-byte unchanged");
            if (!File.Exists(tempAutosave + ".bak")) return;
            File.WriteAllText(tempAutosave, "corrupt autosave");
            bool recovered = InvokePath(save, "TryLoadFromPath", tempAutosave);
            Check(recovered, "autosave load recovers its backup when the newest autosave is corrupt");
        }

        private static GameObject InstantiateDefinition(BuildModeController build, string id, Vector3 position, out string error)
        {
            BuildPieceDefinition definition = build.availablePieces.FirstOrDefault(piece => piece != null && piece.id == id);
            if (definition == null || definition.prefab == null)
            {
                error = "build definition or prefab is missing for " + id;
                return null;
            }
            GameObject instance = Instantiate(definition.prefab, position, Quaternion.identity);
            if (instance == null || instance.GetComponent<PlacedPiece>() == null)
            {
                if (instance != null) Destroy(instance);
                error = "prefab has no PlacedPiece tag";
                return null;
            }
            error = null;
            return instance;
        }

        private static PlacedPiece FindPlaced(string id, Vector3 position)
        {
            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
                if (piece != null && piece.pieceId == id && Vector3.Distance(piece.transform.position, position) < 0.2f)
                    return piece;
            return null;
        }

        private static bool InvokePath(SaveManager manager, string methodName, string path)
        {
            MethodInfo method = typeof(SaveManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(typeof(SaveManager).FullName, methodName);
            return (bool)method.Invoke(manager, new object[] { path });
        }

        private void Finish()
        {
            if (!string.IsNullOrEmpty(tempSave))
            {
                try { if (File.Exists(tempSave)) File.Delete(tempSave); } catch (Exception error) { Debug.LogWarning("[SaveCoverageProbe] temp save cleanup: " + error.Message); }
                try { if (File.Exists(tempSave + ".bak")) File.Delete(tempSave + ".bak"); } catch (Exception) { }
            }
            if (!string.IsNullOrEmpty(tempAutosave))
            {
                try { if (File.Exists(tempAutosave)) File.Delete(tempAutosave); } catch (Exception) { }
                try { if (File.Exists(tempAutosave + ".bak")) File.Delete(tempAutosave + ".bak"); } catch (Exception) { }
            }
            if (world != null && oldLoadRadius > 0) world.loadRadius = oldLoadRadius;
            if (probeRoot != null) Destroy(probeRoot);
            Debug.Log(failures == 0 ? "[SaveCoverageProbe] ALL CHECKS PASSED" : $"[SaveCoverageProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private void Check(bool condition, string message)
        {
            Debug.Log($"[SaveCoverageProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("[SaveCoverageProbe] FAIL: " + message);
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(120f);
            Fail("timed out");
            Finish();
        }
    }
}
