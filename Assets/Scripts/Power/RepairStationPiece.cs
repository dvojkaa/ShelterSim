using System;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Power
{
    /// <summary>
    /// Repairs damaged structures in reach, one at a time, paying in scrap from nearby crates. Every hit point comes out of
    /// HP already paid for, so nothing is ever repaired for free. Leftover paid HP is saved.
    /// </summary>
    public sealed class RepairStationPiece : PowerConsumerDevice
    {
        public const string ScrapId = "scrap_metal";
        private const float SoundEveryHp = 6f;

        public float powerKw = 1f;
        public float heatPerSecond = 0.5f;
        public float repairRange = 12f;
        public float crateRange = 8f;
        public float hpPerSecond = 3f;
        public float hpPerScrap = 40f;

        private float paidHp;
        private float hpSinceSound;
        private bool warnedNoScrap;
        private StructureHealth target;

        [Serializable]
        private class State { public float paidHp; }

        public float PaidHp => paidHp;
        public override string PowerName => "Repair Station";
        public override int PowerPriority => 1;
        public override float PowerDemand => FindTarget() != null && CanPay ? powerKw : 0f;
        public override float HeatOutput => Granted ? heatPerSecond : 0f;
        private bool CanPay => paidHp > 0f || StorageRegistry.CountNearby(PowerPosition, crateRange, ScrapId) > 0;

        private bool InRange(StructureHealth structure) =>
            (structure.transform.position - PowerPosition).sqrMagnitude <= repairRange * repairRange;

        private StructureHealth FindTarget()
        {
            if (target != null && target.isActiveAndEnabled && target.IsDamaged && InRange(target)) return target;
            target = null;
            float best = float.PositiveInfinity;
            foreach (StructureHealth structure in StructureHealth.All)
            {
                if (structure == null || !structure.IsDamaged || !InRange(structure)) continue;
                float distance = (structure.transform.position - PowerPosition).sqrMagnitude;
                if (distance < best) { best = distance; target = structure; }
            }
            return target;
        }

        public override string PowerStatus
        {
            get
            {
                StructureHealth current = FindTarget();
                if (current == null) return $"idle, nothing damaged within {repairRange:0} m";
                if (!CanPay) return $"stopped, no Scrap Metal in crates within {crateRange:0} m";
                string repairing = $"repairing {current.name.Replace("(Clone)", "")} ({Mathf.RoundToInt(current.Fraction * 100f)}%)";
                return Granted ? repairing : $"{NoPowerReason()} ({repairing})";
            }
        }

        public override void Advance(float seconds)
        {
            StructureHealth current = FindTarget();
            bool blocked = current != null && !CanPay;
            if (blocked && !warnedNoScrap) GameMessageBus.Post($"Repair Station stopped: no Scrap Metal in crates within {crateRange:0} m.");
            warnedNoScrap = blocked;
            if (!Granted || current == null) return;

            float want = Mathf.Min(hpPerSecond * seconds, current.maxHealth - current.Health);
            while (paidHp < want && StorageRegistry.TryTakeNearby(PowerPosition, crateRange, ScrapId, 1)) paidHp += hpPerScrap;
            float repaired = current.Repair(Mathf.Min(want, paidHp));
            paidHp = Mathf.Max(0f, paidHp - repaired);

            hpSinceSound += repaired;
            if (hpSinceSound < SoundEveryHp) return;
            hpSinceSound = 0f;
            GameAudio.PlayAt(Sfx.RepairTick, current.transform.position, 0.6f, 25f, 0.1f);
        }

        public override string CapturePieceState() => JsonUtility.ToJson(new State { paidHp = paidHp });

        public override void RestorePieceState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            if (saved != null) paidHp = PowerNetworks.Sane(saved.paidHp, 0f, hpPerScrap);
        }
    }
}
