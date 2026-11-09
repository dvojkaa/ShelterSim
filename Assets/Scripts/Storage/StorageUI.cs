using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Storage
{
    /// <summary>
    /// The crate screen: pack on the left, crate on the right. One instance, built the first time a crate is opened,
    /// and gone with the scene on a restart. ModalUI owns the pause and the cursor. Items only move through StorageTransfer.
    /// </summary>
    public sealed class StorageUI : MonoBehaviour
    {
        private const float RowHeight = 68f;
        private const float RowWidth = 556f;
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly int[] Amounts = { 1, 10, StorageTransfer.All };
        private static readonly string[] AmountNames = { "1", "10", "All" };

        private sealed class Row
        {
            public string id;
            public GameObject root;
            public Text name, detail;
            public readonly Button[] buttons = new Button[3];
        }

        private sealed class Column
        {
            public bool isPack;
            public RectTransform content;
            public Text header;
            public readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        }

        private static StorageUI instance;

        private Canvas canvas;
        private Text loadLabel, status;
        private Column packColumn, crateColumn;
        private Button storeMaterials, takeAll, closeButton;
        private StorageCratePiece crate;
        private Inventory pack;
        private bool open, closeRequested;
        private InputAction cancel;

        public static StorageUI Instance => instance;
        public bool IsOpen => open;
        public StorageCratePiece CurrentCrate => crate;
        public Canvas Canvas => canvas;

        /// <summary>Opens the screen for a crate, unless the player is dead, building or another modal is up.</summary>
        public static bool Open(StorageCratePiece target, Inventory inventory)
        {
            if (target == null || inventory == null) return false;
            if (instance == null) new GameObject("StorageUI").AddComponent<StorageUI>();
            return instance.OpenFor(target, inventory);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            instance = this;
            cancel = new InputAction("CloseStorage", InputActionType.Button, "<Keyboard>/escape");
        }

        private void OnEnable() { cancel.performed += OnCancel; cancel.Enable(); }

        private void OnDisable()
        {
            Close();
            cancel.performed -= OnCancel;
            cancel.Disable();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            cancel?.Dispose();
        }

        // Escape is also PauseMenu's key. Closing in LateUpdate stops one press from also opening the pause screen.
        private void OnCancel(InputAction.CallbackContext _) { if (open) closeRequested = true; }

        /// <summary>The same request Escape makes.</summary>
        public void RequestClose() { if (open) closeRequested = true; }

        private void LateUpdate()
        {
            if (!open) return;
            if (closeRequested || crate == null || !crate.isActiveAndEnabled || pack == null) Close();
        }

        private bool OpenFor(StorageCratePiece target, Inventory inventory)
        {
            if (open) return false;
            SurvivalStats stats = inventory.GetComponent<SurvivalStats>();
            if (stats != null && (stats.IsDead || stats.health <= 0f)) return false;
            BuildModeController build = FindFirstObjectByType<BuildModeController>();
            if (build != null && build.IsBuildModeActive)
            {
                GameMessageBus.Post("Leave build mode with B or Escape to open the crate.");
                return false;
            }
            if (!ModalUI.TryOpen(this, Close)) return false;

            if (canvas == null) BuildUI();
            crate = target;
            pack = inventory;
            open = true;
            closeRequested = false;
            canvas.gameObject.SetActive(true);
            status.text = "Store what you don't need to carry. Crafting also uses crates within 6 m of you.";
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.CrateOpen, target.transform.position, 0.85f, 20f);
            Refresh();
            return true;
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            closeRequested = false;
            crate = null;
            pack = null;
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }

        // ---------- Actions ----------

        private void Move(bool fromPack, string id, int amount)
        {
            if (!open || crate == null || pack == null) return;
            string name = ItemCatalog.NameOf(id);
            int moved = fromPack ? StorageTransfer.Store(pack, crate, id, amount) : StorageTransfer.Take(pack, crate, id, amount);
            if (moved > 0) status.text = fromPack ? $"Stored {moved} {name}." : $"Took {moved} {name}.";
            else status.text = fromPack && StorageTransfer.IsEquipped(pack, id) ? $"Unequip the {name} to store it." : "Nothing to move.";
            Refresh();
        }

        private void StoreMaterials()
        {
            if (!open || crate == null || pack == null) return;
            long moved = StorageTransfer.StoreBuildingMaterials(pack, crate);
            status.text = moved > 0
                ? $"Stored {moved} building materials. Food, medicine, ammo and gear stay in your pack."
                : "No building materials in your pack.";
            Refresh();
        }

        private void TakeAll()
        {
            if (!open || crate == null || pack == null) return;
            long moved = StorageTransfer.TakeAll(pack, crate);
            status.text = moved > 0 ? $"Took {moved} items." : "The crate is empty.";
            Refresh();
        }

        // ---------- UI ----------

        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform, "Storage_Canvas", 95, true);
            GameObject backdrop = GameUI.Backdrop(canvas.transform, "StorageBackdrop");
            Transform card = GameUI.Panel(backdrop.transform, "StoragePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1240f, 860f), GameUI.PanelColor, true).transform;

            GameUI.Label(card, "Title", "STORAGE CRATE", TopLeft, TopLeft, new Vector2(28f, -22f), new Vector2(800f, 44f), 30, GameUI.Accent, bold: true);
            closeButton = GameUI.Button(card, "CloseButton", "Close", Vector2.one, Vector2.one, new Vector2(-28f, -24f), new Vector2(120f, 42f), Close);
            GameUI.Label(card, "Subtitle", "Game paused  /  Escape to close", TopLeft, TopLeft, new Vector2(28f, -68f), new Vector2(800f, 26f), 18, GameUI.Muted);
            loadLabel = GameUI.Label(card, "Load", string.Empty, TopLeft, TopLeft, new Vector2(28f, -98f), new Vector2(1180f, 28f), 20, bold: true);
            storeMaterials = GameUI.Button(card, "StoreMaterials", "Store building materials", TopLeft, TopLeft,
                new Vector2(28f, -132f), new Vector2(320f, 42f), StoreMaterials, true);
            takeAll = GameUI.Button(card, "TakeAll", "Take all", TopLeft, TopLeft, new Vector2(630f, -132f), new Vector2(180f, 42f), TakeAll);

            packColumn = BuildColumn(card, "Pack", 28f, true);
            crateColumn = BuildColumn(card, "Crate", 630f, false);

            status = GameUI.Label(card, "Status", string.Empty, TopLeft, TopLeft, new Vector2(28f, -804f), new Vector2(1184f, 44f), 18, GameUI.Accent);
            canvas.gameObject.SetActive(false);
        }

        private static Column BuildColumn(Transform card, string name, float x, bool isPack)
        {
            var column = new Column { isPack = isPack };
            column.header = GameUI.Label(card, name + "Header", string.Empty, TopLeft, TopLeft, new Vector2(x, -184f), new Vector2(582f, 28f), 20, GameUI.Accent, bold: true);
            column.content = GameUI.ScrollContent(card, name + "Scroll", new Vector2(x, -216f), new Vector2(582f, 578f));
            return column;
        }

        private Row GetRow(Column column, string id)
        {
            if (column.rows.TryGetValue(id, out Row existing)) return existing;
            bool fromPack = column.isPack;
            Transform root = GameUI.Panel(column.content, "Row_" + id, TopLeft, TopLeft, Vector2.zero, new Vector2(RowWidth, RowHeight - 6f), GameUI.RowColor).transform;
            var row = new Row { id = id, root = root.gameObject };
            row.name = GameUI.Label(root, "Name", string.Empty, TopLeft, TopLeft, new Vector2(12f, -6f), new Vector2(300f, 28f), 19, bold: true);
            row.detail = GameUI.Label(root, "Detail", string.Empty, TopLeft, TopLeft, new Vector2(12f, -34f), new Vector2(300f, 24f), 15, GameUI.Muted);
            // Right to left so "All" sits at the edge. Arrows point at where the items go.
            for (int i = 0; i < Amounts.Length; i++)
            {
                int amount = Amounts[i];
                string text = fromPack ? AmountNames[i] + " >" : "< " + AmountNames[i];
                float right = -8f - (Amounts.Length - 1 - i) * 76f;
                row.buttons[i] = GameUI.Button(root, "Move_" + AmountNames[i], text, Vector2.one, Vector2.one,
                    new Vector2(right, -10f), new Vector2(70f, 42f), () => Move(fromPack, id, amount));
            }
            column.rows[id] = row;
            return row;
        }

        private void Refresh()
        {
            if (!open || crate == null || pack == null) return;
            float load = Encumbrance.PackWeight(pack);
            loadLabel.text = Encumbrance.Describe(load);
            loadLabel.color = load >= Encumbrance.NoSprintAt ? GameUI.Danger : load > Encumbrance.SoftLimit ? GameUI.Warning : Color.white;
            RefreshColumn(packColumn, pack.GetAllCounts());
            RefreshColumn(crateColumn, crate.Contents);
            if (Encumbrance.Active != null) Encumbrance.Active.RefreshNow();
        }

        private void RefreshColumn(Column column, IReadOnlyDictionary<string, int> items)
        {
            var ids = new List<string>();
            foreach (var stack in items) if (stack.Value > 0) ids.Add(stack.Key);
            ids.Sort(CompareItems);

            foreach (Row row in column.rows.Values) row.root.SetActive(false);
            float y = 0f;
            foreach (string id in ids)
            {
                Row row = GetRow(column, id);
                row.root.SetActive(true);
                ((RectTransform)row.root.transform).anchoredPosition = new Vector2(0f, -y);
                y += RowHeight;

                int count = items[id];
                row.name.text = $"{ItemCatalog.NameOf(id)}   x{count}";
                bool equipped = column.isPack && StorageTransfer.IsEquipped(pack, id);
                int movable = column.isPack ? StorageTransfer.MovableFromPack(pack, id) : count;
                string weight = $"{(double)Encumbrance.WeightOf(id) * count:0.#} kg";
                row.detail.text = !equipped ? weight : movable > 0 ? $"{weight}, equipped: one stays with you" : $"{weight}, equipped: unequip to store";
                foreach (Button button in row.buttons) button.interactable = movable > 0;
            }
            column.content.sizeDelta = new Vector2(0f, y);
            string title = column.isPack ? "YOUR PACK" : "CRATE";
            column.header.text = ids.Count == 0 ? title + " (empty)" : title;
        }

        private static int CompareItems(string a, string b)
        {
            int byCategory = CategoryOf(a).CompareTo(CategoryOf(b));
            return byCategory != 0 ? byCategory : string.CompareOrdinal(ItemCatalog.NameOf(a), ItemCatalog.NameOf(b));
        }

        private static int CategoryOf(string id)
        {
            ItemSpec spec = ItemCatalog.Spec(id);
            return spec != null ? (int)spec.category : int.MaxValue;
        }

        // ---------- Test hooks ----------

        public Button StoreMaterialsButton => storeMaterials;
        public Button TakeAllButton => takeAll;
        public Button CloseButton => closeButton;

        /// <summary>The button of a visible row. which: 0 moves 1, 1 moves 10, 2 moves all. Null if no row is showing.</summary>
        public Button RowButton(bool packSide, string id, int which)
        {
            Column column = packSide ? packColumn : crateColumn;
            if (column == null || !column.rows.TryGetValue(id, out Row row) || !row.root.activeSelf) return null;
            return row.buttons[which];
        }

        public string LoadText => loadLabel != null ? loadLabel.text : string.Empty;
        public string StatusText => status != null ? status.text : string.Empty;
    }
}
