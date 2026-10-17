namespace ValheimFOPReworked
{
    internal static class Texts
    {
        public const string RefineButton = "Refine";
        public const string NotEnoughXp = "Not enough experience";
        public const string MaxLevel = "Maximum level reached";

        private const string Gold = "#FFD700";
        private const string Grey = "#B0B0B0";

        public static string Name(ItemDrop.ItemData item) => Localization.instance.Localize(item.m_shared.m_name);

        public static string ReadyMessage(ItemDrop.ItemData item) =>
            $"{Name(item)} is ready for the Forge of Potential";

        /// <summary>The line added to the item's tooltip.</summary>
        public static string Tooltip(ItemDrop.ItemData item)
        {
            if (ItemXp.IsMaxLevel(item))
            {
                return $"Experience: <color={Grey}>maximum level</color>";
            }
            if (ItemXp.IsFull(item))
            {
                return $"Experience: <color={Gold}>full, ready for the Forge of Potential</color>";
            }
            return $"Experience: <color=orange>{ItemXp.Get(item):N0}</color> / {ItemXp.Required(item):N0}";
        }

        /// <summary>The line under the item in the Forge of Potential, where the game warns about breaking.</summary>
        public static string ForgeStatus(ItemDrop.ItemData item, bool free)
        {
            int next = item.m_quality + 1;
            if (ItemXp.IsMaxLevel(item))
            {
                return $"<color={Grey}>{MaxLevel}</color>";
            }
            if (free)
            {
                return $"Refine to level {next}";
            }
            if (ItemXp.IsFull(item))
            {
                return $"<color={Gold}>Experience full. Refine to level {next}.</color>";
            }
            return $"Experience {ItemXp.Get(item):N0} / {ItemXp.Required(item):N0}. Use it more to refine it.";
        }
    }
}
