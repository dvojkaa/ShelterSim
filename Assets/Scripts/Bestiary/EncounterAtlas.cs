using System;
using System.Collections.Generic;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Which camps and boss lairs the player has found, for the atlas. Saved as the "encounters" section.
    /// Ids are "camp:seed:x:z" and "lair:seed:x:z": the place is recomputed from the seed, nothing else is stored.
    /// </summary>
    public sealed class EncounterAtlas : MonoBehaviour, ISaveableSystem
    {
        private static EncounterAtlas instance;
        private static bool hooked;
        private static readonly HashSet<string> discovered = new HashSet<string>();

        [Serializable]
        private class State { public List<string> found = new List<string>(); }

        public string SaveKey => "encounters";
        public static IEnumerable<string> Discovered => discovered;
        public static bool IsDiscovered(string id) => !string.IsNullOrEmpty(id) && discovered.Contains(id);

        /// <summary>Marks a site found. True the first time.</summary>
        public static bool Discover(string id)
        {
            Ensure();
            return !string.IsNullOrEmpty(id) && discovered.Add(id);
        }

        public static string CampId(Vector2Int c, int seed) => "camp:" + seed + ":" + c.x + ":" + c.y;
        public static string LairId(Vector2Int c, int seed) => "lair:" + seed + ":" + c.x + ":" + c.y;

        public static bool TryParse(string id, out bool camp, out Vector2Int coordinate, out int seed)
        {
            camp = false; coordinate = default; seed = 0;
            if (string.IsNullOrEmpty(id)) return false;
            string[] p = id.Split(':');
            if (p.Length != 4 || (p[0] != "camp" && p[0] != "lair") || !int.TryParse(p[1], out seed) ||
                !int.TryParse(p[2], out int x) || !int.TryParse(p[3], out int z)) return false;
            camp = p[0] == "camp";
            coordinate = new Vector2Int(x, z);
            return true;
        }

        public string CaptureState()
        {
            var state = new State();
            state.found.AddRange(discovered);
            state.found.Sort(string.CompareOrdinal);
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            discovered.Clear();
            if (string.IsNullOrEmpty(json)) return;
            State state = null;
            try { state = JsonUtility.FromJson<State>(json); } catch (Exception) { state = null; }
            if (state?.found != null) foreach (string id in state.found) if (!string.IsNullOrEmpty(id)) discovered.Add(id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; hooked = false; discovered.Clear(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => { discovered.Clear(); Ensure(); };
            }
            Ensure();
        }

        public static void Ensure()
        {
            if (instance == null) new GameObject("EncounterAtlas").AddComponent<EncounterAtlas>();
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
    }

    /// <summary>On a camp or lair root: discovers it for the atlas when the player first comes within 60 m.</summary>
    public sealed class EncounterSite : MonoBehaviour
    {
        public const float DiscoverRadius = 60f;
        public string siteId;
        public string title;
        private Transform player;
        private float nextCheck;

        private void Update()
        {
            if (Time.time < nextCheck || EncounterAtlas.IsDiscovered(siteId)) return;
            nextCheck = Time.time + 0.5f;
            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null) return;
                player = controller.transform;
            }
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < DiscoverRadius * DiscoverRadius && EncounterAtlas.Discover(siteId))
                GameMessageBus.Post($"Spotted: {title}. Marked on the atlas.");
        }
    }
}
