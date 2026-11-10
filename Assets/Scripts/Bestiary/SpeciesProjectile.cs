using UnityEngine;
using SheltreSim.Survival;

namespace SheltreSim.Bestiary
{
    /// <summary>Small visible acid bolt. A sphere cast makes world geometry and the player block it.</summary>
    public sealed class SpeciesProjectile : MonoBehaviour
    {
        private SpeciesBrain owner;
        private Transform source;
        private Vector3 direction;
        private float speed, damageScale, life = 3f;
        private float radius = 0.13f;
        private Material boltMaterial;
        private bool spin;

        public static void Launch(SpeciesBrain owner, Vector3 origin, Vector3 target, float speed, float damage)
        {
            if (owner == null || speed <= 0f) return;
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude < 0.01f) return;
            // Dedicated models (ArtSource fx_*): acid glob for the Mirelurker, thrown flask for the Scavenger.
            string modelId = owner is ScavengerBrain ? "fx_chem_flask" : "fx_acid_glob";
            GameObject model = Resources.Load<GameObject>("Props/" + modelId);
            GameObject bolt;
            bool hasModel = model != null;
            if (hasModel)
            {
                bolt = Instantiate(model);
                bolt.name = modelId;
                foreach (Collider c in bolt.GetComponentsInChildren<Collider>()) Destroy(c);
            }
            else
            {
                bolt = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bolt.name = "Mirelurker_AcidBolt";
                Collider collider = bolt.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                bolt.transform.localScale = Vector3.one * 0.24f;
            }
            bolt.transform.position = origin;
            if (hasModel) bolt.transform.rotation = Quaternion.LookRotation(direction.normalized);
            SpeciesProjectile projectile = bolt.AddComponent<SpeciesProjectile>();
            projectile.spin = modelId == "fx_chem_flask";
            projectile.owner = owner;
            projectile.source = owner.transform;
            projectile.direction = direction.normalized;
            projectile.speed = speed;
            projectile.damageScale = damage / Mathf.Max(0.01f, owner.Spec.Damage);
            Shader shader = hasModel ? null : Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                projectile.boltMaterial = new Material(shader);
                projectile.boltMaterial.color = new Color(.55f, .92f, .22f);
                if (projectile.boltMaterial.HasProperty("_EmissionColor"))
                {
                    projectile.boltMaterial.EnableKeyword("_EMISSION");
                    projectile.boltMaterial.SetColor("_EmissionColor", new Color(.35f, .8f, .08f) * 1.6f);
                }
                bolt.GetComponent<Renderer>().sharedMaterial = projectile.boltMaterial;
            }
        }

        private void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }
            float distance = speed * Time.deltaTime;
            RaycastHit[] hits = Physics.SphereCastAll(transform.position, radius, direction, distance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in hits)
            {
                Transform struck = hit.collider.transform;
                if (source != null && (struck == source || struck.IsChildOf(source))) continue;
                SurvivalStats stats = hit.collider.GetComponentInParent<SurvivalStats>();
                if (stats != null && owner != null) owner.ProjectileHit(damageScale);
                Destroy(gameObject);
                return;
            }
            transform.position += direction * distance;
            if (spin) transform.Rotate(540f * Time.deltaTime, 0f, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (boltMaterial != null) Destroy(boltMaterial);
        }
    }
}
