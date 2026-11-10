using System;
using System.Collections;
using System.Reflection;
using SheltreSim.Combat;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Run the built player with --animation-probe for animation timing and suppression checks.</summary>
    public sealed class AnimationProbe : MonoBehaviour
    {
        private int failures;
        private FirstPersonController player;
        private CharacterController body;
        private Vector3 savedPosition;
        private Quaternion savedRotation;
        private float savedTimeScale;
        private bool savedFeelEnabled;
        private float savedFeelIntensity;
        private GameObject raider;
        private GameObject platform;
        private GameObject testDoor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--animation-probe") >= 0)
                new GameObject(nameof(AnimationProbe)).AddComponent<AnimationProbe>();
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            player = FindFirstObjectByType<FirstPersonController>();
            if (player == null)
            {
                Fail("scene player is missing");
                Finish();
                yield break;
            }

            body = player.GetComponent<CharacterController>();
            savedPosition = player.transform.position;
            savedRotation = player.transform.rotation;
            savedTimeScale = Time.timeScale;
            savedFeelEnabled = CameraFeel.Enabled;
            savedFeelIntensity = CameraFeel.Intensity;
            Time.timeScale = 1f;
            CameraFeel feel = player.GetComponent<CameraFeel>();
            if (feel == null) feel = player.gameObject.AddComponent<CameraFeel>();

            yield return CheckDoorEasing();
            CheckMeleeHitTiming();
            CheckCameraSuppression(feel);

            RestorePlayer();
            Finish();
        }

        private IEnumerator CheckDoorEasing()
        {
            testDoor = new GameObject("AnimationProbeDoor");
            testDoor.transform.position = new Vector3(10000f, 10000f, 10000f);
            var collider = testDoor.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 2f, .2f);
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf";
            leaf.transform.SetParent(testDoor.transform, false);
            leaf.transform.localScale = new Vector3(1.8f, 1.9f, .12f);
            Destroy(leaf.GetComponent<Collider>());
            InteractableDoor door = testDoor.AddComponent<InteractableDoor>();
            door.doorVisual = testDoor.transform;
            door.RestoreState(false);
            door.Interact(null);

            Check(door.IsOpen, "door logical open state changes immediately on interaction");
            Transform motion = testDoor.transform.Find("DoorMotionVisual");
            Check(motion != null, "door creates a separate mesh-only easing visual");
            if (motion != null)
            {
                Quaternion startPose = testDoor.transform.localRotation * motion.localRotation;
                Check(Quaternion.Angle(Quaternion.identity, startPose) < 1f,
                    "door easing starts from the closed visual pose while its logical hinge is already open");
            }

            // One frame advances only the presentation. The logical state must remain immediate.
            yield return null;
            yield return null;
            if (door == null || motion == null) yield break;
            Quaternion displayed = door.transform.localRotation * motion.localRotation;
            float angle = Quaternion.Angle(Quaternion.identity, displayed);
            Check(door.IsOpen && angle > .1f && angle < 89.9f,
                $"door visual eases after the immediate logical transition (shown angle {angle:F1} degrees)");
        }

        private void CheckMeleeHitTiming()
        {
            if (ProceduralWorld.Active == null || ProceduralWorld.Active.assets == null ||
                ProceduralWorld.Active.assets.raiderPrefab == null)
            {
                Fail("raider prefab is missing; cannot verify synchronous melee hit timing");
                return;
            }

            MeleeAttacker melee = player.GetComponentInChildren<MeleeAttacker>(true);
            if (melee == null) { Fail("player MeleeAttacker is missing"); return; }
            bool previousIgnoreCursor = melee.ignoreCursorLock;
            bool previousSuppressed = melee.suppressed;
            float previousCooldown = melee.attackCooldown;
            float previousDamage = melee.attackDamage;
            float previousAttackTime = (float)(typeof(MeleeAttacker)
                .GetField("lastAttackTime", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(melee) ?? float.NegativeInfinity);
            Vector3 originalPosition = player.transform.position;
            Quaternion originalRotation = player.transform.rotation;
            float originalHeight = Time.timeScale;

            Time.timeScale = 1f;
            Vector3 origin = new Vector3(originalPosition.x, 400f, originalPosition.z);
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "AnimationProbePlatform";
            platform.transform.localScale = new Vector3(100f, 1f, 100f);
            platform.transform.position = origin + Vector3.down * .5f;
            body.enabled = false;
            player.transform.SetPositionAndRotation(origin + Vector3.up * .05f, Quaternion.identity);
            body.enabled = true;

            raider = Instantiate(ProceduralWorld.Active.assets.raiderPrefab);
            raider.name = "AnimationProbeRaider";
            foreach (MonoBehaviour behaviour in raider.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
            CharacterController enemyBody = raider.GetComponent<CharacterController>();
            if (enemyBody == null)
            {
                Fail("raider prefab has no CharacterController");
                RestoreMelee(melee, previousIgnoreCursor, previousSuppressed, previousCooldown, previousDamage, previousAttackTime);
                RestorePlayer();
                return;
            }

            raider.transform.position = origin + Vector3.up * (0.05f - (enemyBody.center.y - enemyBody.height * .5f)) + Vector3.forward * 2f;
            EnemyAI enemy = raider.GetComponent<EnemyAI>();
            if (enemy == null)
            {
                Fail("raider prefab has no EnemyAI");
                RestoreMelee(melee, previousIgnoreCursor, previousSuppressed, previousCooldown, previousDamage, previousAttackTime);
                RestorePlayer();
                return;
            }
            enemy.maxHealth = enemy.health = 500f;
            enemy.enabled = true;
            Physics.SyncTransforms();
            Vector3 aim = enemyBody.bounds.center - (player.cameraPivot != null ? player.cameraPivot.position : player.transform.position);
            Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
            if (flat.sqrMagnitude > .001f) player.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            if (player.cameraPivot != null)
                player.RestoreView(-Mathf.Asin(aim.normalized.y) * Mathf.Rad2Deg);

            melee.ignoreCursorLock = true;
            melee.suppressed = false;
            melee.attackCooldown = .05f;
            melee.attackDamage = 30f;
            AssertSynchronousMeleeHit(melee, enemy);

            enemy.enabled = false;
            RestoreMelee(melee, previousIgnoreCursor, previousSuppressed, previousCooldown, previousDamage, previousAttackTime);
            body.enabled = false;
            player.transform.SetPositionAndRotation(originalPosition, originalRotation);
            body.enabled = true;
            Time.timeScale = originalHeight;
            Physics.SyncTransforms();
        }

        private void AssertSynchronousMeleeHit(MeleeAttacker melee, EnemyAI enemy)
        {
            float before = enemy.health;
            bool accepted = melee.TryAttack();
            Check(accepted, "melee attack is accepted when the cooldown is clear");
            Check(Mathf.Approximately(enemy.health, before - melee.attackDamage),
                $"melee hit damage is synchronous with TryAttack (health {before:F0} to {enemy.health:F0})");
        }

        private static void RestoreMelee(MeleeAttacker melee, bool ignoreCursor, bool suppressed,
            float cooldown, float damage, float lastAttackTime)
        {
            if (melee == null) return;
            melee.ignoreCursorLock = ignoreCursor;
            melee.suppressed = suppressed;
            melee.attackCooldown = cooldown;
            melee.attackDamage = damage;
            typeof(MeleeAttacker).GetField("lastAttackTime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(melee, lastAttackTime);
        }

        private void CheckCameraSuppression(CameraFeel feel)
        {
            if (feel == null || player.cameraPivot == null || body == null)
            {
                Fail("CameraFeel, camera pivot or CharacterController is missing");
                return;
            }

            CameraFeel.Enabled = true;
            CameraFeel.Intensity = 1f;
            VehicleMountView mount = VehicleMountView.For(player);
            SetField(feel, "mountView", mount);
            MethodInfo update = typeof(CameraFeel).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo lateUpdate = typeof(CameraFeel).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (update == null || lateUpdate == null) { Fail("CameraFeel update hooks are missing"); return; }

            Vector3 cleanPosition = player.cameraPivot.localPosition;
            Quaternion cleanRotation = player.cameraPivot.localRotation;
            SetField(feel, "dipTime", .0625f);
            SetField(feel, "dipDepth", .1f);
            lateUpdate.Invoke(feel, null);
            float positiveControl = Vector3.Distance(cleanPosition, player.cameraPivot.localPosition);
            Check(positiveControl > .05f, $"camera feel positive control applies visible motion ({positiveControl:F3} m)");
            update.Invoke(feel, null);
            Check(Vector3.Distance(cleanPosition, player.cameraPivot.localPosition) < .001f &&
                  Quaternion.Angle(cleanRotation, player.cameraPivot.localRotation) < .01f,
                "camera feel removes its offset before the controller's next pose");

            SetField(feel, "dipTime", .0625f);
            Time.timeScale = 0f;
            lateUpdate.Invoke(feel, null);
            Check(Vector3.Distance(cleanPosition, player.cameraPivot.localPosition) < .001f &&
                  Quaternion.Angle(cleanRotation, player.cameraPivot.localRotation) < .01f,
                "camera feel emits zero while paused");
            Time.timeScale = 1f;

            SetField(mount, "mounted", true);
            SetField(feel, "dipTime", .0625f);
            lateUpdate.Invoke(feel, null);
            Check(Vector3.Distance(cleanPosition, player.cameraPivot.localPosition) < .001f &&
                  Quaternion.Angle(cleanRotation, player.cameraPivot.localRotation) < .01f,
                "camera feel emits zero while mounted");
            SetField(mount, "mounted", false);
            update.Invoke(feel, null);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }

        private void RestorePlayer()
        {
            if (raider != null) Destroy(raider);
            if (platform != null) Destroy(platform);
            if (testDoor != null) Destroy(testDoor);
            if (player != null && body != null)
            {
                body.enabled = false;
                player.transform.SetPositionAndRotation(savedPosition, savedRotation);
                body.enabled = true;
            }
            Time.timeScale = savedTimeScale;
            CameraFeel.Enabled = savedFeelEnabled;
            CameraFeel.Intensity = savedFeelIntensity;
            Physics.SyncTransforms();
        }

        private void Check(bool condition, string message)
        {
            Debug.Log($"[AnimationProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }

        private void Fail(string message)
        {
            failures++;
            Debug.LogError("[AnimationProbe] FAIL: " + message);
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[AnimationProbe] ALL CHECKS PASSED" : $"[AnimationProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
