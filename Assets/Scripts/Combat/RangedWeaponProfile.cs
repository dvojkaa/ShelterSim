using SheltreSim.Audio;
using SheltreSim.Crafting;
using UnityEngine;

namespace SheltreSim.Combat
{
    /// <summary>Per-weapon feel that isn't balance data: reload time, sounds, colours, recoil and the fallback model's size.</summary>
    public static class RangedWeaponProfile
    {
        public const float AutomaticRpm = 300f;

        public static bool IsAutomatic(ItemSpec spec) => spec.roundsPerMinute >= AutomaticRpm;
        public static bool IsPlasma(ItemSpec spec) => spec.energy && spec.splashRadius > 0f;

        public static float ReloadSeconds(string id)
        {
            switch (id)
            {
                case "pistol": return 1.4f;
                case "smg": return 1.8f;
                case "shotgun": return 2.8f;
                case "hunting_rifle": return 2.4f;
                case "assault_rifle": return 2.1f;
                case "laser_pistol": return 1.2f;
                case "laser_rifle": return 1.6f;
                case "plasma_rifle": return 2.2f;
                default: return 2f;
            }
        }

        public static string ShotSound(string id)
        {
            switch (id)
            {
                case "pistol": return Sfx.GunPistol;
                case "smg": return Sfx.GunSmg;
                case "shotgun": return Sfx.GunShotgun;
                case "hunting_rifle": case "assault_rifle": return Sfx.GunRifle;
                case "plasma_rifle": return Sfx.PlasmaShot;
                default: return Sfx.LaserShot;
            }
        }

        public static Color ShotColor(ItemSpec spec)
        {
            if (IsPlasma(spec)) return new Color(0.45f, 1f, 0.55f);
            return spec.energy ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.55f);
        }

        /// <summary>Viewmodel kick for one shot, 0..1: heavier shots kick more.</summary>
        public static float Kick(ItemSpec spec) => Mathf.Clamp(spec.damage * Mathf.Max(1, spec.pellets) / 90f, 0.2f, 1f);

        /// <summary>Fallback viewmodel size in metres: x width, y height, z length ahead of the grip.</summary>
        public static Vector3 FallbackSize(string id)
        {
            switch (id)
            {
                case "pistol": return new Vector3(0.035f, 0.05f, 0.17f);
                case "laser_pistol": return new Vector3(0.045f, 0.06f, 0.2f);
                case "smg": return new Vector3(0.045f, 0.07f, 0.3f);
                case "shotgun": return new Vector3(0.05f, 0.06f, 0.52f);
                case "hunting_rifle": return new Vector3(0.04f, 0.055f, 0.6f);
                case "assault_rifle": return new Vector3(0.05f, 0.075f, 0.46f);
                case "laser_rifle": return new Vector3(0.055f, 0.08f, 0.5f);
                case "plasma_rifle": return new Vector3(0.08f, 0.1f, 0.48f);
                default: return new Vector3(0.05f, 0.06f, 0.3f);
            }
        }
    }
}
