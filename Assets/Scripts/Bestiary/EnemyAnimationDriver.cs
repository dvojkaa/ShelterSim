using SheltreSim.Enemies;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Plays GPT's five takes (Idle, Walk, Attack, Hit, Death) when the model has an Animator, and switches off the procedural
    /// walk and pose so only one thing writes the limbs. The Attack take is sped up or slowed so its impact frame lands exactly
    /// when the brain applies damage (the end of the wind-up): impact is 0.5 s into the take for humanoids and creatures, 0.65 s
    /// for bosses (ArtSource/M23_ANIMATION_REPORT.json). Death plays on a detached copy of the model, because the enemy object
    /// is destroyed the moment it dies.
    /// </summary>
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed"), AttackId = Animator.StringToHash("Attack"),
            HitId = Animator.StringToHash("Hit"), DeadId = Animator.StringToHash("Dead");
        private const float CorpseSeconds = 12f;

        private Animator animator;
        private EnemyAI enemy;
        private CharacterController body;
        private SpeciesBrain brain;
        private float lastWindup = -1f, lastHealth, attackEnds;
        private bool boss;

        public bool Driving => animator != null && animator.runtimeAnimatorController != null;

        public static void Attach(GameObject enemy)
        {
            if (enemy.GetComponent<EnemyAnimationDriver>() == null) enemy.AddComponent<EnemyAnimationDriver>();
            else enemy.GetComponent<EnemyAnimationDriver>().Rebind();
        }

        private void Awake() => Rebind();

        public void Rebind()
        {
            enemy = GetComponent<EnemyAI>();
            body = GetComponent<CharacterController>();
            Transform visual = transform.Find("Visual");
            animator = visual != null ? visual.GetComponentInChildren<Animator>() : null;
            if (enemy != null)
            {
                lastHealth = enemy.health;
                enemy.OnDeath -= OnDeath;
                enemy.OnDeath += OnDeath;
            }
            var actor = GetComponent<SpeciesActor>();
            boss = actor != null && actor.Spec.IsBoss;
        }

        private void OnDestroy() { if (enemy != null) enemy.OnDeath -= OnDeath; }

        private void Update()
        {
            if (!Driving) return;
            // Exactly one writer of the limbs.
            var walker = GetComponent<RaiderWalkAnimator>(); if (walker != null && walker.enabled) walker.enabled = false;
            var creature = GetComponent<CreatureWalkAnimator>(); if (creature != null && creature.enabled) creature.enabled = false;
            var pose = GetComponent<EnemyPose>(); if (pose != null && pose.enabled) pose.enabled = false;
            if (brain == null) brain = GetComponent<SpeciesBrain>();

            Vector3 v = body != null && body.enabled ? body.velocity : Vector3.zero;
            v.y = 0f;
            animator.SetFloat(SpeedId, v.magnitude);

            if (brain != null && brain.WindupStartedAt > 0f && brain.WindupStartedAt != lastWindup && brain.CurrentAttack != null)
            {
                lastWindup = brain.WindupStartedAt;
                float impact = boss ? 0.65f : 0.5f;
                animator.speed = Mathf.Clamp(impact / Mathf.Max(0.05f, brain.CurrentAttack.Windup), 0.3f, 3f);
                animator.SetTrigger(AttackId);
                attackEnds = Time.time + (boss ? 1f : 0.77f) / animator.speed;
            }
            else if (animator.speed != 1f && Time.time > attackEnds) animator.speed = 1f;

            if (enemy != null)
            {
                // A boss in its swing doesn't flinch: the hit reaction would hide the telegraph.
                if (enemy.health < lastHealth - 0.01f && (!enemy.superArmor || Time.time > attackEnds)) animator.SetTrigger(HitId);
                lastHealth = enemy.health;
            }
        }

        private void OnDeath(EnemyAI dead)
        {
            if (!Driving) return;
            Transform visual = animator.transform.parent != null ? animator.transform.parent : animator.transform;
            GameObject corpse = Instantiate(visual.gameObject, visual.position, visual.rotation);
            corpse.name = name + "_Corpse";
            corpse.transform.localScale = visual.lossyScale;
            foreach (Collider c in corpse.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Animator corpseAnimator = corpse.GetComponentInChildren<Animator>();
            corpseAnimator.speed = 1f;
            corpseAnimator.SetTrigger(DeadId);
            Destroy(corpse, CorpseSeconds);
        }
    }
}
