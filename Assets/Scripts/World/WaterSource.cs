using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.World
{
    /// <summary>
    /// Lets the player fill an Empty Bottle with Dirty Water at any stream or lake.
    /// The water meshes have no collider (the player wades through them), so the interact ray lands on the terrain
    /// under the water. This system casts the same ray first, and when it ends in water it puts one tiny invisible
    /// collider just in front of that point, which PlayerInteractor then hits like any other interactable.
    /// The collider is on the Ignore Raycast layer, which collides with nothing, and default-mask raycasts skip it.
    /// Runs before PlayerInteractor, creates itself after every scene load, no scene edit.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public sealed class WaterSourceSystem : MonoBehaviour
    {
        public const string BottleId = "empty_bottle", DirtyWaterId = "dirty_water";
        private const int TargetLayer = 2; // Ignore Raycast
        private const float TargetSize = 0.06f;
        private static readonly Vector3 Parked = new Vector3(0f, -10000f, 0f);

        private static WaterSourceSystem instance;
        private static bool hooked;

        private PlayerInteractor interactor;
        private BuildModeController build;
        private Collider playerBody;
        private WaterSourceTarget target;
        private BoxCollider targetCollider;
        private float nextLookup;

        /// <summary>True while the player's interact ray ends in water (for probes and HUD hints).</summary>
        public static bool LookingAtWater => instance != null && instance.target != null && instance.target.Active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (instance == null) new GameObject("WaterSourceSystem").AddComponent<WaterSourceSystem>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            // Nothing else in the project uses this layer: make it a pure query target that never touches a body.
            for (int layer = 0; layer < 32; layer++) Physics.IgnoreLayerCollision(TargetLayer, layer, true);

            var go = new GameObject("WaterSourceTarget");
            go.transform.SetParent(transform, false);
            go.layer = TargetLayer;
            targetCollider = go.AddComponent<BoxCollider>();
            targetCollider.size = Vector3.one * TargetSize;
            target = go.AddComponent<WaterSourceTarget>();
            go.transform.position = Parked;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return; // PlayerInteractor doesn't refresh while paused either
            if ((interactor == null || build == null) && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 1f;
                if (build == null) build = FindFirstObjectByType<BuildModeController>();
                if (interactor == null)
                {
                    interactor = FindFirstObjectByType<PlayerInteractor>();
                    target.Interactor = interactor;
                    playerBody = interactor != null ? interactor.GetComponentInParent<CharacterController>() : null;
                }
            }
            if (TryFindWater(out Vector3 point)) Place(point);
            else Park();
        }

        private bool TryFindWater(out Vector3 point)
        {
            point = default;
            if (interactor == null || !interactor.isActiveAndEnabled) return false;
            if (build != null && build.IsBuildModeActive) return false;
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) return false;
            Transform eye = interactor.transform;
            Vector3 origin = eye.position, direction = eye.forward;
            if (WorldLayout.UndergroundAmount(origin) > 0.5f) return false;

            int mask = interactor.interactableMask & ~(1 << TargetLayer);
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, interactor.interactRange, mask, QueryTriggerInteraction.Ignore))
                return false;
            // Something real is in the way: a crate, a door or a node standing in the shallows wins.
            if (hit.collider.GetComponentInParent<IInteractable>() != null) return false;
            if (!WorldLayout.IsWater(hit.point.x, hit.point.z, world.Seed)) return false;
            float surface = WorldLayout.WaterSurfaceAt(hit.point.x, hit.point.z, world.Seed);
            // Only the river bed itself, not a rock or a car roof that happens to stand in the water.
            if (float.IsNaN(surface) || hit.point.y > surface + 0.05f || hit.point.y < surface - 1f) return false;
            point = hit.point - direction * (TargetSize + 0.02f);
            return true;
        }

        private void Place(Vector3 point)
        {
            target.Active = true;
            Transform t = target.transform;
            if ((t.position - point).sqrMagnitude < 0.0001f) return;
            t.position = point;
            if (playerBody != null) Physics.IgnoreCollision(targetCollider, playerBody, true);
            // Auto-sync is off in this project: without this the interact ray this frame would still see the old spot.
            Physics.SyncTransforms();
        }

        private void Park()
        {
            if (target == null) return;
            target.Active = false;
            if (target.transform.position == Parked) return;
            target.transform.position = Parked;
            Physics.SyncTransforms();
        }
    }

    /// <summary>The invisible interactable WaterSourceSystem moves onto the water the player is looking at.</summary>
    public sealed class WaterSourceTarget : MonoBehaviour, IInteractable
    {
        public bool Active { get; set; }
        public PlayerInteractor Interactor { get; set; }

        private Inventory Inventory => Interactor != null ? Interactor.GetComponentInParent<Inventory>() : null;

        public string InteractPrompt
        {
            get
            {
                if (!Active) return string.Empty;
                Inventory inventory = Inventory;
                return inventory != null && inventory.GetCount(WaterSourceSystem.BottleId) > 0
                    ? "Press E to fill a container with Dirty Water"
                    : "Water. You need an Empty Bottle to fill it.";
            }
        }

        public void Interact(GameObject interactor)
        {
            if (!Active || interactor == null || Time.timeScale <= 0f) return;
            SurvivalStats stats = interactor.GetComponentInParent<SurvivalStats>();
            if (stats != null && stats.IsDead) return;
            Inventory inventory = interactor.GetComponentInParent<Inventory>();
            if (inventory == null) return;
            if (!inventory.TryConsume(WaterSourceSystem.BottleId, 1))
            {
                GameAudio.Play2D(Sfx.UiDeny, 0.35f);
                GameMessageBus.Post("You need an Empty Bottle to collect water. Craft one from Glass.");
                return;
            }
            inventory.Add(WaterSourceSystem.DirtyWaterId, 1);
            GameAudio.PlayAt(Sfx.FootWater, transform.position, 0.8f, 12f);
            int left = inventory.GetCount(WaterSourceSystem.BottleId);
            GameMessageBus.Post(left > 0
                ? $"Filled a bottle with Dirty Water ({left} empty left). Boil or filter it before drinking."
                : "Filled a bottle with Dirty Water. Boil or filter it before drinking.");
        }
    }
}
