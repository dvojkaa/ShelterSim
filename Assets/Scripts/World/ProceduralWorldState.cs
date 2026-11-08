using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SheltreSim.Items;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Only player changes are cached; generated content is reconstructed from its seed.</summary>
    public static class ProceduralWorldState
    {
        [Serializable]
        public class ChunkState
        {
            public int x, z;
            public List<NodeState> nodes = new List<NodeState>();
            public List<CrateState> crates = new List<CrateState>();
            public List<EncounterState> encounters = new List<EncounterState>();
        }

        [Serializable]
        public class NodeState
        {
            public string id;
            public int charges;
            public bool depleted;
            public float elapsed;
        }

        [Serializable]
        public class CrateState
        {
            public string id;
            public bool searched;
        }

        [Serializable]
        public class EncounterState
        {
            public string id;
            public bool defeated;
            public float health;
        }

        private static readonly ConditionalWeakTable<GameObject, Dictionary<string, int>> Baselines
            = new ConditionalWeakTable<GameObject, Dictionary<string, int>>();

        // Weak keys do not retain unloaded chunks. Values contain no Unity object references.
        // Keep the generator's initial charge count before applying saved state.
        public static void Initialize(GameObject chunk)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (Baselines.TryGetValue(chunk, out _)) return;
            Dictionary<string, int> baseline = new Dictionary<string, int>();
            foreach (ResourceNode node in chunk.GetComponentsInChildren<ResourceNode>(true))
                baseline.Add(RelativeId(chunk.transform, node.transform), node.remainingCharges);
            Baselines.Add(chunk, baseline);
        }

        public static ChunkState Capture(GameObject chunk, int x, int z)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            Baselines.TryGetValue(chunk, out Dictionary<string, int> baseline);
            ChunkState state = new ChunkState { x = x, z = z };
            foreach (ResourceNode node in chunk.GetComponentsInChildren<ResourceNode>(true))
            {
                string id = RelativeId(chunk.transform, node.transform);
                if (baseline != null && baseline.TryGetValue(id, out int original)
                    && original == node.remainingCharges && !node.IsDepleted && node.RegrowthElapsed <= 0f) continue;
                state.nodes.Add(new NodeState
                {
                    id = id,
                    charges = node.remainingCharges,
                    depleted = node.IsDepleted,
                    elapsed = node.RegrowthElapsed,
                });
            }
            foreach (LootContainer crate in chunk.GetComponentsInChildren<LootContainer>(true))
                if (crate.Searched)
                    state.crates.Add(new CrateState { id = RelativeId(chunk.transform, crate.transform), searched = true });
            foreach (var slot in chunk.GetComponentsInChildren<ChunkEncounterSlot>(true))
                if(slot.Defeated || (slot.Actor!=null && slot.Health < slot.Actor.maxHealth))
                state.encounters.Add(new EncounterState { id = RelativeId(chunk.transform,slot.transform), defeated=slot.Defeated, health=slot.Health });
            state.nodes.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            state.crates.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return state;
        }

        public static void Apply(GameObject chunk, ChunkState state)
        {
            if (chunk == null) throw new ArgumentNullException(nameof(chunk));
            if (state == null) return;
            // Initialize before restoring any charges, including when the caller only uses Apply.
            Initialize(chunk);
            Dictionary<string, ResourceNode> nodes = new Dictionary<string, ResourceNode>();
            foreach (ResourceNode node in chunk.GetComponentsInChildren<ResourceNode>(true))
                nodes.Add(RelativeId(chunk.transform, node.transform), node);
            if (state.nodes != null)
                foreach (NodeState saved in state.nodes)
                    if (saved != null && nodes.TryGetValue(saved.id, out ResourceNode node))
                        node.RestoreState(saved.charges, saved.depleted, saved.elapsed);
            var encounters = new Dictionary<string, ChunkEncounterSlot>();
            foreach(var slot in chunk.GetComponentsInChildren<ChunkEncounterSlot>(true)) encounters.Add(RelativeId(chunk.transform,slot.transform),slot);
            if(state.encounters!=null) foreach(var saved in state.encounters)
                if(saved!=null&&encounters.TryGetValue(saved.id,out var slot))slot.Restore(saved.defeated,saved.health);
            Dictionary<string, LootContainer> crates = new Dictionary<string, LootContainer>();
            foreach (LootContainer crate in chunk.GetComponentsInChildren<LootContainer>(true))
                crates.Add(RelativeId(chunk.transform, crate.transform), crate);
            if (state.crates != null)
                foreach (CrateState saved in state.crates)
                    if (saved != null && crates.TryGetValue(saved.id, out LootContainer crate))
                        crate.RestoreSearchedState(saved.searched);
        }

        private static string RelativeId(Transform root, Transform target)
        {
            List<string> segments = new List<string>();
            for (Transform current = target; current != root; current = current.parent)
            {
                if (current == null) throw new ArgumentException("The saved object must belong to the chunk.");
                int ordinal = 0;
                for (int i = 0; i < current.GetSiblingIndex(); i++)
                    if (current.parent.GetChild(i).name == current.name) ordinal++;
                segments.Add(Uri.EscapeDataString(current.name) + "[" + ordinal + "]");
            }
            segments.Reverse();
            return segments.Count == 0 ? "." : string.Join("/", segments);
        }
    }

}
