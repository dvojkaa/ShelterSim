using UnityEngine;

namespace SheltreSim.Audio
{
    /// <summary>Soft ceiling on the final mix. Below the knee a sample passes untouched, so it never colours normal play.</summary>
    public sealed class AudioLimiter : MonoBehaviour
    {
        private const float Knee = 0.8f;
        private const float Range = 1f - Knee;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float level = Mathf.Abs(data[i]);
                if (level <= Knee) continue;
                float excess = (level - Knee) / Range;
                float limited = Knee + Range * (excess / (1f + excess));
                data[i] = data[i] > 0f ? limited : -limited;
            }
        }
    }
}
