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
        public string ExposureStatus
        {
            get
            {
                if (player == null) return string.Empty;
                if (IsSheltered) return "SHELTERED — weather drain blocked";
                int seed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 20261108;
                ExposureRates(WorldLayout.BiomeIndexAt(player.position.x, player.position.z, seed), out float heat, out float cold);
                if (heat > 0f && cold > 0f) return "EXPOSED — heat drains water; cold drains food";
                if (heat > 0f) return "EXPOSED — heat drains water faster";
                if (cold > 0f) return "EXPOSED — cold drains food faster";
                return "EXPOSED — no extra weather drain";
            }
        }
        public event Action<string> OnWeatherChanged;
        public const float WeatherPeriod = 150f, WeatherFade = 20f;
        /// <summary>A load jumps the clock: presentation shouldn't announce that as the weather changing.</summary>
        public int RestoredAtFrame { get; private set; } = -100;
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
                IsSheltered = HasRoof();
            }
            UpdateWeather();
        }

        public void RestoreTime(float seconds)
        {
            ElapsedSeconds = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0f, seconds);
            RestoredAtFrame = Time.frameCount;
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
            int bucket = Mathf.FloorToInt(ElapsedSeconds / WeatherPeriod);
            string next = WeatherFor(bucket, biome, seed);
            float intoBucket = ElapsedSeconds - bucket * WeatherPeriod;
            float peak = .7f + (WorldLayout.Hash(bucket, 7, seed + 733) % 31) / 100f;
            WeatherIntensity = next == "Clear" ? 0f : peak * Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(Mathf.Min(intoBucket, WeatherPeriod - intoBucket) / WeatherFade));
            if (next != WeatherName)
            {
                WeatherName = next;
                OnWeatherChanged?.Invoke(next);
            }
            return biome;
        }

        // Each period is clear, fog or precipitation. The same category is never rolled twice running, so the sky
        // always changes within a few minutes. Biome only decides the form it takes. Clear used to come up about
        // half the time per biome, with long runs (the desert stayed clear for most of an hour), and the sky could
        // look the same for 10 minutes or more. The sequence stays deterministic and doesn't need saving.
        private static int RawCategory(int bucket, int seed)
        {
            int roll = WorldLayout.Hash(bucket, 11, seed + 481) % 100;
            return roll < 34 ? 0 : roll < 50 ? 1 : 2;
        }

        private static int Category(int bucket, int seed)
        {
            int category = RawCategory(bucket, seed);
            if (bucket > 0 && category == RawCategory(bucket - 1, seed))
                category = (category + 1 + WorldLayout.Hash(bucket, 13, seed + 577) % 2) % 3;
            return category;
        }

        /// <summary>Pure: what the weather is in a given period and biome. Also used by the probe.</summary>
        public static string WeatherFor(int bucket, int biome, int seed)
        {
            int category = Category(bucket, seed);
            if (category == 0) return "Clear";
            if (category == 1) return "Fog";
            if (biome == 3) return "Snow";
            if (biome == 2) return "Fog";   // no rain in the red desert: blowing dust, drawn as fog
            if (biome == 5 && WorldLayout.Hash(bucket, 17, seed + 601) % 2 == 0) return "Snow";
            return "Rain";
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
            ExposureRates(biome, out float heat, out float cold);
            stats.thirst = Mathf.Clamp(stats.thirst - heat * seconds, 0f, stats.maxThirst);
            stats.hunger = Mathf.Clamp(stats.hunger - cold * seconds, 0f, stats.maxHunger);
        }

        private void ExposureRates(int biome, out float heat, out float cold)
        {
            float protection = ExpeditionProgression.Active != null ? Mathf.Clamp01(ExpeditionProgression.Active.ExposureProtection) : 1f;
            var gear = player != null ? player.GetComponent<SheltreSim.Crafting.PlayerGear>() : null;
            if (gear != null) protection *= Mathf.Clamp01(gear.ExposureMultiplier);
            heat = biome == 2 && Hour > 10f && Hour < 17f ? .055f : 0f;
            cold = biome == 3 ? .04f : 0f;
            if (Hour < 6f || Hour > 20f) cold += .025f;
            if (WeatherName == "Rain" || WeatherName == "Snow") cold += .035f * WeatherIntensity;
            heat *= protection;
            cold *= protection;
        }
    }
}
