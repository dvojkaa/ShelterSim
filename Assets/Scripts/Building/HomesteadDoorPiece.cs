using System;
using SheltreSim.Audio;
using SheltreSim.Interaction;
using SheltreSim.Saving;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// A hinged door or gate. The root stays still and marks the closed doorway; a child hinge swings the leaf and its collider.
    /// Saved open or closed through IPieceState. Awake only poses the leaf, so a build preview has no gameplay effect.
    /// </summary>
    public sealed class HomesteadDoorPiece : MonoBehaviour, IInteractable, IPieceState
    {
        [Serializable] private sealed class SavedDoor { public bool open; }

        public Transform hinge;
        public Vector3 closedSize = new Vector3(1.6f, 2.2f, .08f);
        public float swingDegrees = 90f;
        public string noun = "door";

        private bool isOpen;
        private DoorVisualTween visualTween;

        public bool IsOpen => isOpen;
        public Vector3 PassageCenter => transform.position;
        public Vector3 PassageNormal => transform.forward;
        public string InteractPrompt => isOpen ? $"Press E to close the {noun}" : $"Press E to open the {noun}";

        private void Awake() => ApplyPose();

        private void Update() => visualTween?.Tick(Time.deltaTime);

        private void OnDisable() => visualTween?.Finish();

        public void Interact(GameObject interactor)
        {
            if (Time.timeScale <= 0f) return;
            if (isOpen && DoorwayOccupied())
            {
                GameAudio.Play2D(Sfx.UiDeny, 0.35f);
                GameMessageBus.Post($"The {noun} is blocked. Move clear before closing it.");
                return;
            }
            isOpen = !isOpen;
            ApplyPose(true);
            GameAudio.PlayAt(isOpen ? Sfx.DoorOpen : Sfx.DoorClose, transform.position, 0.85f, 28f, 0.03f);
        }

        /// <summary>For raiders breaking through: opens with no doorway check.</summary>
        public void ForceOpen()
        {
            if (isOpen) return;
            isOpen = true;
            ApplyPose(true, true);
            GameAudio.PlayAt(Sfx.DoorBash, transform.position, 1f, 45f);
        }

        private void ApplyPose(bool animate = false, bool forced = false)
        {
            if (hinge != null)
            {
                if (visualTween == null) visualTween = new DoorVisualTween(hinge);
                visualTween.SetPose(Quaternion.Euler(0f, isOpen ? swingDegrees : 0f, 0f),
                    animate && Application.isPlaying && isActiveAndEnabled,
                    forced ? .12f : isOpen ? .35f : .3f, isOpen ? forced ? 10f : 4f : 0f);
            }
            Physics.SyncTransforms();
        }

        /// <summary>Anyone standing in the doorway, not only touching the closed leaf.</summary>
        public bool DoorwayOccupied()
        {
            Vector3 half = new Vector3(closedSize.x / 2f, closedSize.y / 2f, Mathf.Max(closedSize.z / 2f, .1f));
            foreach (Collider other in Physics.OverlapBox(transform.position, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (other.GetComponentInParent<CharacterController>() != null) return true;
            return false;
        }

        public string CapturePieceState() => JsonUtility.ToJson(new SavedDoor { open = isOpen });

        public void RestorePieceState(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            try { isOpen = JsonUtility.FromJson<SavedDoor>(state)?.open ?? false; }
            catch (ArgumentException) { isOpen = false; }
            ApplyPose();
        }
    }
}
