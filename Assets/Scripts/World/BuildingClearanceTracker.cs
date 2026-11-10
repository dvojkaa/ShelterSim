using System;
using System.Collections.Generic;
using SheltreSim.Saving;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.World
{
    /// <summary>Persistent atlas state for generated buildings whose searchable loot is fully cleared.</summary>
    public sealed class BuildingClearanceTracker : MonoBehaviour, ISaveableSystem
    {
        [Serializable]
        public sealed class ClearedBuilding
        {
            public string id;
            public int seed;
            public float x;
            public float z;
        }

        [Serializable]
        private sealed class State
        {
            public List<ClearedBuilding> cleared = new List<ClearedBuilding>();
        }

        private static BuildingClearanceTracker instance;
        private static bool hooked;
        private readonly Dictionary<string, ClearedBuilding> cleared = new Dictionary<string, ClearedBuilding>(StringComparer.Ordinal);

        public string SaveKey => "building_clearance";
        public static IEnumerable<ClearedBuilding> Cleared => Ensure().cleared.Values;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            Ensure();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (instance != null) instance.cleared.Clear();
            Ensure();
        }

        public static BuildingClearanceTracker Ensure()
        {
            if (instance == null) new GameObject("BuildingClearanceTracker").AddComponent<BuildingClearanceTracker>();
            return instance;
        }

        public static bool MarkCleared(string id, int seed, Vector3 position)
        {
            if (string.IsNullOrEmpty(id) || !Finite(position.x) || !Finite(position.z)) return false;
            BuildingClearanceTracker tracker = Ensure();
            if (tracker.cleared.ContainsKey(id)) return false;
            tracker.cleared.Add(id, new ClearedBuilding { id = id, seed = seed, x = position.x, z = position.z });
            return true;
        }

        public string CaptureState()
        {
            var state = new State();
            foreach (ClearedBuilding building in cleared.Values) state.cleared.Add(building);
            state.cleared.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            cleared.Clear();
            if (string.IsNullOrEmpty(json)) return;
            State state = null;
            try { state = JsonUtility.FromJson<State>(json); }
            catch (ArgumentException) { state = null; }
            if (state?.cleared == null) return;
            foreach (ClearedBuilding building in state.cleared)
                if (building != null && !string.IsNullOrEmpty(building.id) &&
                    Finite(building.x) && Finite(building.z)) cleared[building.id] = building;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SaveableSystems.Register(this);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            SaveableSystems.Unregister(this);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
