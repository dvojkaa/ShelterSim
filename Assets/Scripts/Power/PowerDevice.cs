using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Power
{
    /// <summary>
    /// What every power device shares: joining the grid only while enabled (a build ghost is enabled once and then
    /// disabled, so nothing here may have a gameplay effect), game-time updates, and an optional modal panel.
    /// Advance holds all the gameplay and gets game time only, so it never runs while paused.
    /// </summary>
    public abstract class PowerDevice : MonoBehaviour, IInteractable, IPieceState, IPowerStatus
    {
        private const float PanelRefreshSeconds = 0.25f;

        private Vector3 lastPosition;
        private Canvas canvas;
        private InputAction cancel;
        private bool panelOpen, closeRequested;
        private float nextRefresh;

        public abstract string PowerName { get; }
        public abstract string PowerStatus { get; }
        public abstract bool PowerRunning { get; }

        public virtual string InteractPrompt => $"{PowerName}: {PowerStatus}";
        public virtual void Interact(GameObject interactor) => GameMessageBus.Post(InteractPrompt);

        public virtual string CapturePieceState() => null;
        public virtual void RestorePieceState(string state) { }

        /// <summary>Stays valid after destruction, because a paused grid can still list a destroyed device.</summary>
        public Vector3 PowerPosition
        {
            get
            {
                if (this != null) lastPosition = transform.position;
                return lastPosition;
            }
        }

        public bool IsPanelOpen => panelOpen;
        protected Inventory PanelInventory { get; private set; }
        protected Text PanelMessage { get; private set; }
        protected virtual Vector2 PanelSize => new Vector2(880f, 620f);

        protected abstract void JoinGrid();
        protected abstract void LeaveGrid();
        protected virtual void BuildPanel(Transform panel) { }
        protected virtual void RefreshPanel() { }

        /// <summary>Game time only. Public so a probe can drive the exact same code in fixed steps.</summary>
        public virtual void Advance(float seconds) { }

        private void OnEnable()
        {
            lastPosition = transform.position;
            JoinGrid();
        }

        private void OnDisable()
        {
            LeaveGrid();
            ClosePanel();
        }

        private void OnDestroy()
        {
            cancel?.Dispose();
            cancel = null;
        }

        private void Update()
        {
            if (Time.deltaTime > 0f) Advance(Time.deltaTime);
            if (panelOpen && Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + PanelRefreshSeconds;
                RefreshPanel();
            }
        }

        private void LateUpdate()
        {
            if (closeRequested) ClosePanel();
        }

        protected bool OpenPanel(GameObject interactor)
        {
            if (interactor == null || panelOpen) return false;
            SurvivalStats stats = interactor.GetComponentInParent<SurvivalStats>();
            if (stats != null && stats.IsDead) return false;
            if (!ModalUI.TryOpen(this, ClosePanel)) return false;

            PanelInventory = PowerNetworks.InventoryOf(interactor);
            if (canvas == null) CreatePanel();
            panelOpen = true;
            closeRequested = false;
            canvas.gameObject.SetActive(true);
            PanelMessage.text = "";
            nextRefresh = Time.unscaledTime + PanelRefreshSeconds;

            // Handled in LateUpdate, so one Escape can't also open the pause menu.
            cancel = new InputAction("ClosePowerPanel", InputActionType.Button, "<Keyboard>/escape");
            cancel.performed += OnCancel;
            cancel.Enable();

            RefreshPanel();
            return true;
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (panelOpen) closeRequested = true;
        }

        public void ClosePanel()
        {
            if (!panelOpen) return;
            panelOpen = false;
            closeRequested = false;
            if (cancel != null)
            {
                cancel.performed -= OnCancel;
                cancel.Disable();
                cancel.Dispose();
                cancel = null;
            }
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }

        private void CreatePanel()
        {
            Vector2 top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f), middle = Vector2.one * 0.5f;
            canvas = GameUI.CreateCanvas(transform, "PowerPanel", 130, true);
            GameUI.Backdrop(canvas.transform, "Backdrop");
            Transform panel = GameUI.Panel(canvas.transform, "Panel", middle, middle, Vector2.zero, PanelSize, GameUI.PanelColor).transform;
            GameUI.Label(panel, "Title", PowerName.ToUpperInvariant(), top, top, new Vector2(0f, -20f), new Vector2(PanelSize.x - 40f, 44f), 28, GameUI.Accent, TextAnchor.MiddleCenter);
            PanelMessage = GameUI.Label(panel, "Message", "", bottom, bottom, new Vector2(0f, 62f), new Vector2(PanelSize.x - 40f, 34f), 18, GameUI.Warning, TextAnchor.MiddleCenter);
            GameUI.Button(panel, "Close", "CLOSE [ESC]", bottom, bottom, new Vector2(0f, 14f), new Vector2(240f, 42f), ClosePanel);
            BuildPanel(panel);
        }
    }
}
