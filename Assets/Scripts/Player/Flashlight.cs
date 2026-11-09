using SheltreSim.Audio;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SheltreSim.Player
{
    /// <summary>
    /// Camera-mounted spot light on F. One energy cell lasts BatterySeconds of light. With the battery
    /// empty, F loads a cell from the pack. Starts full so the dark stairs on day one are never a trap.
    /// Added to the player by FirstPersonController. Saved as "flashlight".
    /// </summary>
    public class Flashlight : MonoBehaviour, ISaveableSystem
    {
        public const string CellId = "energy_cell";
        public float batterySeconds = 20f * 60f;
        public float range = 28f;
        public float spotAngle = 62f;
        public float intensity = 1.5f;

        public static Flashlight Active { get; private set; }

        /// <summary>0..1 of one cell.</summary>
        public float Charge { get; private set; } = 1f;
        public bool IsOn { get; private set; }

        public string SaveKey => "flashlight";

        private Light spot;
        private InputAction toggle;
        private SurvivalStats stats;
        private Inventory inventory;
        private bool warnedLow;

        [System.Serializable]
        private class State
        {
            public bool on;
            public float charge = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active = null;

        private void Awake()
        {
            Active = this;
            stats = GetComponent<SurvivalStats>();
            inventory = GetComponent<Inventory>();
            FirstPersonController controller = GetComponent<FirstPersonController>();
            Transform mount = controller != null && controller.cameraPivot != null ? controller.cameraPivot : transform;

            var lightObject = new GameObject("Flashlight");
            lightObject.transform.SetParent(mount, false);
            lightObject.transform.localPosition = new Vector3(0.12f, -0.08f, 0.05f);
            lightObject.transform.localRotation = Quaternion.identity;
            spot = lightObject.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = range;
            spot.spotAngle = spotAngle;
            spot.intensity = intensity;
            spot.color = new Color(1f, 0.95f, 0.85f);
            spot.shadows = LightShadows.None;
            // Forward rendering keeps only a few per-pixel lights; the player's own light must never drop to vertex.
            spot.renderMode = LightRenderMode.ForcePixel;
            spot.enabled = false;

            toggle = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/f");
            toggle.AddBinding("<Gamepad>/dpad/up");
            SaveableSystems.Register(this);
        }

        private void OnEnable() => toggle.Enable();
        private void OnDisable() => toggle.Disable();

        private void OnDestroy()
        {
            SaveableSystems.Unregister(this);
            toggle?.Dispose();
            if (spot != null) Destroy(spot.gameObject);
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (toggle.WasPressedThisFrame() && Time.timeScale > 0f && (stats == null || !stats.IsDead)) Press();
            if (!IsOn) return;

            Charge = Mathf.Max(0f, Charge - Time.deltaTime / Mathf.Max(1f, batterySeconds));
            if (Charge <= 0.1f && !warnedLow)
            {
                warnedLow = true;
                GameMessageBus.Post("Flashlight battery low.");
            }
            if (Charge <= 0f)
            {
                SetOn(false);
                GameAudio.Play2D(Sfx.UiTick, 0.6f);
                GameMessageBus.Post(HasCell()
                    ? "Flashlight battery is empty. Press F to load an Energy Cell."
                    : "Flashlight battery is empty. You need an Energy Cell.");
            }
        }

        private void Press()
        {
            if (IsOn)
            {
                SetOn(false);
                GameAudio.Play2D(Sfx.UiTick, 0.7f);
                return;
            }
            if (Charge <= 0f)
            {
                if (inventory == null || !inventory.TryConsume(CellId, 1))
                {
                    GameAudio.Play2D(Sfx.UiDeny, 0.7f);
                    GameMessageBus.Post("Flashlight battery is empty. You need an Energy Cell.");
                    return;
                }
                Charge = 1f;
                warnedLow = false;
                GameMessageBus.Post("Loaded an Energy Cell into the flashlight.");
            }
            SetOn(true);
            GameAudio.Play2D(Sfx.UiTick, 0.7f);
        }

        private bool HasCell() => inventory != null && inventory.GetCount(CellId) > 0;

        private void SetOn(bool value)
        {
            IsOn = value && Charge > 0f;
            if (spot != null) spot.enabled = IsOn;
        }

        public string CaptureState() => JsonUtility.ToJson(new State { on = IsOn, charge = Charge });

        public void RestoreState(string json)
        {
            State state = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { state = JsonUtility.FromJson<State>(json); }
                catch (System.Exception) { state = null; }
            }
            if (state == null) state = new State { on = false, charge = 1f };
            float charge = float.IsNaN(state.charge) ? 1f : state.charge;
            Charge = Mathf.Clamp01(charge);
            warnedLow = Charge <= 0.1f;
            SetOn(state.on);
        }
    }
}
