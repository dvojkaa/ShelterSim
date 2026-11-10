using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SheltreSim.Core;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>Checks that low food/water/power is warned about but never blocks the second-click seal.</summary>
    public sealed class SealReadinessProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--seal-readiness-probe") >= 0)
                new GameObject(nameof(SealReadinessProbe)).AddComponent<SealReadinessProbe>();
        }

        private IEnumerator Start()
        {
            if (ModalUI.IsOpen) ModalUI.DismissCurrent();
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            // MainMenuController opens from Start, after AfterSceneLoad callbacks.
            if (ModalUI.IsOpen) ModalUI.DismissCurrent();
            Time.timeScale = 1f;

            HomeBase home = HomeBase.Active;
            Check(home != null, "the bunker site exists");
            if (home == null) { Finish(); yield break; }
            home.ResolveSupplyCage();
            if (home.inventory != null) home.inventory.Clear();
            if (home.supplyCage != null)
                foreach (var stack in home.supplyCage.Contents.ToArray()) home.supplyCage.TryTake(stack.Key, stack.Value);
            home.Restore(30000, 3, false, 0f, false);
            Check(home.CanSeal, "probe has a valid ready-to-seal bunker state");
            if (!home.CanSeal) { Finish(); yield break; }

            var player = FindFirstObjectByType<SheltreSim.Player.FirstPersonController>();
            BunkerTerminalUI.Open(player != null ? player.gameObject : gameObject);
            BunkerTerminalUI terminal = FindFirstObjectByType<BunkerTerminalUI>();
            Check(terminal != null && BunkerTerminalUI.IsOpen, "terminal opens for seal confirmation");
            if (terminal == null || !BunkerTerminalUI.IsOpen) { Finish(); yield break; }

            MethodInfo seal = typeof(BunkerTerminalUI).GetMethod("Seal", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo readinessField = typeof(BunkerTerminalUI).GetField("sealReadiness", BindingFlags.NonPublic | BindingFlags.Instance);
            seal?.Invoke(terminal, null);
            Text readiness = readinessField?.GetValue(terminal) as Text;
            string rendered = readiness != null ? readiness.text : string.Empty;
            Check(rendered.Contains("FOOD"), "first click warns about low food readiness");
            Check(rendered.Contains("WATER"), "first click warns about low clean-water readiness");
            Check(rendered.Contains("POWER"), "first click warns about missing power readiness");
            Check(rendered.Contains("#FF4B4B") && rendered.Contains("confirm still allowed"),
                "warnings use red emphasis and explicitly leave sealing available");
            Check(!home.IsSealing, "first click only arms confirmation");

            seal?.Invoke(terminal, null);
            Check(home.IsSealing, "second click starts life support despite readiness warnings");
            Finish();
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[SealReadinessProbe] PASS: " + message);
            else { failures++; Debug.LogError("[SealReadinessProbe] FAIL: " + message); }
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? "[SealReadinessProbe] ALL CHECKS PASSED" : $"[SealReadinessProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
