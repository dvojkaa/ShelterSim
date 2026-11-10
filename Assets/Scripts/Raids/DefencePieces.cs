using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Power;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>Where a turret shoots from: its Head if the model has one (barrel along forward), otherwise a fixed spot.</summary>
    internal static class TurretRig
    {
        public static Transform FindHead(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == "Head") return t;
            return null;
        }

        public static Vector3 Muzzle(Transform root, Transform head, float fallbackHeight)
        {
            return head != null ? head.position + head.forward * 0.55f : root.position + Vector3.up * fallbackHeight;
        }

        public static void Aim(Transform head, Vector3 target, float dt)
        {
            if (head == null) return;
            Vector3 flat = target - head.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(flat), 360f * dt);
        }
    }

    /// <summary>
    /// What makes a flat trap something you walk across: every solid collider it came with becomes a trigger, and a 4 cm plate
    /// (well under the step height) stays solid for the build tools' raycasts. Traps find their victims in EnemyAI.Live,
    /// which holds no player, instead of relying on trigger events.
    /// </summary>
    internal static class TrapRig
    {
        public static Vector3 MakeWalkable(GameObject piece)
        {
            Vector3 size = new Vector3(2f, 0.3f, 2f);
            BoxCollider rootBox = piece.GetComponent<BoxCollider>();
            if (rootBox != null) size = rootBox.size;
            foreach (Collider collider in piece.GetComponentsInChildren<Collider>(true)) collider.isTrigger = true;
            var plate = piece.AddComponent<BoxCollider>();
            plate.center = Vector3.up * 0.02f;
            plate.size = new Vector3(size.x, 0.04f, size.z);
            return size;
        }

        public static bool IsOnTrap(Transform trap, Vector3 size, EnemyAI enemy)
        {
            Vector3 local = trap.InverseTransformPoint(enemy.FeetPosition);
            return Mathf.Abs(local.x) <= size.x * 0.5f + 0.2f && Mathf.Abs(local.z) <= size.z * 0.5f + 0.2f && Mathf.Abs(local.y) < 1f;
        }
    }
}
