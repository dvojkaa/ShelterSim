using System;
using SheltreSim.Saving;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Core
{
    /// <summary>
    /// Gives the player ten minutes of game time after signing the bunker contract.
    /// If the bunker is not sealed by then, emergency radio news starts the normal siren sequence.
    /// </summary>
    public sealed class RadioNewsCountdown : MonoBehaviour, ISaveableSystem
    {
        public const float DeadlineSeconds = 600f;
        public const string SaveKeyName = "radio_news_countdown";

        [Serializable]
        private sealed class SavedState
        {
            public bool armed;
            public bool bulletinDelivered;
            public float secondsRemaining;
        }

        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private WorldAtmosphere atmosphere;
        private GameStateManager gameState;
        private HomeBase homeBase;
        private Canvas canvas;
        private Text ticker;
        private float deadline;
        private bool armed;
        private bool bulletinDelivered;

        public string SaveKey => SaveKeyName;
        public bool IsArmed => armed;
        public bool BulletinDelivered => bulletinDelivered;
        public float SecondsRemaining => armed && atmosphere != null
            ? Mathf.Max(0f, deadline - atmosphere.ElapsedSeconds) : 0f;

        private void OnEnable() => SaveableSystems.Register(this);
        private void OnDisable() => SaveableSystems.Unregister(this);

        private void Start()
        {
            ResolveReferences();
            BuildTicker();
        }

        private void Update()
        {
            ResolveReferences();
            if (canvas == null) BuildTicker();
            if (!armed || atmosphere == null || gameState == null || homeBase == null)
            {
                SetVisible(false);
                return;
            }

            if (!homeBase.HasContract || gameState.CurrentPhase != GamePhase.Building)
            {
                armed = false;
                SetVisible(false);
                return;
            }

            float remaining = SecondsRemaining;
            if (Time.timeScale <= 0f)
            {
                UpdateTicker(remaining);
                SetVisible(true);
                return;
            }

            if (remaining <= 0f)
            {
                armed = false;
                bulletinDelivered = true;
                SetVisible(false);
                GameMessageBus.Post("RADIO NEWS: Emergency sirens are beginning. Missiles are inbound. Get underground now!");
                gameState.TriggerApocalypse();
                return;
            }

            UpdateTicker(remaining);
            SetVisible(true);
        }

        /// <summary>Called exactly once when the player signs the bunker contract.</summary>
        public void StartAfterContract()
        {
            ResolveReferences();
            if (atmosphere == null || gameState == null || gameState.CurrentPhase != GamePhase.Building) return;
            deadline = atmosphere.ElapsedSeconds + DeadlineSeconds;
            armed = true;
            bulletinDelivered = false;
            GameMessageBus.Post("Radio news countdown started. Seal the bunker before the emergency broadcast.");
            UpdateTicker(DeadlineSeconds);
            SetVisible(true);
        }

        public void ResetForNewRun()
        {
            armed = false;
            bulletinDelivered = false;
            deadline = 0f;
            SetVisible(false);
        }

        public string CaptureState()
        {
            float remaining = SecondsRemaining;
            return JsonUtility.ToJson(new SavedState
            {
                armed = armed,
                bulletinDelivered = bulletinDelivered,
                secondsRemaining = remaining
            });
        }

        public void RestoreState(string state)
        {
            ResolveReferences();
            if (string.IsNullOrEmpty(state))
            {
                // Older saves predate this timer. Give an existing signed contract one full,
                // fair grace period; a new run with no contract stays inactive.
                if (homeBase != null && homeBase.HasContract && gameState != null &&
                    gameState.CurrentPhase == GamePhase.Building && atmosphere != null)
                {
                    deadline = atmosphere.ElapsedSeconds + DeadlineSeconds;
                    armed = true;
                    bulletinDelivered = false;
                }
                else ResetForNewRun();
                return;
            }

            SavedState saved = JsonUtility.FromJson<SavedState>(state);
            if (saved == null)
            {
                ResetForNewRun();
                return;
            }

            bulletinDelivered = saved.bulletinDelivered;
            armed = saved.armed && !bulletinDelivered && homeBase != null && homeBase.HasContract &&
                gameState != null && gameState.CurrentPhase == GamePhase.Building && atmosphere != null;
            float remaining = float.IsNaN(saved.secondsRemaining) || float.IsInfinity(saved.secondsRemaining)
                ? DeadlineSeconds : Mathf.Clamp(saved.secondsRemaining, 0f, DeadlineSeconds);
            deadline = atmosphere != null ? atmosphere.ElapsedSeconds + remaining : 0f;
            if (!armed && gameState != null && gameState.CurrentPhase == GamePhase.Building &&
                homeBase != null && homeBase.HasContract && !bulletinDelivered && atmosphere != null)
            {
                deadline = atmosphere.ElapsedSeconds + DeadlineSeconds;
                armed = true;
            }
            if (armed) UpdateTicker(SecondsRemaining); else SetVisible(false);
        }

        private void ResolveReferences()
        {
            if (homeBase == null) homeBase = HomeBase.Active;
            if (gameState == null && homeBase != null) gameState = homeBase.gameState;
            if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
            if (atmosphere == null) atmosphere = FindFirstObjectByType<WorldAtmosphere>();
        }

        private void BuildTicker()
        {
            if (ticker != null) return;
            canvas = GameUI.CreateCanvas(transform, "RadioNewsCanvas", 20, false);
            ticker = GameUI.Label(canvas.transform, "Deadline", string.Empty, TopCenter, TopCenter,
                new Vector2(0f, -226f), new Vector2(560f, 34f), 20, GameUI.Warning,
                TextAnchor.MiddleCenter, true);
            Shadow shadow = ticker.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            SetVisible(false);
        }

        private void UpdateTicker(float seconds)
        {
            if (ticker == null) return;
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            ticker.text = $"EMERGENCY RADIO BROADCAST IN {whole / 60:00}:{whole % 60:00}  /  SEAL THE BUNKER";
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null) canvas.enabled = visible && !ModalUI.IsOpen;
        }
    }
}
