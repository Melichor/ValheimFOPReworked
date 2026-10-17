namespace ValheimFOPReworked
{
    /// <summary>The mod's text, translated through the game's localization (see Translations).</summary>
    internal static class Texts
    {
        private const string Gold = "#FFD700";
        private const string Grey = "#B0B0B0";

        private static string L(string token, params string[] words) => Localization.instance.Localize(token, words);

        public static string RefineButton => L("$fopreworked_refine");

        public static string NotEnoughXp => L("$fopreworked_not_enough_xp");

        public static string MaxLevel => L("$fopreworked_max_level");

        public static string Name(ItemDrop.ItemData item) => L(item.m_shared.m_name);

        public static string ReadyMessage(ItemDrop.ItemData item) => L("$fopreworked_ready", Name(item));

        /// <summary>The line added to the item's tooltip.</summary>
        public static string Tooltip(ItemDrop.ItemData item)
        {
            string label = L("$fopreworked_xp");
            if (ItemXp.IsMaxLevel(item))
            {
                return $"{label}: <color={Grey}>{L("$fopreworked_xp_max")}</color>";
            }
            if (ItemXp.IsFull(item))
            {
                return $"{label}: <color={Gold}>{L("$fopreworked_xp_full")}</color>";
            }
            return $"{label}: <color=orange>{ItemXp.Get(item):N0}</color> / {ItemXp.Required(item):N0}";
        }

        /// <summary>The line under the item in the Forge of Potential, where the game warns about breaking.</summary>
        public static string ForgeStatus(ItemDrop.ItemData item, bool free)
        {
            string next = (item.m_quality + 1).ToString();
            if (ItemXp.IsMaxLevel(item))
            {
                return $"<color={Grey}>{MaxLevel}</color>";
            }
            if (free)
            {
                return L("$fopreworked_refine_to", next);
            }
            if (ItemXp.IsFull(item))
            {
                return $"<color={Gold}>{L("$fopreworked_xp_full_refine_to", next)}</color>";
            }
            return L("$fopreworked_xp_progress", ItemXp.Get(item).ToString("N0"), ItemXp.Required(item).ToString("N0"));
        }
    }
}
