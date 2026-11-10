using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>
    /// Limits interior rendering to the player's current storey and its neighbours. Geometry and
    /// furniture colliders remain live so doors, navigation, projectiles, and save state keep working.
    /// </summary>
    public sealed class BuildingInterior : MonoBehaviour
    {
        private const float StoreyHeight = 3f;
        private const float GroundFloorRevealDistance = 25f;

        private float halfWidth, halfDepth;
        private Transform[] floorRoots = System.Array.Empty<Transform>();
        private Renderer[][] floorRenderers = System.Array.Empty<Renderer[]>();
        private bool[] floorVisible = System.Array.Empty<bool>();
        private Transform player;
        private float nextUpdate;

        public void Configure(float width, float depth, int storeys)
        {
            halfWidth = Mathf.Max(0f, width * .5f);
            halfDepth = Mathf.Max(0f, depth * .5f);
            int count = Mathf.Max(1, storeys);
            floorRoots = new Transform[count];
            floorRenderers = new Renderer[count][];
            floorVisible = new bool[count];
            for (int floor = 0; floor < count; floor++)
            {
                Transform group = transform.Find("Floor_" + floor + "/Interior");
                floorRoots[floor] = group;
                floorRenderers[floor] = group != null ? group.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
                floorVisible[floor] = true;
            }
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .2f;
            if (player == null)
            {
                FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null) return;
                player = controller.transform;
            }
            if (floorRenderers == null || floorRenderers.Length == 0) return;

            Vector3 local = transform.InverseTransformPoint(player.position);
            bool inside = Mathf.Abs(local.x) <= halfWidth + 2f && Mathf.Abs(local.z) <= halfDepth + 2f &&
                          local.y >= -.5f && local.y <= floorRenderers.Length * StoreyHeight + 2f;
            int currentFloor = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, local.y - .1f) / StoreyHeight), 0, floorRenderers.Length - 1);
            bool groundVisible = new Vector2(local.x, local.z).sqrMagnitude <= GroundFloorRevealDistance * GroundFloorRevealDistance;

            for (int floor = 0; floor < floorRenderers.Length; floor++)
            {
                bool visible = (inside && Mathf.Abs(floor - currentFloor) <= 1) || (floor == 0 && groundVisible);
                if (visible == floorVisible[floor]) continue;
                floorVisible[floor] = visible;
                Renderer[] renderers = floorRenderers[floor];
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = visible;
            }
        }
    }
}
