using System;
using SheltreSim.Interaction;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Deterministic door leaves for generated building openings.</summary>
    internal static class BuildingDoors
    {
        public static void Place(Transform parent, string openingName, bool alongZ, float axis, float center,
            float floorY, float width, float wallThickness, string modelId, int seed, bool ruined)
        {
            if (parent == null || width <= 0f) return;
            Vector3 world = parent.TransformPoint(alongZ ? new Vector3(axis, floorY, center) : new Vector3(center, floorY, axis));
            seed = StableHash("lot", seed, world.x, world.z, 0f);
            int hash = StableHash(openingName, seed, axis, center, floorY);
            // The same draw in the normal and the ruined pass (it only decides what the ruin looks like): intact, swung open, or gone.
            int ruinRoll = Mathf.Abs(hash % 100);
            if (ruined && ruinRoll < 14) return;
            bool open = ruined && ruinRoll >= 56 && ruinRoll < 86; // entrances; room doors use `ruined` directly (open once ruined)
            bool entrance = modelId == "bld_door_house_front" || modelId == "bld_door_apt_entrance" || modelId == "bld_door_shop_glass" || modelId == "bld_door_office";
            modelId = StyleVariant(modelId, hash);
            GameObject model = Resources.Load<GameObject>("Props/" + modelId);

            Transform group = DoorsGroup(parent);
            Quaternion baseRotation = parent.rotation * Quaternion.Euler(0f, alongZ ? 270f : 0f, 0f);
            float half = width * .5f;
            Vector3 lowEdge = parent.TransformPoint(alongZ ? new Vector3(axis, floorY, center - half) : new Vector3(center - half, floorY, axis));
            Vector3 highEdge = parent.TransformPoint(alongZ ? new Vector3(axis, floorY, center + half) : new Vector3(center + half, floorY, axis));
            if (!entrance)
            {
                // Doors between rooms are dressing: closed before the bombs, afterwards wrecked open or gone, never a solid obstacle
                // (raiders and the stairs do not route around closed room doors).
                Decor(group, openingName, model, lowEdge, baseRotation, width, ruined);
                return;
            }
            if (width > 2.05f)
            {
                // A wide entrance: two leaves, each hinged at its own jamb and meeting in the middle.
                Leaf(group, openingName + "_A", model, lowEdge, baseRotation, half, open);
                Leaf(group, openingName + "_B", model, highEdge, baseRotation * Quaternion.Euler(0f, 180f, 0f), half, open);
            }
            else Leaf(group, openingName, model, lowEdge, baseRotation, width, open);
        }

        /// <summary>A room door without collision: hinge, a "Leaf" that is rotated open, and the model.</summary>
        private static void Decor(Transform group, string name, GameObject model, Vector3 hingePosition, Quaternion hingeRotation, float width, bool open)
        {
            if (model == null) return;
            var hinge = new GameObject("Door_" + name);
            hinge.transform.SetParent(group, false);
            hinge.transform.SetPositionAndRotation(hingePosition, hingeRotation);
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(hinge.transform, false);
            if (open) leaf.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            GameObject mesh = UnityEngine.Object.Instantiate(model, leaf.transform);
            mesh.name = "Model";
            mesh.transform.localPosition = Vector3.right * (width * .5f);
            mesh.transform.localScale = new Vector3(width / 1.8f, 1f, 1f);
        }

        /// <summary>
        /// One hinged leaf: a hinge at the jamb, a swinging child with a collider and an InteractableDoor (so the player opens it with E
        /// and raiders force it open), and the model (or a plain slab if the model is missing) hanging in the child. The leaf swings
        /// toward its front (-Z of the hinge), which the caller chooses by the hinge rotation.
        /// </summary>
        private static void Leaf(Transform group, string name, GameObject model, Vector3 hingePosition, Quaternion hingeRotation,
            float width, bool open)
        {
            const float height = 2.2f;
            var hinge = new GameObject("Door_" + name);
            hinge.transform.SetParent(group, false);
            hinge.transform.SetPositionAndRotation(hingePosition, hingeRotation);

            var door = new GameObject("Leaf");
            door.SetActive(false); // So InteractableDoor.Awake runs after doorVisual is set (it reads the hinge rotation there).
            door.transform.SetParent(hinge.transform, false);
            door.transform.localPosition = new Vector3(width * .5f, height * .5f, 0f);
            var collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, .1f);
            var interactable = door.AddComponent<InteractableDoor>();
            interactable.doorVisual = hinge.transform;
            interactable.maxRaidHealth = 150f;

            if (model != null)
            {
                GameObject mesh = UnityEngine.Object.Instantiate(model, door.transform);
                mesh.name = "Model";
                mesh.transform.localPosition = new Vector3(0f, -height * .5f, 0f);
                mesh.transform.localScale = new Vector3(width / 1.8f, 1f, 1f);
            }
            else
            {
                // Plain slab so a missing model still gives a working door.
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.Destroy(slab.GetComponent<Collider>());
                slab.name = "Model";
                slab.transform.SetParent(door.transform, false);
                slab.transform.localScale = new Vector3(width, height, .06f);
            }
            door.SetActive(true);
            if (open) interactable.RestoreState(true, false);
        }

        /// <summary>The building's own "Doors" group, outside the Static group that gets combined into one mesh (doors move).</summary>
        private static Transform DoorsGroup(Transform parent)
        {
            Transform root = parent;
            for (Transform t = parent; t != null; t = t.parent)
            {
                if (t.name.StartsWith("Building_", StringComparison.Ordinal) || t.name.StartsWith("House_", StringComparison.Ordinal)) { root = t; break; }
            }
            Transform group = root.Find("Doors");
            if (group != null) return group;
            var created = new GameObject("Doors");
            created.transform.SetParent(root, false);
            return created.transform;
        }

        public static string InteriorModel(string openingName)
        {
            if (openingName.IndexOf("Bath", StringComparison.OrdinalIgnoreCase) >= 0) return "bld_door_bath";
            if (openingName.IndexOf("Bed", StringComparison.OrdinalIgnoreCase) >= 0) return "bld_door_bedroom";
            if (openingName.IndexOf("Flat", StringComparison.OrdinalIgnoreCase) >= 0) return "bld_door_flat";
            return "bld_door_interior_wood";
        }

        private static string StyleVariant(string modelId, int hash)
        {
            string[] options = modelId switch
            {
                "bld_door_interior_wood" => new[] { "bld_door_interior_wood", "bld_door_flat", "bld_door_bedroom" },
                "bld_door_flat" => new[] { "bld_door_flat", "bld_door_interior_wood" },
                "bld_door_bedroom" => new[] { "bld_door_bedroom", "bld_door_interior_wood" },
                "bld_door_bath" => new[] { "bld_door_bath", "bld_door_interior_wood" },
                _ => null
            };
            return options == null ? modelId : options[Mathf.Abs(hash % options.Length)];
        }

        private static int StableHash(string name, int seed, float axis, float center, float floorY)
        {
            unchecked
            {
                int hash = seed;
                for (int i = 0; i < name.Length; i++) hash = hash * 31 + name[i];
                hash = hash * 31 + Mathf.RoundToInt(axis * 10f);
                hash = hash * 31 + Mathf.RoundToInt(center * 10f);
                hash = hash * 31 + Mathf.RoundToInt(floorY * 10f);
                return hash;
            }
        }
    }
}
