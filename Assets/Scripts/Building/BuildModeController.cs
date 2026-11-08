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

        private bool isBuildModeActive;
        private int currentPieceIndex;
        private float currentYawDegrees;
        private GameObject currentGhost;
        private BuildPieceDefinition currentGhostPiece;
        private bool placementValid;
        private MaterialPropertyBlock materialPropertyBlock;

        private float lastCycleTime;
        private bool loggedMissingPiecesOrCamera;
        private bool loggedMissingPrefab;

        private void Awake()
        {
            input = new BuildInputActions();
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            input.Enable();
            input.ToggleBuildMode.performed += OnToggleBuildMode;
            input.CancelBuild.performed += OnCancelBuild;
            input.RotatePiece.performed += OnRotatePiece;
            input.PlacePiece.performed += OnPlacePiece;
        }

        private void OnDisable()
        {
            input.ToggleBuildMode.performed -= OnToggleBuildMode;
            input.CancelBuild.performed -= OnCancelBuild;
            input.RotatePiece.performed -= OnRotatePiece;
            input.PlacePiece.performed -= OnPlacePiece;
            input.Disable();
            input.Dispose();
        }

        private void OnToggleBuildMode(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            // B is still live while the pause menu has the game time-frozen (Update and input
            // callbacks both keep running at timeScale 0). Entering build mode from behind the
            // pause panel would float a ghost over it and turn the Resume click into a piece
            // placement. Blocking entry here is enough to keep the two modes mutually
            // exclusive: PauseMenu already refuses to open while build mode is active.
            if (Time.timeScale == 0f)
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

            DestroyGhost();
            isBuildModeActive = false;
            lastCancelFrame = Time.frameCount;
        }

        private void OnRotatePiece(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!isBuildModeActive)
            {
                return;
            }

            currentYawDegrees = (currentYawDegrees + 90f) % 360f;
        }

        private void OnPlacePiece(UnityEngine.InputSystem.InputAction.CallbackContext context)
        {
            if (!isBuildModeActive || !placementValid || currentGhost == null)
            {
                return;
            }

            BuildPieceDefinition piece = GetActivePiece();
            if (piece == null)
            {
                return;
            }

            if (!SheltreSim.Crafting.CraftingSystem.TryAfford(inventory, piece.craftingCost))
            {
                Debug.Log($"Cannot afford '{piece.displayName}'.");
                return;
            }

            SheltreSim.Crafting.CraftingSystem.Spend(inventory, piece.craftingCost);

            Instantiate(piece.prefab, currentGhost.transform.position, currentGhost.transform.rotation);
            Debug.Log($"Placed '{piece.displayName}' at {currentGhost.transform.position}.");
        }

        private void Update()
        {
            HandleCycleInput();

            if (!isBuildModeActive)
            {
                DestroyGhost();
                ClearBuildReadout();
                return;
            }

            if (availablePieces.Count == 0 || raycastCamera == null)
            {
                if (!loggedMissingPiecesOrCamera)
                {
                    Debug.LogError($"{nameof(BuildModeController)} on '{name}' needs a raycastCamera and at least one entry in availablePieces.");
                    loggedMissingPiecesOrCamera = true;
                }
                return;
            }

            BuildPieceDefinition activePiece = GetActivePiece();
            UpdateBuildReadout(activePiece);

            if (activePiece.prefab == null)
            {
                if (!loggedMissingPrefab)
                {
                    Debug.LogError($"BuildPieceDefinition '{activePiece.displayName}' has no prefab assigned.");
                    loggedMissingPrefab = true;
                }
                return;
            }

            if (!Physics.Raycast(raycastCamera.transform.position, raycastCamera.transform.forward, out RaycastHit hit, 100f, groundMask))
            {
                DestroyGhost();
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
            snapped.y = hit.point.y + activePiece.footprintSize.y / 2f;
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
                obstructionMask);

            TintGhost(placementValid ? Color.green : Color.red);
        }

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
            sb.Append(piece.displayName).Append(" — ");
            if (piece.craftingCost == null || piece.craftingCost.Count == 0)
            {
                sb.Append("free");
            }
            else
            {
                for (int i = 0; i < piece.craftingCost.Count; i++)
                {
                    var line = piece.craftingCost[i];
                    if (line.resource == null)
                    {
                        continue;
                    }
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(line.amount).Append(' ').Append(line.resource.displayName);
                }
            }

            // Rebuilt and re-sent every frame while in build mode (not just on piece-change) —
            // affordability can flip without the selected piece changing (gathering/spending
            // resources while still looking at the same piece), and this project's scale makes
            // a per-frame string rebuild here irrelevant, same as the stat bars above.
            lastBuildReadoutText = sb.ToString();
            survivalHud.SetBuildReadout(lastBuildReadoutText, canAfford);
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
            if (currentGhost != null)
            {
                Destroy(currentGhost);
                currentGhost = null;
                currentGhostPiece = null;
            }
        }

        private void OnDestroy()
        {
            DestroyGhost();
        }
    }
}
