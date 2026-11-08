using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Core;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--home-probe folder`: plays the opening for real in the built player. Spawn, normal world with nothing
    /// to find, savings, each stage, walking down the real stairs through the real door, the bombs,
    /// the ruined world, and saves on both sides of it. Writes screenshots. Run without -nographics.
    /// </summary>
    public sealed class HomeProbe : MonoBehaviour
    {
        private string folder;
        private int failures;
        private string savePath;
        private FirstPersonController player;
        private CharacterController body;
        private SaveManager save;
        private HomeBase home;
        private ProceduralWorld world;
        private GameStateManager state;
        private SurvivalStats stats;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--home-probe");
            if (index < 0) return;
            var probe = new GameObject("HomeProbe").AddComponent<HomeProbe>();
            probe.folder = index + 1 < args.Length ? args[index + 1] : Application.temporaryCachePath;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            savePath = Path.Combine(Application.temporaryCachePath, "home-probe-" + Guid.NewGuid().ToString("N") + ".json");
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            body = player.GetComponent<CharacterController>();
            save = FindFirstObjectByType<SaveManager>();
            home = HomeBase.Active;
            world = ProceduralWorld.Active;
            state = FindFirstObjectByType<GameStateManager>();
            stats = player.GetComponent<SurvivalStats>();
            Check(home != null && world != null && state != null, "Home site, world and game state exist");

            yield return Run(CheckSpawn());
            yield return Run(CheckNormalWorld());
            yield return Run(CheckTerminal());
            yield return Run(CheckStagesAndStairs());
            yield return Run(CheckBombs());
            yield return Run(CheckSaves());

            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[HomeProbe] ALL CHECKS PASSED" : $"[HomeProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator Run(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "Exception: " + error);
                    yield break;
                }
                yield return current;
            }
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(280f);
            Debug.LogError("[HomeProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private IEnumerator CheckSpawn()
        {
            yield return new WaitForSeconds(1.5f);
            Check(Mathf.Abs(player.transform.position.y - WorldLayout.HomeGroundY) < 0.2f, $"Player stands on the ground at the site (y={player.transform.position.y:F2})");
            Check(state.CurrentPhase == GamePhase.Building && stats.decayPaused, "Game starts before the apocalypse, needs paused");
            Check(home.Money == HomeBase.StartingMoney, "Savings at the start");
            Check(WorldLayout.UndergroundAmount(player.transform.position) == 0f, "Spawn is on the surface");

            // The real interaction ray at a natural slightly-down look.
            player.RestoreView(10f);
            yield return null;
            Camera camera = player.GetComponentInChildren<Camera>();
            bool hit = Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit info, 2.5f, ~0, QueryTriggerInteraction.Ignore);
            Check(hit && info.collider.GetComponentInParent<IInteractable>() is SiteTerminal, "Laptop is in reach and in view from the spawn");
            Shot("spawn", camera);
            Capture("site_overview", new Vector3(40f, 22f, -258f), new Vector3(76f, 6f, -226f));
            Capture("street", new Vector3(100f, 7.6f, -252f), new Vector3(170f, 8f, -252f));
            Capture("suburb_house", new Vector3(60f, 7.6f, -176f), new Vector3(66f, 8f, -150f));
            Capture("west_hill", new Vector3(52f, 7.6f, -240f), new Vector3(0f, 12f, -240f));
            Capture("town_edge_overhead", new Vector3(40f, 80f, -290f), new Vector3(190f, 5f, -220f));

            // The surface has no holes before the stairs exist.
            int holes = 0;
            for (float x = 60f; x <= 84f; x += 1f)
                for (float z = -244f; z <= -220f; z += 1f)
                    if (!Physics.Raycast(new Vector3(x, 30f, z), Vector3.down, out RaycastHit ground, 40f, ~0, QueryTriggerInteraction.Ignore) || ground.point.y < WorldLayout.HomeGroundY - 0.1f) holes++;
            Check(holes == 0, $"No hole in the ground on the site before excavation ({holes})");
        }

        private IEnumerator CheckNormalWorld()
        {
            foreach (var coordinate in new[] { new Vector2Int(0, -2), new Vector2Int(1, -2), new Vector2Int(1, -3), new Vector2Int(0, -3), new Vector2Int(-3, -3), new Vector2Int(-2, -3), new Vector2Int(6, -6) })
                world.EnsureChunk(coordinate);
            yield return null;
            Check(FindObjectsByType<LootContainer>(FindObjectsSortMode.None).Length == 0, "No loot in the normal world");
            Check(FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Length == 0, "No raiders in the normal world");
            Check(FindObjectsByType<WorldWildlife>(FindObjectsSortMode.None).Length == 0, "No animals in the normal world");
            Check(FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).Length == 0, "Nothing to gather in the normal world");
            int houses = world.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("House_", StringComparison.Ordinal));
            int buildings = world.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Building_", StringComparison.Ordinal));
            int trees = world.GetComponentsInChildren<CapsuleCollider>().Length;
            Debug.Log($"[HomeProbe] houses={houses} buildings={buildings} trees={trees} seedBiomeAtHome={WorldLayout.BiomeIndexAt(72f, -230f, world.Seed)}");
            Check(houses >= 4, "Family houses stand near the site");
            Check(buildings >= 4, "Town blocks stand nearby");
            Check(trees > 0, "Trees exist");
            Check(!WorldLayout.IsTownChunk(WorldLayout.Coordinate(home.transform.position), world.Seed), "The site is on the edge, not in the town blocks");
        }

        private IEnumerator CheckTerminal()
        {
            var terminal = FindFirstObjectByType<SiteTerminal>();
            terminal.Interact(player.gameObject);
            yield return null;
            Check(ModalUI.IsOpen && BunkerTerminalUI.IsOpen && Time.timeScale == 0f, "Terminal is a modal: game paused, cursor free");
            Check(Cursor.lockState == CursorLockMode.None, "Cursor free in the terminal");
            Button start = FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.transform.parent.name == "Stage0");
            Button locked = FindObjectsByType<Button>(FindObjectsSortMode.None).First(b => b.transform.parent.name == "Stage3");
            Check(start.interactable && !locked.interactable, "Only the next stage can be started");

            int spendableBefore = home.SpendableMoney;
            Button[] items = FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b => b.transform.parent.name.StartsWith("Item_", StringComparison.Ordinal)).ToArray();
            Check(items.Length >= 10, $"Shop lists goods ({items.Length})");
            items[0].onClick.Invoke();
            Check(home.SpendableMoney < spendableBefore, "Buying costs money");
            Check(player.GetComponent<Inventory>().GetAllCounts().Values.Sum() > 0, "Bought goods arrive in the inventory");
            ShotUI("terminal_ui", "TerminalCanvas");

            // Spending everything possible must still leave enough for the bunker.
            for (int guard = 0; guard < 5000 && home.SpendableMoney > 0; guard++)
            {
                var cheapest = SheltreSim.Crafting.ItemCatalog.Shop.OrderBy(e => e.price).First();
                if (!home.TryBuy(cheapest, out _)) break;
            }
            Check(home.Money >= home.ReservedForBunker, $"The shop cannot eat the bunker's money (${home.Money} left, ${home.ReservedForBunker} needed)");
            var expensive = SheltreSim.Crafting.ItemCatalog.Shop.OrderByDescending(e => e.price).First();
            if (home.SpendableMoney < expensive.price) Check(!home.TryBuy(expensive, out _), "Unaffordable purchase is refused");
            home.Restore(HomeBase.StartingMoney, 0, false, 0f, false);
            player.GetComponent<Inventory>().Clear();

            start.onClick.Invoke();
            Check(home.IsBuilding && home.Money == HomeBase.StartingMoney - HomeBase.Stages[0].price, "Clicking Start pays and starts the crew");
            ModalUI.DismissCurrent();
            yield return null;
            Check(!ModalUI.IsOpen && Time.timeScale == 1f && (Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked), "Closing the terminal gives the game back");
        }

        private IEnumerator WaitForCrew()
        {
            Time.timeScale = 6f;
            float limit = Time.realtimeSinceStartup + 30f;
            while (home.IsBuilding && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = 1f;
        }

        private IEnumerator CheckStagesAndStairs()
        {
            yield return WaitForCrew();
            Check(home.CompletedStages == 1 && !home.IsBuilding, "Stage one finishes on its own");
            Check(home.stageVisuals[0].activeSelf, "Excavation shows on the site");

            // Closed entrance holds the player's weight.
            Teleport(new Vector3(72f, WorldLayout.HomeGroundY + 0.2f, -236f));
            yield return new WaitForSeconds(0.5f);
            Check(player.transform.position.y > WorldLayout.HomeGroundY - 0.2f, "Entrance is covered until the stairs are bought");

            Check(home.TryStartNextStage(out _), "Stage two starts");
            Check(Invoke("TrySaveToPath"), "Save in the middle of construction");
            float elapsed = home.BuildElapsed;
            yield return WaitForCrew();
            Check(home.CompletedStages == 2, "Stage two finishes");
            Check(Invoke("TryLoadFromPath"), "Load back to the middle of construction");
            yield return null;
            Check(home.CompletedStages == 1 && home.IsBuilding && Mathf.Abs(home.BuildElapsed - elapsed) < 1f && home.Money == HomeBase.StartingMoney - HomeBase.Stages[0].price - HomeBase.Stages[1].price,
                "Load restores the stage, its progress and the money");
            Check(!home.stageVisuals[1].activeSelf, "Load removes what was not built yet");
            yield return WaitForCrew();
            Check(home.CompletedStages == 2, "Interrupted stage finishes after the load");

            Check(home.TryBuyAnnex(out _), "Annex can be bought after the shell");
            Check(home.TryStartNextStage(out _), "Stage three starts");
            yield return WaitForCrew();
            Check(home.CompletedStages == 3 && !home.entranceCover.activeSelf, "Stairs are open");
            Camera camera = player.GetComponentInChildren<Camera>();

            // Really walk down, with the same controller and sweep the game uses.
            Teleport(new Vector3(72f, WorldLayout.HomeGroundY + 0.1f, -240f));
            player.RestoreView(5f);
            yield return null;
            Shot("stair_entrance", camera);
            InteractableDoor door = FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None).First();
            door.RestoreState(false);
            yield return Walk(new Vector3(0f, 0f, 3.5f), 7f);
            Shot("stairs_down", camera);
            Check(player.transform.position.z > -225.5f && player.transform.position.y < 0.3f, $"Walked down to the door (at {player.transform.position:F2})");
            Check(player.transform.position.z < RoomDoorZ, "Closed blast door stops the player");
            door.RestoreState(true);
            yield return Walk(new Vector3(0f, 0f, 3.5f), 3f);
            Check(player.transform.position.z > -222f && player.transform.position.y < 0.3f, $"Open door lets the player into the bunker (at {player.transform.position:F2})");
            Check(WorldLayout.UndergroundAmount(camera.transform.position) >= 1f, "Inside the bunker counts as underground");
            Check(FindFirstObjectByType<WorldAtmosphere>() != null, "Weather simulation exists");
            yield return new WaitForSeconds(0.7f);
            Check(FindFirstObjectByType<WorldAtmosphere>().IsSheltered, "Bunker shelters from the weather");
            Shot("bunker_inside", camera);
            Capture("bunker_wide", new Vector3(77.8f, 2.2f, -222.6f), new Vector3(68f, 0.6f, -213f));
            Capture("bunker_door", new Vector3(72f, 1.6f, -216f), new Vector3(72f, 1.2f, -224f));

            // Annex opening.
            yield return Walk(new Vector3(0f, 0f, 2f), 1.5f);
            Teleport(new Vector3(76f, 0.1f, -217.5f));
            yield return Walk(new Vector3(3f, 0f, 0f), 3f);
            Check(player.transform.position.x > 80.5f, $"Annex can be walked into (at {player.transform.position:F2})");
            Capture("bunker_annex", new Vector3(81f, 1.6f, -217.5f), new Vector3(87f, 1f, -217.5f));

            // And back up to the surface.
            Teleport(new Vector3(72f, 0.1f, -222.5f));
            yield return Walk(new Vector3(0f, 0f, -4.5f), 6f);
            Check(player.transform.position.y > WorldLayout.HomeGroundY - 0.1f, $"Climbed the stairs back to the surface (at {player.transform.position:F2})");

            // A raider's bigger capsule fits too.
            GameObject raider = Instantiate(world.assets.raiderPrefab, new Vector3(72f, WorldLayout.HomeGroundY + 0.3f, -240f), Quaternion.identity);
            raider.GetComponent<EnemyAI>().enabled = false;
            CharacterController raiderBody = raider.GetComponent<CharacterController>();
            raiderBody.enabled = true;
            yield return null;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                raiderBody.Move((Vector3.forward * 3f + Vector3.down * 5f) * Time.deltaTime);
                yield return null;
            }
            Check(raider.transform.position.z > -222f && raider.transform.position.y < 1.3f, $"A raider fits down the stairs and through the door (at {raider.transform.position:F2})");
            Destroy(raider);
            yield return null;
        }

        private const float RoomDoorZ = -223.5f;

        private IEnumerator CheckBombs()
        {
            // Underground when they fall: no blast damage.
            Teleport(new Vector3(72f, 0.1f, -218f));
            Check(home.TryStartNextStage(out _), "Last stage starts");
            yield return WaitForCrew();
            Check(home.BunkerComplete && state.CurrentPhase == GamePhase.Apocalypse, "Finishing the bunker starts the apocalypse");
            Check(!world.Ruined, "The world stays normal during the warning");
            Check(FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Length == 0, "No raiders during the warning");
            float health = stats.health;
            Time.timeScale = 6f;
            float limit = Time.realtimeSinceStartup + 20f;
            while (state.CurrentPhase != GamePhase.Survival && Time.realtimeSinceStartup < limit) yield return null;
            Time.timeScale = 1f;
            Check(state.CurrentPhase == GamePhase.Survival && world.Ruined, "The bombs ruin the world");
            Check(!stats.decayPaused, "Needs start to drain");
            Check(stats.health == health, "Underground the blast does no damage");
            yield return new WaitForSeconds(4f);
            foreach (var coordinate in new[] { new Vector2Int(0, -2), new Vector2Int(1, -2), new Vector2Int(1, -3), new Vector2Int(6, -6), new Vector2Int(15, 15), new Vector2Int(16, 15), new Vector2Int(17, 15) })
                world.EnsureChunk(coordinate);
            yield return null;
            int loot = FindObjectsByType<LootContainer>(FindObjectsSortMode.None).Length;
            int nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).Length;
            int animals = FindObjectsByType<WorldWildlife>(FindObjectsSortMode.None).Length;
            int raiders = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Length;
            Debug.Log($"[HomeProbe] after the bombs: loot={loot} nodes={nodes} animals={animals} raiders={raiders}");
            Check(loot > 5, "Loot exists after the bombs");
            Check(nodes > 5, "Things to gather exist after the bombs");
            Check(animals > 0, "Animals exist after the bombs");
            Check(raiders > 0, "Raiders exist after the bombs");
            Check(FindObjectsByType<LootContainer>(FindObjectsSortMode.None).All(l => l.entries.Count > 0 || !l.hasLoot), "Crates hold real entries");
            Teleport(new Vector3(60f, WorldLayout.HomeGroundY + 0.1f, -241f));
            player.RestoreView(0f);
            yield return new WaitForSeconds(8f); // Daylight comes back gradually after the bunker.
            Capture("ruined_site", new Vector3(40f, 22f, -258f), new Vector3(76f, 6f, -226f));
            Capture("ruined_town", new Vector3(100f, 7.6f, -252f), new Vector3(170f, 8f, -252f));
            Capture("ruined_house", new Vector3(60f, 7.6f, -176f), new Vector3(66f, 8f, -150f));
            var inventoryUi = FindFirstObjectByType<InventoryUI>();
            var gearInventory = player.GetComponent<Inventory>();
            foreach (var entry in SheltreSim.Crafting.ItemCatalog.Shop) gearInventory.Add(entry.id, 3);
            inventoryUi.Open();
            yield return null;
            ShotUI("inventory_ui", "Inventory_Canvas");
            inventoryUi.CloseIfOpen();
            var crafting = SheltreSim.Crafting.CraftingUI.Instance;
            Check(crafting != null, "Crafting window exists");
            if (crafting != null)
            {
                crafting.Open(SheltreSim.Crafting.StationKind.Workbench);
                yield return null;
                ShotUI("crafting_ui", "Crafting_Canvas");
                crafting.Close();
            }
            yield return null;
            Check(FindFirstObjectByType<SiteTerminal>().InteractPrompt.Contains("dead"), "The laptop is dead after the bombs");
        }

        private IEnumerator CheckSaves()
        {
            string survivalPath = savePath + ".s";
            string earlier = savePath;
            Check(Invoke("TrySaveToPath", survivalPath), "Save after the bombs");
            int discoveries = 0;
            stats.hunger = 11f;
            Check(Invoke("TryLoadFromPath", earlier), "Load a save from before the bombs");
            yield return null;
            yield return null;
            Check(state.CurrentPhase == GamePhase.Building && !world.Ruined && stats.decayPaused, "Loading back undoes the apocalypse");
            Check(FindObjectsByType<LootContainer>(FindObjectsSortMode.None).Count(l => l.gameObject.activeInHierarchy) == 0, "No loot again in the older save");
            Check(FindObjectsByType<EnemyAI>(FindObjectsSortMode.None).Count(e => e.gameObject.activeInHierarchy) == 0, "No raiders again in the older save");
            Check(Invoke("TryLoadFromPath", survivalPath), "Load the save from after the bombs");
            yield return null;
            yield return null;
            Check(state.CurrentPhase == GamePhase.Survival && world.Ruined && !stats.decayPaused, "Loading forward restores the ruined world");
            Check(home.BunkerComplete && home.HasAnnex, "Bunker and annex stay built");
            Check(!home.entranceCover.activeSelf, "Stairs stay open");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(survivalPath + suffix)) File.Delete(survivalPath + suffix);
            _ = discoveries;
        }

        private IEnumerator Walk(Vector3 velocity, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                body.Move((velocity + Vector3.down * 4f) * Time.deltaTime);
                yield return null;
            }
        }

        private void Teleport(Vector3 position)
        {
            body.enabled = false;
            player.transform.position = position;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private bool Invoke(string method, string path = null)
        {
            var action = typeof(SaveManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)action.Invoke(save, new object[] { path ?? savePath });
        }

        private void Capture(string name, Vector3 eye, Vector3 target)
        {
            var cameraObject = new GameObject("ProbeCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(player.GetComponentInChildren<Camera>());
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            Render(name, camera);
            cameraObject.SetActive(false);
            Destroy(cameraObject);
        }

        private void Shot(string name, Camera camera)
        {
            if (camera != null) { Render(name, camera); return; }
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        }

        /// <summary>Overlay canvases don't draw into a camera in batch mode, so borrow one for the picture.</summary>
        private void ShotUI(string name, string canvasName)
        {
            Canvas canvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None).FirstOrDefault(c => c.name == canvasName);
            if (canvas == null) { Check(false, "Canvas " + canvasName + " exists"); return; }
            Camera camera = player.GetComponentInChildren<Camera>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            Canvas.ForceUpdateCanvases();
            Render(name, camera);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        private void Render(string name, Camera camera)
        {
            const int width = 1280, height = 720;
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            target.Release();
            Destroy(target);
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[HomeProbe] PASS: " : "[HomeProbe] FAIL: ") + message);
        }
    }
}
