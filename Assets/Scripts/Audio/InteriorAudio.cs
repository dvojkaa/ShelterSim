using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Audio
{
    /// <summary>
    /// Room tone and the odd creak while the player is inside a building. Added to the player by GameAudio. Purely observes
    /// BuildingInterior.PlayerIn, so building code needs no audio of its own. The outdoor ambience keeps playing; the room tone
    /// fades in underneath and takes the edge off the wind.
    /// </summary>
    public sealed class InteriorAudio : MonoBehaviour
    {
        private const float FadeSeconds = 1.2f;
        private AudioSource source;

        /// <summary>For probes: the room tone is audible right now.</summary>
        public bool Playing => source != null && source.isPlaying;
        private float level;
        private float nextCreak;
        private readonly System.Random random = new System.Random(4711);

        private void Update()
        {
            bool inside = BuildingInterior.PlayerIn != null && Time.timeScale > 0f;
            float target = inside ? 1f : 0f;
            level = Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / FadeSeconds);

            if (level <= 0f)
            {
                if (source != null && source.isPlaying) source.Pause();
                return;
            }
            if (source == null)
            {
                AudioClip clip = GameAudio.GetClip(Sfx.AmbInterior);
                if (clip == null) return;
                source = gameObject.AddComponent<AudioSource>();
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.volume = 0.55f * level * GameAudio.EffectsVolume;
            if (!source.isPlaying) source.Play();

            if (inside && Time.time >= nextCreak)
            {
                nextCreak = Time.time + 14f + (float)random.NextDouble() * 22f;
                GameAudio.Play2D(Sfx.AmbCreak, 0.35f, 0.08f, (float)random.NextDouble() * 0.5f);
            }
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source);
        }
    }
}
