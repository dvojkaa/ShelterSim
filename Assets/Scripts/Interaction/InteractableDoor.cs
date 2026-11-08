using UnityEngine;

namespace SheltreSim.Interaction
{
    public class InteractableDoor : MonoBehaviour, IInteractable
    {
        private static readonly Vector3 ClosedRotation = Vector3.zero;
        private static readonly Vector3 OpenRotation = new Vector3(0f, 90f, 0f);

        public Transform doorVisual;

        private bool isOpen;
        private BoxCollider fallbackCollider;

        public string InteractPrompt => isOpen ? "Press E to close" : "Press E to open";

        public bool IsOpen => isOpen;

        /// <summary>
        /// Opens the door without going through player Interact() — used by a chasing
        /// <see cref="SheltreSim.Enemies.EnemyAI"/> to break into the bunker. A closed door
        /// was previously only ever toggled by the player pressing E, so "hide inside and
        /// close the door" was an absolute safe-room with no in-game way for a raider to
        /// follow through it. No-ops if already open.
        /// </summary>
        public void ForceOpen()
        {
            if (isOpen)
            {
                return;
            }

            isOpen = true;

            if (doorVisual != null)
            {
                doorVisual.localRotation = Quaternion.Euler(OpenRotation);
            }
            else if (fallbackCollider != null)
            {
                fallbackCollider.enabled = false;
            }

            Debug.Log($"{name} was forced open!");
        }

        private void Awake()
        {
            if (doorVisual == null)
            {
                fallbackCollider = GetComponent<BoxCollider>();
                Debug.LogWarning($"{name}: no doorVisual assigned, falling back to toggling BoxCollider (test-mode only)", this);
            }
        }

        public void Interact(GameObject interactor)
        {
            isOpen = !isOpen;

            if (doorVisual != null)
            {
                doorVisual.localRotation = Quaternion.Euler(isOpen ? OpenRotation : ClosedRotation);
            }
            else if (fallbackCollider != null)
            {
                fallbackCollider.enabled = !isOpen;
            }

            Debug.Log($"Door is now {(isOpen ? "open" : "closed")}");
        }
    }
}
