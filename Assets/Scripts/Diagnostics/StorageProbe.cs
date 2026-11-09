using System;
using System.Collections;
using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--storage-probe`: crates, moving items, carrying weight and crafting from nearby crates, on the real player in
    /// the built player, during the safe pre-apocalypse phase. Never saves. Works with or without -nographics.
    /// </summary>
    public sealed class StorageProbe : MonoBehaviour
    {
        private int failures;
        private FirstPersonController player;
        private Inventory pack;
        private readonly List<GameObject> spawned = new List<GameObject>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--storage-probe") < 0) return;
            new GameObject("StorageProbe").AddComponent<StorageProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            pack = player != null ? player.GetComponent<Inventory>() : null;
            Check(player != null && pack != null, "The player and the pack exist");
            if (player == null || pack == null) { Finish(); yield break; }
            yield return new WaitForSeconds(1f); // let the player settle on the ground

            // The starting crate is real content: take it out of the way so distances and totals are only this probe's.
            foreach (IItemStore store in new List<IItemStore>(StorageRegistry.All))
                if (store is Component component) component.gameObject.SetActive(false);
            Check(StorageRegistry.All.Count == 0, "No crates in play at the start of the checks");

            yield return Run(CheckContents());
            yield return Run(CheckConservation());
            yield return Run(CheckEquipped());
            yield return Run(CheckSaving());
            yield return Run(CheckWeights());
            yield return Run(CheckEncumbrance());
            yield return Run(CheckCrafting());
            yield return Run(CheckCraftingUI());
            yield return Run(CheckLifecycle());
            yield return Run(CheckStorageUI());
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[StorageProbe] ALL CHECKS PASSED" : $"[StorageProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator Run(IEnumerator routine)
        {
            pack.Clear();
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "Exception: " + error);
                    break;
                }
                yield return current;
            }
            // Each section starts clean, whatever the last one left behind.
            if (StorageUI.Instance != null) StorageUI.Instance.Close();
            foreach (GameObject item in spawned)
                if (item != null) { item.SetActive(false); Destroy(item); }
            spawned.Clear();
            yield return null;
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(110f);
            Debug.LogError("[StorageProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private StorageCratePiece MakeCrate(float metersAway = 3f)
        {
            var item = new GameObject("ProbeCrate");
            item.transform.position = player.transform.position + Vector3.right * metersAway;
            spawned.Add(item);
            return item.AddComponent<StorageCratePiece>();
        }

        private static ResourceDefinition Fake(string id)
        {
            var definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            definition.id = id;
            return definition;
        }

        private static List<CraftingCost> Cost(params (ResourceDefinition resource, int amount)[] lines)
        {
            var cost = new List<CraftingCost>();
            foreach (var line in lines) cost.Add(new CraftingCost { resource = line.resource, amount = line.amount });
            return cost;
        }

        // ---------- Contents ----------

        private IEnumerator CheckContents()
        {
            StorageCratePiece a = MakeCrate(), b = MakeCrate(4f);
            a.Add("wood", 5);
            Check(a.GetCount("wood") == 5 && b.GetCount("wood") == 0 && b.IsEmpty, "Two crates don't share contents");
            StorageCratePiece clone = Instantiate(a.gameObject).GetComponent<StorageCratePiece>();
            spawned.Add(clone.gameObject);
            Check(clone.IsEmpty, "A copy of a crate starts empty, so one prefab can't share contents");
            clone.Add("stone", 1);
            Check(a.GetCount("stone") == 0, "Adding to the copy doesn't touch the original");
            Check(a.InteractPrompt == "Press E to open the crate (5 items)", "Prompt counts items: " + a.InteractPrompt);
            Check(!a.TryTake("wood", 6) && a.GetCount("wood") == 5, "Taking too many takes nothing");
            Check(!a.TryTake("wood", -1) && a.GetCount("wood") == 5, "A negative take does nothing");
            a.Add("wood", -3);
            a.Add(" ", 3);
            Check(a.GetCount("wood") == 5 && a.Contents.Count == 1, "Negative amounts and blank ids are ignored");
            Check(a.TryTake("wood", 5) && a.IsEmpty, "An emptied stack disappears");
            pack.SetCount("wood", 100);
            Check(StorageTransfer.Store(pack, a, "wood", 0) == 0 && StorageTransfer.Store(pack, a, "wood", -5) == 0
                && pack.GetCount("wood") == 100, "Zero and negative moves do nothing");
            yield break;
        }

        private IEnumerator CheckConservation()
        {
            StorageCratePiece a = MakeCrate(), b = MakeCrate(4f);
            string[] ids = { "wood", "stone", "planks", "nails", "canned_food", "bandage", "ammo_9mm", "iron_axe" };
            var totals = new Dictionary<string, long>();
            foreach (string id in ids) { pack.SetCount(id, 137); totals[id] = 137; }
            var random = new System.Random(22022);
            int[] amounts = { 1, 10, StorageTransfer.All, 3, 0, -7, 50 };
            int broken = 0, moved = 0;
            for (int step = 0; step < 1000; step++)
            {
                StorageCratePiece crate = random.Next(2) == 0 ? a : b;
                string id = ids[random.Next(ids.Length)];
                int amount = amounts[random.Next(amounts.Length)];
                switch (random.Next(6))
                {
                    case 0: case 1: moved += StorageTransfer.Store(pack, crate, id, amount); break;
                    case 2: case 3: moved += StorageTransfer.Take(pack, crate, id, amount); break;
                    case 4: moved += (int)StorageTransfer.StoreBuildingMaterials(pack, crate); break;
                    default: moved += (int)StorageTransfer.TakeAll(pack, crate); break;
                }
                foreach (string item in ids)
                {
                    int x = pack.GetCount(item), y = a.GetCount(item), z = b.GetCount(item);
                    if (x < 0 || y < 0 || z < 0 || (long)x + y + z != totals[item]) broken++;
                }
            }
            Check(moved > 0, $"The random run really moved items ({moved})");
            Check(broken == 0, $"1000 random moves over two crates conserve every total ({broken} violations)");

            StorageTransfer.TakeAll(pack, a);
            StorageTransfer.TakeAll(pack, b);
            pack.SetCount("canned_food", 5); pack.SetCount("first_aid", 2); pack.SetCount("ammo_9mm", 40); pack.SetCount("energy_cell", 3);
            pack.SetCount("machete", 1); pack.SetCount("hammer", 1); pack.SetCount("steel_plate_armor", 1);
            pack.SetCount("wood", 9); pack.SetCount("planks", 8); pack.SetCount("raw_meat", 2);
            long stored = StorageTransfer.StoreBuildingMaterials(pack, a);
            Check(a.GetCount("wood") == 9 && a.GetCount("planks") == 8 && pack.GetCount("wood") == 0, $"Store building materials moves raw and refined ({stored})");
            Check(pack.GetCount("canned_food") == 5 && pack.GetCount("first_aid") == 2 && pack.GetCount("ammo_9mm") == 40 && pack.GetCount("energy_cell") == 3,
                "Consumables and ammo stay in the pack");
            Check(pack.GetCount("machete") == 1 && pack.GetCount("hammer") == 1 && pack.GetCount("steel_plate_armor") == 1, "Tools and gear stay in the pack");
            yield break;
        }

        private IEnumerator CheckEquipped()
        {
            PlayerGear gear = PlayerGear.Ensure(player.gameObject);
            StorageCratePiece crate = MakeCrate();
            pack.SetCount("wooden_club", 1);
            Check(gear.Equip("wooden_club", out _), "A club can be equipped");
            Check(StorageTransfer.Store(pack, crate, "wooden_club", StorageTransfer.All) == 0
                && pack.GetCount("wooden_club") == 1 && gear.IsEquipped("wooden_club"), "The equipped copy can't be stored");
            pack.SetCount("wooden_club", 3);
            Check(StorageTransfer.Store(pack, crate, "wooden_club", StorageTransfer.All) == 2
                && pack.GetCount("wooden_club") == 1 && crate.GetCount("wooden_club") == 2, "Spare copies can, one stays equipped");
            yield return null;
            Check(gear.IsEquipped("wooden_club"), "Gear is still equipped a frame later");
            gear.Unequip(GearSlot.Weapon);
            Check(StorageTransfer.Store(pack, crate, "wooden_club", StorageTransfer.All) == 1 && pack.GetCount("wooden_club") == 0,
                "Once unequipped it stores normally");
        }

        // ---------- Saving ----------

        private IEnumerator CheckSaving()
        {
            StorageCratePiece original = MakeCrate();
            original.Add("wood", 7); original.Add("canned_food", 2);
            string state = original.CapturePieceState();
            Check(!string.IsNullOrEmpty(state), "A crate with contents has state to save");
            Check(MakeCrate().CapturePieceState() == null, "An empty crate has nothing to save");

            // The way SaveManager restores: an instance under an inactive parent, before Awake and OnEnable.
            var staging = new GameObject("ProbeStaging");
            staging.SetActive(false);
            spawned.Add(staging);
            var body = new GameObject("RestoredCrate");
            body.transform.SetParent(staging.transform, false);
            StorageCratePiece fresh = body.AddComponent<StorageCratePiece>();
            spawned.Add(body);
            int registeredBefore = StorageRegistry.All.Count;
            fresh.RestorePieceState(state);
            Check(fresh.GetCount("wood") == 7 && fresh.GetCount("canned_food") == 2, "A fresh inactive instance restores its contents");
            Check(StorageRegistry.All.Count == registeredBefore, "An inactive crate isn't registered");
            fresh.RestorePieceState(state);
            fresh.RestorePieceState(state);
            Check(fresh.GetCount("wood") == 7, "Restoring repeatedly doesn't multiply");
            body.transform.SetParent(null, true);
            Check(body.activeInHierarchy && StorageRegistry.All.Count == registeredBefore + 1, "Going live registers it");
            Check(fresh.GetCount("wood") == 7 && fresh.GetCount("canned_food") == 2, "Going live keeps the restored contents");
            Check(fresh.CapturePieceState() == state, "Saving it again gives the same state");

            StorageCratePiece old = MakeCrate();
            pack.SetCount("wood", 44);
            old.RestorePieceState(null);
            Check(old.IsEmpty && pack.GetCount("wood") == 44, "An old save's crate is empty and the pack stays whole");
            old.Add("wood", 1);
            old.RestorePieceState("this is not json");
            Check(old.IsEmpty, "An unreadable state leaves the crate empty, no exception");
            old.RestorePieceState("{\"items\":[{\"id\":\"wood\",\"count\":-5},{\"id\":\"\",\"count\":9},{\"id\":\"stone\",\"count\":3}]}");
            Check(old.GetCount("wood") == 0 && old.GetCount("stone") == 3 && old.Contents.Count == 1, "Bad entries in a state are dropped");
            yield break;
        }

        // ---------- Weight ----------

        private IEnumerator CheckWeights()
        {
            Check(Mathf.Approximately(Encumbrance.WeightOf("wood"), 1f) && Mathf.Approximately(Encumbrance.WeightOf("planks"), 0.5f)
                && Mathf.Approximately(Encumbrance.WeightOf("canned_food"), 0.4f) && Mathf.Approximately(Encumbrance.WeightOf("bandage"), 0.2f)
                && Mathf.Approximately(Encumbrance.WeightOf("iron_axe"), 3f) && Mathf.Approximately(Encumbrance.WeightOf("machete"), 3f)
                && Mathf.Approximately(Encumbrance.WeightOf("leather_armor"), 8f), "Category weights");
            Check(Mathf.Approximately(Encumbrance.WeightOf("ammo_9mm"), 0.02f) && Mathf.Approximately(Encumbrance.WeightOf("stone"), 2f)
                && Mathf.Approximately(Encumbrance.WeightOf("iron_ore"), 2f) && Mathf.Approximately(Encumbrance.WeightOf("metal_plate"), 2f)
                && Mathf.Approximately(Encumbrance.WeightOf("steel_bar"), 2.5f) && Mathf.Approximately(Encumbrance.WeightOf("coal"), 1f), "Per-item weights");
            int bad = 0;
            foreach (ItemSpec spec in ItemCatalog.All)
            {
                float weight = Encumbrance.WeightOf(spec.id);
                if (float.IsNaN(weight) || weight <= 0f) bad++;
            }
            Check(bad == 0, "Every item has a positive weight");
            pack.SetCount("stone", 3); pack.SetCount("planks", 4); pack.SetCount("wood", 0);
            Check(Mathf.Approximately(Encumbrance.PackWeight(pack), 8f), "Pack weight adds up");
            yield break;
        }

        private IEnumerator CheckEncumbrance()
        {
            Encumbrance load = Encumbrance.Active;
            Check(load != null, "Encumbrance created itself");
            if (load == null) yield break;
            float walk = player.walkSpeed, sprint = player.sprintSpeed;
            StorageCratePiece crate = MakeCrate();

            load.RefreshNow();
            Check(player.sprintMultiplier == 1f && !load.LabelVisible, "An empty pack has full sprint and no label");
            pack.SetCount("stone", 24); // 48 kg, exactly 80%
            load.RefreshNow();
            Check(player.sprintMultiplier == 1f && !load.LabelVisible, "No label at exactly 80%");
            pack.SetCount("stone", 27); // 54 kg
            load.RefreshNow();
            Check(player.sprintMultiplier == 1f && load.LabelVisible && load.LabelText == "Load 54/60 kg", "Label over 80%, sprint untouched: " + load.LabelText);
            pack.SetCount("stone", 30); // 60 kg
            load.RefreshNow();
            Check(player.sprintMultiplier == 1f, "Exactly at the limit keeps full sprint");
            pack.SetCount("stone", 40); // 80 kg
            load.RefreshNow();
            float halfway = player.sprintMultiplier;
            float worst = walk / sprint;
            Check(halfway < 1f && halfway > worst, $"Over the limit sprint fades ({halfway:F3})");
            Check(load.LabelText.StartsWith("Overloaded"), "The label says overloaded: " + load.LabelText);
            pack.SetCount("stone", 50); // 100 kg
            load.RefreshNow();
            Check(Mathf.Abs(sprint * player.sprintMultiplier - walk) < 0.01f, "At 100 kg sprint is walking speed");
            Check(load.LabelText.StartsWith("Overloaded: no sprint"), "The label says no sprint: " + load.LabelText);
            pack.SetCount("stone", 100000);
            load.RefreshNow();
            Check(player.sprintMultiplier > 0f && Mathf.Abs(sprint * player.sprintMultiplier - walk) < 0.01f, "Any load: sprint stops at walking speed");
            Check(player.walkSpeed == walk && player.sprintSpeed == sprint, "Walking and sprint speed fields are untouched");
            Check(Mathf.Max(player.walkSpeed, player.sprintSpeed * Mathf.Clamp01(player.sprintMultiplier)) >= walk, "The controller's sprint speed is never under walking");
            pack.SetCount("canned_food", 2);
            Check(pack.TryConsume("canned_food", 1), "Food stays usable however heavy the pack is");
            Check(pack.GetCount("stone") == 100000, "Nothing is dropped");

            player.sprintSpeed = sprint * 1.2f; // the expedition upgrade
            load.RefreshNow();
            Check(Mathf.Abs(player.sprintSpeed * player.sprintMultiplier - walk) < 0.01f && Mathf.Approximately(player.sprintSpeed, sprint * 1.2f),
                "With the sprint upgrade the worst case is still walking speed, the upgrade kept");
            pack.SetCount("stone", 0); pack.SetCount("canned_food", 0);
            load.RefreshNow();
            Check(player.sprintMultiplier == 1f && Mathf.Approximately(player.sprintSpeed, sprint * 1.2f), "Unloaded, the upgrade works in full");
            player.sprintSpeed = sprint;

            pack.SetCount("stone", 100);
            yield return new WaitForSecondsRealtime(Encumbrance.RefreshSeconds * 2f);
            Check(player.sprintMultiplier < 1f, "It updates by itself while playing");
            StorageTransfer.Store(pack, crate, "stone", StorageTransfer.All);
            yield return new WaitForSecondsRealtime(Encumbrance.RefreshSeconds * 2f);
            Check(player.sprintMultiplier == 1f && !load.LabelVisible, "Storing the weight in a crate restores sprint exactly");

            Canvas canvas = load.HudCanvas;
            Check(canvas != null && canvas.GetComponent<GraphicRaycaster>() == null, "The load label's canvas has no raycaster");
            int targets = 0;
            if (canvas != null) foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>(true)) if (graphic.raycastTarget) targets++;
            Check(targets == 0, "Nothing on the load label takes clicks");
        }

        // ---------- Crafting ----------

        private IEnumerator CheckCrafting()
        {
            ResourceDefinition wood = Fake("wood"), stone = Fake("stone");
            try
            {
                StorageCratePiece near = MakeCrate(5f), far = MakeCrate(120f);
                pack.SetCount("wood", 2);
                near.Add("wood", 3);
                far.Add("wood", 100);
                Check(CraftingSystem.CountInCrates(pack, "wood") == 3 && CraftingSystem.CountAvailable(pack, "wood") == 5, "A crate 5 m away counts and one 120 m away doesn't");
                Check(CraftingSystem.NearbyCrateCount(pack) == 1, "One crate in reach");
                Check(CraftingSystem.TryAfford(pack, Cost((wood, 5))) && !CraftingSystem.TryAfford(pack, Cost((wood, 6))), "Afford counts pack and in-range crate only");
                Check(!CraftingSystem.TrySpend(pack, Cost((wood, 6))) && pack.GetCount("wood") == 2 && near.GetCount("wood") == 3 && far.GetCount("wood") == 100,
                    "Too expensive spends nothing, and the out-of-range crate doesn't help");
                Check(!CraftingSystem.TrySpend(pack, Cost((wood, 5), (stone, 1))) && pack.GetCount("wood") == 2 && near.GetCount("wood") == 3,
                    "A missing ingredient spends nothing from anywhere");
                Check(!CraftingSystem.TrySpend(pack, Cost((wood, 3), (wood, 3))) && pack.GetCount("wood") == 2 && near.GetCount("wood") == 3,
                    "Duplicate rows are added up, no overdraft");
                Check(CraftingSystem.TrySpend(pack, Cost((wood, 2), (wood, 2))) && pack.GetCount("wood") == 0 && near.GetCount("wood") == 1,
                    "Duplicate rows spend the exact total, pack first");
                Check(far.GetCount("wood") == 100, "The far crate is untouched");

                pack.SetCount("wood", 4);
                near.Add("wood", 9); // now 10
                Check(CraftingSystem.TrySpend(pack, Cost((wood, 6))) && pack.GetCount("wood") == 0 && near.GetCount("wood") == 8, "Spends the pack first and then the crate");
                StorageCratePiece second = MakeCrate(-4f);
                second.Add("wood", 4);
                pack.SetCount("wood", 1);
                Check(CraftingSystem.TrySpend(pack, Cost((wood, 12))) && pack.GetCount("wood") == 0
                    && near.GetCount("wood") + second.GetCount("wood") == 1, "Spending can span two crates");

                var outsider = new GameObject("ProbeFixture");
                outsider.transform.position = player.transform.position;
                spawned.Add(outsider);
                Inventory fixture = outsider.AddComponent<Inventory>();
                near.Add("wood", 50);
                fixture.SetCount("wood", 1);
                Check(!CraftingSystem.UsesCrates(fixture) && CraftingSystem.CountAvailable(fixture, "wood") == 1
                    && !CraftingSystem.TrySpend(fixture, Cost((wood, 2))) && fixture.GetCount("wood") == 1, "An inventory that isn't the player's never reaches crates");

                var refuses = new GameObject("ProbeStubbornStore").AddComponent<RefusingStore>();
                refuses.transform.position = player.transform.position + Vector3.forward;
                spawned.Add(refuses.gameObject);
                int nearBefore = near.GetCount("wood"), secondBefore = second.GetCount("wood");
                near.Add("stone", 2);
                refuses.count = 50;
                pack.SetCount("stone", 1);
                // Stone comes from the near crate, then wood that only the refusing store can finish.
                int woodNeeded = nearBefore + secondBefore + 1;
                Check(CraftingSystem.TryAfford(pack, Cost((stone, 3), (wood, woodNeeded))), "The refusing store is counted as holding wood");
                Check(!CraftingSystem.TrySpend(pack, Cost((stone, 3), (wood, woodNeeded))) && pack.GetCount("stone") == 1 && near.GetCount("stone") == 2
                    && near.GetCount("wood") == nearBefore && second.GetCount("wood") == secondBefore,
                    "A store refusing part way puts everything back");
                refuses.gameObject.SetActive(false);
            }
            finally
            {
                Destroy(wood);
                Destroy(stone);
            }
            yield break;
        }

        private IEnumerator CheckCraftingUI()
        {
            CraftingUI ui = CraftingUI.Find();
            Recipe recipe = null;
            foreach (Recipe candidate in RecipeBook.ForStation(StationKind.Hand))
                if (candidate.Resolves() && candidate.inputs.Length > 0) { recipe = candidate; break; }
            Check(ui != null && recipe != null, "Crafting screen and a hand recipe exist");
            if (ui == null || recipe == null) yield break;

            string id = recipe.inputs[0].id;
            int need = recipe.inputs[0].amount;
            foreach (Ingredient input in recipe.inputs) pack.SetCount(input.id, 0);
            pack.SetCount(recipe.output, 0);
            Check(!RecipeBook.CanCraft(pack, recipe, 1), "CanCraft refuses with nothing");

            StorageCratePiece crate = MakeCrate(4f);
            foreach (Ingredient input in recipe.inputs) crate.Add(input.id, input.amount);
            Check(RecipeBook.CanCraft(pack, recipe, 1) && !RecipeBook.CanCraft(pack, recipe, 2), "CanCraft agrees with a crate in reach");
            Check(!RecipeBook.CanCraft(pack, recipe, 0) && !RecipeBook.CanCraft(pack, recipe, -1), "CanCraft refuses a nonsense batch");

            Check(ui.OpenHand(), "The crafting screen opens");
            Button craft = ui.CraftButton(recipe.id);
            Check(craft != null && craft.interactable, "Craft is enabled when only a crate holds the materials");
            Text inputs = craft != null ? craft.transform.parent.Find("Inputs")?.GetComponent<Text>() : null;
            Check(inputs != null && inputs.text.Contains("0 + " + need + " in crates"), "Owned counts show the crate: " + inputs?.text);
            if (craft != null) craft.onClick.Invoke();
            Check(pack.GetCount(recipe.output) == recipe.outputAmount, "Clicking Craft makes the item in the pack");
            Check(crate.GetCount(id) == 0, "The crate paid for it");
            Check(craft != null && !craft.interactable, "Craft disables once the crate is empty");
            ui.Close();
            Check(!ModalUI.IsOpen && Time.timeScale == 1f, "The crafting screen closes cleanly");

            Check(!RecipeBook.TryCraft(pack, recipe, 1, out _), "TryCraft agrees that it's empty");
        }

        // ---------- Lifecycle ----------

        private IEnumerator CheckLifecycle()
        {
            int before = StorageRegistry.All.Count;

            // A build ghost: Awake and OnEnable, then all MonoBehaviours switched off.
            StorageCratePiece ghost = MakeCrate();
            Check(StorageRegistry.All.Count == before + 1, "A live crate registers");
            foreach (MonoBehaviour behaviour in ghost.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            Check(StorageRegistry.All.Count == before, "A ghost leaves nothing registered");
            bool wasOpen = ModalUI.IsOpen;
            ghost.Interact(player.gameObject);
            Check(ModalUI.IsOpen == wasOpen, "A ghost can't be opened");

            StorageCratePiece doomed = MakeCrate();
            doomed.Add("wood", 3);
            doomed.gameObject.SetActive(false);
            Destroy(doomed.gameObject);
            Check(StorageRegistry.All.Count == before && StorageRegistry.CountNearby(player.transform.position, 100f, "wood") == 0,
                "A crate leaves the registry the moment it's deactivated");
            yield return null;
            Check(StorageRegistry.All.Count == before, "It stays gone after Destroy");

            // Destroyed by damage: the contents go to a crate in range. A load's removal must not move them.
            StorageCratePiece keeper = MakeCrate(2f);
            StorageCratePiece broken = MakeCrate(4f);
            broken.Add("wood", 11);
            StructureHealth health = broken.gameObject.AddComponent<StructureHealth>();
            health.TakeDamage(health.maxHealth + 1f);
            Check(keeper.GetCount("wood") == 11 && StorageRegistry.All.Count == before + 1, "A broken crate's contents survive in a nearby crate");
            yield return null;
            StorageCratePiece replaced = MakeCrate(4f);
            replaced.Add("wood", 5);
            replaced.gameObject.SetActive(false);
            Destroy(replaced.gameObject);
            Check(keeper.GetCount("wood") == 11, "A crate removed by loading moves nothing");
        }

        // ---------- The screen ----------

        private IEnumerator CheckStorageUI()
        {
            StorageCratePiece crate = MakeCrate();
            PlayerGear gear = PlayerGear.Ensure(player.gameObject);
            pack.SetCount("wood", 25); pack.SetCount("planks", 10); pack.SetCount("canned_food", 3); pack.SetCount("ammo_9mm", 50); pack.SetCount("wooden_club", 1);
            gear.Equip("wooden_club", out _);

            crate.Interact(player.gameObject);
            StorageUI ui = StorageUI.Instance;
            Check(ui != null && ui.IsOpen && ModalUI.IsOpen && Time.timeScale == 0f, "Interacting opens the screen through ModalUI and pauses");
            if (ui == null || !ui.IsOpen) yield break;
            crate.Interact(player.gameObject);
            Check(ui.IsOpen && ui.CurrentCrate == crate, "Interacting again changes nothing");

            Check(ui.Canvas.GetComponent<GraphicRaycaster>() != null && FindFirstObjectByType<EventSystem>() != null, "The screen has a raycaster and an event system");
            Canvas.ForceUpdateCanvases();
            yield return null;
            Check(InsideCard(ui.StoreMaterialsButton) && InsideCard(ui.TakeAllButton) && InsideCard(ui.CloseButton) && InsideCard(ui.RowButton(true, "wood", 2)),
                "Buttons are inside the panel");
            Check(TopHitIs(ui.StoreMaterialsButton) && TopHitIs(ui.TakeAllButton) && TopHitIs(ui.CloseButton) && TopHitIs(ui.RowButton(true, "wood", 2)),
                "A real UI raycast finds each button on top");
            Check(ui.LoadText.StartsWith("Load"), "The screen shows the load: " + ui.LoadText);

            Click(ui.RowButton(true, "wood", 0));
            Check(pack.GetCount("wood") == 24 && crate.GetCount("wood") == 1, "The 1 button stores one");
            Click(ui.RowButton(true, "wood", 1));
            Check(pack.GetCount("wood") == 14 && crate.GetCount("wood") == 11, "The 10 button stores ten");
            Click(ui.RowButton(false, "wood", 0));
            Check(pack.GetCount("wood") == 15 && crate.GetCount("wood") == 10, "The crate's 1 button takes one");
            Click(ui.RowButton(false, "wood", 1));
            Check(pack.GetCount("wood") == 25 && crate.GetCount("wood") == 0, "The crate's 10 button takes ten");
            Click(ui.RowButton(true, "wood", 1));
            Click(ui.RowButton(true, "wood", 0));
            Click(ui.RowButton(false, "wood", 2));
            Check(pack.GetCount("wood") == 25 && crate.GetCount("wood") == 0, "The crate's All button takes the stack");
            Check(ui.RowButton(false, "wood", 0) == null, "An empty stack's row is gone");
            Button all = ui.RowButton(true, "wood", 2);
            Click(all);
            Check(pack.GetCount("wood") == 0 && crate.GetCount("wood") == 25, "The All button stores the stack");
            Check(ui.RowButton(true, "wood", 2) == null, "The pack's empty row is gone");
            all.onClick.Invoke();
            Check(pack.GetCount("wood") == 0 && crate.GetCount("wood") == 25, "A stale click on a hidden row moves nothing");

            Button club = ui.RowButton(true, "wooden_club", 2);
            Check(club != null && !club.interactable, "The equipped club's buttons are disabled");
            club?.onClick.Invoke();
            Check(pack.GetCount("wooden_club") == 1 && crate.GetCount("wooden_club") == 0 && ui.StatusText.Contains("Unequip"), "A click on it says why: " + ui.StatusText);

            Click(ui.StoreMaterialsButton);
            Check(pack.GetCount("planks") == 0 && crate.GetCount("planks") == 10, "Store building materials moves planks");
            Check(pack.GetCount("canned_food") == 3 && pack.GetCount("ammo_9mm") == 50 && pack.GetCount("wooden_club") == 1, "Food, ammo and gear stay");
            Click(ui.TakeAllButton);
            Check(crate.IsEmpty && pack.GetCount("wood") == 25 && pack.GetCount("planks") == 10, "Take all empties the crate");

            ui.RequestClose();
            yield return null;
            Check(!ui.IsOpen && !ModalUI.IsOpen && Time.timeScale == 1f && !ui.Canvas.gameObject.activeSelf, "The Escape path closes and restores time");

            crate.Interact(player.gameObject);
            Check(ui.IsOpen, "It opens again");
            Click(ui.CloseButton);
            Check(!ui.IsOpen && !ModalUI.IsOpen && Time.timeScale == 1f, "The Close button closes and restores time");

            crate.Interact(player.gameObject);
            Button stale = ui.StoreMaterialsButton;
            ModalUI.DismissCurrent();
            Check(!ui.IsOpen && !ModalUI.IsOpen && Time.timeScale == 1f, "A load's DismissCurrent closes it");
            stale.onClick.Invoke();
            Check(pack.GetCount("wood") == 25 && crate.IsEmpty, "A click after closing moves nothing");

            crate.Add("wood", 1);
            crate.Interact(player.gameObject);
            crate.gameObject.SetActive(false);
            Destroy(crate.gameObject);
            yield return null;
            Check(!ui.IsOpen && !ModalUI.IsOpen && Time.timeScale == 1f, "A crate disappearing under the screen closes it");

            StorageCratePiece other = MakeCrate();
            Check(ModalUI.TryOpen(this, null), "The probe can hold the modal");
            other.Interact(player.gameObject);
            Check(!ui.IsOpen, "The crate won't open over another modal");
            ModalUI.Close(this);
            Check(Time.timeScale == 1f, "Time is back");
            gear.Unequip(GearSlot.Weapon);
        }

        // The real thing a click does once the event system has found the button.
        private void Click(Button button)
        {
            if (button == null) { Check(false, "Expected a button"); return; }
            if (!button.interactable || !button.gameObject.activeInHierarchy) { Check(false, button.name + " is clickable"); return; }
            button.onClick.Invoke();
        }

        private static bool TopHitIs(Button button)
        {
            EventSystem events = EventSystem.current;
            if (button == null || events == null) return false;
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            var data = new PointerEventData(events) { position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f) };
            var results = new List<RaycastResult>();
            events.RaycastAll(data, results);
            return results.Count > 0 && (results[0].gameObject == button.gameObject || results[0].gameObject.transform.IsChildOf(button.transform));
        }

        private static bool InsideCard(Button button)
        {
            if (button == null) return false;
            var rect = (RectTransform)button.transform;
            Transform walk = rect;
            while (walk != null && walk.name != "StoragePanel") walk = walk.parent;
            if (walk == null) return false;
            var card = (RectTransform)walk;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
                if (!RectTransformUtility.RectangleContainsScreenPoint(card, corner, null)) return false;
            return true;
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[StorageProbe] PASS: " : "[StorageProbe] FAIL: ") + message);
        }

        /// <summary>Claims to hold wood but refuses to hand it over, to exercise the rollback.</summary>
        private sealed class RefusingStore : MonoBehaviour, IItemStore
        {
            public int count;
            private static readonly Dictionary<string, int> Empty = new Dictionary<string, int>();
            public Vector3 StorePosition => transform.position;
            public IReadOnlyDictionary<string, int> Contents => Empty;
            public int GetCount(string id) => id == "wood" ? count : 0;
            public int GetAddRoom(string id) => int.MaxValue - GetCount(id);
            public void Add(string id, int amount) { }
            public bool TryTake(string id, int amount) => false;
            private void OnEnable() => StorageRegistry.Register(this);
            private void OnDisable() => StorageRegistry.Unregister(this);
        }
    }
}
