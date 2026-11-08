using System;
using SheltreSim.Player;
using SheltreSim.Survival;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Saved twenty minute days and seeded local weather. Presentation belongs to the visual/audio systems.</summary>
    public sealed class WorldAtmosphere : MonoBehaviour
    {
        public float ElapsedSeconds { get; private set; }
        public float Hour => Mathf.Repeat(8f + ElapsedSeconds / 50f, 24f);
        public string WeatherName { get; private set; } = "Clear";
        public float WeatherIntensity { get; private set; }
        public bool IsSheltered { get; private set; }
        public event Action<string> OnWeatherChanged;
        private Transform player;
        private SurvivalStats stats;
        private float shelterTimer;
        private readonly RaycastHit[] roofHits = new RaycastHit[24];

        private void Start()
        {
            var controller = FindFirstObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                stats = controller.GetComponent<SurvivalStats>();
            }
            UpdateWeather();
        }

        public void RestoreTime(float seconds)
        {
            ElapsedSeconds = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0f, seconds);
            shelterTimer = 0f;
            UpdateWeather();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            float seconds = Mathf.Max(0f, Time.deltaTime);
            ElapsedSeconds += seconds;
            int biome = UpdateWeather();
            if (player == null) return;
            shelterTimer -= seconds;
            if (shelterTimer <= 0f)
            {
                shelterTimer = .5f;
                IsSheltered = HasRoof();
            }
            // A long render frame cannot charge a large, sudden exposure penalty.
            ApplyExposure(biome, Mathf.Min(seconds, .25f));
        }

        private int UpdateWeather()
        {
            int seed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 20261108;
            int biome = player != null ? WorldLayout.BiomeIndexAt(player.position.x, player.position.z, seed) : 0;
            int roll = WorldLayout.Hash(Mathf.FloorToInt(ElapsedSeconds / 180f), biome, seed + 481) % 100;
            string next = roll < 48 ? "Clear" : roll < 68 ? "Fog"
                : biome == 3 ? "Snow" : biome == 2 ? "Clear" : "Rain";
            WeatherIntensity = next == "Clear" ? 0f : Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(Mathf.Min(ElapsedSeconds % 180f, 180f - ElapsedSeconds % 180f) / 10f));
            if (next != WeatherName)
            {
                WeatherName = next;
                OnWeatherChanged?.Invoke(next);
            }
            return biome;
        }

        private bool HasRoof()
        {
            if (WorldLayout.UndergroundAmount(player.position + Vector3.up * 1.5f) > .5f) return true;
            int count = Physics.RaycastNonAlloc(player.position + Vector3.up * 1.5f, Vector3.up, roofHits, 35f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = roofHits[i];
                if (!hit.transform.IsChildOf(player) && hit.transform != player && hit.normal.y < -.25f) return true;
            }
            return false;
        }

        private void ApplyExposure(int biome, float seconds)
        {
            if (IsSheltered || stats == null || stats.decayPaused || stats.IsDead || seconds <= 0f) return;
            float protection = ExpeditionProgression.Active != null ? Mathf.Clamp01(ExpeditionProgression.Active.ExposureProtection) : 1f;
            var gear = stats.GetComponent<SheltreSim.Crafting.PlayerGear>();
            if (gear != null) protection *= Mathf.Clamp01(gear.ExposureMultiplier);
            float heat = biome == 2 && Hour > 10f && Hour < 17f ? .055f : 0f;
            float cold = biome == 3 ? .04f : 0f;
            if (Hour < 6f || Hour > 20f) cold += .025f;
            if (WeatherName == "Rain" || WeatherName == "Snow") cold += .035f * WeatherIntensity;
            stats.thirst = Mathf.Clamp(stats.thirst - heat * protection * seconds, 0f, stats.maxThirst);
            stats.hunger = Mathf.Clamp(stats.hunger - cold * protection * seconds, 0f, stats.maxHunger);
        }
    }
}
