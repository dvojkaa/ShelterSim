using SheltreSim.Interaction;
using UnityEngine;

namespace SheltreSim.Player
{
    /// <summary>
    /// Hands the player's Interact InputAction to PlayerInteractor at runtime. Must run in Start(),
    /// since FirstPersonController constructs its PlayerInputActions in Awake() and Unity guarantees
    /// all Awake() calls in the scene finish before any Start() call.
    /// </summary>
    public class PlayerWiring : MonoBehaviour
    {
        public FirstPersonController firstPersonController;
        public PlayerInteractor interactor;

        private void Start()
        {
            if (firstPersonController == null || interactor == null)
            {
                Debug.LogError("PlayerWiring: firstPersonController and interactor must both be assigned.", this);
                return;
            }

            interactor.Initialize(firstPersonController.GetInteractAction());
        }
    }
}
