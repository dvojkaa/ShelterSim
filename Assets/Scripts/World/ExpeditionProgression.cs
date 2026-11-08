using System;
using System.Collections.Generic;
using System.Linq;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Discover landmarks, recover specialized parts and improve expedition capability at home.</summary>
    public sealed class ExpeditionProgression : MonoBehaviour
    {
        public static ExpeditionProgression Active { get; private set; }
        public ResourceDefinition expeditionParts, wood, scrap, iron;
        public int UpgradeMask { get; private set; }
        public int DiscoveryCount => discoveries.Count;
        public float ExposureProtection => HasUpgrade(0) ? .35f : 1f;
        public float NeedsMultiplier => HasUpgrade(1) ? .8f : 1f;
        public string LastDiscovery { get; private set; } = "Explore landmarks to recover expedition parts.";
        public event Action Changed;
        private readonly HashSet<string> discoveries = new HashSet<string>();
        private float startingSprint;
        private FirstPersonController player;
        public static readonly string[] UpgradeNames = { "Insulated clothing", "Efficient rations", "Trail training" };
        public static readonly string[] UpgradeBenefits = {
            "Weather exposure reduced by 65%", "Passive food and water use reduced by 20%", "Sprint speed increased by 20%"
        };

        private void Awake()
        {
            Active = this;
            player = GetComponent<FirstPersonController>();
            startingSprint = player != null ? player.sprintSpeed : 7f;
        }
        public bool HasUpgrade(int index) => index >= 0 && index < 3 && (UpgradeMask & (1 << index)) != 0;
        public List<string> CaptureDiscoveries() => discoveries.OrderBy(id => id, StringComparer.Ordinal).ToList();
        public void Restore(int upgrades, List<string> sites)
        {
            UpgradeMask = upgrades & 7;
            discoveries.Clear();
            if (sites != null) foreach (string id in sites) if (!string.IsNullOrEmpty(id)) discoveries.Add(id);
            ApplyBenefits();
            Changed?.Invoke();
        }
        public void DiscoverLandmark(string id, string title)
        {
            if (string.IsNullOrEmpty(id) || !discoveries.Add(id)) return;
            LastDiscovery = title;
            GameMessageBus.Post("Discovered " + title + ". Search for expedition parts and return to the bunker console.");
            Changed?.Invoke();
        }
        public List<CraftingCost> UpgradeCost(int index)
        {
            var material = index == 0 ? wood : index == 1 ? scrap : iron;
            return new List<CraftingCost> {
                new CraftingCost { resource = expeditionParts, amount = 3 + index },
                new CraftingCost { resource = material, amount = 4 + index * 2 }
            };
        }
        public bool TryUpgrade(int index, out string result)
        {
            if (index < 0 || index >= 3) { result = "Unknown upgrade."; return false; }
            if (HasUpgrade(index)) { result = "Already installed."; return false; }
            var stats = GetComponent<SheltreSim.Survival.SurvivalStats>();
            if (stats == null || stats.IsDead || stats.health <= 0f) { result = "Cannot upgrade now."; return false; }
            if (DiscoveryCount < index + 1) { result = $"Discover {index + 1} landmarks first."; return false; }
            if (expeditionParts == null || wood == null || scrap == null || iron == null ||
                !CraftingSystem.TrySpend(GetComponent<Inventory>(), UpgradeCost(index)))
            { result = "Need more expedition parts and materials."; return false; }
            UpgradeMask |= 1 << index;
            ApplyBenefits();
            result = "Installed " + UpgradeNames[index] + ": " + UpgradeBenefits[index] + ".";
            GameMessageBus.Post(result);
            Changed?.Invoke();
            return true;
        }
        private void ApplyBenefits()
        {
            if (player != null) player.sprintSpeed = startingSprint * (HasUpgrade(2) ? 1.2f : 1f);
        }
        private void OnDestroy() { if (Active == this) Active = null; }
    }
}
