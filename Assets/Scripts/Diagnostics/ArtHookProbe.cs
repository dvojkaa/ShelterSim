using System;
using System.Collections;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Checks ground-item mappings, Resources prefabs, and visual-only spawning. Run with --art-hook-probe.</summary>
    public sealed class ArtHookProbe : MonoBehaviour
    {
        private static readonly (string itemId, string modelId)[] ExpectedMappings =
        {
            ("canned_food", "item_canned_food"),
            ("bottled_water", "item_bottle"),
            ("first_aid", "item_medkit"),
            ("expedition_parts", "item_scrap_bundle"),
        };

        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--art-hook-probe") >= 0)
                new GameObject(nameof(ArtHookProbe)).AddComponent<ArtHookProbe>();
        }

        private IEnumerator Start()
        {
            yield return null;
            var parent = new GameObject("ArtHookProbeTemp");
            try
            {
                for (int i = 0; i < ExpectedMappings.Length; i++)
                {
                    var (itemId, expectedModel) = ExpectedMappings[i];
                    string actualModel = SheltreSim.Items.PropModels.ModelFor(itemId);
                    Check(actualModel == expectedModel, $"{itemId} maps to {expectedModel} (got {actualModel ?? "<none>"})");
                    if (actualModel != expectedModel) continue;

                    GameObject visual = SheltreSim.Items.PropModels.Spawn(actualModel, parent.transform, new Vector3(i * 2f, 0f, 0f));
                    if (visual == null)
                    {
                        Check(false, $"Resources/Props/{expectedModel} loads and spawns");
                        continue;
                    }
                    Check(true, $"Resources/Props/{expectedModel} loads and spawns");
                    Check(visual.name == "Visual_" + expectedModel, expectedModel + " creates a named visual instance");
                    Check(visual.GetComponentsInChildren<Collider>(true).Length == 0, expectedModel + " visual has no colliders");
                    Check(visual.transform.parent == parent.transform, expectedModel + " remains under the requested interaction root");
                }
            }
            finally
            {
                Destroy(parent);
            }

            yield return null;
            Debug.Log(failures == 0 ? "[ArtHookProbe] ALL CHECKS PASSED" : $"[ArtHookProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private void Check(bool condition, string message)
        {
            if (!condition) failures++;
            Debug.Log((condition ? "[ArtHookProbe] PASS: " : "[ArtHookProbe] FAIL: ") + message);
        }
    }
}
