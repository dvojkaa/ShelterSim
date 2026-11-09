using SheltreSim.Audio;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Breacher: anything the player built or a closed door in its way gets a slow, loud, hard blow (StructureDamage). In a
    /// personal fight it is slow: long wind-up, long recovery.
    /// </summary>
    public sealed class BreacherBrain : SpeciesBrain
    {
        public const float StructureDamage = 45f;
        public const float StructureWindup = 0.9f;
        public int StructureHits { get; private set; }
        private Attack attack;
        private Collider targetCollider;
        private SheltreSim.Building.StructureHealth targetStructure;
        private SheltreSim.Interaction.InteractableDoor targetDoor;
        private float breakStarted = -1f;

        protected override void OnStart()
        {
            attack = Melee("hammer", 0.9f, Spec.AttackRange, 45f, 1.6f, 2.0f, PoseKind.ArmsUp, PoseKind.Slam, Sfx.RaiderGrowl);
            attack.TurnRate = 90f;
        }

        protected override Attack ChooseAttack(float distance) => attack;

        protected override bool OnBlocked(Collider blocker)
        {
            var door = blocker.GetComponentInParent<SheltreSim.Interaction.InteractableDoor>();
            var structure = blocker.GetComponentInParent<SheltreSim.Building.StructureHealth>();
            if (door != null && (door.IsOpen || door.IsBroken)) door = null;
            if (door == null && structure == null) return false;
            targetCollider = blocker;
            targetDoor = door;
            targetStructure = door == null ? structure : null;
            breakStarted = -1f;
            return true;
        }

        protected override bool TickSpecial(Vector3 toPlayer, float distance)
        {
            if (targetCollider == null) return false;
            bool gone = targetDoor != null ? targetDoor.IsOpen || targetDoor.IsBroken
                : targetStructure == null || !targetCollider.gameObject.activeInHierarchy;
            // The player right in front matters more than the wall.
            if (gone || (distance <= attack.StartRange && PlayerVisible)) { targetCollider = null; return false; }
            Vector3 closest = targetCollider.bounds.ClosestPoint(Enemy.CenterPosition);
            Vector3 flat = Flat(closest - Enemy.CenterPosition);
            if (flat.magnitude - Spec.Radius > 1.0f) { targetCollider = null; return false; }
            Detail = "breaking " + targetCollider.name;
            Move(Vector3.zero);
            Face(flat, 180f);
            if (breakStarted < 0f) { breakStarted = Time.time; pose?.Set(PoseKind.ArmsUp, 300f); GameAudio.PlayAt(Sfx.RaiderGrowl, transform.position + Vector3.up, 0.8f, 35f); }
            Enemy.BrainPoseWeapon((Time.time - breakStarted) / StructureWindup);
            if (Time.time - breakStarted < StructureWindup) return true;
            pose?.Set(PoseKind.Slam, 900f);
            Enemy.BrainPoseWeapon(-1f);
            StructureHits++;
            GameAudio.PlayAt(Sfx.StructureHit, closest, 1f, 50f);
            if (targetDoor != null) targetDoor.TakeRaidDamage(StructureDamage);
            else targetStructure.TakeDamage(StructureDamage);
            breakStarted = Time.time + 0.8f; // a pause before the next wind-up
            return true;
        }
    }
}
