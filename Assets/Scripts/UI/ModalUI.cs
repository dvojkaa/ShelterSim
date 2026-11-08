using System;
using UnityEngine;

namespace SheltreSim.UI
{
    /// <summary>One owner for the paused world and free cursor. Every modal releases through this seam.</summary>
    public static class ModalUI
    {
        private static MonoBehaviour owner;
        private static Action dismiss;
        private static float resumeTimeScale = 1f;

        public static bool IsOpen => owner != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            owner = null;
            dismiss = null;
            resumeTimeScale = 1f;
            Time.timeScale = 1f;
        }

        public static bool TryOpen(MonoBehaviour nextOwner, Action onDismiss)
        {
            if (nextOwner == null || IsOpen || Time.timeScale <= 0f)
            {
                return false;
            }

            resumeTimeScale = Time.timeScale;
            owner = nextOwner;
            dismiss = onDismiss;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        /// <summary>Death takes ownership after dismissing any existing menu.</summary>
        public static void Replace(MonoBehaviour nextOwner, Action onDismiss)
        {
            DismissCurrent();
            if (Time.timeScale <= 0f)
            {
                Time.timeScale = 1f;
            }
            TryOpen(nextOwner, onDismiss);
        }

        public static void Close(MonoBehaviour closingOwner)
        {
            if (owner != closingOwner)
            {
                return;
            }

            owner = null;
            dismiss = null;
            Time.timeScale = resumeTimeScale;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Called only once a load is validated, or when replacing the active modal.</summary>
        public static void DismissCurrent()
        {
            Action close = dismiss;
            close?.Invoke();
        }
    }
}
