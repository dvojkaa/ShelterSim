using SheltreSim.Crafting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SheltreSim.Combat
{
    /// <summary>
    /// The gun in view. Uses Resources/Weapons/&lt;id&gt; when that asset exists, otherwise boxes, so a weapon is never invisible.
    /// A model follows the player_weapon convention: grip at its origin, barrel along Blender Z, which imports as Unity +Y.
    /// M8/M16 turn that prop forward with a 90 degree X rotation on the parent, and the same is done here.
    /// </summary>
    public sealed class RangedViewmodel
    {
        private static readonly Vector3 RestPosition = new Vector3(0.24f, -0.21f, 0.46f);
        private static readonly Quaternion ModelForward = Quaternion.Euler(90f, 0f, 0f);
        private const float NearClipMargin = 0.03f;

        private readonly Transform holder;
        private readonly Camera camera;
        private GameObject instance;
        private string shownId = "";
        private Vector3 muzzleLocal = Vector3.forward * 0.3f;
        private Vector3 restPosition = RestPosition;
        private float kick, reloadDip;

        public Transform Root => holder;
        public string ShownId => shownId;
        /// <summary>Closest point of the model to the camera along its view axis, at rest.</summary>
        public float NearestViewDepth { get; private set; }
        public Vector3 MuzzleWorld => holder.TransformPoint(muzzleLocal);

        public RangedViewmodel(Transform cameraPivot, Camera camera)
        {
            this.camera = camera;
            holder = new GameObject("RangedViewmodel").transform;
            holder.SetParent(cameraPivot, false);
            holder.localPosition = RestPosition;
            holder.gameObject.SetActive(false);
        }

        public void Show(string id)
        {
            bool visible = !string.IsNullOrEmpty(id);
            if (visible && id != shownId) Build(id);
            holder.gameObject.SetActive(visible);
        }

        public void AddKick(float amount) => kick = Mathf.Min(1.3f, kick + amount);

        public void Tick(float deltaTime, bool reloading)
        {
            if (deltaTime <= 0f) return;
            kick = Mathf.Lerp(kick, 0f, 1f - Mathf.Exp(-11f * deltaTime));
            reloadDip = Mathf.MoveTowards(reloadDip, reloading ? 1f : 0f, deltaTime * 5f);
            holder.localPosition = restPosition + new Vector3(0f, kick * 0.008f - reloadDip * 0.05f, -kick * 0.035f);
            holder.localRotation = Quaternion.Euler(-kick * 5f + reloadDip * 28f, 0f, 0f);
        }

        private void Build(string id)
        {
            if (instance != null)
            {
                instance.SetActive(false);
                Object.Destroy(instance);
            }
            shownId = id;
            holder.localPosition = RestPosition;
            holder.localRotation = Quaternion.identity;

            GameObject asset = Resources.Load<GameObject>("Weapons/" + id);
            if (asset != null)
            {
                instance = Object.Instantiate(asset, holder, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = ModelForward;
            }
            else
            {
                instance = BuildFallback(id);
            }
            instance.name = "Model_" + id;

            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false; // Destroy is deferred to the end of the frame.
                Object.Destroy(collider);
            }
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            Measure();
        }

        /// <summary>Finds the muzzle from the model's real extents, and keeps the stock from crossing the camera's near plane.</summary>
        private void Measure()
        {
            bool any = false;
            Bounds local = new Bounds();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = holder.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!any) { local = new Bounds(point, Vector3.zero); any = true; }
                    else local.Encapsulate(point);
                }
            }
            if (!any) local = new Bounds(Vector3.forward * 0.15f, Vector3.one * 0.3f);
            muzzleLocal = new Vector3(local.center.x, local.center.y, local.max.z);

            float near = camera != null ? camera.nearClipPlane : 0.3f;
            restPosition = RestPosition;
            float depth = restPosition.z + local.min.z;
            if (depth < near + NearClipMargin)
            {
                restPosition.z += near + NearClipMargin - depth;
                depth = near + NearClipMargin;
            }
            NearestViewDepth = depth;
            holder.localPosition = restPosition;
        }

        private GameObject BuildFallback(string id)
        {
            var model = new GameObject("Model");
            model.transform.SetParent(holder, false);
            ItemSpec spec = ItemCatalog.Spec(id);
            bool energy = spec != null && spec.energy;
            Color body = energy ? new Color(0.72f, 0.74f, 0.78f) : new Color(0.16f, 0.16f, 0.17f);
            Color detail = energy ? (spec.splashRadius > 0f ? new Color(0.3f, 0.95f, 0.45f) : new Color(0.9f, 0.25f, 0.2f)) : new Color(0.3f, 0.21f, 0.14f);
            Vector3 size = RangedWeaponProfile.FallbackSize(id);
            Box(model.transform, new Vector3(0f, 0f, size.z * 0.35f), new Vector3(size.x, size.y, size.z * 0.8f), body);
            Box(model.transform, new Vector3(0f, size.y * 0.15f, size.z * 0.85f), new Vector3(size.x * 0.5f, size.y * 0.45f, size.z * 0.3f), body * 0.75f);
            Box(model.transform, new Vector3(0f, -size.y * 1.1f, -0.005f), new Vector3(size.x * 0.8f, size.y * 1.6f, 0.04f), detail);
            if (size.z > 0.4f) Box(model.transform, new Vector3(0f, -size.y * 0.3f, -0.1f), new Vector3(size.x * 0.8f, size.y * 1.1f, 0.2f), detail);
            return model;
        }

        private static void Box(Transform parent, Vector3 position, Vector3 scale, Color color)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().material.color = color;
        }
    }
}
