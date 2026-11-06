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

                if (focused != null)
                {
                    Debug.Log(focused.InteractPrompt);
                }
                else
                {
                    Debug.Log("No interactable in range");
                }
            }
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            focused?.Interact(gameObject);
        }
    }
}
