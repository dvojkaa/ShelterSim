using System.Collections;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Enemies;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Combat
{
    /// <summary>One melee weapon with a procedural swing and a forgiving forward sweep.</summary>
    public class MeleeAttacker : MonoBehaviour
    {
        public SheltreSim.Building.BuildModeController buildModeController;
        public Transform weaponVisual;

        public float attackRange = 2.5f;
        public float attackRadius = 0.5f;
        public float attackDamage = 20f;
        public float EquippedDamage => attackDamage;

        /// <summary>Unarmed damage. PlayerGear sets attackDamage to the equipped weapon's value, or back to this.</summary>
        public const float DefaultDamage = 20f;

        /// <summary>Test hook: lets probes swing without the locked-cursor requirement (batch mode has no real cursor).</summary>
        public bool ignoreCursorLock;

        /// <summary>True while the ranged hand is raised: no swing, and the prop is hidden. Set by RangedWeaponController.</summary>
        public bool suppressed
        {
            get => isSuppressed;
            set
            {
                if (isSuppressed == value) return;
                isSuppressed = value;
                ResetSwing();
                if (weaponVisual != null) weaponVisual.gameObject.SetActive(!value);
            }
        }
        private bool isSuppressed;

        public float attackCooldown = 0.6f;
        // Keep the existing serialized timing controls: their original defaults now map
        // to the proposal's 0.09 s strike and 0.2 s recovery, respectively.
        public float swingDuration = 0.1f;
        public float returnDuration = 0.15f;

        private MeleeInputActions input;
        private SurvivalStats stats;
        private Inventory inventory;
        private PlayerGear gear;
        private FirstPersonController playerController;
        private Transform playerRoot;
        private float lastAttackTime = float.NegativeInfinity;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Coroutine swingCoroutine;

        private static readonly AnimationCurve AnticipationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        private static readonly AnimationCurve StrikeCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 2f));
        private static readonly AnimationCurve RecoveryCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));

        public void ApplyWeaponUpgrade(float bonusDamage)
        {
            if (bonusDamage > 0f && !float.IsNaN(bonusDamage) && !float.IsInfinity(bonusDamage))
            {
                attackDamage += bonusDamage;
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Equip, 0.8f);
            }
        }

        private void Awake()
        {
            input = new MeleeInputActions();
            stats = GetComponentInParent<SurvivalStats>();
            inventory = stats != null ? stats.GetComponent<Inventory>() : GetComponentInParent<Inventory>();
            gear = stats != null ? stats.GetComponent<PlayerGear>() : GetComponentInParent<PlayerGear>();
            playerController = GetComponentInParent<FirstPersonController>();
            playerRoot = playerController != null ? playerController.transform : transform.root;
            if (weaponVisual != null)
            {
                restPosition = weaponVisual.localPosition;
                restRotation = weaponVisual.localRotation;
            }
        }

        private void OnEnable()
        {
            input.Attack.performed += OnAttackPerformed;
            input.Enable();
        }

        private void OnDisable()
        {
            if (input != null)
            {
                input.Attack.performed -= OnAttackPerformed;
                input.Disable();
            }
            ResetSwing();
        }

        private void OnDestroy() => input?.Dispose();

        private void Update()
        {
            bool riding = playerController != null && playerController.IsVehicleMounted;
            if (weaponVisual != null) weaponVisual.gameObject.SetActive(!suppressed && !riding);
            if (swingCoroutine != null && ((stats != null && (stats.IsDead || stats.health <= 0f)) ||
                riding || (buildModeController != null && buildModeController.IsBuildModeActive)))
            {
                ResetSwing();
            }
        }

        private void OnAttackPerformed(InputAction.CallbackContext context) => TryAttack();

        /// <summary>One swing with the real hit-detection path. Returns false when gated (paused, dead, cooldown...).</summary>
        public bool TryAttack()
        {
            if (suppressed || (playerController != null && playerController.IsVehicleMounted) || !isActiveAndEnabled || Time.timeScale <= 0f || (!ignoreCursorLock && Cursor.lockState != CursorLockMode.Locked) ||
                (stats != null && (stats.IsDead || stats.health <= 0f)) ||
                (playerController != null && !playerController.isActiveAndEnabled) ||
                (buildModeController != null && buildModeController.IsBuildModeActive) ||
                Time.time - lastAttackTime < Mathf.Max(0.05f, attackCooldown))
            {
                return false;
            }

            lastAttackTime = Time.time;
            SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.Swing, 0.55f, 0.08f);
            Collider target = FindSwingTarget();
            // Damage remains synchronous: existing gameplay/probe callers inspect the
            // result of TryAttack immediately. Only the grip's animation is paused on contact.
            bool madeContact = target != null;
            EnemyAI enemy = target != null ? target.GetComponentInParent<EnemyAI>() : null;
            if (enemy != null)
            {
                enemy.TakeDamage(attackDamage, transform.position);
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.HitFlesh, enemy.transform.position + Vector3.up, 0.9f, 25f);
                string targetName = RangedCombat.EnemyDisplayName(enemy);
                GameMessageBus.Post(enemy.IsDead
                    ? targetName + " defeated."
                    : $"Hit {targetName} for {attackDamage:0} damage ({enemy.health:0} HP left).");
            }
            else if (target != null && target.GetComponentInParent<SheltreSim.World.WorldWildlife>() is SheltreSim.World.WorldWildlife animal)
            {
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.HitFlesh, animal.transform.position + Vector3.up, 0.9f, 25f);
                GameMessageBus.Post(animal.TakeHit(attackDamage, transform.position));
            }
            else if (target != null && target.GetComponentInParent<SheltreSim.Items.ResourceNode>() is SheltreSim.Items.ResourceNode node &&
                node.Strike(playerRoot != null ? playerRoot.gameObject : gameObject))
            {
                // ResourceNode posts the harvest result and uses the existing gathering sound path.
            }
            else if (target != null && TryDamageStructure(target))
            {
                // The StructureHealth component owns the damage/break presentation and persistence.
            }
            else
            {
                if (target != null) SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.HitBlock, transform.position + transform.forward, 0.75f, 20f);
                if (target != null) GameMessageBus.Post("Swing blocked.");
            }

            ResetSwing();
            if (weaponVisual != null) swingCoroutine = StartCoroutine(SwingWeapon(madeContact));
            return true;
        }

        private bool TryDamageStructure(Collider target)
        {
            PlacedPiece piece = target.GetComponentInParent<PlacedPiece>();
            StructureHealth health = target.GetComponentInParent<StructureHealth>();
            if (health == null) return false;

            HomesteadMaterial material = piece != null ? HomesteadCatalog.MaterialFor(piece.pieceId) : health.material;
            float damage = 0f;
            if (inventory != null)
            {
                damage = Mathf.Max(damage, OwnedToolDamage("hammer", material));
                damage = Mathf.Max(damage, OwnedToolDamage("stone_axe", material));
                damage = Mathf.Max(damage, OwnedToolDamage("iron_axe", material));
                damage = Mathf.Max(damage, OwnedToolDamage("steel_axe", material));
                damage = Mathf.Max(damage, OwnedToolDamage("stone_pickaxe", material));
                damage = Mathf.Max(damage, OwnedToolDamage("iron_pickaxe", material));
                damage = Mathf.Max(damage, OwnedToolDamage("steel_pickaxe", material));
            }

            string weaponId = gear != null ? gear.WeaponId : string.Empty;
            // A carried starter hammer is a passive gathering tool, not permission to
            // chip a placed wall or crate whenever the player misclicks. Require a
            // weapon visibly equipped to damage player-built pieces.
            if (piece != null && string.IsNullOrEmpty(weaponId)) return false;
            if (!string.IsNullOrEmpty(weaponId) && attackDamage > MeleeAttacker.DefaultDamage)
            {
                float factor = StructureDamageRules.MeleeWeaponMultiplier(material);
                damage = Mathf.Max(damage, attackDamage * factor);
            }

            if (damage <= 0f)
            {
                GameMessageBus.Post("You need a hammer, axe, pickaxe or melee weapon to damage this structure.");
                return true;
            }

            health.TakeDamage(damage);
            string label = piece != null ? piece.pieceId.Replace("piece_", "").Replace('_', ' ') : health.name.Replace('_', ' ');
            if (health.Health > 0f) GameMessageBus.Post($"Damaged {label} ({health.Health:0}/{health.maxHealth:0} HP).");
            return true;
        }

        private float OwnedToolDamage(string id, HomesteadMaterial material) =>
            inventory.GetCount(id) > 0 ? StructureDamageRules.ToolDamage(id, material) : 0f;

        /// <summary>
        /// SphereCastAll preserves initial overlaps, which are essential at point-blank range.
        /// Their distance/point carry no direction, so validate enemies against the view and a
        /// separate sight ray. Nearby scenery beside the player cannot swallow the swing, and
        /// an overlapping enemy behind a thin wall or behind the camera cannot be hit.
        /// </summary>
        private Collider FindSwingTarget()
        {
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, Mathf.Max(0.01f, attackRadius),
                transform.forward, Mathf.Max(0f, attackRange), ~0, QueryTriggerInteraction.Ignore);
            Collider nearestEnemy = null;
            Collider nearestObstacle = null;
            Collider nearestAnimal = null;
            Collider nearestCuttable = null;
            float animalDistance = float.PositiveInfinity;
            float cuttableDistance = float.PositiveInfinity;
            float enemyDistance = float.PositiveInfinity;
            float obstacleDistance = float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                Collider candidate = hit.collider;
                if (BelongsToPlayer(candidate))
                {
                    continue;
                }

                EnemyAI enemy = candidate.GetComponentInParent<EnemyAI>();
                if (enemy == null)
                {
                    SheltreSim.World.WorldWildlife animal = candidate.GetComponentInParent<SheltreSim.World.WorldWildlife>();
                    if (animal != null)
                    {
                        if (animal.IsDead) continue;
                        Vector3 animalOffset = candidate.bounds.center - transform.position;
                        if (Vector3.Dot(animalOffset.normalized, transform.forward) >= 0.25f && HasClearStrikeAnimal(candidate, animal) &&
                            animalOffset.sqrMagnitude < animalDistance)
                        {
                            animalDistance = animalOffset.sqrMagnitude;
                            nearestAnimal = candidate;
                        }
                        continue;
                    }
                    SheltreSim.Items.ResourceNode resource = candidate.GetComponentInParent<SheltreSim.Items.ResourceNode>();
                    if (resource != null && resource.CanStrike(playerRoot != null ? playerRoot.gameObject : gameObject))
                    {
                        Vector3 forageOffset = candidate.bounds.center - transform.position;
                        if (Vector3.Dot(forageOffset.normalized, transform.forward) >= 0.25f &&
                            forageOffset.sqrMagnitude < cuttableDistance)
                        {
                            cuttableDistance = forageOffset.sqrMagnitude;
                            nearestCuttable = candidate;
                        }
                        continue;
                    }
                    if (hit.distance > 0f && hit.distance < obstacleDistance)
                    {
                        obstacleDistance = hit.distance;
                        nearestObstacle = candidate;
                    }
                    continue;
                }
                if (!enemy.isActiveAndEnabled || enemy.IsDead)
                {
                    continue;
                }

                Vector3 offset = candidate.bounds.center - transform.position;
                if (Vector3.Dot(offset.normalized, transform.forward) < 0.25f ||
                    !HasClearStrike(candidate, enemy))
                {
                    continue;
                }
                if (offset.sqrMagnitude < enemyDistance)
                {
                    enemyDistance = offset.sqrMagnitude;
                    nearestEnemy = candidate;
                }
            }

            if (nearestEnemy != null) return nearestEnemy;
            if (nearestAnimal != null) return nearestAnimal;
            if (nearestCuttable != null && cuttableDistance <= obstacleDistance) return nearestCuttable;
            return nearestObstacle;
        }

        private bool HasClearStrikeAnimal(Collider target, SheltreSim.World.WorldWildlife animal)
        {
            Bounds bounds = target.bounds;
            Vector3 point = bounds.center;
            point.y = Mathf.Clamp(transform.position.y, bounds.min.y + 0.1f, bounds.max.y - 0.1f);
            Vector3 direction = point - transform.position;
            RaycastHit[] hits = Physics.RaycastAll(transform.position, direction.normalized,
                direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (!BelongsToPlayer(hit.collider) && hit.distance < distance)
                {
                    nearest = hit.collider;
                    distance = hit.distance;
                }
            }
            return nearest == null || nearest.GetComponentInParent<SheltreSim.World.WorldWildlife>() == animal;
        }

        private bool HasClearStrike(Collider target, EnemyAI enemy)
        {
            Bounds bounds = target.bounds;
            Vector3 point = bounds.center;
            point.y = Mathf.Clamp(transform.position.y, bounds.min.y + 0.1f, bounds.max.y - 0.1f);
            Vector3 direction = point - transform.position;
            RaycastHit[] hits = Physics.RaycastAll(transform.position, direction.normalized,
                direction.magnitude, ~0, QueryTriggerInteraction.Ignore);
            Collider nearest = null;
            float distance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (!BelongsToPlayer(hit.collider) && hit.distance < distance)
                {
                    nearest = hit.collider;
                    distance = hit.distance;
                }
            }
            return nearest == null || nearest.GetComponentInParent<EnemyAI>() == enemy;
        }

        private bool BelongsToPlayer(Collider candidate)
        {
            Transform root = playerRoot != null ? playerRoot : transform.root;
            return candidate.transform == root || candidate.transform.IsChildOf(root);
        }

        private void ResetSwing()
        {
            if (swingCoroutine != null)
            {
                StopCoroutine(swingCoroutine);
                swingCoroutine = null;
            }
            if (weaponVisual != null)
            {
                weaponVisual.SetLocalPositionAndRotation(restPosition, restRotation);
            }
        }

        private float WeaponTimeWeight()
        {
            // The Makeshift Blade is the current knife-sized weapon. Passive inventory
            // tools do not change the equipped hand's timing.
            switch (gear != null ? gear.WeaponId : string.Empty)
            {
                case "weapon_upgrade": return 0.8f;
                case "fire_axe": return 1.3f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Selects a passive tool to show while aiming at a harvest node or player-built structure.
        /// Tool damage and harvest bonuses remain based on the best owned tool, so merely changing
        /// the viewmodel cannot consume, equip, or alter the player's gear state.
        /// </summary>
        public string ContextualToolId()
        {
            if (gear == null) gear = GetComponentInParent<PlayerGear>();
            if (inventory == null) inventory = GetComponentInParent<Inventory>();
            if (gear == null || inventory == null || gear.WeaponId.Length > 0) return null;

            Collider target = FindSwingTarget();
            if (target == null) return null;

            SheltreSim.Items.ResourceNode node = target.GetComponentInParent<SheltreSim.Items.ResourceNode>();
            if (node != null && node.resourceType != null)
                return gear.BestOwnedToolId(PlayerGear.ToolFor(node.resourceType.id));

            PlacedPiece piece = target.GetComponentInParent<PlacedPiece>();
            StructureHealth structure = target.GetComponentInParent<StructureHealth>();
            if (structure == null) return null;

            HomesteadMaterial material = piece != null ? HomesteadCatalog.MaterialFor(piece.pieceId) : structure.material;
            ToolKind preferred = material == HomesteadMaterial.Wood ? ToolKind.Axe : ToolKind.Pickaxe;
            string best = gear.BestOwnedToolId(preferred);
            if (best != null) return best;
            return inventory.GetCount("hammer") > 0 ? "hammer" : null;
        }

        private IEnumerator SwingWeapon(bool madeContact)
        {
            float weight = WeaponTimeWeight();
            float anticipation = 0.05f * weight;
            float strike = Mathf.Max(0.01f, swingDuration * 0.9f) * weight;
            float followThrough = 0.06f * weight;
            float recovery = Mathf.Max(0.01f, returnDuration * (0.2f / 0.15f)) * weight;
            float contactTime = anticipation + strike;
            float total = contactTime + followThrough + recovery;
            float elapsed = 0f;
            float holdRemaining = 0f;
            bool contactReached = false;

            while (elapsed < total && weaponVisual != null)
            {
                ApplySwingPose(elapsed, anticipation, strike, followThrough, recovery);
                yield return null;

                float advance = Time.deltaTime;
                if (holdRemaining > 0f)
                {
                    float held = Mathf.Min(advance, holdRemaining);
                    holdRemaining -= held;
                    advance -= held;
                }
                if (!contactReached && elapsed + advance >= contactTime)
                {
                    contactReached = true;
                    if (madeContact)
                    {
                        // Stop only this curve for 50 ms. Actors, physics, cooldown and
                        // global time continue normally, and pause still freezes the grip.
                        elapsed = contactTime;
                        holdRemaining = 0.05f;
                        continue;
                    }
                }
                elapsed += advance;
            }

            if (weaponVisual != null) weaponVisual.SetLocalPositionAndRotation(restPosition, restRotation);
            swingCoroutine = null;
        }

        private void ApplySwingPose(float elapsed, float anticipation, float strike, float followThrough, float recovery)
        {
            Vector3 windupPosition = new Vector3(0f, 0f, -0.04f);
            Vector3 strikePosition = new Vector3(-0.06f, 0f, 0.12f);
            Vector3 overshootPosition = new Vector3(-0.08f, -0.02f, 0.14f);
            // The held model points up and forward (MeleeViewmodel.HeldRotation): wind up by raising the tip further back, then chop it
            // FORWARD and down through the target. (Negative X here swung the tip back towards the player.)
            Quaternion windupRotation = Quaternion.Euler(-22f, -6f, 0f);
            Quaternion strikeRotation = Quaternion.Euler(80f, 20f, 15f);
            Quaternion overshootRotation = Quaternion.Euler(92f, 25f, 18f);
            Vector3 position;
            Quaternion rotation;
            if (elapsed < anticipation)
            {
                float u = AnticipationCurve.Evaluate(elapsed / anticipation);
                position = Vector3.LerpUnclamped(Vector3.zero, windupPosition, u);
                rotation = Quaternion.SlerpUnclamped(Quaternion.identity, windupRotation, u);
            }
            else if (elapsed < anticipation + strike)
            {
                float u = StrikeCurve.Evaluate((elapsed - anticipation) / strike);
                position = Vector3.LerpUnclamped(windupPosition, strikePosition, u);
                rotation = Quaternion.SlerpUnclamped(windupRotation, strikeRotation, u);
            }
            else if (elapsed < anticipation + strike + followThrough)
            {
                float u = RecoveryCurve.Evaluate((elapsed - anticipation - strike) / followThrough);
                position = Vector3.LerpUnclamped(strikePosition, overshootPosition, u);
                rotation = Quaternion.SlerpUnclamped(strikeRotation, overshootRotation, u);
            }
            else
            {
                float u = RecoveryCurve.Evaluate((elapsed - anticipation - strike - followThrough) / recovery);
                position = Vector3.LerpUnclamped(overshootPosition, Vector3.zero, u);
                rotation = Quaternion.SlerpUnclamped(overshootRotation, Quaternion.identity, u);
            }
            // Translation is in camera/grip-parent space; the authored rest rotation
            // turns the weapon model upright and must not rotate forward into downward.
            weaponVisual.SetLocalPositionAndRotation(restPosition + position, restRotation * rotation);
        }
    }
}
