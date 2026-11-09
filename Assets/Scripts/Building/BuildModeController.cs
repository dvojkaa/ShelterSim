using System.Collections.Generic;
using SheltreSim.Saving;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Drives build mode: ghost preview, grid-snapped placement, rotation, piece cycling,
    /// and overlap validity checking. Input map stays enabled the whole time this component
    /// is active; build-mode on/off is tracked with an internal bool instead of enabling or
    /// disabling individual actions, which avoids partial-map-enable edge cases.
    /// </summary>
    public class BuildModeController : MonoBehaviour
    {
        public Camera raycastCamera;
        public LayerMask groundMask = ~0;
        public float placementRange = 12f;
        public LayerMask obstructionMask = ~0;
        public List<BuildPieceDefinition> availablePieces = new List<BuildPieceDefinition>();

        /// <summary>M5 — wired by the M5 editor setup tool to the Player's Inventory.</summary>
        public SheltreSim.Items.Inventory inventory;

        /// <summary>
        /// Optional, null-checked — on-screen readout for the currently-selected build piece
        /// and whether you can afford it. Opus-review suggestion: cycling pieces previously
        /// gave zero feedback, so you only learned you couldn't afford one after clicking to
        /// place it.
        /// </summary>
        public SheltreSim.Survival.SurvivalHUD survivalHud;
        private string lastBuildReadoutText;

        /// <summary>
        /// Exposed so other systems that also bind Escape or left-click (melee attack, pause
        /// menu) can defer to build mode instead of double-firing on the same input.
        /// </summary>
        public bool IsBuildModeActive => isBuildModeActive;

        /// <summary>
        /// True during the frame in which Escape was used to leave build mode.
        ///
        /// PauseMenu binds Escape too and skips its own handling while build mode is active —
        /// but both maps receive the same keypress in the same frame, and the Input System
        /// gives no ordering guarantee between two independently-enabled action maps bound to
        /// the same control. If this controller's CancelBuild callback happens to run first it
        /// has already cleared isBuildModeActive by the time PauseMenu looks at it, so one
        /// Escape press would both exit build mode AND open the pause menu. Checking this flag
        /// as well makes the hand-off deterministic regardless of callback order.
        /// </summary>
        public bool ConsumedCancelThisFrame => Time.frameCount == lastCancelFrame;

        private BuildInputActions input;
        private int lastCancelFrame = -1;

        // V and X only do anything while in build mode. BuildInputActions stays as it is.
        private UnityEngine.InputSystem.InputAction repairAction;
        private UnityEngine.InputSystem.InputAction dismantleAction;
        private PlacedPiece aimedPiece;
        private PlacedPiece dismantleArmed;
        private float dismantleArmedAt;
        private const float DismantleConfirmSeconds = 2f;
        // Session only, by design: a loaded piece was not placed just now.
        private readonly Dictionary<PlacedPiece, float> placedAt = new Dictionary<PlacedPiece, float>();
        private Ray? debugAim;

        private bool isBuildModeActive;
        private int currentPieceIndex;
        private float currentYawDegrees;
        private GameObject currentGhost;
        private BuildPieceDefinition currentGhostPiece;
        private bool placementValid;
        private string placementFeedback = string.Empty;
        private MaterialPropertyBlock materialPropertyBlock;

        private float lastCycleTime;
        private bool loggedMissingPiecesOrCamera;
        private bool loggedMissingPrefab;

        private void Awake()
        {
            input = new BuildInputActions();
            repairAction = new UnityEngine.InputSystem.InputAction("RepairPiece", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/v");
            dismantleAction = new UnityEngine.InputSystem.InputAction("DismantlePiece", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/x");
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            input.Enable();
            input.ToggleBuildMode.performed += OnToggleBuildMode;
            input.CancelBuild.performed += OnCancelBuild;
            input.RotatePiece.performed += OnRotatePiece;
            input.PlacePiece.performed += OnPlacePiece;
            repairAction.Enable();
            dismantleAction.Enable();
            repairAction.performed += OnRepair;
            dismantleAction.performed += OnDismantle;
        }

        private void OnDisable()
        {
            input.ToggleBuildMode.performed -= OnToggleBuildMode;
            input.CancelBuild.performed -= OnCancelBuild;
            input.RotatePiece.performed -= OnRotatePiece;
            input.PlacePiece.performed -= OnPlacePiece;
            input.Disable();
            repairAction.performed -= OnRepair;
            dismantleAction.performed -= OnDismantle;
            repairAction.Disable();
            dismantleAction.Disable();
            ExitBuildMode();
        }

        /// <summary>Enters build mode on a specific blueprint. For the build palette; does nothing while paused.</summary>
        public bool BeginBuild(int pieceIndex)
        {
            if (!CanBuild || pieceIndex < 0 || pieceIndex >= availablePieces.Count) return false;
            DestroyGhost();
            currentPieceIndex = pieceIndex;
            currentYawDegrees = 0f;
            isBuildModeActive = true;
            return true;
        }

        public void ExitBuildMode()
        {
            isBuildModeActive = false;
            aimedPiece = null;
            dismantleArmed = null;
            DestroyGhost();
            ClearBuildReadout();
        }

        private bool CanBuild => Time.timeScale > 0f &&
            !(GetComponentInParent<SheltreSim.Survival.SurvivalStats>()?.IsDead ?? false);

        private void OnToggleBuildMode(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            // B is still live while the pause menu has the game time-frozen (Update and input
            // callbacks both keep running at timeScale 0). Entering build mode from behind the
            // pause panel would float a ghost over it and turn the Resume click into a piece
            // placement. Blocking entry here is enough to keep the two modes mutually
            // exclusive: PauseMenu already refuses to open while build mode is active.
            if (!CanBuild)
            {
                return;
            }

            isBuildModeActive = !isBuildModeActive;

            if (isBuildModeActive)
            {
                currentPieceIndex = 0;
                currentYawDegrees = 0f;
            }
            else
            {
                DestroyGhost();
            }
        }

        private void OnCancelBuild(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!isBuildModeActive)
            {
                return;
            }

            ExitBuildMode();
            lastCancelFrame = Time.frameCount;
        }

        private void OnRotatePiece(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!isBuildModeActive || !CanBuild)
            {
                return;
            }

            currentYawDegrees = (currentYawDegrees + 90f) % 360f;
        }

        private void OnPlacePiece(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!CanBuild || !isBuildModeActive)
            {
                return;
            }

            TryPlaceCurrent(out _);
        }

        private bool TryPlaceCurrent(out GameObject placed)
        {
            placed = null;
            // Input callbacks run before Update: validate the current view, never last frame's ghost.
            RefreshPlacement();
            if (!placementValid || currentGhost == null)
            {
                SheltreSim.UI.GameMessageBus.Post(placementFeedback);
                return false;
            }

            BuildPieceDefinition piece = GetActivePiece();
            if (piece == null)
            {
                return false;
            }

            if (!SheltreSim.Crafting.CraftingSystem.TrySpend(inventory, piece.craftingCost))
            {
                placementFeedback = $"Cannot afford '{piece.displayName}'.";
                SheltreSim.UI.GameMessageBus.Post(placementFeedback);
                return false;
            }

            placed = Instantiate(piece.prefab, currentGhost.transform.position, currentGhost.transform.rotation);
            PlacedPiece tag = placed.GetComponent<PlacedPiece>();
            if (tag != null) placedAt[tag] = Time.time;
            Physics.SyncTransforms();
            HomesteadWorldGuard.ClearFootprint(placed);
            SheltreSim.UI.GameMessageBus.Post($"Placed {piece.displayName}.");
            RefreshPlacement();
            return true;
        }

        private void Update()
        {
            if (!CanBuild)
            {
                ExitBuildMode();
                return;
            }

            HandleCycleInput();
            RefreshPlacement();
        }

        private Ray AimRay => debugAim ?? new Ray(raycastCamera.transform.position, raycastCamera.transform.forward);

        private void RefreshPlacement()
        {
            placementValid = false;
            if (!isBuildModeActive)
            {
                DestroyGhost();
                ClearBuildReadout();
                return;
            }

            if (availablePieces.Count == 0 || raycastCamera == null)
            {
                DestroyGhost();
                ClearBuildReadout();
                placementFeedback = "Build mode needs a camera and an available blueprint.";
                if (!loggedMissingPiecesOrCamera)
                {
                    Debug.LogError($"{nameof(BuildModeController)} on '{name}' needs a raycastCamera and at least one entry in availablePieces.");
                    loggedMissingPiecesOrCamera = true;
                }
                return;
            }

            BuildPieceDefinition activePiece = GetActivePiece();

            if (activePiece == null)
            {
                DestroyGhost();
                ClearBuildReadout();
                placementFeedback = "No build piece selected.";
                return;
            }

            if (activePiece.prefab == null)
            {
                DestroyGhost();
                placementFeedback = "This blueprint has no buildable prefab.";
                UpdateBuildReadout(activePiece);
                if (!loggedMissingPrefab)
                {
                    Debug.LogError($"BuildPieceDefinition '{activePiece.displayName}' has no prefab assigned.");
                    loggedMissingPrefab = true;
                }
                return;
            }

            aimedPiece = HomesteadTools.PieceUnderRay(AimRay, placementRange, groundMask);

            if (activePiece.placementRule != BuildPlacementRule.Free)
            {
                RefreshStructural(activePiece);
                return;
            }

            Ray aim = AimRay;
            if (!Physics.Raycast(aim.origin, aim.direction, out RaycastHit hit, placementRange, groundMask, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.7f)
            {
                DestroyGhost();
                placementFeedback = "Aim at a level surface within build range.";
                UpdateBuildReadout(activePiece);
                return;
            }

            // Snapping can move the preview beyond the supporting surface's edge.
            Vector3 support = BuildGrid.SnapPosition(hit.point);
            support.y = hit.point.y + 0.1f;
            if (!Physics.Raycast(support, Vector3.down, out RaycastHit supportHit, 0.2f,
                    groundMask, QueryTriggerInteraction.Ignore) || supportHit.normal.y < 0.7f)
            {
                DestroyGhost();
                placementFeedback = "Move away from the edge: the snapped position needs support.";
                UpdateBuildReadout(activePiece);
                return;
            }

            EnsureGhostForPiece(activePiece);

            // BUG FIX (playtest): positioning the ghost's pivot (box center) exactly AT the
            // raycast hit point means half the box's height sits below whatever surface it hit
            // — a wall/floor piece ghost permanently intersects the ground/floor collider it's
            // standing on, so the overlap check below always failed and the ghost was always
            // red, regardless of resources. Offset up by half the footprint height so the box's
            // BOTTOM face rests on the hit surface instead of its center.
            Vector3 snapped = BuildGrid.SnapPosition(hit.point);
            snapped.y = supportHit.point.y + activePiece.footprintSize.y / 2f;
            currentGhost.transform.position = snapped;
            currentGhost.transform.rotation = BuildGrid.SnapRotation(currentYawDegrees);

            // Shrink the overlap box by a hair before testing. Because of the offset above, the
            // ghost's bottom face is now exactly coplanar with the surface it rests on, and an
            // exact-tangency overlap test is a floating-point coin flip — a false positive there
            // would resurrect the very "always red" bug the offset was added to fix. 1cm of skin
            // is invisible in-game but puts the test unambiguously on the right side.
            const float overlapSkin = 0.01f;
            Vector3 halfExtents = Vector3.Max(
                activePiece.footprintSize / 2f - Vector3.one * overlapSkin,
                Vector3.one * overlapSkin);

            placementValid = !Physics.CheckBox(
                currentGhost.transform.position,
                halfExtents,
                currentGhost.transform.rotation,
                obstructionMask, QueryTriggerInteraction.Ignore);

            bool affordable = SheltreSim.Crafting.CraftingSystem.TryAfford(inventory, activePiece.craftingCost);
            placementFeedback = !placementValid ? "Blocked: move the preview clear of obstacles."
                : !affordable ? "Need supplies: gather the missing resources." : "Ready: click to place.";
            TintGhost(!placementValid ? Color.red : affordable ? Color.green : Color.yellow);
            UpdateBuildReadout(activePiece);
        }

        /// <summary>Structural pieces position themselves by rule. Same preview, tint and readout as the Free path.</summary>
        private void RefreshStructural(BuildPieceDefinition activePiece)
        {
            HomesteadPose pose = HomesteadPlacement.Evaluate(activePiece, AimRay, placementRange, currentYawDegrees, groundMask, obstructionMask);
            if (!pose.hasPose)
            {
                DestroyGhost();
                placementFeedback = pose.reason;
                UpdateBuildReadout(activePiece);
                return;
            }

            EnsureGhostForPiece(activePiece);
            currentGhost.transform.SetPositionAndRotation(pose.position, pose.rotation);
            placementValid = pose.valid;
            bool affordable = SheltreSim.Crafting.CraftingSystem.TryAfford(inventory, activePiece.craftingCost);
            placementFeedback = !placementValid ? pose.reason
                : !affordable ? "Need supplies: gather the missing resources." : "Ready: click to place.";
            TintGhost(!placementValid ? Color.red : affordable ? Color.green : Color.yellow);
            UpdateBuildReadout(activePiece);
        }

        private void OnRepair(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!isBuildModeActive || !CanBuild) return;
            RepairPiece(HomesteadTools.PieceUnderRay(AimRay, placementRange, groundMask));
        }

        private void OnDismantle(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!isBuildModeActive || !CanBuild) return;
            RequestDismantle(HomesteadTools.PieceUnderRay(AimRay, placementRange, groundMask));
        }

        private bool RepairPiece(PlacedPiece piece)
        {
            bool repaired = HomesteadTools.TryRepair(inventory, piece, out string message);
            SheltreSim.UI.GameMessageBus.Post(message);
            return repaired;
        }

        /// <summary>The first press names the refund, a second press on the same piece within 2 seconds takes it apart.</summary>
        private bool RequestDismantle(PlacedPiece piece)
        {
            if (!HomesteadTools.CanDismantle(inventory, piece, out string reason))
            {
                dismantleArmed = null;
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                SheltreSim.UI.GameMessageBus.Post(reason);
                return false;
            }

            BuildPieceDefinition definition = DefinitionFor(piece.pieceId);
            bool forgiven = IsForgiven(piece);
            if (dismantleArmed != piece || Time.unscaledTime - dismantleArmedAt > DismantleConfirmSeconds)
            {
                dismantleArmed = piece;
                dismantleArmedAt = Time.unscaledTime;
                SheltreSim.UI.GameMessageBus.Post($"Press X again to dismantle. Refund: {HomesteadTools.DescribeRefund(definition, piece, forgiven)}.");
                return false;
            }

            dismantleArmed = null;
            placedAt.Remove(piece);
            string refund = HomesteadTools.Dismantle(inventory, definition, piece, forgiven);
            SheltreSim.UI.GameMessageBus.Post($"Dismantled. Got back: {refund}.");
            aimedPiece = null;
            return true;
        }

        private bool IsForgiven(PlacedPiece piece)
        {
            StructureHealth health = piece.GetComponent<StructureHealth>();
            return placedAt.TryGetValue(piece, out float time) && Time.time - time < HomesteadCatalog.ForgivenessSeconds
                && (health == null || !health.IsDamaged);
        }

        private BuildPieceDefinition DefinitionFor(string id)
        {
            foreach (BuildPieceDefinition definition in availablePieces)
                if (definition != null && definition.id == id) return definition;
            return null;
        }

        // ---------- Test seams: these drive the same paths as the mouse and keyboard ----------

        public string CurrentPieceId => isBuildModeActive ? GetActivePiece()?.id : null;
        public string PlacementFeedback => placementFeedback;
        public bool PlacementValid => placementValid;

        /// <summary>Probe only: replaces the camera ray. Null gives the camera back.</summary>
        public void DebugAim(Ray? ray) => debugAim = ray;

        /// <summary>Probe only: selects a piece and aims, then runs the real preview. Returns whether it would be placed, and why not.</summary>
        public bool DebugPreview(string id, Ray ray, float yawDegrees, out string reason)
        {
            reason = "Unknown piece " + id;
            int index = availablePieces.FindIndex(p => p != null && p.id == id);
            if (index < 0 || !CanBuild) return false;
            if (!isBuildModeActive || currentPieceIndex != index) BeginBuild(index);
            currentYawDegrees = yawDegrees;
            debugAim = ray;
            RefreshPlacement();
            reason = placementFeedback;
            return placementValid;
        }

        /// <summary>Probe only: the real placement path, from a given aim. Returns the placed instance, or null with the reason.</summary>
        public GameObject DebugPlaceAt(string id, Ray ray, float yawDegrees, out string reason)
        {
            DebugPreview(id, ray, yawDegrees, out reason);
            if (!isBuildModeActive || !CanBuild) return null;
            TryPlaceCurrent(out GameObject placed);
            if (placed == null) reason = placementFeedback;
            return placed;
        }

        public bool DebugRepair(PlacedPiece piece) => RepairPiece(piece);
        public bool DebugDismantle(PlacedPiece piece) => RequestDismantle(piece);
        public void DebugForgetPlacementTimes() => placedAt.Clear();

        private void HandleCycleInput()
        {
            if (!isBuildModeActive)
            {
                return;
            }

            float cycleValue = input.CyclePiece.ReadValue<float>();
            if (Mathf.Abs(cycleValue) <= 0.01f)
            {
                return;
            }

            if (Time.time - lastCycleTime <= 0.15f)
            {
                return;
            }

            if (availablePieces.Count == 0)
            {
                return;
            }

            int direction = cycleValue > 0f ? 1 : -1;
            currentPieceIndex = ((currentPieceIndex + direction) % availablePieces.Count + availablePieces.Count) % availablePieces.Count;
            DestroyGhost();
            lastCycleTime = Time.time;
        }

        private void UpdateBuildReadout(BuildPieceDefinition piece)
        {
            if (survivalHud == null || piece == null)
            {
                return;
            }

            bool canAfford = SheltreSim.Crafting.CraftingSystem.TryAfford(inventory, piece.craftingCost);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(currentPieceIndex + 1).Append('/').Append(availablePieces.Count)
                .Append("  ").Append(piece.displayName).Append('\n');
            bool hasCost = false;
            if (piece.craftingCost != null)
            {
                foreach (var line in piece.craftingCost)
                {
                    if (line == null || line.resource == null || line.amount <= 0) continue;
                    if (hasCost) sb.Append("   ");
                    int owned = SheltreSim.Crafting.CraftingSystem.CountAvailable(inventory, line.resource.id);
                    sb.Append(line.resource.displayName).Append(' ').Append(owned).Append('/').Append(line.amount);
                    hasCost = true;
                }
            }
            if (!hasCost) sb.Append("Free");
            sb.Append('\n').Append(placementFeedback);
            if (aimedPiece != null)
            {
                StructureHealth aimedHealth = aimedPiece.GetComponent<StructureHealth>();
                sb.Append('\n');
                if (aimedHealth != null) sb.Append(Mathf.CeilToInt(aimedHealth.Health)).Append('/').Append(Mathf.CeilToInt(aimedHealth.maxHealth)).Append(" HP   ");
                sb.Append("V repair  X dismantle");
            }

            // Rebuilt and re-sent every frame while in build mode (not just on piece-change) —
            // affordability can flip without the selected piece changing (gathering/spending
            // resources while still looking at the same piece), and this project's scale makes
            // a per-frame string rebuild here irrelevant, same as the stat bars above.
            lastBuildReadoutText = sb.ToString();
            survivalHud.SetBuildReadout(lastBuildReadoutText, canAfford && placementValid);
        }

        private void ClearBuildReadout()
        {
            if (survivalHud == null || lastBuildReadoutText == null)
            {
                return;
            }

            lastBuildReadoutText = null;
            survivalHud.SetBuildReadout(string.Empty, false);
        }

        private BuildPieceDefinition GetActivePiece()
        {
            if (availablePieces.Count == 0)
            {
                return null;
            }

            currentPieceIndex = Mathf.Clamp(currentPieceIndex, 0, availablePieces.Count - 1);
            return availablePieces[currentPieceIndex];
        }

        private void EnsureGhostForPiece(BuildPieceDefinition piece)
        {
            if (currentGhost != null && currentGhostPiece == piece)
            {
                return;
            }

            DestroyGhost();

            currentGhost = Instantiate(piece.prefab);
            currentGhost.name = "Ghost_" + piece.displayName;

            foreach (Collider col in currentGhost.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Piece prefabs carry a PlacedPiece component that self-registers with
            // PlacedPieceRegistry on enable (for save/load). A ghost is a preview, not a
            // placed piece, so disable it immediately — otherwise a save triggered while a
            // ghost is visible would incorrectly include it in the save file.
            PlacedPiece ghostPlacedPiece = currentGhost.GetComponent<PlacedPiece>();
            if (ghostPlacedPiece != null)
            {
                ghostPlacedPiece.enabled = false;
            }

            // Functional furniture must never collect water, heal, or subscribe as gameplay
            // while it is merely a preview. Keep only its renderers alive.
            foreach (MonoBehaviour behavior in currentGhost.GetComponentsInChildren<MonoBehaviour>())
            {
                behavior.enabled = false;
            }

            currentGhostPiece = piece;
        }

        private void TintGhost(Color color)
        {
            if (currentGhost == null)
            {
                return;
            }

            // The project turned out to be Built-in Render Pipeline (Standard shader reads
            // "_Color"), not URP (Lit shader reads "_BaseColor") as originally planned — set
            // both so this works regardless; setting a property name the active shader doesn't
            // have is a harmless no-op. This is a flat tint only (no alpha blending); true
            // transparency would additionally require the material's render mode/surface type
            // set to transparent, which these stub prefabs don't have. Acceptable simplification,
            // revisit later if wanted.
            materialPropertyBlock.SetColor("_Color", color);
            materialPropertyBlock.SetColor("_BaseColor", color);

            foreach (MeshRenderer renderer in currentGhost.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.SetPropertyBlock(materialPropertyBlock);
            }
        }

        private void DestroyGhost()
        {
            placementValid = false;
            if (currentGhost != null)
            {
                currentGhost.SetActive(false);
                Destroy(currentGhost);
                currentGhost = null;
                currentGhostPiece = null;
            }
        }

        private void OnDestroy()
        {
            input?.Dispose();
            repairAction?.Dispose();
            dismantleAction?.Dispose();
            DestroyGhost();
        }
    }
}
