using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Audio
{
    /// <summary>
    /// The motor (or the chain of the bicycle) of the vehicle the player is riding. Added to the player at runtime by GameAudio. Purely
    /// observes public state: whether the rider is on a vehicle (FirstPersonController.IsRiding, VehicleMountView.IsAttachedTo) and how
    /// fast that vehicle actually moves (its own transform), so vehicle code needs no audio of its own. The pitch follows the speed.
    /// The loops come from ArtSource/audio (section m29).
    /// </summary>
    public sealed class VehicleAudio : MonoBehaviour
    {
        private struct Profile
        {
            public string key;
            public float topSpeed, idlePitch, topPitch, idleVolume, topVolume;
            public bool silentWhenStill;
        }

        private static readonly Profile Buggy = new Profile { key = Sfx.MotorBuggy, topSpeed = 22f, idlePitch = 0.72f, topPitch = 1.85f, idleVolume = 0.34f, topVolume = 0.8f };
        private static readonly Profile Van = new Profile { key = Sfx.MotorVan, topSpeed = 18f, idlePitch = 0.8f, topPitch = 1.7f, idleVolume = 0.4f, topVolume = 0.85f };
        private static readonly Profile Bicycle = new Profile { key = Sfx.BikeChain, topSpeed = 13f, idlePitch = 0.85f, topPitch = 1.55f, idleVolume = 0f, topVolume = 0.5f, silentWhenStill = true };

        private FirstPersonController rider;
        private VehicleMountView mountView;
        private Transform vehicle;
        private Profile profile;
        private AudioSource source;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private float startupUntil;

        /// <summary>For probes: the key currently playing, or null.</summary>
        public string PlayingKey => source != null && source.isPlaying ? profile.key : null;

        private void Awake() => rider = GetComponent<FirstPersonController>();

        private void Update()
        {
            if (rider == null || !rider.IsRiding)
            {
                Release();
                return;
            }
            if (vehicle == null && !FindVehicle()) { Release(); return; }

            Vector3 position = vehicle.position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 moved = position - lastPosition;
            moved.y = 0f;
            lastPosition = position;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, moved.magnitude / dt, 1f - Mathf.Exp(-6f * dt));

            float t = Mathf.Clamp01(smoothedSpeed / profile.topSpeed);
            float volume = Mathf.Lerp(profile.idleVolume, profile.topVolume, t);
            if (profile.silentWhenStill) volume *= Mathf.Clamp01(smoothedSpeed / 1.5f);
            if (Time.time < startupUntil) volume = 0f; // The ignition sound covers the start.
            if (Time.timeScale == 0f) volume = 0f; // Paused: the engine does not run on.
            Apply(volume, Mathf.Lerp(profile.idlePitch, profile.topPitch, t));
        }

        private bool FindVehicle()
        {
            if (mountView == null) mountView = VehicleMountView.For(rider);
            if (mountView == null) return false;
            vehicle = Match<RideableScoutBuggy>(Buggy) ?? Match<RideableArmoredVan>(Van) ?? Match<RideableBicycle>(Bicycle);
            if (vehicle == null) return false;
            lastPosition = vehicle.position;
            smoothedSpeed = 0f;
            if (profile.key != Sfx.BikeChain)
            {
                // Ignition: the starter and the engine catching, then the idle loop takes over.
                GameAudio.PlayAt(Sfx.EngineStart, vehicle.position, 0.85f, 40f);
                startupUntil = Time.time + 1.5f;
            }
            else startupUntil = 0f;
            return true;
        }

        private Transform Match<T>(Profile candidate) where T : MonoBehaviour
        {
            foreach (T v in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (!mountView.IsAttachedTo(v.transform)) continue;
                profile = candidate;
                return v.transform;
            }
            return null;
        }

        private void Apply(float volume, float pitch)
        {
            if (source == null || source.clip == null || source.transform.parent != vehicle)
            {
                AudioClip clip = GameAudio.GetClip(profile.key);
                if (clip == null) return;
                if (source == null) source = new GameObject("VehicleAudioSource").AddComponent<AudioSource>();
                source.transform.SetParent(vehicle, false);
                source.transform.localPosition = Vector3.zero;
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0.65f; // Mostly in the cab, still located.
                source.dopplerLevel = 0f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                source.maxDistance = 45f;
            }
            source.volume = volume * GameAudio.EffectsVolume;
            source.pitch = pitch;
            if (volume > 0.001f && !source.isPlaying) source.Play();
            else if (volume <= 0.001f && source.isPlaying) source.Pause();
        }

        private void Release()
        {
            if (vehicle != null && profile.key != Sfx.BikeChain) GameAudio.PlayAt(Sfx.EngineStop, vehicle.position, 0.75f, 40f);
            vehicle = null;
            if (source != null && source.isPlaying) source.Stop();
        }

        private void OnDestroy()
        {
            if (source != null) Destroy(source.gameObject);
        }
    }
}
