using System.Collections.Generic;
using System.Text;
using SheltreSim.Interaction;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Items
{
    /// <summary>One rolled item stack inside a loot crate.</summary>
    [System.Serializable]
    public struct LootEntry
    {
        public ResourceDefinition resource;
        public int amount;
    }

    /// <summary>
    /// A single-use searchable container (furniture, a car trunk, etc.) placed at a
    /// "LootSpawn" marker by the scene-population tool. Unlike ResourceNode, it isn't a
    /// repeatable/charge-limited gather point — it's searched exactly once, and the
    /// outcome (empty or not) is pre-rolled externally via hasLoot/entries rather than
    /// decided by this component.
    ///
    /// Holds a LIST of entries (not a single resource/amount pair) since the M10 QC-era
    /// loot rebalance lets one crate contain more than one item stack — a playtester
    /// complaint ("10 crates, got 2 Water and nothing else") traced to crates handing out
    /// at most one stack each, not just the empty-crate rate.
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        public List<LootEntry> entries = new List<LootEntry>();
        public bool hasLoot = true;

        private bool searched;

        public string InteractPrompt => searched
            ? "Already searched"
            : "Press E to search";

        /// <summary>Exposed for SaveManager, which persists this via a hierarchy-path id.</summary>
        public bool Searched => searched;

        /// <summary>
        /// Restores save-loaded state. Called by SaveManager after matching this instance to
        /// a saved entry; kept separate from the private field so <c>searched</c> itself can
        /// stay private to the normal Interact() flow.
        /// </summary>
        public void RestoreSearchedState(bool isSearched)
        {
            searched = isSearched;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }
            if (searched)
            {
                GameMessageBus.Post("Already searched — nothing left.");
                return;
            }

            if (!hasLoot || entries == null || entries.Count == 0)
            {
                searched = true;
                GameMessageBus.Post("Searched — found nothing.");
                return;
            }

            Inventory inv = interactor.GetComponent<Inventory>();
            if (inv == null)
            {
                inv = interactor.GetComponentInParent<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot loot.");
                return;
            }

            searched = true;
            StringBuilder found = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                LootEntry entry = entries[i];
                if (entry.resource == null || string.IsNullOrEmpty(entry.resource.id) || entry.amount <= 0)
                {
                    continue;
                }

                inv.Add(entry.resource.id, entry.amount);

                if (found.Length > 0)
                {
                    found.Append(", ");
                }
                found.Append(entry.amount).Append(' ').Append(entry.resource.displayName);
            }

            GameMessageBus.Post(found.Length > 0 ? $"Found {found}." : "Searched — found nothing.");
        }
    }
}
