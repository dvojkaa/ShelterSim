namespace SheltreSim.Building
{
    /// <summary>Shared material response for player tools and firearms against placed structures.</summary>
    public static class StructureDamageRules
    {
        public static float FirearmMultiplier(HomesteadMaterial material) => material switch
        {
            HomesteadMaterial.Wood => 0.18f,
            HomesteadMaterial.Stone => 0.08f,
            _ => 0.04f,
        };

        public static float ToolDamage(string toolId, HomesteadMaterial material)
        {
            float baseDamage;
            float wood;
            float stone;
            float metal;
            switch (toolId)
            {
                case "hammer": baseDamage = 24f; wood = .8f; stone = .75f; metal = .55f; break;
                case "stone_axe": baseDamage = 26f; wood = 1f; stone = .45f; metal = .22f; break;
                case "iron_axe": baseDamage = 38f; wood = 1f; stone = .45f; metal = .22f; break;
                case "steel_axe": baseDamage = 52f; wood = 1f; stone = .45f; metal = .22f; break;
                case "stone_pickaxe": baseDamage = 24f; wood = .3f; stone = 1f; metal = .9f; break;
                case "iron_pickaxe": baseDamage = 38f; wood = .3f; stone = 1f; metal = .9f; break;
                case "steel_pickaxe": baseDamage = 52f; wood = .3f; stone = 1f; metal = .9f; break;
                default: return 0f;
            }

            float multiplier = material == HomesteadMaterial.Wood ? wood :
                material == HomesteadMaterial.Stone ? stone : metal;
            return baseDamage * multiplier;
        }

        public static float MeleeWeaponMultiplier(HomesteadMaterial material) =>
            material == HomesteadMaterial.Wood ? .75f : material == HomesteadMaterial.Stone ? .55f : .35f;
    }
}
