using System;
using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Hit points of a build piece. Raiders damage it, repairs restore it, and at zero the piece is destroyed.
    /// SaveManager stores Health per piece. RestoreHealth runs before Awake, so nothing here needs Awake.
    /// </summary>
    public sealed class StructureHealth : MonoBehaviour
    {
        public float maxHealth = 100f;
        /// <summary>Lower numbers are attacked first by raiders with no better target.</summary>
        public int targetPriority = 5;

        private float health = -1f;
        private static readonly List<StructureHealth> all = new List<StructureHealth>();

        public static IReadOnlyList<StructureHealth> All => all;
        public static event Action<StructureHealth> Destroyed;

        public float Health => health < 0f ? maxHealth : health;
        public float Fraction => maxHealth > 0f ? Mathf.Clamp01(Health / maxHealth) : 1f;
        public bool IsDamaged => Health < maxHealth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { all.Clear(); Destroyed = null; }

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() => all.Remove(this);

        public void RestoreHealth(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) return;
            health = Mathf.Min(value, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) || Health <= 0f) return;
            health = Mathf.Max(0f, Health - amount);
            GameAudio.PlayAt(Sfx.StructureHit, transform.position, 0.7f, 30f, 0.08f);
            if (health > 0f) return;
            GameAudio.PlayAt(Sfx.StructureBreak, transform.position, 1f, 45f);
            var piece = GetComponent<SheltreSim.Saving.PlacedPiece>();
            GameMessageBus.Post($"{Label(piece)} was destroyed!");
            Destroyed?.Invoke(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        /// <summary>Returns the amount actually repaired.</summary>
        public float Repair(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return 0f;
            float before = Health;
            health = Mathf.Min(maxHealth, before + amount);
            return health - before;
        }

        private string Label(SheltreSim.Saving.PlacedPiece piece)
        {
            string id = piece != null ? piece.pieceId : name;
            return string.IsNullOrEmpty(id) ? "A structure" : "A " + id.Replace("piece_", "").Replace('_', ' ');
        }
    }
}
