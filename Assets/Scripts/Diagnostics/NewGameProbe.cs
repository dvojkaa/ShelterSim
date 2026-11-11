using System;
using System.Collections;
using SheltreSim.Core;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// <c>--newgame-probe</c>: the start menu must open once, "New Game" must reload the scene WITHOUT showing the menu again,
    /// leave the game unpaused and not modal. (Regression: New Game used to bring the menu straight back.)
    /// </summary>
    public sealed class NewGameProbe : MonoBehaviour
    {
        private static NewGameProbe instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--newgame-probe") < 0 || instance != null) return;
            var go = new GameObject("NewGameProbe");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<NewGameProbe>();
        }

        private int failures;
        private void Check(bool ok, string what) { if (ok) Debug.Log("[NewGameProbe] PASS: " + what); else { failures++; Debug.LogError("[NewGameProbe] FAIL: " + what); } }

        private IEnumerator Start()
        {
            for (int i = 0; i < 10; i++) yield return null;
            var menu = FindAnyObjectByType<MainMenuController>();
            Check(menu != null && menu.IsMenuOpen, "the start menu is open on launch");
            Check(ModalUI.IsOpen, "the menu is modal while open");
            if (menu == null) { Application.Quit(1); yield break; }
            menu.StartNewGame();
            for (int i = 0; i < 30; i++) yield return null;
            var after = FindAnyObjectByType<MainMenuController>();
            Check(after != null && !after.IsMenuOpen, "after New Game the menu does not open again");
            Check(!ModalUI.IsOpen, "no modal window is open after New Game");
            Check(Mathf.Approximately(Time.timeScale, 1f), "the game is running (timeScale 1)");
            Debug.Log(failures == 0 ? "[NewGameProbe] ALL CHECKS PASSED" : "[NewGameProbe] CHECKS FAILED: " + failures);
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
