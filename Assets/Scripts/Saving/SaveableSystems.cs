using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Saving
{
    /// <summary>
    /// Per-device saved state for a build piece, stored once per placed instance. At most one per prefab.
    /// RestorePieceState runs on an inactive instance BEFORE Awake and OnEnable: set plain fields only.
    /// State is whatever string you choose (JsonUtility.ToJson of a small class is the convention).
    /// Null or empty means an older save or a piece with no state: keep the fresh defaults (e.g. empty and off).
    /// </summary>
    public interface IPieceState
    {
        string CapturePieceState();
        void RestorePieceState(string state);
    }

    /// <summary>
    /// A whole-game system with saved state, so systems don't need to change SaveManager or SaveData.
    /// Register in Awake or OnEnable, unregister when destroyed. SaveKey must be unique and never change.
    /// RestoreState(null) means the save has no section for this system: reset to a new game's state.
    /// Restore runs at the end of a load, after the phase, the world, pieces and the player are in place.
    /// </summary>
    public interface ISaveableSystem
    {
        string SaveKey { get; }
        string CaptureState();
        void RestoreState(string state);
    }

    public static class SaveableSystems
    {
        private static readonly List<ISaveableSystem> systems = new List<ISaveableSystem>();

        public static void Register(ISaveableSystem system) { if (!systems.Contains(system)) systems.Add(system); }
        public static void Unregister(ISaveableSystem system) => systems.Remove(system);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => systems.Clear();

        public static List<SaveData.SystemState> CaptureAll()
        {
            var list = new List<SaveData.SystemState>();
            foreach (ISaveableSystem system in systems)
                list.Add(new SaveData.SystemState { key = system.SaveKey, json = system.CaptureState() });
            return list;
        }

        public static void RestoreAll(List<SaveData.SystemState> saved)
        {
            var byKey = new Dictionary<string, string>();
            if (saved != null)
                foreach (SaveData.SystemState state in saved)
                    if (state != null && !string.IsNullOrEmpty(state.key)) byKey[state.key] = state.json;
            foreach (ISaveableSystem system in new List<ISaveableSystem>(systems))
            {
                byKey.TryGetValue(system.SaveKey, out string json);
                try { system.RestoreState(string.IsNullOrEmpty(json) ? null : json); }
                catch (System.Exception error) { Debug.LogError($"[SaveableSystems] {system.SaveKey} failed to restore: {error}"); }
            }
        }
    }
}
