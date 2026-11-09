using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Power
{
    /// <summary>One message when a network overheats and one when it recovers. Creates itself after every scene load.</summary>
    public sealed class PowerAlerts : MonoBehaviour
    {
        private const float CheckSeconds = 0.5f;

        private static PowerAlerts instance;
        private static bool hooked;

        private readonly HashSet<Vector3Int> overheated = new HashSet<Vector3Int>();
        private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        private float timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; hooked = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (instance == null) new GameObject("PowerAlerts").AddComponent<PowerAlerts>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            timer += Time.unscaledDeltaTime;
            if (timer < CheckSeconds) return;
            timer = 0f;

            seen.Clear();
            foreach (PowerNetwork network in PowerGrid.Networks)
            {
                // The same cell the grid keeps heat in. Networks are rebuilt each tick, so they have no other identity.
                Vector3Int cell = new Vector3Int(Mathf.RoundToInt(network.Anchor.x / 200f), 0, Mathf.RoundToInt(network.Anchor.z / 200f));
                seen.Add(cell);
                if (network.Overheated && overheated.Add(cell))
                {
                    GameAudio.Play2D(Sfx.WarnVital, 0.5f, 0f);
                    GameMessageBus.Post("A power network overheated: output is halved. Build or power a Cooling Unit.");
                }
                else if (!network.Overheated && overheated.Remove(cell))
                {
                    GameMessageBus.Post("The power network cooled down. Output is back to normal.");
                }
            }
            overheated.IntersectWith(seen);
        }
    }
}
