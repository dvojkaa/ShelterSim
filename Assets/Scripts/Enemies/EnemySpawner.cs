using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Opus-model review: raiders are <c>Destroy()</c>d on death with no spawner, so killing
    /// every raider once leaves the world permanently pressure-free. This tracks every raider
    /// present when the scene starts and, whenever one dies, rebuilds a fresh one at the same
    /// spot after <see cref="respawnDelaySeconds"/> — long enough that killing a raider is real
    /// relief, not instant replacement — and immediately re-subscribes the new raider's own
    /// <see cref="EnemyAI.OnDeath"/> to the same handler, which is what keeps the cycle going
    /// indefinitely instead of being a one-shot "respawns once then stops."
    ///
    /// Deliberately a runtime MonoBehaviour under Assets/Scripts, NOT Assets/Editor: it has to
    /// exist in Play Mode and in a real build, where the Assets/Editor assembly (which holds
    /// M6EnemySetup, the editor-time raider builder) does not exist at all. Referencing
    /// SheltreSim.EditorTools from here would compile fine in the Editor (both assemblies are
    /// loaded there) but break a real build, and would also be a wrong-direction dependency this
    /// project's folder structure deliberately avoids. So the raider-construction steps
    /// (capsule primitive, swap CapsuleCollider for CharacterController, add EnemyAI, tint body
    /// + cosmetic Head/WeaponProp children) are duplicated here rather than shared — it is a
    /// small amount of logic and not worth a cross-assembly dependency. Keep the colors and
    /// child-object shapes here in sync with M6EnemySetup's if those ever change; EnemyAI's own
    /// hit-flash revert colors also hardcode these same three constants, so there are now three
    /// copies of this palette by design (see EnemyAI's doc comment on BodyColor/HeadColor/
    /// WeaponColor for why hardcoding was preferred to a shared read-back).
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public float respawnDelaySeconds = 90f;

        private static readonly Color BodyColor = new Color(0.35f, 0.12f, 0.1f);
        private static readonly Color HeadColor = new Color(0.55f, 0.42f, 0.35f);
        private static readonly Color WeaponColor = new Color(0.2f, 0.19f, 0.18f);

        /// <summary>
        /// One slot per raider present at Start - position/parent recorded once and reused
        /// forever, since every respawn should reappear exactly where the original raider was.
        /// </summary>
        private readonly List<(Vector3 position, Transform parent)> slots = new List<(Vector3 position, Transform parent)>();

        /// <summary>
        /// Maps each currently-alive raider instance to the slot it occupies, so the death
        /// handler (which only gets handed the dying EnemyAI, not a slot index) knows which
        /// slot to respawn into. Updated every time a raider - original or respawned - is
        /// created for a slot.
        /// </summary>
        private readonly Dictionary<EnemyAI, int> slotByRaider = new Dictionary<EnemyAI, int>();

        private void Start()
        {
            EnemyAI[] existingRaiders = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            foreach (EnemyAI raider in existingRaiders)
            {
                int slotIndex = slots.Count;
                slots.Add((raider.transform.position, raider.transform.parent));
                TrackRaider(raider, slotIndex);
            }

            Debug.Log($"{nameof(EnemySpawner)} tracking {slots.Count} raider(s) - each respawns {respawnDelaySeconds}s after death.");
        }

        private void TrackRaider(EnemyAI raider, int slotIndex)
        {
            slotByRaider[raider] = slotIndex;
            raider.OnDeath += HandleRaiderDeath;
        }

        private void HandleRaiderDeath(EnemyAI raider)
        {
            if (!slotByRaider.TryGetValue(raider, out int slotIndex))
            {
                // Not a raider this spawner is tracking (shouldn't happen - every raider in the
                // scene is picked up at Start) - nothing to respawn.
                return;
            }

            slotByRaider.Remove(raider);
            raider.OnDeath -= HandleRaiderDeath;
            StartCoroutine(RespawnAfterDelay(slotIndex));
        }

        private IEnumerator RespawnAfterDelay(int slotIndex)
        {
            yield return new WaitForSeconds(respawnDelaySeconds);

            (Vector3 position, Transform parent) slot = slots[slotIndex];
            EnemyAI freshRaider = SpawnRaider($"Raider_Respawn_{slotIndex}", slot.position, slot.parent);

            // Immediately re-subscribe the NEW raider's own OnDeath to this same handler for the
            // same slot - this is what makes the cycle self-sustaining indefinitely rather than
            // a one-shot respawn.
            TrackRaider(freshRaider, slotIndex);
        }

        /// <summary>
        /// Runtime duplicate of M6EnemySetup.SpawnEnemy + AddVisualDetail - see the class doc
        /// comment for why this can't just call that editor-only method instead.
        /// </summary>
        private static EnemyAI SpawnRaider(string raiderName, Vector3 position, Transform parent)
        {
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = raiderName;
            if (parent != null)
            {
                enemy.transform.SetParent(parent, false);
            }
            enemy.transform.position = position;
            ApplyColor(enemy, BodyColor);

            // Same CharacterController swap M6EnemySetup does - CharacterController is itself a
            // Collider subclass, so it satisfies both physics collision and MeleeAttacker's
            // SphereCastAll hit detection without the CapsuleCollider CreatePrimitive adds.
            Destroy(enemy.GetComponent<CapsuleCollider>());
            enemy.AddComponent<CharacterController>();
            EnemyAI enemyAi = enemy.AddComponent<EnemyAI>();

            AddVisualDetail(enemy.transform);

            return enemyAi;
        }

        private static void AddVisualDetail(Transform enemy)
        {
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(enemy, false);
            head.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            Destroy(head.GetComponent<Collider>());
            ApplyColor(head, HeadColor);

            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weapon.name = "WeaponProp";
            weapon.transform.SetParent(enemy, false);
            weapon.transform.localPosition = new Vector3(0.32f, 0.05f, 0.25f);
            weapon.transform.localRotation = Quaternion.Euler(35f, 0f, 15f);
            weapon.transform.localScale = new Vector3(0.08f, 0.08f, 0.65f);
            Destroy(weapon.GetComponent<Collider>());
            ApplyColor(weapon, WeaponColor);
        }

        private static void ApplyColor(GameObject target, Color color)
        {
            MeshRenderer meshRenderer = target.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material.color = color;
            }
        }
    }
}
