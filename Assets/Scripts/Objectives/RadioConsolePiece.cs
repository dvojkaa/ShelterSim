using System;
using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Core;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Power;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Objectives
{
    /// <summary>
    /// Scans the air for a site you need, and later contacts the repaired relay. Progress only moves on powered game
    /// time: a power cut keeps it, E doesn't touch it, and the result goes through ObjectiveTracker exactly once.
    /// </summary>
    public sealed class RadioConsolePiece : MonoBehaviour, IInteractable, IPowerConsumer, IPieceState
    {
        public const float ScanSeconds = 45f, ContactSeconds = 60f, DrawKilowatts = 1f;
        public enum Operation { None, Scan, Contact }

        [Serializable]
        private class SavedState
        {
            public int operation;
            public int need;
            public float elapsed;
        }

        private static readonly List<RadioConsolePiece> placed = new List<RadioConsolePiece>();
        public static bool AnyPlaced => placed.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => placed.Clear();

        private Operation operation;
        private RadioNeed need;
        private float elapsed;
        private bool powered;
        private float nextStatic;

        private Canvas canvas;
        private Text statusLabel;
        private Button[] scanButtons;
        private Button contactButton, cancelButton;
        private bool open, closeRequested;
        private InputAction cancel;
        private GameStateManager gameState;

        private static readonly RadioNeed[] Needs = { RadioNeed.Fuel, RadioNeed.Materials, RadioNeed.Parts, RadioNeed.Medicine, RadioNeed.Food, RadioNeed.MilitaryRelay };

        public Operation CurrentOperation => operation;
        public RadioNeed CurrentNeed => need;
        public float Elapsed => elapsed;
        public bool IsPowered => powered;
        private float Duration => operation == Operation.Contact ? ContactSeconds : ScanSeconds;
        private string OperationName => operation == Operation.Contact ? "Contact relay" : "Scan for " + ObjectiveTracker.NeedName(need);

        public Vector3 PowerPosition => transform.position;
        public float PowerDemand => operation != Operation.None ? DrawKilowatts : 0f;
        public int PowerPriority => 2;
        public float HeatOutput => 0f;
        public float CoolingOutput => 0f;
        public string PowerName => "Radio Console";
        public void SetPowered(bool value) => powered = value;

        public string InteractPrompt
        {
            get
            {
                if (operation == Operation.None)
                    return powered ? "Press E to use the Radio Console" : "Radio Console: NO POWER. Press E to open";
                int percent = Mathf.FloorToInt(elapsed / Duration * 100f);
                return powered ? $"Press E: {OperationName}, {percent}%" : $"{OperationName} stopped at {percent}%: NO POWER";
            }
        }

        private void Awake() => cancel = new InputAction("CloseRadio", InputActionType.Button, "<Keyboard>/escape");

        private void OnEnable()
        {
            cancel.Enable();
            cancel.performed += OnCancel;
            PowerGrid.Register(this);
            if (!placed.Contains(this)) placed.Add(this);
        }

        private void OnDisable()
        {
            Close();
            cancel.performed -= OnCancel;
            cancel.Disable();
            PowerGrid.Unregister(this);
            placed.Remove(this);
            powered = false;
        }

        private void OnDestroy() => cancel?.Dispose();

        private void OnCancel(InputAction.CallbackContext _) { if (open) closeRequested = true; }

        private void Update()
        {
            if (operation == Operation.None || !powered || Time.timeScale <= 0f) return;
            Advance(Time.deltaTime);
        }

        private void LateUpdate() { if (closeRequested) Close(); }

        /// <summary>Probe only: game time passing, gated by power exactly like Update.</summary>
        public void DebugAdvance(float seconds)
        {
            if (operation != Operation.None && powered) Advance(seconds);
        }

        private void Advance(float seconds)
        {
            elapsed += seconds;
            if (Time.time >= nextStatic)
            {
                nextStatic = Time.time + 9f;
                GameAudio.PlayAt(Sfx.RadioStatic, transform.position, 0.45f, 12f);
            }
            if (elapsed >= Duration) Finish();
        }

        private void Finish()
        {
            ObjectiveTracker tracker = ObjectiveTracker.Ensure();
            if (operation == Operation.Scan)
            {
                RadioNeed finished = need;
                operation = Operation.None;
                elapsed = 0f;
                tracker.CompleteScan(finished, transform.position);
                return;
            }
            if (tracker.Step == RelayStep.Contact)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                Inventory inventory = controller != null ? controller.GetComponent<Inventory>() : null;
                if (inventory == null) { elapsed = ContactSeconds; return; } // try again next frame, nothing is lost
                tracker.CompleteRelayContact(inventory);
            }
            operation = Operation.None;
            elapsed = 0f;
        }

        /// <summary>Starts a scan unless the console is busy. An operation in progress is never restarted or sped up.</summary>
        public bool BeginScan(RadioNeed wanted, out string message)
        {
            if (operation != Operation.None) { message = "The console is busy: " + OperationName + "."; return false; }
            if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
            if (gameState != null && gameState.CurrentPhase != GamePhase.Survival)
            {
                message = "Nothing but the usual broadcasts on the air.";
                return false;
            }
            if (wanted == RadioNeed.MilitaryRelay && ObjectiveTracker.Ensure().RelayRepaired)
            {
                message = "The relay is already repaired.";
                return false;
            }
            operation = Operation.Scan;
            need = wanted;
            elapsed = 0f;
            GameAudio.PlayAt(Sfx.RadioStatic, transform.position, 0.45f, 12f);
            nextStatic = Time.time + 9f;
            message = "Scanning for " + ObjectiveTracker.NeedName(wanted) + ".";
            return true;
        }

        public bool BeginContact(out string message)
        {
            if (operation != Operation.None) { message = "The console is busy: " + OperationName + "."; return false; }
            if (ObjectiveTracker.Ensure().Step != RelayStep.Contact)
            {
                message = ObjectiveTracker.Ensure().RelayRewarded ? "The relay is already online." : "No repaired relay to contact.";
                return false;
            }
            operation = Operation.Contact;
            elapsed = 0f;
            GameAudio.PlayAt(Sfx.RadioStatic, transform.position, 0.45f, 12f);
            nextStatic = Time.time + 9f;
            message = "Calling the relay.";
            return true;
        }

        public void CancelOperation()
        {
            operation = Operation.None;
            elapsed = 0f;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            SurvivalStats stats = interactor.GetComponentInParent<SurvivalStats>();
            if (stats != null && stats.IsDead) return;
            if (!ModalUI.TryOpen(this, Close)) return;
            if (canvas == null) BuildUI();
            open = true;
            canvas.gameObject.SetActive(true);
            Refresh(null);
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            closeRequested = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }

        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform, "RadioConsole", 130, true);
            GameUI.Backdrop(canvas.transform, "Backdrop");
            Transform p = GameUI.Panel(canvas.transform, "Panel", Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(780, 640), GameUI.PanelColor).transform;
            Vector2 top = new Vector2(.5f, 1);
            GameUI.Label(p, "Title", "RADIO CONSOLE", top, top, new Vector2(0, -22), new Vector2(720, 44), 28, GameUI.Accent, TextAnchor.MiddleCenter);
            statusLabel = GameUI.Label(p, "Status", "", top, top, new Vector2(0, -74), new Vector2(720, 104), 19, Color.white, TextAnchor.MiddleCenter);
            scanButtons = new Button[Needs.Length];
            for (int i = 0; i < Needs.Length; i++)
            {
                RadioNeed wanted = Needs[i];
                Vector2 position = new Vector2(i % 2 == 0 ? -182 : 182, -190 - (i / 2) * 68);
                scanButtons[i] = GameUI.Button(p, "Scan_" + wanted, ObjectiveTracker.NeedName(wanted), top, top, position, new Vector2(348, 58), () => ClickScan(wanted));
            }
            contactButton = GameUI.Button(p, "ContactRelay", "CONTACT RELAY (60 s)", top, top, new Vector2(0, -400), new Vector2(712, 58), ClickContact, true);
            cancelButton = GameUI.Button(p, "CancelOperation", "CANCEL OPERATION", top, top, new Vector2(0, -468), new Vector2(712, 50), ClickCancel);
            GameUI.Button(p, "Close", "CLOSE [ESC]", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 16), new Vector2(240, 44), Close);
            canvas.gameObject.SetActive(false);
        }

        private void ClickScan(RadioNeed wanted) { BeginScan(wanted, out string message); Refresh(message); }
        private void ClickContact() { BeginContact(out string message); Refresh(message); }
        private void ClickCancel() { CancelOperation(); Refresh("Operation cancelled."); }

        private void Refresh(string note)
        {
            ObjectiveTracker tracker = ObjectiveTracker.Ensure();
            string line = operation == Operation.None
                ? "Choose what you need. A scan takes 45 s of powered time and marks the nearest site you haven't been to."
                : $"{OperationName}: {elapsed:0} / {Duration:0} s of powered time.";
            string power = powered ? "Power: OK." : "NO POWER: a running generator or battery must be within 25 m. Progress is kept.";
            statusLabel.text = (string.IsNullOrEmpty(note) ? "" : note + "\n") + line + "\n" + power;
            bool idle = operation == Operation.None;
            for (int i = 0; i < Needs.Length; i++)
                scanButtons[i].interactable = idle && (Needs[i] != RadioNeed.MilitaryRelay || !tracker.RelayRepaired);
            contactButton.interactable = idle && tracker.Step == RelayStep.Contact;
            contactButton.GetComponentInChildren<Text>().text = tracker.RelayRewarded ? "RELAY ONLINE" : "CONTACT RELAY (60 s)";
            cancelButton.interactable = !idle;
        }

        public string CapturePieceState() => JsonUtility.ToJson(new SavedState { operation = (int)operation, need = (int)need, elapsed = elapsed });

        public void RestorePieceState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            SavedState saved;
            try { saved = JsonUtility.FromJson<SavedState>(json); }
            catch (ArgumentException) { return; }
            if (saved == null || saved.operation < 0 || saved.operation > (int)Operation.Contact ||
                saved.need < 0 || saved.need > (int)RadioNeed.MilitaryRelay) return;
            operation = (Operation)saved.operation;
            need = (RadioNeed)saved.need;
            float maximum = operation == Operation.Contact ? ContactSeconds : ScanSeconds;
            elapsed = operation == Operation.None || float.IsNaN(saved.elapsed) ? 0f : Mathf.Clamp(saved.elapsed, 0f, maximum);
        }
    }
}
