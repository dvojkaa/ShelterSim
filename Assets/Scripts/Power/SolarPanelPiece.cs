using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>Free power when the sun is up. Follows the real clock and weather, gives nothing underground.</summary>
    public sealed class SolarPanelPiece : PowerDevice, IPowerSource
    {
        private const float Sunrise = 5f, Sunset = 19f, OvercastFactor = 0.4f, FullWeatherIntensity = 0.7f;

        public float peakKw = 1.5f;

        private WorldAtmosphere atmosphere;

        public override string PowerName => "Solar Panel";
        public override bool PowerRunning => PowerOutput > 0f;
        float IPowerSource.HeatOutput => 0f;

        /// <summary>Pure: 0 at night, 1 at noon in a clear sky, about 0.4 of that at the peak of rain, snow or fog.</summary>
        public static float SkyFactor(float hour, string weather, float intensity)
        {
            if (hour <= Sunrise || hour >= Sunset) return 0f;
            float daylight = Mathf.Sin(Mathf.PI * (hour - Sunrise) / (Sunset - Sunrise));
            return daylight * Mathf.Lerp(1f, OvercastFactor, Overcast(weather, intensity));
        }

        private static float Overcast(string weather, float intensity) =>
            weather == "Rain" || weather == "Snow" || weather == "Fog" ? Mathf.Clamp01(intensity / FullWeatherIntensity) : 0f;

        private WorldAtmosphere Atmosphere
        {
            get
            {
                if (atmosphere == null && ProceduralWorld.Active != null) atmosphere = ProceduralWorld.Active.GetComponent<WorldAtmosphere>();
                return atmosphere;
            }
        }

        private bool Underground => WorldLayout.UndergroundAmount(PowerPosition) > 0.5f;

        public float PowerOutput
        {
            get
            {
                if (Underground || Atmosphere == null) return 0f;
                return peakKw * SkyFactor(atmosphere.Hour, atmosphere.WeatherName, atmosphere.WeatherIntensity);
            }
        }

        public override string PowerStatus
        {
            get
            {
                if (Underground) return "no output, no sun reaches it underground";
                if (Atmosphere == null) return "no output";
                float output = PowerOutput;
                if (output <= 0f) return "no output, it is night";
                string line = $"{output:F2} of {peakKw:0.#} kW";
                if (Overcast(atmosphere.WeatherName, atmosphere.WeatherIntensity) > 0.05f) return $"{line}, low because of {atmosphere.WeatherName.ToLowerInvariant()}";
                float hour = atmosphere.Hour;
                return hour < 9f || hour > 15f ? $"{line}, low because the sun is low" : line;
            }
        }

        protected override void JoinGrid() => PowerGrid.Register(this);
        protected override void LeaveGrid() => PowerGrid.Unregister(this);
    }
}
