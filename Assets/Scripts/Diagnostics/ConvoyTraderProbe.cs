using System;
using System.Linq;
using SheltreSim.Items;
using SheltreSim.Objectives;
using SheltreSim.Player;
using SheltreSim.Raids;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Isolated player integration check for post-rescue hordes and the radio-trader parts sink.</summary>
    public sealed class ConvoyTraderProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--convoy-trader-probe") >= 0)
                new GameObject(nameof(ConvoyTraderProbe)).AddComponent<ConvoyTraderProbe>();
        }

        private void Start()
        {
            try { Run(); }
            catch (Exception error)
            {
                Debug.LogError("[ConvoyTraderProbe] FAILED: " + error);
                Application.Quit(1);
            }
        }

        private static void Run()
        {
            Require(RaidDirector.PostVictoryIntervalSeconds == 15f * 60f, "Post-victory horde interval is 15 game minutes");
            Require(RaidDirector.PostVictoryWarningSeconds == 90f, "Post-victory warning is 90 seconds");
            for (int n = 1; n <= 10; n++)
            {
                var roster = RaidDirector.PostVictoryRoster(n, 1234);
                Require(roster.Count <= RaidDirector.PostVictoryMaxRaiders, $"Horde {n} stays within the cap");
                Require(roster.Contains(n % 5 == 0 ? RaidVariant.Warlord : RaidVariant.Raider),
                    $"Horde {n} includes its expected regular/Warlord leader");
            }

            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            ExpeditionProgression progression = ExpeditionProgression.Active;
            Inventory inventory = player != null ? player.GetComponent<Inventory>() : null;
            ObjectiveTracker objectives = ObjectiveTracker.Instance;
            Require(player != null && inventory != null && progression != null && objectives != null,
                "Player, inventory, progression and objectives are initialized");
            string objectiveSave = objectives.CaptureState();
            int previousUpgradeMask = progression.UpgradeMask;
            var previousDiscoveries = progression.CaptureDiscoveries();
            int previousParts = inventory.GetCount(progression.expeditionParts.id);
            int previousAmmo = inventory.GetCount("ammo_rifle");
            int previousMedical = inventory.GetCount("first_aid");
            int previousCell = inventory.GetCount("energy_cell");
            try
            {
                progression.Restore(0, new System.Collections.Generic.List<string>());
                inventory.SetCount(progression.expeditionParts.id, 2);
                objectives.RestoreState("{}");
                Require(!progression.TryRequestConvoyCache(out _), "Trader rejects requests before rescue");
                objectives.RestoreState("{\"victory\":true}");
                Require(!progression.TryRequestConvoyCache(out _), "Trader rejects requests until all upgrades are installed");
                progression.Restore(7, new System.Collections.Generic.List<string>());
                Require(progression.TryRequestConvoyCache(out _), "Trader accepts the request after rescue and all upgrades");
                Require(inventory.GetCount(progression.expeditionParts.id) == 0, "Trader consumes exactly two expedition parts");
                Require(inventory.GetCount("ammo_rifle") == previousAmmo + 18 &&
                    inventory.GetCount("first_aid") == previousMedical + 1 &&
                    inventory.GetCount("energy_cell") == previousCell + 1, "Trader delivers the stated cache contents");
                Require(!progression.TryRequestConvoyCache(out _), "Trader rejects a second request without parts");
                objectives.RestoreState(objectiveSave);
                Debug.Log("[ConvoyTraderProbe] Horde schedule, Warlord cadence, trader gates, costs and cache contents passed");
                Application.Quit(0);
            }
            finally
            {
                objectives.RestoreState(objectiveSave);
                progression.Restore(previousUpgradeMask, previousDiscoveries);
                inventory.SetCount(progression.expeditionParts.id, previousParts);
                inventory.SetCount("ammo_rifle", previousAmmo);
                inventory.SetCount("first_aid", previousMedical);
                inventory.SetCount("energy_cell", previousCell);
            }
        }

        private static void Require(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label);
        }
    }
}
