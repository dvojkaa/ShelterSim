using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// A loot container a boss keeps shut: a camp's chest or a lair's store. The lock is the guard slot's Defeated, which
    /// the chunk saves, so it survives unloading, save and load. Searched is saved like any chunk crate (it is a LootContainer).
    /// Touching it while locked wakes the guard.
    /// </summary>
    public sealed class EncounterChest : LootContainer, IInteractable
    {
        public ChunkEncounterSlot guard;
        public string title = "Chest";
        public string lockedMessage = "Locked.";

        /// <summary>Locked while the guard lives. A guard that never spawned (missing prefab) can't keep it shut forever.</summary>
        public bool Locked => guard != null && !guard.Defeated && guard.Actor != null && !guard.Actor.IsDead;

        public new string InteractPrompt => Locked ? title + " (locked)" : Searched ? title + ": already searched" : "Press E to open the " + title.ToLowerInvariant();

        public new void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            if (Locked)
            {
                var actor = guard.Actor.GetComponent<SpeciesActor>();
                if (actor != null) SpeciesBehaviours.Wake(actor);
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.LootEmpty, transform.position, 0.6f, 15f);
                GameMessageBus.Post(lockedMessage);
                return;
            }
            base.Interact(interactor);
        }
    }
}
