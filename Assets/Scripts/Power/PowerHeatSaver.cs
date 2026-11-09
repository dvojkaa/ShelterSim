using System;
using SheltreSim.Saving;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Power
{
    /// <summary>Saves the heat of the network at the bunker. Creates itself after every scene load, no scene edit.</summary>
    public sealed class PowerHeatSaver : MonoBehaviour, ISaveableSystem
    {
        private static PowerHeatSaver instance;
        private static bool hooked;

        [Serializable]
        private class State { public float heat; }

        public string SaveKey => "power";

        public string CaptureState() => JsonUtility.ToJson(new State { heat = PowerGrid.CaptureHeat(WorldLayout.BunkerCenter) });

        public void RestoreState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            PowerGrid.RestoreHeat(WorldLayout.BunkerCenter, saved != null ? saved.heat : 0f);
        }

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
            if (instance == null) new GameObject("PowerHeatSaver").AddComponent<PowerHeatSaver>();
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
}
