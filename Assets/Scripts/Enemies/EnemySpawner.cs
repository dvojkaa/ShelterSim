using System.Collections.Generic;
using SheltreSim.Player;
using SheltreSim.Survival;
using UnityEngine;

namespace SheltreSim.Enemies
{
    /// <summary>
    /// Keeps one spawn slot for each original scene raider. Inactive templates preserve the
    /// actual art, controller dimensions and tuning instead of rebuilding a different enemy.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public float respawnDelaySeconds = 90f;
        public float minimumPlayerDistance = 12f;

        [System.Serializable]
        public class RaiderState
        {
            public string id;
            public Vector3 position;
            public Quaternion rotation;
            public float health;
            public float respawnRemaining;
        }

        private sealed class SpawnSlot
        {
            public string id;
            public Vector3 position;
            public Quaternion rotation;
            public Transform parent;
            public EnemyAI template;
            public EnemyAI current;
            public float respawnRemaining;
        }

        private readonly List<SpawnSlot> slots = new List<SpawnSlot>();
        private readonly Dictionary<EnemyAI, SpawnSlot> slotByRaider = new Dictionary<EnemyAI, SpawnSlot>();
        private bool initialized;
        private Transform templateRoot;
        private Transform playerTransform;
        private SurvivalStats playerStats;

