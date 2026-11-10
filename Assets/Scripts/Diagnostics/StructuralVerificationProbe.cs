using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Building;
using SheltreSim.Combat;
using SheltreSim.Crafting;
using SheltreSim.Raids;
using SheltreSim.Saving;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Runtime checks for structural tier prefabs, damage stages, salvage persistence, and support collapse.
    /// Run in an isolated player with --structural-verification-probe.
    /// </summary>
    public sealed class StructuralVerificationProbe : MonoBehaviour
    {
        private static readonly string[] TierIds =
        {
            "piece_foundation_brick", "piece_foundation_reinforced", "piece_foundation_concrete",
            "piece_wall_brick", "piece_wall_reinforced", "piece_wall_concrete"
        };

        private int failures;
        private BuildModeController build;
        private RaidDirector raids;
        private SaveManager save;
        private string savePath;
        private GameObject fixture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--structural-verification-probe") >= 0)
                new GameObject(nameof(StructuralVerificationProbe)).AddComponent<StructuralVerificationProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            yield return null;
            yield return null;
            build = FindFirstObjectByType<BuildModeController>();
            raids = FindFirstObjectByType<RaidDirector>();
            save = FindFirstObjectByType<SaveManager>();
            Check(build != null && raids != null && save != null, "player build definitions, RaidDirector, and SaveManager are available");
            if (build == null || raids == null || save == null) { Finish(); yield break; }

            fixture = new GameObject("StructuralVerificationProbeFixtures");
            Vector3 anchor = (FindFirstObjectByType<SheltreSim.Player.FirstPersonController>()?.transform.position ?? Vector3.zero) + new Vector3(30f, 0f, 18f);
            anchor.y = SheltreSim.World.WorldLayout.HeightAt(anchor.x, anchor.z,
                SheltreSim.World.ProceduralWorld.Active != null ? SheltreSim.World.ProceduralWorld.Active.Seed : 0) + 2f;

            CheckTierDefinitions();
            CheckTierPlacement(anchor + Vector3.right * 40f);
            CheckDamageStages(anchor);
            CheckDamageRules(anchor + Vector3.right * 20f);
            yield return CheckHealthAndSalvageSave(anchor);
            yield return CheckBottomUpCollapse(anchor + Vector3.right * 8f);
            Finish();
        }

        private void CheckTierDefinitions()
        {
            foreach (string id in TierIds)
            {
                BuildPieceDefinition definition = Definition(id);
                PlacedPiece tag = definition != null && definition.prefab != null ? definition.prefab.GetComponent<PlacedPiece>() : null;
                StructureHealth health = definition != null && definition.prefab != null ? definition.prefab.GetComponent<StructureHealth>() : null;
                HomesteadEntry entry = HomesteadCatalog.Find(id);
                Check(definition != null && tag != null && tag.pieceId == id && health != null && entry != null,
                    id + " has a build definition, tagged prefab, health, and structural catalog entry");
                if (entry != null && health != null)
                    Check(Mathf.Abs(health.maxHealth - entry.hitPoints) < .01f,
                        id + " prefab health matches its tier hit points (" + health.maxHealth + "/" + entry.hitPoints + ")");
            }
            Check(HomesteadCatalog.Find("piece_wall_brick").hitPoints < HomesteadCatalog.Find("piece_wall_reinforced").hitPoints &&
                  HomesteadCatalog.Find("piece_wall_reinforced").hitPoints < HomesteadCatalog.Find("piece_wall_concrete").hitPoints,
                "brick, reinforced, and concrete wall hit points increase by tier");
        }

        private void CheckDamageStages(Vector3 anchor)
        {
            Check(StructureDamageVisual.StageFor(.65f) == 0 && StructureDamageVisual.StageFor(.649f) == 1 &&
                  StructureDamageVisual.StageFor(.3f) == 1 && StructureDamageVisual.StageFor(.299f) == 2 &&
                  StructureDamageVisual.StageFor(0f) == 2,
                "damage thresholds keep intact at 65%, damaged below 65%, and critical below 30%");

            string[] ids = { "piece_wall_brick", "piece_wall_reinforced", "piece_wall_concrete" };
            for (int i = 0; i < ids.Length; i++)
            {
                BuildPieceDefinition definition = Definition(ids[i]);
                GameObject instance = definition != null && definition.prefab != null
                    ? Instantiate(definition.prefab, anchor + Vector3.right * (i * 3f), Quaternion.identity, fixture.transform) : null;
                StructureHealth health = instance != null ? instance.GetComponent<StructureHealth>() : null;
                StructureDamageVisual visual = instance != null ? instance.GetComponent<StructureDamageVisual>() : null;
                Check(instance != null && health != null && visual != null, ids[i] + " prefab wires health to a damage visual");
                if (health == null || visual == null) continue;
                health.RestoreHealth(health.maxHealth * .5f);
                Check(visual.Stage == 1, ids[i] + " enters its damaged visual stage at half health");
                health.RestoreHealth(health.maxHealth * .2f);
                Check(visual.Stage == 2, ids[i] + " enters its critical visual stage at 20% health");
                health.Repair(health.maxHealth);
                Check(visual.Stage == 0, ids[i] + " returns to intact visual after repair");
            }
        }

        private void CheckDamageRules(Vector3 anchor)
        {
            Check(Mathf.Abs(StructureDamageRules.ToolDamage("steel_axe", HomesteadMaterial.Wood) - 52f) < .01f &&
                  StructureDamageRules.ToolDamage("steel_axe", HomesteadMaterial.Wood) > StructureDamageRules.ToolDamage("steel_axe", HomesteadMaterial.Stone) &&
                  StructureDamageRules.ToolDamage("steel_pickaxe", HomesteadMaterial.Stone) > StructureDamageRules.ToolDamage("steel_pickaxe", HomesteadMaterial.Wood) &&
                  StructureDamageRules.ToolDamage("unknown", HomesteadMaterial.Wood) == 0f,
                "axes favor wood, pickaxes favor stone, and unsupported or bare-hand tool ids deal no structure damage");
            Check(Mathf.Abs(StructureDamageRules.FirearmMultiplier(HomesteadMaterial.Wood) - .18f) < .001f &&
                  Mathf.Abs(StructureDamageRules.FirearmMultiplier(HomesteadMaterial.Stone) - .08f) < .001f &&
                  Mathf.Abs(StructureDamageRules.FirearmMultiplier(HomesteadMaterial.Metal) - .04f) < .001f,
                "firearms use the intended wood, stone and metal multipliers");

            string[] ids = { "wall_segment", "piece_wall_brick", "piece_wall_steel" };
            float[] expectedDamage = { 18f, 8f, 4f };
            var combat = new RangedCombat(null, null, null);
            bool allDamageCorrect = true;
            for (int i = 0; i < ids.Length; i++)
            {
                BuildPieceDefinition definition = Definition(ids[i]);
                GameObject instance = definition != null && definition.prefab != null
                    ? Instantiate(definition.prefab, anchor + Vector3.right * (i * 3f), Quaternion.identity, fixture.transform) : null;
                PlacedPiece piece = instance != null ? instance.GetComponent<PlacedPiece>() : null;
                StructureHealth health = instance != null ? instance.GetComponent<StructureHealth>() : null;
                Collider collider = instance != null ? instance.GetComponentInChildren<Collider>() : null;
                if (piece == null || health == null || collider == null)
                {
                    allDamageCorrect = false;
                    continue;
                }
                combat.ApplyDamage(collider, 100f, anchor);
                allDamageCorrect &= Mathf.Abs((health.maxHealth - health.Health) - expectedDamage[i]) < .01f;
            }
            Check(allDamageCorrect, "hitscan damage applies 18/8/4 HP to wood, stone and metal placed wall prefabs");
        }

        private void CheckTierPlacement(Vector3 anchor)
        {
            anchor = BuildGrid.SnapPosition(anchor);
            string[] foundations = { "piece_foundation_brick", "piece_foundation_reinforced", "piece_foundation_concrete" };
            string[] walls = { "piece_wall_brick", "piece_wall_reinforced", "piece_wall_concrete" };
            for (int i = 0; i < foundations.Length; i++)
            {
                Vector3 point = anchor + Vector3.right * (i * 6f);
                GameObject foundationObject = Create(foundations[i], point + Vector3.up * .3f);
                PlacedPiece foundation = foundationObject != null ? foundationObject.GetComponent<PlacedPiece>() : null;
                BuildPieceDefinition wall = Definition(walls[i]);
                if (foundation == null || wall == null)
                {
                    Check(false, walls[i] + " can be tested against its matching foundation");
                    continue;
                }

                float top = HomesteadPlacement.TopOf(foundation);
                Ray aim = new Ray(new Vector3(point.x, top + 2f, point.z), Vector3.down);
                HomesteadPose pose = HomesteadPlacement.Evaluate(wall, aim, 4f, 0f, ~0, 0);
                Check(pose.hasPose && pose.valid, walls[i] + " placement preview accepts the matching tier foundation");
                if (!pose.valid) continue;
                GameObject placedWall = Instantiate(wall.prefab, pose.position, pose.rotation, fixture.transform);
                PlacedPiece wallTag = placedWall.GetComponent<PlacedPiece>();
                Check(wallTag != null && HomesteadPlacement.IsSupported(wallTag),
                    walls[i] + " remains supported after placement on its matching foundation");
            }
        }

        private IEnumerator CheckHealthAndSalvageSave(Vector3 anchor)
        {
            string id = "piece_wall_reinforced";
            BuildPieceDefinition definition = Definition(id);
            GameObject pieceObject = definition != null && definition.prefab != null
                ? Instantiate(definition.prefab, anchor + Vector3.right * 16f, Quaternion.identity, fixture.transform) : null;
            StructureHealth health = pieceObject != null ? pieceObject.GetComponent<StructureHealth>() : null;
            if (pieceObject == null || health == null) { Check(false, "reinforced wall fixture is available for health save check"); yield break; }
            Vector3 piecePosition = pieceObject.transform.position;

            savePath = Path.Combine(Application.temporaryCachePath, "structural-verification-" + Guid.NewGuid().ToString("N") + ".json");
            health.RestoreHealth(health.maxHealth * .4f);
            bool saved = InvokePath(save, "TrySaveToPath", savePath);
            Check(saved, "SaveManager writes a temporary structural-health snapshot");
            if (saved)
            {
                health.RestoreHealth(health.maxHealth);
                bool loaded = InvokePath(save, "TryLoadFromPath", savePath);
                yield return null;
                PlacedPiece restored = FindPlaced(id, piecePosition);
                StructureHealth restoredHealth = restored != null ? restored.GetComponent<StructureHealth>() : null;
                Check(loaded && restoredHealth != null && Mathf.Abs(restoredHealth.Fraction - .4f) < .01f,
                    "save/load restores a damaged higher-tier wall's health fraction");
            }

            // Destroying a recipe-backed structure creates the ordinary loot-bag salvage payload.
            // RaidDirector serializes that payload as part of its SaveableSystem state.
            PlacedPiece salvagePiece = FindPlaced(id, piecePosition);
            StructureHealth salvageHealth = salvagePiece != null ? salvagePiece.GetComponent<StructureHealth>() : null;
            int before = LootBagSystem.Bags.Count;
            int destroyedEvents = 0;
            Action<StructureHealth> onDestroyed = destroyed => { if (destroyed == salvageHealth) destroyedEvents++; };
            StructureHealth.Destroyed += onDestroyed;
            if (salvageHealth != null)
            {
                salvageHealth.TakeDamage(salvageHealth.maxHealth + 1f);
                salvageHealth.TakeDamage(salvageHealth.maxHealth + 1f);
            }
            int immediateBagCount = LootBagSystem.Bags.Count;
            LootBag bag = LootBagSystem.Bags.Skip(before).Where(item => item != null)
                .OrderBy(item => (item.transform.position - piecePosition).sqrMagnitude).FirstOrDefault();
            StructureHealth.Destroyed -= onDestroyed;
            bool cappedRecipeSalvage = bag != null && RecipeBook.PieceCosts.TryGetValue(id, out var costs) &&
                bag.Contents.Count == costs.Count(cost => RaidDirector.DestroyedStructureSalvageAmount(cost.amount) > 0 && ItemCatalog.Exists(cost.id)) &&
                costs.Where(cost => RaidDirector.DestroyedStructureSalvageAmount(cost.amount) > 0 && ItemCatalog.Exists(cost.id))
                    .All(cost => bag.Contents.Any(item => item.Key == cost.id && item.Value == RaidDirector.DestroyedStructureSalvageAmount(cost.amount)));
            Check(salvageHealth != null && salvageHealth.IsDestroyed && destroyedEvents == 1 && immediateBagCount == before + 1,
                "two lethal hits emit one destruction event and one immediate salvage bag");
            Check(cappedRecipeSalvage, "destroyed structure salvage contains exactly the floored quarter-cost recipe payload");
            yield return null;

            string raidSnapshot = raids.CaptureState();
            int expectedBagCount = LootBagSystem.Bags.Count;
            int savedBagId = bag != null ? bag.Id : -1;
            string savedContents = bag != null ? string.Join(",", bag.Contents.Select(item => item.Key + ":" + item.Value).OrderBy(item => item, StringComparer.Ordinal)) : string.Empty;
            LootBagSystem.Clear();
            raids.RestoreState(raidSnapshot);
            LootBag restoredBag = LootBagSystem.Bags.FirstOrDefault(item => item != null && item.Id == savedBagId);
            string restoredContents = restoredBag != null
                ? string.Join(",", restoredBag.Contents.Select(item => item.Key + ":" + item.Value).OrderBy(item => item, StringComparer.Ordinal)) : string.Empty;
            Check(LootBagSystem.Bags.Count == expectedBagCount && restoredBag != null && restoredContents == savedContents,
                "RaidDirector save state restores the destroyed structure's salvage bag and contents");

            if (!string.IsNullOrEmpty(savePath))
            {
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
            }
        }

        private IEnumerator CheckBottomUpCollapse(Vector3 anchor)
        {
            GameObject foundationObject = Create("piece_foundation_concrete", anchor + Vector3.up * .3f + Vector3.back);
            GameObject alternateFoundationObject = Create("piece_foundation_concrete", anchor + Vector3.up * .3f + Vector3.forward);
            PlacedPiece foundation = foundationObject != null ? foundationObject.GetComponent<PlacedPiece>() : null;
            PlacedPiece alternateFoundation = alternateFoundationObject != null ? alternateFoundationObject.GetComponent<PlacedPiece>() : null;
            float wallBottom = foundation != null ? HomesteadPlacement.TopOf(foundation) : anchor.y;
            GameObject lowerObject = Create("piece_wall_brick", new Vector3(anchor.x, wallBottom + 1.5f, anchor.z));
            PlacedPiece lower = lowerObject != null ? lowerObject.GetComponent<PlacedPiece>() : null;
            float upperBottom = lower != null ? HomesteadPlacement.TopOf(lower) : wallBottom + 3f;
            GameObject upperObject = Create("piece_wall_reinforced", new Vector3(anchor.x, upperBottom + 1.5f, anchor.z));
            PlacedPiece upper = upperObject != null ? upperObject.GetComponent<PlacedPiece>() : null;
            Check(foundation != null && alternateFoundation != null && lower != null && upper != null,
                "alternate-support collapse fixture instantiates two foundations and two stacked walls");
            if (foundation == null || alternateFoundation == null || lower == null || upper == null) yield break;

            Check(HomesteadPlacement.IsSupported(lower) && HomesteadPlacement.IsSupported(upper),
                "stacked walls report support through the concrete foundations before damage");
            foundation.GetComponent<StructureHealth>()?.TakeDamage(100000f);
            yield return null;
            Check(lower != null && upper != null && HomesteadPlacement.IsSupported(lower),
                "collapse rechecks alternate support and keeps the lower and upper walls standing");

            var order = new System.Collections.Generic.List<string>();
            Action<StructureHealth> recordWallCollapse = destroyed =>
            {
                PlacedPiece piece = destroyed != null ? destroyed.GetComponent<PlacedPiece>() : null;
                if (piece != null && (piece == lower || piece == upper)) order.Add(piece.pieceId);
            };
            StructureHealth.Destroyed += recordWallCollapse;
            alternateFoundation.GetComponent<StructureHealth>()?.TakeDamage(100000f);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < deadline && (lower != null || upper != null)) yield return null;
            StructureHealth.Destroyed -= recordWallCollapse;
            Check(lower == null && upper == null && order.Count == 2 &&
                  order[0] == "piece_wall_brick" && order[1] == "piece_wall_reinforced",
                "destroying the last support collapses lower wall before upper wall and propagates upward");
        }

        private GameObject Create(string id, Vector3 position)
        {
            BuildPieceDefinition definition = Definition(id);
            return definition != null && definition.prefab != null
                ? Instantiate(definition.prefab, position, Quaternion.identity, fixture.transform) : null;
        }

        private BuildPieceDefinition Definition(string id) => build.availablePieces.FirstOrDefault(piece => piece != null && piece.id == id);

        private static PlacedPiece FindPlaced(string id, Vector3 position)
        {
            foreach (PlacedPiece piece in PlacedPieceRegistry.All)
                if (piece != null && piece.pieceId == id && Vector3.Distance(piece.transform.position, position) < .2f) return piece;
            return null;
        }

        private static bool InvokePath(SaveManager manager, string methodName, string path)
        {
            MethodInfo method = typeof(SaveManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(typeof(SaveManager).FullName, methodName);
            return (bool)method.Invoke(manager, new object[] { path });
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(45f);
            Debug.LogError("[StructuralVerificationProbe] FAIL: timed out");
            Application.Quit(1);
        }

        private void Check(bool condition, string message)
        {
            if (condition) Debug.Log("[StructuralVerificationProbe] PASS: " + message);
            else { failures++; Debug.LogError("[StructuralVerificationProbe] FAIL: " + message); }
        }

        private void Finish()
        {
            if (!string.IsNullOrEmpty(savePath))
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(savePath + suffix)) File.Delete(savePath + suffix);
            Debug.Log(failures == 0 ? "[StructuralVerificationProbe] ALL CHECKS PASSED" : $"[StructuralVerificationProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
