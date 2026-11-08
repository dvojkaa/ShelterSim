using System;
using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.Items
{
    public class InventoryInputActions : IDisposable
    {
        private readonly InputActionMap map = new InputActionMap("Inventory");
        public InputAction Toggle { get; }

        public InventoryInputActions()
        {
            Toggle = map.AddAction("Toggle", InputActionType.Button);
            Toggle.AddBinding("<Keyboard>/i");
            Toggle.AddBinding("<Keyboard>/tab");
        }

        public void Enable() => map.Enable();
        public void Disable() => map.Disable();
        public void Dispose() => map.Dispose();
    }

    public class InventoryUI : MonoBehaviour
    {
        public Inventory inventory;
        public List<ResourceDefinition> knownResources = new List<ResourceDefinition>();
        public SheltreSim.Survival.SurvivalStats stats;
        public List<BuildPieceDefinition> knownRecipes = new List<BuildPieceDefinition>();
        public BuildModeController buildModeController;
        public SheltreSim.Combat.MeleeAttacker meleeAttacker;

        public bool IsOpen { get; private set; }

        private GameObject panel;
        private Text vitalsLabel;
        private Text feedbackLabel;
        private InventoryInputActions input;
        private readonly Dictionary<string, Text> rows = new Dictionary<string, Text>();
        private readonly Dictionary<string, Button> actions = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> recipeRows = new Dictionary<string, Text>();
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

        private void Awake()
        {
            input = new InventoryInputActions();
            BuildUI();
        }

        private void OnEnable()
        {
            input.Toggle.performed += OnTogglePerformed;
            input.Enable();
        }

        private void OnDisable()
        {
            input.Toggle.performed -= OnTogglePerformed;
            input.Disable();
            CloseIfOpen();
        }

        private void OnDestroy() => input?.Dispose();

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            if (IsOpen)
            {
                CloseIfOpen();
                return;
            }

            if (stats != null && stats.IsDead) return;
            if (buildModeController != null && buildModeController.IsBuildModeActive)
            {
                GameMessageBus.Post("Leave build mode with B or Escape to open your inventory.");
                return;
            }
            if (!ModalUI.TryOpen(this, CloseIfOpen)) return;

            IsOpen = true;
            panel.SetActive(true);
            feedbackLabel.text = "Use supplies here. Build recipes with B after closing this panel.";
            Refresh();
        }

        public void CloseIfOpen()
        {
            if (!IsOpen) return;
            IsOpen = false;
            panel.SetActive(false);
            ModalUI.Close(this);
        }

        private void BuildUI()
        {
            Canvas canvas = GameUI.CreateCanvas(transform, "Inventory_Canvas", 80, true);
            panel = GameUI.Backdrop(canvas.transform, "InventoryBackdrop");
            Transform card = GameUI.Panel(panel.transform, "InventoryPanel", new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 820f), GameUI.PanelColor, true).transform;

            GameUI.Label(card, "Title", "SUPPLIES & BLUEPRINTS", TopLeft, TopLeft,
                new Vector2(28f, -24f), new Vector2(850f, 44f), 30, GameUI.Accent, bold: true);
            GameUI.Button(card, "CloseButton", "Close", Vector2.one, Vector2.one,
                new Vector2(-28f, -24f), new Vector2(120f, 42f), CloseIfOpen);
            GameUI.Label(card, "Subtitle", "Game paused  /  I, Tab or Escape to return", TopLeft, TopLeft,
                new Vector2(28f, -72f), new Vector2(900f, 26f), 18, GameUI.Muted);
            vitalsLabel = GameUI.Label(card, "Vitals", string.Empty, TopLeft, TopLeft,
                new Vector2(28f, -108f), new Vector2(1084f, 28f), 20);

            GameUI.Label(card, "SuppliesHeader", "INVENTORY", TopLeft, TopLeft,
                new Vector2(28f, -156f), new Vector2(510f, 28f), 18, GameUI.Accent, bold: true);
            GameUI.Label(card, "RecipesHeader", "BUILD RECIPES  /  owned : required", TopLeft, TopLeft,
                new Vector2(590f, -156f), new Vector2(522f, 28f), 18, GameUI.Accent, bold: true);

            RectTransform resourceContent = GameUI.ScrollContent(card, "InventoryScroll",
                new Vector2(28f, -194f), new Vector2(522f, 526f));
            float y = 0f;
            foreach (ResourceDefinition resource in knownResources)
            {
                if (resource == null || string.IsNullOrEmpty(resource.id) || rows.ContainsKey(resource.id)) continue;
                Transform row = GameUI.Panel(resourceContent, "Resource_" + resource.id, TopLeft, TopLeft,
                    new Vector2(0f, -y), new Vector2(520f, 58f), GameUI.RowColor).transform;
                rows[resource.id] = GameUI.Label(row, "Count", resource.displayName, TopLeft, TopLeft,
                    new Vector2(12f, -5f), new Vector2(380f, 26f), 19);
                GameUI.Label(row, "Effect", DescribeResource(resource), TopLeft, TopLeft,
                    new Vector2(12f, -31f), new Vector2(380f, 22f), 15, GameUI.Muted);
                string action = ActionName(resource);
                if (action != null)
                {
                    ResourceDefinition captured = resource;
                    actions[resource.id] = GameUI.Button(row, "Use", action, Vector2.one, Vector2.one,
                        new Vector2(-10f, -9f), new Vector2(100f, 40f), () => UseResource(captured));
                }
                y += 64f;
            }
            resourceContent.sizeDelta = new Vector2(0f, y);

            RectTransform recipeContent = GameUI.ScrollContent(card, "RecipeScroll",
                new Vector2(590f, -194f), new Vector2(522f, 526f));
            y = 0f;
            foreach (BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null || string.IsNullOrEmpty(recipe.id) || recipeRows.ContainsKey(recipe.id)) continue;
                Transform row = GameUI.Panel(recipeContent, "Recipe_" + recipe.id, TopLeft, TopLeft,
                    new Vector2(0f, -y), new Vector2(520f, 68f), GameUI.RowColor).transform;
                GameUI.Label(row, "Name", recipe.displayName, TopLeft, TopLeft,
                    new Vector2(12f, -5f), new Vector2(496f, 26f), 19);
                recipeRows[recipe.id] = GameUI.Label(row, "Cost", string.Empty, TopLeft, TopLeft,
                    new Vector2(12f, -32f), new Vector2(496f, 30f), 16);
                y += 74f;
            }
            recipeContent.sizeDelta = new Vector2(0f, y);

            feedbackLabel = GameUI.Label(card, "Feedback", string.Empty, TopLeft, TopLeft,
                new Vector2(28f, -740f), new Vector2(1084f, 28f), 18, GameUI.Accent);
            GameUI.Label(card, "BuildHelp", "BUILD: B  /  Wheel: choose  /  R: rotate  /  Click: place  /  Scroll lists for more", TopLeft, TopLeft,
                new Vector2(28f, -782f), new Vector2(1084f, 24f), 16, GameUI.Muted);
            panel.SetActive(false);
        }

        private static string ActionName(ResourceDefinition resource)
        {
            if (resource.isFood) return "Eat";
            if (resource.isDrink) return "Drink";
            if (resource.isMedicine) return "Heal";
            if (resource.isWeapon) return "Equip";
            return null;
        }

        private static string DescribeResource(ResourceDefinition resource)
        {
            if (resource.isFood) return $"Restores {resource.hungerRestored:0} food";
            if (resource.isDrink) return $"Restores {resource.thirstRestored:0} water";
            if (resource.isMedicine) return $"Restores {resource.healAmount:0} health";
            if (resource.isWeapon) return $"Permanent +{resource.damageBonus:0} melee damage";
            return "Building and crafting material";
        }

        private bool CanUse(ResourceDefinition resource)
        {
            if (inventory == null || stats == null || stats.IsDead || inventory.GetCount(resource.id) < 1) return false;
            if (resource.isFood) return resource.hungerRestored > 0f && stats.hunger < stats.maxHunger;
            if (resource.isDrink) return resource.thirstRestored > 0f && stats.thirst < stats.maxThirst;
            if (resource.isMedicine) return resource.healAmount > 0f && stats.health < stats.maxHealth;
            if (resource.isWeapon) return meleeAttacker != null && resource.damageBonus > 0f;
            return false;
        }

        private void UseResource(ResourceDefinition resource)
        {
            // Guard stale UI events and avoid wasting a consumable at a full vital.
            if (!IsOpen || !CanUse(resource) || !inventory.TryConsume(resource.id, 1)) return;
            string result;
            if (resource.isFood)
            {
                float before = stats.hunger;
                stats.Eat(resource.hungerRestored);
                result = $"Ate {resource.displayName}: +{stats.hunger - before:0} food.";
            }
            else if (resource.isDrink)
            {
                float before = stats.thirst;
                stats.Drink(resource.thirstRestored);
                result = $"Drank {resource.displayName}: +{stats.thirst - before:0} water.";
            }
            else if (resource.isMedicine)
            {
                float before = stats.health;
                stats.Heal(resource.healAmount);
                result = $"Used {resource.displayName}: +{stats.health - before:0} health.";
            }
            else
            {
                meleeAttacker.ApplyWeaponUpgrade(resource.damageBonus);
                result = $"Equipped {resource.displayName}. Melee damage: {meleeAttacker.attackDamage:0}.";
            }
            feedbackLabel.text = result;
            GameMessageBus.Post(result);
            Refresh();
        }

        private void Update()
        {
            if (IsOpen) Refresh();
        }

        private void Refresh()
        {
            if (stats != null)
            {
                vitalsLabel.text = $"HEALTH  {stats.health:0}/{stats.maxHealth:0}       FOOD  {stats.hunger:0}/{stats.maxHunger:0}       WATER  {stats.thirst:0}/{stats.maxThirst:0}";
                if (meleeAttacker != null) vitalsLabel.text += $"       MELEE  {meleeAttacker.attackDamage:0}";
            }
            foreach (ResourceDefinition resource in knownResources)
            {
                if (resource == null || !rows.TryGetValue(resource.id, out Text row)) continue;
                int count = inventory != null ? inventory.GetCount(resource.id) : 0;
                row.text = $"{resource.displayName}   x{count}";
                row.color = count > 0 ? Color.white : GameUI.Muted;
                if (actions.TryGetValue(resource.id, out Button action)) action.interactable = CanUse(resource);
            }
            foreach (BuildPieceDefinition recipe in knownRecipes)
            {
                if (recipe == null || !recipeRows.TryGetValue(recipe.id, out Text row)) continue;
                List<string> costs = new List<string>();
                if (recipe.craftingCost != null)
                {
                    foreach (CraftingCost cost in recipe.craftingCost)
                    {
                        if (cost == null || cost.resource == null || cost.amount <= 0) continue;
                        int owned = inventory != null ? inventory.GetCount(cost.resource.id) : 0;
                        costs.Add($"{cost.resource.displayName} {owned}:{cost.amount}");
                    }
                }
                bool affordable = CraftingSystem.TryAfford(inventory, recipe.craftingCost);
                row.text = (affordable ? "Ready  /  " : "Need supplies  /  ") +
                    (costs.Count == 0 ? "Free" : string.Join("   ", costs));
                row.color = affordable ? GameUI.Accent : GameUI.Muted;
            }
        }
    }
}
