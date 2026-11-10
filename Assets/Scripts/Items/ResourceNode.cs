using SheltreSim.Interaction;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Items
{
    public class ResourceNode : MonoBehaviour, IInteractable
    {
        public ResourceDefinition resourceType;
        public int quantityPerHarvest = 1;
        public int remainingCharges = 3;
        /// <summary>When true, a suitable axe/blade swing can cut a full harvest charge at once.</summary>
        public bool cutOnHit;

        /// <summary>
        /// Seconds a fully-depleted node takes to regrow back to its starting charge count.
        /// Depletion used to be permanent (gameObject.SetActive(false)), which left the whole
        /// world barren after one sweep — and would have been impossible to fix with a simple
        /// timer anyway, since Update() never runs on a disabled GameObject. Depletion now
        /// disables only the visuals/collider (see Deplete/Regrow below) so this timer keeps
        /// ticking while depleted.
        /// </summary>
        [Min(0.1f)] public float regrowSeconds = 180f;

        /// <summary>M21: optional secondary drop (e.g. coal from rock nodes, plant fiber from bushes), rolled once per harvest.</summary>
        public string bonusId = "";
        [Range(0f, 1f)] public float bonusChance = 0f;
        private System.Random bonusRandom;

        private int startingCharges;
        private bool depleted;
        private float regrowTimer;
        private bool initialized;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool[] rendererEnabled;
        private bool[] colliderEnabled;

        public string InteractPrompt => remainingCharges > 0
            ? $"Press E to gather {(resourceType != null ? resourceType.displayName : "resource")} ({remainingCharges} left)"
            : "Depleted";

        /// <summary>Exposed for SaveManager, which persists this alongside remainingCharges.</summary>
        public bool IsDepleted => depleted;
        public float RegrowthElapsed => depleted ? regrowTimer : 0f;

        public bool CanStrike(GameObject interactor)
        {
            if (remainingCharges <= 0 || interactor == null || resourceType == null) return false;
            Inventory inv = interactor.GetComponent<Inventory>() ?? interactor.GetComponentInParent<Inventory>();
            if (inv == null) return false;
            SheltreSim.Crafting.PlayerGear gear = SheltreSim.Crafting.PlayerGear.Ensure(inv.gameObject);
            ToolKind needed = SheltreSim.Crafting.PlayerGear.ToolFor(resourceType.id);
            bool matchingTool = gear != null && gear.BestOwnedToolId(needed) != null;
            if (matchingTool) return true;

            // Legacy cut-on-hit plants can still be cut by their explicitly supported blades.
            // Trees and mineral nodes require their matching passive tool, so a wrong tool never
            // gets shown as usable or produces a misleading blocked swing.
            return cutOnHit && gear != null && (gear.WeaponId == "machete" || gear.WeaponId == "fire_axe");
        }

        public bool Strike(GameObject interactor)
        {
            if (!CanStrike(interactor)) return false;
            if (cutOnHit) remainingCharges = 1;
            Interact(interactor);
            return true;
        }

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            EnsureInitialized();
            if (remainingCharges <= 0)
            {
                Deplete();
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            startingCharges = Mathf.Max(1, remainingCharges);
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            rendererEnabled = new bool[renderers.Length];
            colliderEnabled = new bool[colliders.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererEnabled[i] = renderers[i].enabled;
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderEnabled[i] = colliders[i].enabled;
            }
        }

        private void Update()
        {
            if (!depleted)
            {
                return;
            }

            regrowTimer += Time.deltaTime;
            if (regrowTimer >= Mathf.Max(0.1f, regrowSeconds))
            {
                Regrow();
            }
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            if (remainingCharges <= 0)
            {
                GameMessageBus.Post("This resource node is depleted.");
                return;
            }

            if (resourceType == null || string.IsNullOrEmpty(resourceType.id) || quantityPerHarvest <= 0)
            {
                Debug.LogError($"ResourceNode on '{name}' has no resourceType assigned.");
                return;
            }

            Inventory inv = interactor.GetComponent<Inventory>();
            if (inv == null)
            {
                inv = interactor.GetComponentInParent<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot gather.");
                return;
            }

            // M21: the best owned axe/pickaxe adds to every harvest. Bare hands still work (bonus 0).
            SheltreSim.Crafting.PlayerGear gear = SheltreSim.Crafting.PlayerGear.Ensure(inv.gameObject);
            int toolBonus = gear != null ? gear.ToolBonus(resourceType.id) : 0;
            int harvested = quantityPerHarvest + toolBonus;
            inv.Add(resourceType.id, harvested);
            string extra = toolBonus > 0 ? $" ({gear.BestToolName(resourceType.id)} +{toolBonus})" : "";
            if (!string.IsNullOrEmpty(bonusId) && bonusChance > 0f)
            {
                // Runtime-only roll: it never touches world layout, so a private Random (not UnityEngine.Random) is enough.
                if (bonusRandom == null) bonusRandom = new System.Random(unchecked(System.Environment.TickCount * 7919 + (int)(transform.position.x * 131f)));
                SheltreSim.Items.ResourceDefinition bonusDef = SheltreSim.Crafting.ItemCatalog.Def(bonusId);
                if (bonusDef != null && bonusRandom.NextDouble() < bonusChance)
                {
                    inv.Add(bonusId, 1);
                    extra += $" +1 {bonusDef.displayName}";
                }
            }
            remainingCharges--;
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.GameAudio.GatherKeyFor(resourceType.id), transform.position, 0.9f, 25f);
            GameMessageBus.Post($"Gathered {harvested} {resourceType.displayName}{extra}.");

            if (remainingCharges <= 0)
            {
                Deplete();
            }
        }

        private void Deplete()
        {
            depleted = true;
            regrowTimer = 0f;
            SetVisible(false);
        }

        private void Regrow()
        {
            depleted = false;
            regrowTimer = 0f;
            remainingCharges = startingCharges;
            SetVisible(true);
        }

        /// <summary>
        /// Enables/disables every child Renderer and Collider, so a
        /// depleted node is invisible and non-interactable while the GameObject itself stays
        /// active (needed so Update() keeps ticking the regrow timer).
        /// </summary>
        private void SetVisible(bool visible)
        {
            EnsureInitialized();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = visible && rendererEnabled[i];
                }
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = visible && colliderEnabled[i];
                }
            }
        }

        /// <summary>
        /// Restores save-loaded state, bypassing the normal gather/regrow flow. Called by
        /// SaveManager after matching this instance to a saved entry via its hierarchy-path id.
        /// </summary>
        public void RestoreState(int charges, bool isDepleted, float regrowthElapsed = 0f)
        {
            EnsureInitialized();
            depleted = isDepleted || charges <= 0;
            remainingCharges = depleted ? 0 : Mathf.Clamp(charges, 1, startingCharges);
            regrowTimer = depleted && !float.IsNaN(regrowthElapsed) && !float.IsInfinity(regrowthElapsed)
                ? Mathf.Clamp(regrowthElapsed, 0f, Mathf.Max(0.1f, regrowSeconds))
                : 0f;
            // Old depletion disabled the entire object. Reactivate it so its regrowth timer
            // can run, after state has been assigned (Awake may run during SetActive).
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            SetVisible(!depleted);
        }
    }
}
