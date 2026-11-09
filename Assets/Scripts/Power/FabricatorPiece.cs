using System;
using System.Collections.Generic;
using System.Text;
using SheltreSim.Audio;
using SheltreSim.Crafting;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Power
{
    /// <summary>
    /// Automated crafting from crates. Inputs are taken once, all or nothing, when a job starts. A job then only needs
    /// power and a crate for the result; without them it waits and loses nothing. Changing the recipe leaves a running job alone.
    /// </summary>
    public sealed class FabricatorPiece : PowerConsumerDevice
    {
        private const float RowHeight = 58f;

        public float powerKw = 2f;
        public float heatPerSecond = 1f;
        public float secondsPerJob = 20f;
        public float crateRange = 8f;

        private string selectedId, jobId;
        private float progress;
        private readonly List<Recipe> rowRecipes = new List<Recipe>();
        private readonly List<Text> rowLabels = new List<Text>();
        private Text statusLabel;

        [Serializable]
        private class State { public string selected; public string job; public float progress; }

        public string SelectedRecipeId => SelectedRecipe != null ? selectedId : null;
        public string JobRecipeId => JobRecipe != null ? jobId : null;
        public float JobProgress => JobRecipe != null ? progress : 0f;
        public override string PowerName => "Fabricator";
        public override float PowerDemand => Blocker() == null ? powerKw : 0f;
        public override float HeatOutput => Granted ? heatPerSecond : 0f;

        public static bool CanMake(Recipe recipe) => recipe != null &&
            (recipe.station == StationKind.Hand || recipe.station == StationKind.Workbench || recipe.station == StationKind.Armory);

        private Recipe SelectedRecipe
        {
            get
            {
                Recipe recipe = RecipeBook.Get(selectedId);
                return CanMake(recipe) ? recipe : null;
            }
        }

        private Recipe JobRecipe
        {
            get
            {
                Recipe recipe = RecipeBook.Get(jobId);
                return CanMake(recipe) ? recipe : null;
            }
        }

        /// <summary>Exactly why it can't work right now, or null if it can.</summary>
        private string Blocker()
        {
            Recipe job = JobRecipe;
            Recipe selected = SelectedRecipe;
            if (job == null && selected == null) return "no recipe selected";
            if (StorageRegistry.Nearest(PowerPosition, crateRange) == null) return $"no crate within {crateRange:0} m";
            return job != null ? null : MissingInputs(selected);
        }

        private string MissingInputs(Recipe recipe)
        {
            StringBuilder missing = null;
            foreach (Ingredient input in recipe.inputs)
            {
                int short_ = input.amount - StorageRegistry.CountNearby(PowerPosition, crateRange, input.id);
                if (short_ <= 0) continue;
                if (missing == null) missing = new StringBuilder("crates are missing ");
                else missing.Append(", ");
                missing.Append(short_).Append(' ').Append(ItemCatalog.NameOf(input.id));
            }
            return missing?.ToString();
        }

        public override string PowerStatus
        {
            get
            {
                string blocker = Blocker();
                if (blocker != null) return blocker;
                Recipe job = JobRecipe;
                string doing = job != null ? $"making {ItemCatalog.NameOf(job.output)}, {PowerNetworks.FormatTime(secondsPerJob - progress)} left" : $"ready to make {ItemCatalog.NameOf(SelectedRecipe.output)}";
                return Granted ? doing : $"{NoPowerReason()} ({doing})";
            }
        }

        public override string InteractPrompt => $"Press E: Fabricator, {PowerStatus}";

        public override void Interact(GameObject interactor) => OpenPanel(interactor);

        /// <summary>Picks what the next job makes. Null stops new jobs. Returns false for a recipe it can't make.</summary>
        public bool SelectRecipe(string id)
        {
            if (id != null && !CanMake(RecipeBook.Get(id))) return false;
            selectedId = id;
            return true;
        }

        public override void Advance(float seconds)
        {
            if (!Granted) return;
            if (JobRecipe == null && !TryStartJob()) return;
            if (StorageRegistry.Nearest(PowerPosition, crateRange) == null) return;
            progress += seconds;
            if (progress < secondsPerJob) return;

            Recipe job = JobRecipe;
            if (!StorageRegistry.TryAddNearby(PowerPosition, crateRange, job.output, job.outputAmount)) return;
            jobId = null;
            progress = 0f;
            GameAudio.PlayAt(Sfx.Craft, transform.position, 0.5f, 15f);
        }

        /// <summary>All inputs or none. If a crate refuses midway, what was already taken goes back.</summary>
        private bool TryStartJob()
        {
            Recipe recipe = SelectedRecipe;
            if (recipe == null || StorageRegistry.Nearest(PowerPosition, crateRange) == null || MissingInputs(recipe) != null) return false;
            for (int i = 0; i < recipe.inputs.Length; i++)
            {
                Ingredient input = recipe.inputs[i];
                if (StorageRegistry.TryTakeNearby(PowerPosition, crateRange, input.id, input.amount)) continue;
                for (int j = 0; j < i; j++)
                    StorageRegistry.TryAddNearby(PowerPosition, crateRange, recipe.inputs[j].id, recipe.inputs[j].amount);
                return false;
            }
            jobId = recipe.id;
            progress = 0f;
            return true;
        }

        public override string CapturePieceState() =>
            JsonUtility.ToJson(new State { selected = selectedId ?? "", job = jobId ?? "", progress = progress });

        public override void RestorePieceState(string state)
        {
            State saved = PowerNetworks.ParseState<State>(state);
            if (saved == null) return;
            selectedId = string.IsNullOrEmpty(saved.selected) ? null : saved.selected;
            jobId = string.IsNullOrEmpty(saved.job) ? null : saved.job;
            progress = jobId != null ? PowerNetworks.Sane(saved.progress, 0f, secondsPerJob) : 0f;
        }

        protected override Vector2 PanelSize => new Vector2(1000f, 860f);

        protected override void BuildPanel(Transform panel)
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            statusLabel = GameUI.Label(panel, "Status", "", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(940f, 56f), 20, Color.white, TextAnchor.MiddleCenter);
            GameUI.Button(panel, "ClearRecipe", "STOP NEW JOBS", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 14f), new Vector2(220f, 42f), () => { SelectRecipe(null); RefreshPanel(); });

            rowRecipes.Clear();
            rowLabels.Clear();
            foreach (StationKind station in new[] { StationKind.Hand, StationKind.Workbench, StationKind.Armory })
                rowRecipes.AddRange(RecipeBook.ForStation(station));

            RectTransform content = GameUI.ScrollContent(panel, "Recipes", new Vector2(30f, -130f), new Vector2(940f, 610f));
            content.sizeDelta = new Vector2(0f, rowRecipes.Count * RowHeight);
            for (int i = 0; i < rowRecipes.Count; i++)
            {
                string id = rowRecipes[i].id;
                Button row = GameUI.Button(content, "Recipe_" + id, "", topLeft, topLeft, new Vector2(6f, -i * RowHeight), new Vector2(910f, RowHeight - 6f),
                    () => { SelectRecipe(id); RefreshPanel(); });
                Text label = row.GetComponentInChildren<Text>();
                label.fontSize = 17;
                rowLabels.Add(label);
            }
        }

        protected override void RefreshPanel()
        {
            statusLabel.text = PowerStatus;
            string selected = SelectedRecipeId;
            for (int i = 0; i < rowRecipes.Count; i++)
            {
                Recipe recipe = rowRecipes[i];
                rowLabels[i].text = (recipe.id == selected ? "> " : "") + Describe(recipe);
                rowLabels[i].color = recipe.id == selected ? GameUI.Accent : MissingInputs(recipe) == null ? Color.white : GameUI.Muted;
            }
        }

        private static string Describe(Recipe recipe)
        {
            var line = new StringBuilder();
            line.Append(recipe.outputAmount).Append(' ').Append(ItemCatalog.NameOf(recipe.output)).Append(" (").Append(RecipeBook.StationName(recipe.station)).Append("): ");
            for (int i = 0; i < recipe.inputs.Length; i++)
            {
                if (i > 0) line.Append(", ");
                line.Append(recipe.inputs[i].amount).Append(' ').Append(ItemCatalog.NameOf(recipe.inputs[i].id));
            }
            return line.ToString();
        }
    }
}
