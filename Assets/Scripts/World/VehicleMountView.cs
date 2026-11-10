using System.Collections;
using System.Collections.Generic;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Moves the first-person camera to a vehicle's authored seat anchor and safely back to an exit.</summary>
    public sealed class VehicleMountView : MonoBehaviour
    {
        private const float TransitionSeconds = 0.6f;
        private static readonly string[] ExitNames = { "Exit_L", "Exit_R" };
        private readonly Collider[] overlaps = new Collider[48];
        private Collider[] vehicleColliders;
        private bool[] vehicleColliderStates;
        private FirstPersonController rider;
        private Transform vehicle, seat, originalCameraParent, cameraPivot;
        private Transform fallbackSeat;
        private Vector3 originalCameraLocalPosition;
        private bool mounted;
        private Coroutine transition;

        public bool IsTransitioning { get; private set; }
        public bool IsBusy => mounted || IsTransitioning;
        public bool IsAttachedTo(Transform vehicleRoot) => vehicleRoot != null && vehicle == vehicleRoot && (mounted || IsTransitioning);

        public static VehicleMountView For(FirstPersonController player)
        {
            if (player == null) return null;
            VehicleMountView view = player.GetComponent<VehicleMountView>();
            return view != null ? view : player.gameObject.AddComponent<VehicleMountView>();
        }

        public bool BeginMount(Transform vehicleRoot)
        {
            if (mounted || IsTransitioning || vehicleRoot == null || rider == null) return false;
            vehicle = vehicleRoot;
            vehicleColliders = vehicle.GetComponentsInChildren<Collider>(true);
            vehicleColliderStates = new bool[vehicleColliders.Length];
            for (int i = 0; i < vehicleColliders.Length; i++) vehicleColliderStates[i] = vehicleColliders[i] != null && vehicleColliders[i].enabled;
            cameraPivot = rider.cameraPivot;
            if (cameraPivot == null) return false;
            originalCameraParent = cameraPivot.parent;
            originalCameraLocalPosition = cameraPivot.localPosition;
            seat = FindAnchor(vehicle, "Seat");
            if (seat == null)
            {
                var fallback = new GameObject("RuntimeSeatFallback");
                fallback.transform.SetParent(vehicle, false);
                fallback.transform.localPosition = new Vector3(0f, cameraPivot.position.y - vehicle.position.y, 0f);
                fallbackSeat = seat = fallback.transform;
            }
            mounted = true;
            rider.SetVehicleTransition(true);
            IsTransitioning = true;
            transition = StartCoroutine(MoveCameraToSeat());
            return true;
        }

        public bool BeginDismount(Transform vehicleRoot)
        {
            if (!mounted || IsTransitioning || rider == null || cameraPivot == null) return false;
            vehicle = vehicleRoot != null ? vehicleRoot : vehicle;
            if (!TryChooseExit(vehicle, out Vector3 exitPosition, out Quaternion exitRotation)) return false;

            Vector3 cameraWorldPosition = cameraPivot.position;
            Quaternion cameraWorldRotation = cameraPivot.rotation;
            CharacterController controller = rider.GetComponent<CharacterController>();
            bool enabledBefore = controller != null && controller.enabled;
            if (enabledBefore) controller.enabled = false;
            rider.transform.SetPositionAndRotation(exitPosition, exitRotation);
            if (enabledBefore) controller.enabled = true;
            cameraPivot.SetPositionAndRotation(cameraWorldPosition, cameraWorldRotation);

            mounted = false;
            rider.SetVehicleTransition(true);
            IsTransitioning = true;
            transition = StartCoroutine(ReturnCameraToPlayer());
            return true;
        }

        /// <summary>Used when the vehicle is disabled or unloaded before a normal exit animation can run.</summary>
        public void ForceDismount(Transform vehicleOwner = null)
        {
            if (vehicleOwner != null && vehicle != vehicleOwner) return;
            if (transition != null) StopCoroutine(transition);
            transition = null;
            if (cameraPivot != null && originalCameraParent != null)
            {
                cameraPivot.SetParent(originalCameraParent, false);
                cameraPivot.localPosition = originalCameraLocalPosition;
                cameraPivot.localRotation = Quaternion.Euler(rider != null ? rider.CameraPitch : 0f, 0f, 0f);
            }
            if (fallbackSeat != null) Destroy(fallbackSeat.gameObject);
            fallbackSeat = null;
            seat = vehicle = null;
            vehicleColliders = null;
            vehicleColliderStates = null;
            mounted = false;
            IsTransitioning = false;
            if (rider != null) rider.SetVehicleTransition(false);
        }

        private void Awake()
        {
            rider = GetComponent<FirstPersonController>();
        }

        private IEnumerator MoveCameraToSeat()
        {
            Vector3 startPosition = cameraPivot.position;
            Quaternion startRotation = cameraPivot.rotation;
            float pitch = rider.CameraPitch;
            float elapsed = 0f;
            while (elapsed < TransitionSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Ease(elapsed / TransitionSeconds);
                cameraPivot.position = Vector3.Lerp(startPosition, seat.position, t);
                cameraPivot.rotation = Quaternion.Slerp(startRotation, seat.rotation * Quaternion.Euler(pitch, 0f, 0f), t);
                yield return null;
            }
            cameraPivot.SetPositionAndRotation(seat.position, seat.rotation * Quaternion.Euler(pitch, 0f, 0f));
            IsTransitioning = false;
            rider.SetVehicleTransition(false);
            transition = null;
        }

        private IEnumerator ReturnCameraToPlayer()
        {
            Vector3 startPosition = cameraPivot.position;
            Quaternion startRotation = cameraPivot.rotation;
            float elapsed = 0f;
            while (elapsed < TransitionSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Ease(elapsed / TransitionSeconds);
                cameraPivot.position = Vector3.Lerp(startPosition, rider.transform.TransformPoint(originalCameraLocalPosition), t);
                cameraPivot.rotation = Quaternion.Slerp(startRotation,
                    rider.transform.rotation * Quaternion.Euler(rider.CameraPitch, 0f, 0f), t);
                yield return null;
            }
            cameraPivot.localPosition = originalCameraLocalPosition;
            cameraPivot.localRotation = Quaternion.Euler(rider.CameraPitch, 0f, 0f);
            if (fallbackSeat != null) Destroy(fallbackSeat.gameObject);
            fallbackSeat = null;
            seat = null;
            vehicle = null;
            IsTransitioning = false;
            rider.SetVehicleTransition(false);
            transition = null;
        }

        private void LateUpdate()
        {
            if (!mounted || IsTransitioning || seat == null || cameraPivot == null) return;
            cameraPivot.SetPositionAndRotation(seat.position, seat.rotation * Quaternion.Euler(rider.CameraPitch, 0f, 0f));
        }

        private bool TryChooseExit(Transform root, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (root == null) return false;
            var statesBeforeCheck = new bool[vehicleColliders != null ? vehicleColliders.Length : 0];
            for (int i = 0; i < statesBeforeCheck.Length; i++)
            {
                Collider collider = vehicleColliders[i];
                statesBeforeCheck[i] = collider != null && collider.enabled;
                if (collider != null && vehicleColliderStates[i]) collider.enabled = true;
            }
            Physics.SyncTransforms();
            try
            {
                foreach (string name in ExitNames)
                {
                    Transform marker = FindAnchor(root, name);
                    if (marker != null && TryRaiseToClearPose(marker.position, marker.rotation, out Vector3 clearMarker))
                    {
                        position = clearMarker;
                        rotation = Yaw(marker.rotation);
                        return true;
                    }
                }

                float extent = 1.6f;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                    extent = Mathf.Max(extent, renderer.bounds.extents.x, renderer.bounds.extents.z);
                float distance = extent + GetComponent<CharacterController>().radius + 0.25f;
                Vector3[] candidates =
                {
                    root.position + root.right * distance,
                    root.position - root.right * distance,
                    root.position - root.forward * distance,
                    root.position + root.forward * distance
                };
                foreach (Vector3 candidate in candidates)
                {
                    if (!TryProjectToSurface(candidate, root, out Vector3 groundPosition) ||
                        !TryRaiseToClearPose(groundPosition, root.rotation, out Vector3 clearPosition)) continue;
                    position = clearPosition;
                    rotation = Yaw(root.rotation);
                    return true;
                }
                return false;
            }
            finally
            {
                for (int i = 0; i < statesBeforeCheck.Length; i++)
                    if (vehicleColliders[i] != null) vehicleColliders[i].enabled = statesBeforeCheck[i];
                Physics.SyncTransforms();
            }
        }

        private bool TryRaiseToClearPose(Vector3 footPosition, Quaternion rootRotation, out Vector3 clearPosition)
        {
            for (float lift = 0.04f; lift <= 0.64f; lift += 0.04f)
            {
                Vector3 candidate = footPosition + Vector3.up * lift;
                if (!CapsuleClear(candidate, rootRotation)) continue;
                clearPosition = candidate;
                return true;
            }
            clearPosition = default;
            return false;
        }

        private bool TryProjectToSurface(Vector3 candidate, Transform vehicleRoot, out Vector3 groundPosition)
        {
            groundPosition = candidate;
            RaycastHit[] hits = Physics.RaycastAll(candidate + Vector3.up * 3f, Vector3.down, 12f,
                ~0, QueryTriggerInteraction.Ignore);
            float bestVerticalDelta = float.PositiveInfinity;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].collider != null ? hits[i].collider.transform : null;
                if (hitTransform == null || hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
                if (vehicleRoot != null && (hitTransform == vehicleRoot || hitTransform.IsChildOf(vehicleRoot))) continue;
                float delta = Mathf.Abs(hits[i].point.y - candidate.y);
                if (delta >= bestVerticalDelta) continue;
                bestVerticalDelta = delta;
                groundPosition.y = hits[i].point.y;
            }
            return !float.IsPositiveInfinity(bestVerticalDelta);
        }

        private bool CapsuleClear(Vector3 rootPosition, Quaternion rootRotation)
        {
            CharacterController controller = GetComponent<CharacterController>();
            if (controller == null) return false;
            float radius = controller.radius + controller.skinWidth;
            float height = Mathf.Max(controller.height, radius * 2f);
            Vector3 up = rootRotation * Vector3.up;
            Vector3 center = rootPosition + rootRotation * controller.center;
            Vector3 bottom = center - up * (height * 0.5f - radius);
            Vector3 top = center + up * (height * 0.5f - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider hit = overlaps[i];
                if (hit == null || hit == controller || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                return false;
            }
            return true;
        }

        private static Transform FindAnchor(Transform root, string anchorName)
        {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != root && child.name == anchorName) return child;
            return null;
        }

        private static Quaternion Yaw(Quaternion rotation) => Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
        private static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
