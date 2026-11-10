using System;
using System.Collections;
using System.Linq;
using SheltreSim.Building;
using SheltreSim.Combat;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// <c>--held-tool-probe</c>: with an iron axe, pickaxe and hammer in the pack and nothing equipped, aiming at a tree shows the
    /// axe in hand, at a rock the pickaxe, at a placed wooden wall the axe, and aiming at nothing shows the plain bat. Reads
    /// <see cref="MeleeViewmodel.ShownModelId"/>; nothing is heard or drawn.
    /// </summary>
    public sealed class HeldToolProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--held-tool-probe") >= 0)
                new GameObject(nameof(HeldToolProbe)).AddComponent<HeldToolProbe>();
        }

        private void Check(bool ok, string message)
        {
            if (ok) Debug.Log("[HeldToolProbe] PASS: " + message);
            else { failures++; Debug.LogError("[HeldToolProbe] FAIL: " + message); }
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            Inventory inventory = player.GetComponent<Inventory>();
            MeleeViewmodel view = player.GetComponentInChildren<MeleeViewmodel>(true);
            BuildModeController build = player.GetComponentInChildren<BuildModeController>();
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            yield return new WaitForSeconds(1f);
            Check(view != null, "the player has a MeleeViewmodel");
            if (view == null) { Finish(); yield break; }
            inventory.Add("iron_axe", 1);
            inventory.Add("iron_pickaxe", 1);
            inventory.Add("hammer", 1);

            yield return Aim(player, view, "wood", "melee_iron_axe", "a tree");
            yield return Aim(player, view, "stone", "melee_iron_pickaxe", "a rock");

            // Aim at nothing: the bat.
            Teleport(player, player.transform.position);
            player.RestoreView(-80f);
            yield return new WaitForSeconds(0.7f);
            Check(view.ShownModelId == null, "aiming at the sky shows the plain bat (shown: " + (view.ShownModelId ?? "bat") + ")");
            Finish();
        }

        private IEnumerator Aim(FirstPersonController player, MeleeViewmodel view, string resourceId, string expected, string what)
        {
            ResourceNode node = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None)
                .Where(n => n.resourceType != null && n.resourceType.id == resourceId && n.GetComponentInChildren<Collider>() != null)
                .OrderBy(n => (n.transform.position - player.transform.position).sqrMagnitude).FirstOrDefault();
            Check(node != null, "found " + what + " in the loaded world");
            if (node == null) yield break;
            Collider collider = node.GetComponentInChildren<Collider>();
            Vector3 target = collider.bounds.center;
            Vector3 flat = new Vector3(target.x - player.transform.position.x, 0f, target.z - player.transform.position.z);
            if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
            Vector3 stand = new Vector3(target.x, collider.bounds.min.y, target.z) - flat.normalized * (collider.bounds.extents.magnitude + 1.2f);
            Teleport(player, stand);
            player.transform.rotation = Quaternion.LookRotation(new Vector3(target.x - stand.x, 0f, target.z - stand.z));
            Transform pivot = player.cameraPivot;
            float pitch = pivot != null ? -Mathf.Atan2(target.y - pivot.position.y, new Vector2(target.x - pivot.position.x, target.z - pivot.position.z).magnitude) * Mathf.Rad2Deg : 0f;
            player.RestoreView(pitch);
            yield return new WaitForSeconds(0.9f);
            Check(view.ShownModelId == expected, $"aiming at {what} shows {expected} (shown: {view.ShownModelId ?? "bat"})");
        }

        private static void Teleport(FirstPersonController player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[HeldToolProbe] ALL CHECKS PASSED" : $"[HeldToolProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
