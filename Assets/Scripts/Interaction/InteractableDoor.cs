using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Interaction
{
    public class InteractableDoor : MonoBehaviour, IInteractable
    {
        public Transform doorVisual;

        private bool isOpen;
        private bool initialized;
        private BoxCollider doorCollider;
        private Quaternion closedRotation;
        private Vector3 passageCenter;
        private Vector3 passageNormal;
        private Vector3 closedHalfExtents;
        private Quaternion closedColliderRotation;

        public string InteractPrompt => isOpen ? "Press E to close" : "Press E to open";

        public bool IsOpen => isOpen;

        // These describe the doorway, not the leaf after it has swung to one side.
        public Vector3 PassageCenter { get { Initialize(); return passageCenter; } }
        public Vector3 PassageNormal { get { Initialize(); return passageNormal; } }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            doorCollider = GetComponent<BoxCollider>();
            closedRotation = doorVisual != null ? doorVisual.localRotation : Quaternion.identity;
            passageCenter = doorCollider != null
                ? transform.TransformPoint(doorCollider.center)
                : transform.position;
            passageNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            closedColliderRotation = transform.rotation;
            if (doorCollider != null)
            {
                Vector3 scale = transform.lossyScale;
                scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                closedHalfExtents = Vector3.Scale(doorCollider.size * 0.5f, scale);
            }
        }

        public void ForceOpen()
        {
            if (isOpen)
            {
                return;
            }

            RestoreState(true);
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.DoorBash, passageCenter, 1f, 45f);
            GameMessageBus.Post("A raider forced the door open!");
        }

        /// <summary>Restores a saved pose without replaying interaction feedback.</summary>
        public void RestoreState(bool open)
        {
            Initialize();
            isOpen = open;
            if (doorVisual != null)
            {
                doorVisual.localRotation = closedRotation * Quaternion.Euler(0f, open ? 90f : 0f, 0f);
            }
            else if (doorCollider != null)
            {
                doorCollider.enabled = !open;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            Initialize();
            if (isOpen && DoorwayOccupied())
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                GameMessageBus.Post("The doorway is blocked. Move clear before closing it.");
                return;
            }

            RestoreState(!isOpen);
            SheltreSim.Audio.GameAudio.PlayAt(isOpen ? SheltreSim.Audio.Sfx.DoorOpen : SheltreSim.Audio.Sfx.DoorClose, passageCenter, 0.85f, 28f, 0.03f);
        }

        private bool DoorwayOccupied()
        {
            if (doorCollider == null)
            {
                return false;
            }

            Collider[] overlaps = Physics.OverlapBox(passageCenter, closedHalfExtents,
                closedColliderRotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps)
            {
                if (other.GetComponentInParent<CharacterController>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
