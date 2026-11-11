using System;
using System.Collections;
using System.IO;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run the built player WITH graphics as <c>--inventory-shots &lt;folder&gt;</c>: fills the inventory with a spread of items, opens it and
    /// saves real screen captures of the grid, a selected item and the Build tab. Checks nothing; for a human/agent to look at.
    /// </summary>
    public sealed class InventoryShotProbe : MonoBehaviour
    {
        private string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--inventory-shots");
            if (i < 0) return;
            new GameObject("InventoryShotProbe").AddComponent<InventoryShotProbe>().folder = i + 1 < args.Length ? args[i + 1] : "inventory_shots";
        }

        private IEnumerator Start()
        {
            for (int i = 0; i < 5; i++) yield return null;
            Directory.CreateDirectory(folder);
            var ui = FindAnyObjectByType<InventoryUI>();
            if (ui == null || ui.inventory == null) { Debug.LogError("[InventoryShots] no inventory UI"); Application.Quit(1); yield break; }
            Inventory inv = ui.inventory;
            inv.Clear();
            string[] ids =
            {
                "pistol", "machete", "assault_rifle", "steel_sword", "ammo_9mm", "ammo_rifle", "leather_armor", "warm_coat", "gas_mask",
                "hammer", "iron_axe", "stone_pickaxe", "bandage", "first_aid", "antibiotics", "stimulant", "canned_food", "water",
                "cooked_meat", "bread", "wood", "stone", "iron_ore", "scrap_metal", "circuit_board", "electric_motor", "fuel", "paper_map",
                "rope", "nails", "planks", "coal",
            };
            foreach (string id in ids) inv.SetCount(id, 3 + id.Length % 5);
            // The opening may leave another window (the bunker terminal) open and owning the modal state: close it first.
            for (int guard = 0; guard < 4 && SheltreSim.UI.ModalUI.IsOpen; guard++) { SheltreSim.UI.ModalUI.DismissCurrent(); yield return null; }
            if (!ui.Open()) { Debug.LogError("[InventoryShots] inventory could not open (another modal is open)"); Application.Quit(1); yield break; }
            yield return null;
            if (!ui.IsOpen) { Debug.LogError("[InventoryShots] inventory is not open"); Application.Quit(1); yield break; }
            yield return Shot("all", 4);
            ui.SelectForProbe("machete");
            yield return Shot("selected_machete", 4);
            ui.SetTabForProbe(1);
            yield return Shot("weapons", 4);
            ui.SetTabForProbe(4);
            yield return Shot("medicine", 4);
            ui.SetTabForProbe(7);
            yield return Shot("build", 4);
            ui.CloseIfOpen();
            yield return null;
            var crafting = SheltreSim.Crafting.CraftingUI.Find();
            if (crafting != null)
            {
                for (int guard = 0; guard < 4 && SheltreSim.UI.ModalUI.IsOpen; guard++) { SheltreSim.UI.ModalUI.DismissCurrent(); yield return null; }
                crafting.OpenHand();
                yield return null;
                if (!crafting.IsOpen) Debug.LogError("[InventoryShots] crafting window did not open");
                yield return Shot("crafting", 5);
            }
            Debug.Log("[InventoryShots] done");
            Application.Quit(0);
        }

        private IEnumerator Shot(string name, int settleFrames)
        {
            for (int i = 0; i < settleFrames; i++) yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            for (int i = 0; i < 3; i++) yield return null;
        }
    }
}
