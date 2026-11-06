using SheltreSim.Items;

namespace SheltreSim.Crafting
{
    /// <summary>One line item in a recipe: how much of a given resource is required.</summary>
    [System.Serializable]
    public class CraftingCost
    {
        public ResourceDefinition resource;
        public int amount = 1;
    }
}