        private void Start() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            FirstPersonController player = Object.FindFirstObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player != null)
            {
                playerTransform = player.transform;
                playerStats = player.GetComponent<SurvivalStats>();
            }

            EnemyAI[] originals = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            GameObject templates = new GameObject("RaiderTemplates");
            templates.transform.SetParent(transform, false);
            templates.SetActive(false);
            templateRoot = templates.transform;

            foreach (EnemyAI raider in originals)
            {
                if (raider.gameObject.scene != gameObject.scene || !raider.isActiveAndEnabled || raider.IsDead) continue;
                SpawnSlot slot = new SpawnSlot
                {
                    id = StableId(raider.transform),
                    position = raider.transform.position,
                    rotation = raider.transform.rotation,
                    parent = raider.transform.parent
                };
                // The parent is inactive before cloning, so the template never runs Awake,
                // Start, input, physics or any other gameplay behavior.
                slot.template = Instantiate(raider, templateRoot, false);
                slot.template.name = raider.name;
                slot.template.gameObject.SetActive(false);
                slots.Add(slot);
                TrackRaider(raider, slot);
            }
        }

        private static string StableId(Transform target)
        {
            string path = target.name + "[" + target.GetSiblingIndex() + "]";
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "[" + target.GetSiblingIndex() + "]/" + path;
            }
            return path;
        }

        private void TrackRaider(EnemyAI raider, SpawnSlot slot)
        {
            slot.current = raider;
            slotByRaider[raider] = slot;
            raider.OnDeath += HandleRaiderDeath;
        }

        private void HandleRaiderDeath(EnemyAI raider)
        {
            if (!slotByRaider.TryGetValue(raider, out SpawnSlot slot)) return;
            slotByRaider.Remove(raider);
            raider.OnDeath -= HandleRaiderDeath;
            slot.current = null;
            slot.respawnRemaining = Mathf.Max(0.1f, respawnDelaySeconds);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || (playerStats != null && (playerStats.IsDead || playerStats.health <= 0f))) return;
            foreach (SpawnSlot slot in slots)
            {
                if (slot.current != null) continue;
                slot.respawnRemaining = Mathf.Max(0f, slot.respawnRemaining - Time.deltaTime);
                if (slot.respawnRemaining > 0f) continue;
                if (!SpawnIsClear(slot))
                {
                    // A built wall or a player camping the old spawn postpones it; never
                    // materialize a raider in someone's body or in a newly placed building.
                    slot.respawnRemaining = 1f;
                    continue;
                }
                SpawnRaider(slot, slot.position, slot.rotation, slot.template.maxHealth);
            }
        }

        private bool SpawnIsClear(SpawnSlot slot)
        {
            if (playerTransform != null &&
                Vector3.ProjectOnPlane(playerTransform.position - slot.position, Vector3.up).sqrMagnitude <
                minimumPlayerDistance * minimumPlayerDistance)
            {
                return false;
            }
            CharacterController capsule = slot.template.GetComponent<CharacterController>();
            Vector3 scale = slot.template.transform.localScale;
            if (slot.parent != null) scale = Vector3.Scale(scale, slot.parent.lossyScale);
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * 0.5f);
            Vector3 center = slot.position + slot.rotation * Vector3.Scale(capsule.center, scale);
            Vector3 end = Vector3.up * (halfHeight - radius);
            // A small inset excludes exact floor tangency, which counts as overlap in PhysX.
            return !Physics.CheckCapsule(center - end, center + end, Mathf.Max(0.01f, radius - 0.02f),
                ~0, QueryTriggerInteraction.Ignore);
        }

        private void SpawnRaider(SpawnSlot slot, Vector3 position, Quaternion rotation, float health)
        {
            EnemyAI fresh = Instantiate(slot.template, slot.parent, false);
            fresh.name = slot.template.name;
            fresh.transform.SetPositionAndRotation(position, rotation);
            fresh.gameObject.SetActive(true);
            fresh.RestoreState(position, rotation, health);
            TrackRaider(fresh, slot);
            slot.respawnRemaining = 0f;
        }

        public List<RaiderState> CaptureState()
        {
            Initialize();
            List<RaiderState> states = new List<RaiderState>(slots.Count);
            foreach (SpawnSlot slot in slots)
            {
                bool alive = slot.current != null && !slot.current.IsDead;
                states.Add(new RaiderState
                {
                    id = slot.id,
                    position = alive ? slot.current.transform.position : slot.position,
                    rotation = alive ? slot.current.transform.rotation : slot.rotation,
                    health = alive ? slot.current.health : 0f,
                    respawnRemaining = alive ? 0f : slot.respawnRemaining
                });
            }
            return states;
        }

        public void RestoreState(List<RaiderState> states)
        {
            if (states == null) return; // Old saves contain no raider snapshot.
            Initialize();
            foreach (RaiderState saved in states)
            {
                if (saved == null) continue;
                SpawnSlot slot = slots.Find(candidate => candidate.id == saved.id);
                if (slot == null) continue;
                if (saved.health > 0f)
                {
                    float health = Mathf.Clamp(saved.health, 0.01f, Mathf.Max(1f, slot.template.maxHealth));
                    if (slot.current == null || slot.current.IsDead)
                    {
                        RemoveCurrent(slot);
                        SpawnRaider(slot, saved.position, saved.rotation, health);
                    }
                    else
                    {
                        slot.current.RestoreState(saved.position, saved.rotation, health);
                    }
                    slot.respawnRemaining = 0f;
                }
                else
                {
                    RemoveCurrent(slot);
                    slot.respawnRemaining = Mathf.Clamp(saved.respawnRemaining, 0f, Mathf.Max(1f, respawnDelaySeconds));
                }
            }
        }

        private void RemoveCurrent(SpawnSlot slot)
        {
            if (slot.current == null) return;
            slot.current.OnDeath -= HandleRaiderDeath;
            slotByRaider.Remove(slot.current);
            // Destroy is deferred: deactivate now so load never leaves a duplicate collider
            // active for the remainder of this frame.
            slot.current.gameObject.SetActive(false);
            Destroy(slot.current.gameObject);
            slot.current = null;
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<EnemyAI, SpawnSlot> pair in slotByRaider)
            {
                if (pair.Key != null) pair.Key.OnDeath -= HandleRaiderDeath;
            }
            slotByRaider.Clear();
            if (templateRoot != null) Destroy(templateRoot.gameObject);
        }
    }
}
