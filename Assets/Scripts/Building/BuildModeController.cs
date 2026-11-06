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

        private BuildInputActions input;

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

            if (!ResourceStub.TryAfford(piece.resourceCost))
            {
                Debug.Log($"Cannot afford '{piece.displayName}' (cost {piece.resourceCost}).");
                return;
            }

            Instantiate(piece.prefab, currentGhost.transform.position, currentGhost.transform.rotation);
            Debug.Log($"Placed '{piece.displayName}' at {currentGhost.transform.position}.");
        }

        private void Update()
        {
            HandleCycleInput();

            if (!isBuildModeActive)
            {
                DestroyGhost();
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

            currentGhost.transform.position = BuildGrid.SnapPosition(hit.point);
            currentGhost.transform.rotation = BuildGrid.SnapRotation(currentYawDegrees);

            placementValid = !Physics.CheckBox(
                currentGhost.transform.position,
                activePiece.footprintSize / 2f,
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

            // URP's Lit shader reads "_BaseColor", not the legacy Built-in "_Color" property —
            // setting "_Color" here would silently do nothing visually. This is a flat tint only
            // (no alpha blending); true transparency would additionally require the material's
            // Surface Type set to Transparent, which these stub prefabs don't have. Acceptable
            // simplification for M2, revisit later if wanted.
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
