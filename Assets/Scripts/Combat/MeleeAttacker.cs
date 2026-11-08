using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Combat
{
    /// <summary>
    /// Playtest feedback: "I want to attack, at least bash the enemies, with an animation."
    /// Purely procedural swing (rotate a weapon prop forward and back) - no Animator/clips,
    /// since this project has no character rig to animate.
    /// </summary>
    public class MeleeAttacker : MonoBehaviour
    {
        public SheltreSim.Building.BuildModeController buildModeController;
        public Transform weaponVisual;

        public float attackRange = 2.5f;
        public float attackRadius = 0.5f;
        public float attackDamage = 20f;
        public float attackCooldown = 0.6f;
        public float swingDuration = 0.1f;
        public float returnDuration = 0.15f;

        private MeleeInputActions input;
        private float lastAttackTime = float.NegativeInfinity;
        private Quaternion restRotation;

        /// <summary>
        /// M10 — called by InventoryUI's Equip button after Inventory.TryConsume has already
        /// removed the weapon item. Permanent, additive, and there's no un-equip — a weapon
        /// pickup here is a consumable damage upgrade, not an inventory slot swap (no weapon
        /// switching UI exists, and the viewmodel is a single fixed prop).
        /// </summary>
        public void ApplyWeaponUpgrade(float bonusDamage)
        {
            attackDamage += bonusDamage;
        }

        private void Awake()
        {
            input = new MeleeInputActions();

            if (weaponVisual != null)
            {
                restRotation = weaponVisual.localRotation;
            }
        }

        private void OnEnable()
        {
            input.Enable();
            input.Attack.performed += OnAttackPerformed;
        }

        private void OnDisable()
        {
            input.Attack.performed -= OnAttackPerformed;
            input.Disable();
            input.Dispose();
        }

        private void OnAttackPerformed(InputAction.CallbackContext context)
        {
            if (buildModeController != null && buildModeController.IsBuildModeActive)
            {
                // Left-click already places a build piece in this mode - don't also attack.
                return;
            }

            // Nothing consumes UI clicks away from gameplay input in this project (every map
            // is hand-built and always listening), so without this the click that presses the
            // pause menu's Resume button would also swing the weapon - and the swing coroutine
            // would then sit frozen at timeScale 0 until the game resumed.
            if (Time.timeScale == 0f)
            {
                return;
            }

            if (Time.time - lastAttackTime < attackCooldown)
            {
                return;
            }

            lastAttackTime = Time.time;

            Collider target = FindSwingTarget();
            if (target != null)
            {
                SheltreSim.Enemies.EnemyAI enemy = target.GetComponent<SheltreSim.Enemies.EnemyAI>();
                if (enemy == null)
                {
                    enemy = target.GetComponentInParent<SheltreSim.Enemies.EnemyAI>();
                }

                if (enemy != null)
                {
                    enemy.TakeDamage(attackDamage);
                    Debug.Log($"Hit {enemy.name} for {attackDamage} damage.");
                }
                else
                {
                    Debug.Log("Swing missed - nothing to hit.");
                }
            }
            else
            {
                Debug.Log("Swing missed - nothing in range.");
            }

            StartCoroutine(SwingWeapon());
        }

        /// <summary>
        /// Sweeps forward from the camera and returns the collider the swing connects with,
        /// or null if it connects with nothing.
        ///
        /// Deliberately NOT the single-hit Physics.SphereCast: that overload drops every
        /// collider the sphere already overlaps at the start of the sweep, so an enemy pressed
        /// right up against you (enemy capsule radius 0.5 + player controller radius 0.35 means
        /// contact at ~0.85m centre-to-centre, inside this cast's 0.5 + 0.5 = 1.0m overlap
        /// threshold) is silently skipped — exactly when a melee swing most needs to land.
        ///
        /// SphereCastAll does return those overlaps, but it reports them with distance 0 and
        /// point (0,0,0): there is no contact geometry for an overlap, so the numbers carry no
        /// directional information at all. That means they cannot be fed into a plain
        /// nearest-hit comparison, which is what the first version of this did — any collider
        /// merely touching the sphere won at distance 0 and swallowed the swing. Since the
        /// sphere has a 0.5m radius and the player can stand within 0.35m of a surface, that
        /// included every wall, crate and bit of rubble the player happened to be standing
        /// NEXT TO rather than facing: standing anywhere near bunker geometry made every swing
        /// report "nothing to hit" regardless of what was actually in front of you.
        ///
        /// So the two cases are handled separately, which is what their different semantics
        /// require:
        ///  - distance 0 (already overlapping): only meaningful as "this thing is touching me".
        ///    Used solely to find a point-blank enemy; overlapping scenery is ignored, because
        ///    scenery you are standing against is not between you and anything.
        ///  - distance > 0 (actually swept into): real, directional, ordered. Nearest wins, and
        ///    if the nearest is scenery the swing is blocked — a wall genuinely between you and
        ///    a raider reports a real distance (measured: 0.4 for a wall at 1.0m with a raider
        ///    behind it at 0.805), so that behaviour is preserved.
        /// Anything in the player's own hierarchy is dropped in both cases — the player's own
        /// body does come back as a distance-0 hit (verified by probe, not assumed).
        /// </summary>
        private Collider FindSwingTarget()
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, attackRadius, transform.forward, attackRange);

            Transform playerRoot = transform.root;
            Collider pointBlankEnemy = null;
            Collider nearestSwept = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (RaycastHit candidate in hits)
            {
                if (candidate.collider.transform.root == playerRoot)
                {
                    // The player's own CharacterController (and anything else parented under
                    // the player rig) - never a valid melee target.
                    continue;
                }

                if (candidate.distance <= 0f)
                {
                    if (pointBlankEnemy == null && candidate.collider.GetComponentInParent<SheltreSim.Enemies.EnemyAI>() != null)
                    {
                        pointBlankEnemy = candidate.collider;
                    }

                    continue;
                }

                if (candidate.distance < nearestDistance)
                {
                    nearestDistance = candidate.distance;
                    nearestSwept = candidate.collider;
                }
            }

            // An enemy touching you is closer than anything the sweep can reach, so it wins.
            return pointBlankEnemy != null ? pointBlankEnemy : nearestSwept;
        }

        private IEnumerator SwingWeapon()
        {
            if (weaponVisual == null)
            {
                yield break;
            }

            Quaternion swungRotation = restRotation * Quaternion.Euler(-60f, 0f, 0f);

            float t = 0f;
            while (t < swingDuration)
            {
                t += Time.deltaTime;
                weaponVisual.localRotation = Quaternion.Slerp(restRotation, swungRotation, t / swingDuration);
                yield return null;
            }
            weaponVisual.localRotation = swungRotation;

            t = 0f;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                weaponVisual.localRotation = Quaternion.Slerp(swungRotation, restRotation, t / returnDuration);
                yield return null;
            }
            weaponVisual.localRotation = restRotation;
        }
    }
}
