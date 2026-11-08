using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Interaction
{
    public class PlayerInteractor : MonoBehaviour
    {
        public float interactRange = 2.5f;
        public LayerMask interactableMask = ~0;

        public event Action<IInteractable> FocusChanged;

        private IInteractable focused;
        private string lastLoggedPrompt;
        private InputAction interactAction;

        public void Initialize(InputAction action)
        {
            interactAction = action;
            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
            }
        }

        private void OnDestroy()
        {
            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
            }
        }

        private void Update()
        {
            IInteractable hitInteractable = null;

            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, interactRange, interactableMask))
            {
                hitInteractable = hit.collider.GetComponent<IInteractable>();
                if (hitInteractable == null)
                {
                    hitInteractable = hit.collider.GetComponentInParent<IInteractable>();
                }
            }

            if (!ReferenceEquals(hitInteractable, focused))
            {
                focused = hitInteractable;
                FocusChanged?.Invoke(focused);
            }

            // The prompt is re-read every frame and logged whenever its TEXT changes, not only
            // when the focused object changes. Nothing subscribes to FocusChanged, so the
            // console log is the player's only readout — and some prompts change while you
            // keep staring at the same object: M11's Water Collector goes from "none ready
            // yet" to "Press E to collect N Water" purely with time, and a resource node's
            // "(N left)" counts down as you gather. Logging on focus change alone froze those
            // at whatever they said the moment you looked, which for the Water Collector meant
            // its entire state was invisible unless you looked away and back.
            string prompt = focused != null ? focused.InteractPrompt : "No interactable in range";
            if (prompt != lastLoggedPrompt)
            {
                lastLoggedPrompt = prompt;
                Debug.Log(prompt);
            }
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            // A modal panel (pause menu, inventory) sets Time.timeScale to 0, but input
            // callbacks keep firing — this project has no notion of UI input consuming
            // gameplay input (every action map is hand-built and always listening; see the
            // second QC pass's "three ways the pause menu wasn't actually paused"). Without
            // this, E behind an open panel still crafts at a Workbench, drains a Water
            // Collector, opens a door, or fires M7's one-way apocalypse trigger — aimed at
            // whatever the frozen view happened to be pointing at. Build mode is not gated
            // here: it doesn't touch timeScale and interacting during it was already possible.
            if (Time.timeScale == 0f)
            {
                return;
            }

            focused?.Interact(gameObject);
        }
    }
}
