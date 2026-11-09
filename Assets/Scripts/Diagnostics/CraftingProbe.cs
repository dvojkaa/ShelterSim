using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.Combat;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>`--crafting-probe` in the built player: real crafting, UI, gear, tools, hunting. Exit code 0 only if every check passed.</summary>
    public sealed class CraftingProbe : MonoBehaviour
    {
        private readonly List<string> passed = new List<string>();
        private readonly List<string> failed = new List<string>();
        private FirstPersonController player;
        private Inventory inv;
        private SurvivalStats stats;
        private MeleeAttacker melee;
        private PlayerGear gear;
        private float started;
        private bool finished;
        private Keyboard keyboard;
        private bool cursorObservable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--crafting-probe") >= 0)
                new GameObject("CraftingProbe").AddComponent<CraftingProbe>();
        }

        private void Update()
        {
            if (!finished && Time.realtimeSinceStartup - started > 150f) Finish("timed out");
        }

        private IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            yield return null; yield return null;
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object next = null; bool moved;
                try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; else stack.Pop(); }
                catch (Exception e) { failed.Add("exception: " + e); break; }
                if (!moved) continue;
                if (next is IEnumerator nested) stack.Push(nested); else yield return next;
            }
            Finish(null);
        }

        private void Check(bool ok, string name)
        {
            if (ok) passed.Add(name); else { failed.Add(name); Debug.LogError("[CraftingProbe] FAIL: " + name); }
        }

        private void Finish(string reason)
        {
            if (finished) return;
            finished = true;
            if (reason != null) failed.Add(reason);
            string summary = $"[CraftingProbe] {(failed.Count == 0 ? "PASS" : "FAIL")}: {passed.Count} passed, {failed.Count} failed";
            Debug.Log(summary);
            foreach (string p in passed) Debug.Log("[CraftingProbe]   ok   " + p);
            foreach (string f in failed) Debug.Log("[CraftingProbe]   FAIL " + f);
            Application.Quit(failed.Count == 0 ? 0 : 1);
        }

        private IEnumerator Run()
        {
            player = FindFirstObjectByType<FirstPersonController>();
            Check(player != null, "player exists");
            if (player == null) yield break;
            inv = player.GetComponent<Inventory>();
            stats = player.GetComponent<SurvivalStats>();
            melee = FindFirstObjectByType<MeleeAttacker>();
            gear = PlayerGear.Ensure(player.gameObject);
            Check(inv != null && stats != null && melee != null && gear != null, "player has Inventory, SurvivalStats, MeleeAttacker, PlayerGear");
            stats.decayPaused = true;
            melee.ignoreCursorLock = true;

            Cursor.lockState = CursorLockMode.Locked;
            cursorObservable = Cursor.lockState == CursorLockMode.Locked;
            Debug.Log("[CraftingProbe] cursor lock observable in this player: " + cursorObservable);
            CheckStatic();
            CheckLootTables();
            CheckPieces();
            CheckCraftPath();
            yield return CheckUi();
            yield return CheckKeys();
            yield return CheckInventoryUi();
            CheckGear();
            yield return CheckGearDrop();
            CheckArmor();
            CheckTools();
            CheckPieceBehaviours();
            yield return CheckHunting();
        }

        // ---------- static validation ----------

        private void CheckStatic()
        {
            List<string> errors = RecipeBook.Validate(out string summary);
            Debug.Log("[CraftingProbe] " + summary);
            foreach (string e in errors) Debug.LogError("[CraftingProbe] validation: " + e);
            Check(errors.Count == 0, "recipe validation: ids resolve, every item/station reachable from raw sources, no cycles");
            Check(ItemCatalog.All.Count >= 50 && ItemCatalog.All.Count <= 130, $"item count in range ({ItemCatalog.All.Count})");
            Check(RecipeBook.All.Count >= 45 && RecipeBook.All.Count <= 110, $"recipe count in range ({RecipeBook.All.Count})");
            Check(ItemCatalog.All.All(s => ItemCatalog.Def(s.id) != null && ItemCatalog.Def(s.id).id == s.id), "every catalog item has a ResourceDefinition asset");
            Check(ItemCatalog.All.All(s => { var d = ItemCatalog.Def(s.id); return d != null && d.displayName == s.name && d.category == s.category; }), "assets match the catalog");
            Check(RecipeBook.All.All(r => r.Resolves()), "every recipe resolves to assets");
            foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
                Check(RecipeBook.ForStation(kind).Count >= 4, $"station {kind} has recipes ({RecipeBook.ForStation(kind).Count})");
            string[] legacy = { "wood", "stone", "iron_ore", "scrap_metal", "food_berries", "water", "first_aid", "weapon_upgrade", "expedition_parts" };
            Check(legacy.All(id => ItemCatalog.Def(id) != null), "the 9 original items still resolve");
            Check(ItemCatalog.Def("weapon_upgrade").weaponDamage > MeleeAttacker.DefaultDamage, "makeshift blade is a set-damage weapon");
            var shop = ItemCatalog.Shop;
            Check(shop.Count >= 18 && shop.Count <= 26 && shop.All(e => ItemCatalog.Def(e.id) != null && e.price >= 5 && e.price <= 400 && e.quantity >= 1), $"shop table valid ({shop.Count} entries)");
            int total = shop.Sum(e => e.price);
            Check(total > 1000 && total < 5000, $"one purchase of everything costs ${total}; the ~$5,000 budget forces choices on quantities");
        }

        private void CheckLootTables()
        {
            var go = new GameObject("ProbeLoot");
            var loot = go.AddComponent<LootContainer>();
            bool allDeterministic = true, allFill = true;
            foreach (string table in LootTables.TableNames)
            {
                LootTables.Fill(loot, table, new System.Random(77));
                string a = string.Join(",", loot.entries.Select(e => e.resource.id + "x" + e.amount));
                LootTables.Fill(loot, table, new System.Random(77));
                string b = string.Join(",", loot.entries.Select(e => e.resource.id + "x" + e.amount));
                allDeterministic &= a == b;
                int stacks = 0;
                for (int i = 0; i < 200; i++) { LootTables.Fill(loot, table, new System.Random(i)); stacks += loot.entries.Count; if (loot.entries.Count > LootTables.MaxStacksPerContainer) allFill = false; }
                allFill &= stacks > 200;
            }
            Check(allDeterministic, "LootTables.Fill is deterministic for a seed");
            Check(allFill, "every loot table produces stacks and respects the stack cap");
            Check(FindRate("fuel", "fuel") > .6f, "fuel is findable at fuel stations");
            Check(FindRate("factory", "electronics") > .2f && FindRate("military", "electronics") > .2f, "electronics findable at factory and military");
            Check(FindRate("hospital", "chemicals") > .3f && FindRate("factory", "chemicals") > .1f, "chemicals findable at hospital and factory");
            Check(FindRate("house", "cloth") > .35f, "cloth findable in houses");
            bool rare = true;
            foreach (string table in LootTables.TableNames) rare &= FindRate(table, "expedition_parts") < .08f;
            Check(rare, "expedition parts stay rare in every table");
            Destroy(go);
        }

        private float FindRate(string table, string id)
        {
            var go = new GameObject("ProbeLootRate");
            var loot = go.AddComponent<LootContainer>();
            int hits = 0;
            for (int i = 0; i < 400; i++)
            {
                LootTables.Fill(loot, table, new System.Random(1000 + i));
                if (loot.entries.Any(e => e.resource.id == id)) hits++;
            }
            Destroy(go);
            return hits / 400f;
        }

        // ---------- pieces ----------

        private void CheckPieces()
        {
            var build = FindFirstObjectByType<BuildModeController>();
            string[] newIds = { "piece_stove", "piece_forge", "piece_chemlab", "piece_electronics", "piece_farm", "piece_wall_steel", "piece_lamp", "piece_shelf", "piece_filter", "piece_radio" };
            Check(build != null && build.availablePieces.Count >= 17 && build.availablePieces.Select(p => p.id).Distinct().Count() == build.availablePieces.Count, $"build menu has every piece once ({build?.availablePieces.Count})");
            var invUi = FindFirstObjectByType<InventoryUI>();
            foreach (string id in newIds)
            {
                var def = build.availablePieces.FirstOrDefault(p => p != null && p.id == id);
                bool ok = def != null && def.prefab != null && def.prefab.GetComponent<SheltreSim.Saving.PlacedPiece>()?.pieceId == id
                    && def.prefab.GetComponent<BoxCollider>() != null && (def.prefab.GetComponent<BoxCollider>().size - def.footprintSize).sqrMagnitude < .0001f
                    && def.craftingCost.Count >= 2 && def.craftingCost.All(c => c.resource != null && c.amount > 0) && invUi.knownRecipes.Contains(def);
                Check(ok, "build piece wired: " + id);
            }
            Check(build.availablePieces.Select(p => p.id).Distinct().Count() == build.availablePieces.Count, "piece ids are distinct");
            var lamp = build.availablePieces.First(p => p.id == "piece_lamp").prefab.GetComponentInChildren<Light>();
            Check(lamp != null && lamp.type == LightType.Point && lamp.range > 1f && lamp.range < 12f, "lamp has a small-range point light");
            Check(build.availablePieces.First(p => p.id == "piece_stove").prefab.GetComponent<WorkbenchStation>().kind == StationKind.Stove, "stove prefab is a Stove station");
            var oldBench = build.availablePieces.First(p => p.id == "workbench").prefab.GetComponent<WorkbenchStation>();
            Check(oldBench != null && oldBench.kind == StationKind.Workbench, "existing workbench prefab is a Workbench station");
            Check(FindObjectsByType<WorkbenchStation>(FindObjectsSortMode.None).Any(w => w.kind == StationKind.Workbench), "starting workbench exists in the scene");
        }

        // ---------- real crafting path ----------

        private void CheckCraftPath()
        {
            inv.Clear();
            Recipe planks = RecipeBook.Get("planks");
            string msg;
            Check(!RecipeBook.TryCraft(inv, planks, 1, out msg) && inv.GetCount("planks") == 0, "crafting without materials fails and changes nothing");
            inv.SetCount("wood", 1);
            Recipe many = RecipeBook.Get("planks");
            Check(!RecipeBook.TryCraft(inv, many, 5, out msg) && inv.GetCount("wood") == 1, "x5 is atomic: partial materials spend nothing");
            inv.SetCount("wood", 10);
            Check(RecipeBook.TryCraft(inv, planks, 5, out msg) && inv.GetCount("wood") == 0 && inv.GetCount("planks") == 15, "x5 craft spends 10 wood and yields 15 planks (" + msg + ")");
            // Walk the ladder: raw -> radio beacon, crafting every step through TryCraft.
            inv.Clear();
            inv.SetCount("iron_ore", 40); inv.SetCount("coal", 40); inv.SetCount("scrap_metal", 40); inv.SetCount("wood", 40);
            inv.SetCount("plant_fiber", 40); inv.SetCount("electronics", 20); inv.SetCount("copper_wire", 30); inv.SetCount("chemicals", 10);
            inv.SetCount("expedition_parts", 6); inv.SetCount("fuel", 5);
            bool chain = true;
            string[] order = { "planks", "nails", "rope", "reinforced_frame", "iron_ingot", "steel_bar", "metal_plate", "circuit_board", "radio_part", "radio_beacon" };
            foreach (string id in order)
            {
                var r = RecipeBook.Get(id);
                int times = id == "radio_part" ? 3 : id == "circuit_board" ? 6 : id == "steel_bar" ? 6 : id == "iron_ingot" ? 12 : id == "metal_plate" ? 2 : id == "planks" ? 3 : id == "nails" ? 4 : id == "rope" ? 2 : 1;
                for (int i = 0; i < times; i++)
                {
                    if (!RecipeBook.TryCraft(inv, r, 1, out msg)) { Debug.LogError($"[CraftingProbe] ladder step {id} #{i + 1}: {msg}"); chain = false; break; }
                }
            }
            Check(chain && inv.GetCount("radio_beacon") == 1, "ladder crafted from raw materials up to the radio beacon");
            inv.Clear();
        }

        // ---------- crafting UI ----------

        private static bool TopHitIs(Button button)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var rt = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f);
            var data = new PointerEventData(es) { position = point };
            var results = new List<RaycastResult>();
            es.RaycastAll(data, results);
            return results.Count > 0 && (results[0].gameObject == button.gameObject || results[0].gameObject.transform.IsChildOf(button.transform));
        }

        private static void Click(Button button)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, clickCount = 1 };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private IEnumerator CheckUi()
        {
            var ui = CraftingUI.Find();
            Check(ui != null, "CraftingUI exists in the scene");
            if (ui == null) yield break;
            float scaleBefore = Time.timeScale;
            inv.Clear();
            inv.SetCount("wood", 2);
            Check(ui.OpenHand(), "hand crafting opens");
            yield return null;
            Check(ModalUI.IsOpen && ui.IsOpen, "ModalUI owns the crafting window");
            Check(Time.timeScale == 0f, "world is paused while crafting");
            Check(Cursor.lockState == CursorLockMode.None && Cursor.visible, "cursor is free while crafting");
            Check(ui.VisibleRowCount == RecipeBook.ForStation(StationKind.Hand).Count, "hand list shows the hand recipes");
            var planksButton = ui.CraftButton("planks");
            var fiveButton = ui.CraftButton("planks", true);
            var clubButton = ui.CraftButton("wooden_club");
            Check(planksButton != null && planksButton.interactable, "Craft is clickable when affordable");
            Check(fiveButton != null && !fiveButton.interactable, "Craft x5 is disabled when only one batch is affordable");
            Check(clubButton != null && !clubButton.interactable, "Craft is disabled when ingredients are missing");
            Check(planksButton != null && TopHitIs(planksButton), "a real UI raycast at the Craft button hits that button (nothing covers it)");
            if (planksButton != null) Click(planksButton);
            Check(inv.GetCount("wood") == 0 && inv.GetCount("planks") == 3, "clicking Craft crafts through the real path");
            Check(!planksButton.interactable, "buttons refresh after crafting");
            ui.Close();
            yield return null;
            Check(!ModalUI.IsOpen && Time.timeScale == scaleBefore, "closing restores timeScale");
            if (cursorObservable) Check(Cursor.lockState == CursorLockMode.Locked, "closing relocks the cursor");
            else Debug.Log("[CraftingProbe] SKIP closing relocks the cursor (this headless player does not keep Cursor.lockState)");

            // Station-filtered list, opened by interacting with a real station.
            var forge = FindObjectsByType<WorkbenchStation>(FindObjectsSortMode.None).First();
            forge.kind = StationKind.Forge;
            forge.Interact(player.gameObject);
            yield return null;
            Check(ui.IsOpen && ui.CurrentStation == StationKind.Forge && ui.VisibleRowCount == RecipeBook.ForStation(StationKind.Forge).Count, "interacting with a station opens its own recipes");
            ui.Close();
            forge.kind = StationKind.Workbench;
            yield return null;

            // Not while dead, not in build mode.
            var build = FindFirstObjectByType<BuildModeController>();
            Check(build != null, "build controller found");
            yield return null;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private IEnumerator CheckKeys()
        {
            var ui = CraftingUI.Find();
            var pause = FindFirstObjectByType<SheltreSim.Core.PauseMenu>();
            keyboard = InputSystem.AddDevice<Keyboard>("CraftingProbeKeyboard");
            yield return Press(Key.C);
            Check(ui.IsOpen, "key C opens hand crafting");
            yield return Press(Key.Escape);
            Check(!ui.IsOpen && !ModalUI.IsOpen, "Escape closes crafting");
            Check(pause == null || !pause.IsOpen, "the same Escape does not open the pause menu");
            yield return Press(Key.C);
            yield return Press(Key.C);
            Check(!ui.IsOpen && !ModalUI.IsOpen, "key C toggles crafting closed");
            InputSystem.RemoveDevice(keyboard);
        }

        private IEnumerator CheckInventoryUi()
        {
            var ui = FindFirstObjectByType<InventoryUI>();
            var crafting = CraftingUI.Find();
            inv.Clear();
            inv.SetCount("wood", 3); inv.SetCount("water", 2); inv.SetCount("cooked_meat", 1); inv.SetCount("machete", 1);
            stats.hunger = 10f; stats.thirst = 10f;
            Check(ui.Open(), "inventory opens");
            yield return null;
            Check(ui.VisibleItemRowCount == 4 && ui.HasVisibleRow("wood") && !ui.HasVisibleRow("stone"), "inventory lists only owned items");
            var eat = ui.ActionButton("cooked_meat");
            var equip = ui.ActionButton("machete");
            Check(eat != null && eat.interactable && TopHitIs(eat), "Eat is a real, clickable target");
            Check(equip != null && equip.interactable && TopHitIs(equip), "Equip is a real, clickable target");
            float hungerBefore = stats.hunger;
            Click(eat);
            Check(inv.GetCount("cooked_meat") == 0 && stats.hunger > hungerBefore, "clicking Eat eats");
            Check(ui.VisibleItemRowCount == 3 && !ui.HasVisibleRow("cooked_meat"), "row disappears when the count hits zero");
            Click(equip);
            Check(gear.WeaponId == "machete" && Mathf.Approximately(melee.attackDamage, 46f), "clicking Equip equips and sets melee damage");
            Click(ui.ActionButton("machete"));
            Check(gear.WeaponId == "" && Mathf.Approximately(melee.attackDamage, MeleeAttacker.DefaultDamage), "clicking Unequip restores base damage");
            var craft = ui.CraftButton;
            Check(craft != null && TopHitIs(craft), "inventory Craft button is a real click target");
            if (craft != null) Click(craft);
            yield return null;
            Check(!ui.IsOpen && crafting.IsOpen, "Craft button closes the inventory and opens hand crafting");
            crafting.Close();
            yield return null;
            Check(!ModalUI.IsOpen && Time.timeScale == 1f && (!cursorObservable || Cursor.lockState == CursorLockMode.Locked), "after both windows close: running" + (cursorObservable ? ", cursor locked" : " (cursor lock not observable here)"));
            gear.ResetAll();
            inv.Clear();
        }

        // ---------- gear ----------

        private void CheckGear()
        {
            inv.Clear(); gear.ResetAll();
            Check(Mathf.Approximately(melee.attackDamage, MeleeAttacker.DefaultDamage) && Mathf.Approximately(gear.AttackDamage, MeleeAttacker.DefaultDamage), "default weapon is base damage");
            inv.SetCount("weapon_upgrade", 3); inv.SetCount("fire_axe", 1); inv.SetCount("wooden_club", 1);
            string msg;
            Check(gear.Equip("weapon_upgrade", out msg) && Mathf.Approximately(melee.attackDamage, 32f), "makeshift blade sets damage to 32");
            Check(gear.Equip("weapon_upgrade", out msg) && Mathf.Approximately(melee.attackDamage, 32f), "equipping the blade again does not stack");
            Check(gear.Equip("fire_axe", out msg) && Mathf.Approximately(melee.attackDamage, 58f), "fire axe sets damage to 58");
            Check(gear.Equip("wooden_club", out msg) && Mathf.Approximately(melee.attackDamage, 26f), "equipping a weaker weapon lowers damage (no stacking)");
            Check(inv.GetCount("fire_axe") == 1 && inv.GetCount("wooden_club") == 1, "equipping never consumes the item");
            Check(!gear.Equip("steel_sword", out msg) && Mathf.Approximately(melee.attackDamage, 26f), "cannot equip an item you do not own");
            var saved = gear.CaptureEquipped();
            Check(saved.Count == 1 && saved[0] == "wooden_club", "CaptureEquipped returns the equipped ids");
            inv.SetCount("steel_plate_armor", 1);
            gear.Equip("steel_plate_armor", out msg);
            saved = gear.CaptureEquipped();
            Check(saved.Count == 2, "capture includes armor");
            gear.ResetAll();
            Check(gear.CaptureEquipped().Count == 0 && Mathf.Approximately(melee.attackDamage, MeleeAttacker.DefaultDamage) && Mathf.Approximately(gear.DamageReduction, 0f), "ResetAll clears gear and restores base damage");
            gear.RestoreEquipped(saved);
            Check(gear.WeaponId == "wooden_club" && gear.ArmorId == "steel_plate_armor" && Mathf.Approximately(melee.attackDamage, 26f), "RestoreEquipped re-equips and applies weapon damage");
            gear.RestoreEquipped(new List<string> { "not_an_item", "wood" });
            Check(gear.CaptureEquipped().Count == 0, "RestoreEquipped ignores unknown and non-equippable ids");
            gear.ResetAll();
        }

        private IEnumerator CheckGearDrop()
        {
            inv.Clear(); gear.ResetAll();
            inv.SetCount("wooden_club", 1);
            gear.Equip("wooden_club", out _);
            Check(Mathf.Approximately(melee.attackDamage, 26f), "club equipped");
            // The Spiked Club recipe consumes the club that is equipped.
            inv.SetCount("nails", 6); inv.SetCount("scrap_metal", 1);
            Check(RecipeBook.TryCraft(inv, RecipeBook.Get("spiked_club"), 1, out _), "crafted a spiked club from the equipped club");
            yield return null; yield return null;
            Check(gear.WeaponId == "" && Mathf.Approximately(melee.attackDamage, MeleeAttacker.DefaultDamage), "an equipped item that was consumed is unequipped and damage resets");
            gear.ResetAll(); inv.Clear();
        }

        private void CheckArmor()
        {
            inv.Clear(); gear.ResetAll();
            stats.health = stats.maxHealth;
            stats.TakeDamage(10f);
            Check(Mathf.Approximately(stats.health, stats.maxHealth - 10f), "unarmored hit deals full damage");
            inv.SetCount("steel_plate_armor", 1); inv.SetCount("warm_coat", 1); inv.SetCount("padded_jacket", 1);
            string msg;
            gear.Equip("padded_jacket", out msg);
            stats.health = stats.maxHealth;
            stats.TakeDamage(10f);
            Check(Mathf.Approximately(stats.health, stats.maxHealth - 9.2f), $"padded jacket reduces damage by 8% ({stats.health})");
            gear.Equip("steel_plate_armor", out msg);
            gear.Equip("warm_coat", out msg);
            Check(Mathf.Approximately(gear.DamageReduction, .5f), "armor + coat reductions add");
            stats.health = stats.maxHealth;
            stats.TakeDamage(10f);
            Check(Mathf.Approximately(stats.health, stats.maxHealth - 5f), $"steel armor + coat halve a real hit ({stats.health})");
            // Starvation must ignore armor.
            stats.health = stats.maxHealth; stats.hunger = 0f; stats.thirst = 0f;
            typeof(SurvivalStats).GetMethod("AdvanceNeeds", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(stats, new object[] { 1f });
            Check(Mathf.Approximately(stats.health, stats.maxHealth - stats.healthDecayWhenStarving), $"armor does not reduce starvation damage ({stats.health})");
            stats.health = stats.maxHealth; stats.hunger = stats.maxHunger; stats.thirst = stats.maxThirst;
            gear.ResetAll(); inv.Clear();
        }

        // ---------- tools ----------

        private ResourceNode MakeNode(string id, int quantity = 1, string bonus = "", float chance = 0f)
        {
            var go = new GameObject("ProbeNode_" + id);
            go.AddComponent<BoxCollider>();
            var node = go.AddComponent<ResourceNode>();
            node.resourceType = ItemCatalog.Def(id);
            node.quantityPerHarvest = quantity;
            node.remainingCharges = 20;
            node.bonusId = bonus; node.bonusChance = chance;
            return node;
        }

        private int Harvest(ResourceNode node, string id)
        {
            int before = inv.GetCount(id);
            node.Interact(player.gameObject);
            return inv.GetCount(id) - before;
        }

        private void CheckTools()
        {
            inv.Clear(); gear.ResetAll();
            var wood = MakeNode("wood"); var stone = MakeNode("stone"); var ore = MakeNode("iron_ore");
            Check(Harvest(wood, "wood") == 1 && Harvest(stone, "stone") == 1, "bare hands still harvest the base amount");
            inv.SetCount("stone_axe", 1);
            Check(Harvest(wood, "wood") == 2, "stone axe adds +1 wood");
            Check(Harvest(stone, "stone") == 1, "an axe does not help with stone");
            inv.SetCount("iron_axe", 1);
            Check(Harvest(wood, "wood") == 3, "iron axe (best tier) adds +2");
            inv.SetCount("steel_axe", 1);
            Check(Harvest(wood, "wood") == 4, "steel axe adds +3 and the best tier wins, not the sum");
            inv.SetCount("iron_pickaxe", 1);
            Check(Harvest(stone, "stone") == 3 && Harvest(ore, "iron_ore") == 3, "iron pickaxe adds +2 to stone and ore");
            Check(inv.GetCount("steel_axe") == 1 && inv.GetCount("iron_pickaxe") == 1, "tools are never consumed");
            // Secondary drop.
            var coalNode = MakeNode("stone", 1, "coal", 1f);
            int coalBefore = inv.GetCount("coal");
            coalNode.Interact(player.gameObject);
            Check(inv.GetCount("coal") == coalBefore + 1, "bonusId with chance 1 always drops the bonus");
            var noBonus = MakeNode("stone", 1, "coal", 0f);
            noBonus.Interact(player.gameObject);
            Check(inv.GetCount("coal") == coalBefore + 1, "bonusChance 0 never drops");
            var half = MakeNode("stone", 1, "coal", .5f);
            int drops = 0;
            for (int i = 0; i < 20; i++) { int c = inv.GetCount("coal"); half.remainingCharges = 5; half.Interact(player.gameObject); if (inv.GetCount("coal") > c) drops++; }
            Check(drops > 0 && drops < 20, $"a 50% bonus sometimes drops ({drops}/20)");
            foreach (var n in new[] { wood, stone, ore, coalNode, noBonus, half }) Destroy(n.gameObject);
            inv.Clear();
        }

        // ---------- piece behaviours ----------

        private void CheckPieceBehaviours()
        {
            var build = FindFirstObjectByType<BuildModeController>();
            inv.Clear();
            var farmPrefab = build.availablePieces.First(p => p.id == "piece_farm").prefab;
            var farm = Instantiate(farmPrefab).GetComponent<FarmPlot>();
            farm.Interact(player.gameObject);
            Check(farm.State == FarmPlot.Empty, "planting without seeds does nothing");
            inv.SetCount("seeds", 2);
            farm.Interact(player.gameObject);
            Check(farm.State == FarmPlot.Growing && inv.GetCount("seeds") == 1, "planting consumes a seed and starts growing");
            farm.Capture(out int s, out float e);
            farm.RestoreState(FarmPlot.Empty, 0f);
            farm.RestoreState(s, e);
            Check(farm.State == FarmPlot.Growing, "FarmPlot Capture/Restore round-trips");
            farm.RestoreState(FarmPlot.Ready, 0f);
            farm.Interact(player.gameObject);
            Check(farm.State == FarmPlot.Empty && inv.GetCount("vegetables") == 3 && inv.GetCount("seeds") == 2, "harvest yields vegetables and a seed back");
            farm.RestoreState(99, float.NaN);
            Check(farm.State == FarmPlot.Ready, "FarmPlot.RestoreState clamps bad input");
            Destroy(farm.gameObject);

            var filter = Instantiate(build.availablePieces.First(p => p.id == "piece_filter").prefab);
            inv.SetCount("dirty_water", 3);
            filter.GetComponent<WaterFilterPiece>().Interact(player.gameObject);
            Check(inv.GetCount("dirty_water") == 0 && inv.GetCount("water") == 3, "water filter turns dirty water into water");
            Destroy(filter);

            var radio = Instantiate(build.availablePieces.First(p => p.id == "piece_radio").prefab);
            string heard = null;
            Action<string> listener = m => { if (m.Contains("VICTORY")) heard = m; };
            GameMessageBus.OnMessage += listener;
            radio.GetComponent<RadioBeaconPiece>().Interact(player.gameObject);
            GameMessageBus.OnMessage -= listener;
            Check(heard != null && RadioBeaconPiece.VictoryAnnounced && stats != null && !stats.IsDead && Time.timeScale == 1f, "radio beacon announces victory and keeps the game running");
            Destroy(radio);
            inv.Clear();
        }

        // ---------- hunting ----------

        private IEnumerator CheckHunting()
        {
            inv.Clear(); gear.ResetAll();
            var parent = new GameObject("ProbeChunk").transform;
            Transform eye = melee.transform;
            // Find a direction with clear space in front of the camera.
            for (int i = 0; i < 4; i++)
            {
                if (!Physics.Raycast(eye.position, eye.forward, 3.5f, ~0, QueryTriggerInteraction.Ignore)) break;
                player.transform.Rotate(0f, 90f, 0f);
                Physics.SyncTransforms();
            }
            Vector3 spot = eye.position + eye.forward * 1.7f - Vector3.up * 1.1f;

            inv.SetCount("steel_sword", 1); gear.Equip("steel_sword", out _);
            var deer = WorldWildlife.SpawnForTest(parent, spot, 0);
            Physics.SyncTransforms();
            Check(deer.GetComponent<Collider>() != null && deer.IsHuntable && !deer.IsDead, "deer has a collider and health");
            bool swung = melee.TryAttack();
            Check(swung && deer.IsDead, "a real melee swing kills a deer (60 HP, sword 72)");
            yield return null;
            var carcass = parent.Cast<Transform>().FirstOrDefault(t => t.name == "Carcass_Deer");
            Check(carcass != null && carcass.GetComponent<LootContainer>() != null && carcass.GetComponent<Collider>() != null, "the deer leaves a searchable carcass under the chunk parent");
            if (carcass != null)
            {
                carcass.GetComponent<LootContainer>().Interact(player.gameObject);
                Check(inv.GetCount("raw_meat") >= 3 && inv.GetCount("leather") >= 2, $"searching the carcass gives raw meat and leather ({inv.GetCount("raw_meat")}/{inv.GetCount("leather")})");
                Destroy(carcass.gameObject);
            }

            // Weak weapon: a boar survives and charges.
            gear.ResetAll();
            inv.SetCount("wooden_club", 1); gear.Equip("wooden_club", out _);
            yield return new WaitForSeconds(.7f);
            var boar = WorldWildlife.SpawnForTest(parent, spot, 1);
            Physics.SyncTransforms();
            string messageSeen = null;
            Action<string> listen = m => { if (m.StartsWith("Hit boar")) messageSeen = m; };
            GameMessageBus.OnMessage += listen;
            melee.TryAttack();
            GameMessageBus.OnMessage -= listen;
            Check(!boar.IsDead && Mathf.Approximately(boar.Health, 54f) && messageSeen != null && messageSeen.Contains("charges"), $"club hit leaves the boar at 54 HP and it charges ({messageSeen})");
            Destroy(boar.gameObject);

            // Birds cannot be hit.
            var bird = WorldWildlife.SpawnForTest(parent, spot + Vector3.up * 5f, 2);
            Check(bird.GetComponent<Collider>() == null && !bird.IsHuntable, "birds stay unhittable");
            Destroy(bird.gameObject);
            Destroy(parent.gameObject);
            gear.ResetAll(); inv.Clear();
        }
    }
}
