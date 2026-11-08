using SheltreSim.Interaction;
using UnityEngine;

namespace SheltreSim.Items
{
    public class ResourceNode : MonoBehaviour, IInteractable
    {
        public ResourceDefinition resourceType;
        public int quantityPerHarvest = 1;
        public int remainingCharges = 3;

        /// <summary>
        /// Seconds a fully-depleted node takes to regrow back to its starting charge count.
        /// Depletion used to be permanent (gameObject.SetActive(false)), which left the whole
        /// world barren after one sweep — and would have been impossible to fix with a simple
        /// timer anyway, since Update() never runs on a disabled GameObject. Depletion now
        /// disables only the visuals/collider (see Deplete/Regrow below) so this timer keeps
        /// ticking while depleted.
        /// </summary>
        public float regrowSeconds = 180f;

        private int startingCharges;
        private bool depleted;
        private float regrowTimer;

        public string InteractPrompt => remainingCharges > 0
            ? $"Press E to gather {(resourceType != null ? resourceType.displayName : "resource")} ({remainingCharges} left)"
            : "Depleted";

        /// <summary>Exposed for SaveManager, which persists this alongside remainingCharges.</summary>
        public bool IsDepleted => depleted;

        private void Awake()
        {
            // Cache the inspector/editor-tool-assigned starting value so Regrow() can restore
            // to it later, since remainingCharges itself gets mutated down to 0 by gathering.
            startingCharges = remainingCharges;
        }

        private void Update()
        {
            if (!depleted)
            {
                return;
            }

            regrowTimer += Time.deltaTime;
            if (regrowTimer >= regrowSeconds)
            {
                Regrow();
            }
        }

        public void Interact(GameObject interactor)
        {
            if (remainingCharges <= 0)
            {
                Debug.Log("This resource node is depleted.");
                return;
            }

            if (resourceType == null)
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

            inv.Add(resourceType.id, quantityPerHarvest);
            remainingCharges--;

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
        /// Enables/disables every child Renderer and this node's own Collider(s), so a
        /// depleted node is invisible and non-interactable while the GameObject itself stays
        /// active (needed so Update() keeps ticking the regrow timer).
        /// </summary>
        private void SetVisible(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = visible;
            }

            foreach (Collider c in GetComponents<Collider>())
            {
                c.enabled = visible;
            }
        }

        /// <summary>
        /// Restores save-loaded state, bypassing the normal gather/regrow flow. Called by
        /// SaveManager after matching this instance to a saved entry via its hierarchy-path id.
        /// </summary>
        public void RestoreState(int charges, bool isDepleted)
        {
            remainingCharges = charges;
            depleted = isDepleted;
            regrowTimer = 0f;
            SetVisible(!isDepleted);
        }
    }
}
