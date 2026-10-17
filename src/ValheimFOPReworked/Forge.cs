using UnityEngine;

namespace ValheimFOPReworked
{
    /// <summary>
    /// Refinement at the Forge of Potential: no chance and no breaking. An item can be refined once its experience bar
    /// is full, for a number of idols that grows with the level.
    /// </summary>
    internal static class Forge
    {
        public static int IdolCost(int newLevel) =>
            Mathf.Max(1, Mathf.CeilToInt((float)newLevel / Mathf.Max(1, Settings.LevelsPerIdol.Value)));

        public static bool AtForge(Player player)
        {
            CraftingStation station = player.GetCurrentCraftingStation();
            return station && station.m_upgrader;
        }

        /// <summary>The "nocost" cheat and the "no crafting cost" world setting skip the experience too.</summary>
        public static bool IsFree(Player player) =>
            player.NoCostCheat() || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);

        public static bool IsReady(ItemDrop.ItemData item, Player player) =>
            !ItemXp.IsMaxLevel(item) && (IsFree(player) || ItemXp.IsFull(item));

        /// <summary>Takes the place of the game's roll in InventoryGui.DoCrafting when the station is the Forge.</summary>
        public static void Refine(InventoryGui gui, Player player, CraftingStation station)
        {
            ItemDrop.ItemData item = gui.m_craftUpgradeItem;
            Recipe recipe = gui.m_craftRecipe;
            if (item == null || !recipe || !player.GetInventory().ContainsItem(item))
            {
                return;
            }
            int level = item.m_quality + 1;
            bool free = IsFree(player);
            if (ItemXp.IsMaxLevel(item))
            {
                player.Message(MessageHud.MessageType.Center, Texts.MaxLevel);
                return;
            }
            if (!free && !ItemXp.IsFull(item))
            {
                player.Message(MessageHud.MessageType.Center, Texts.NotEnoughXp);
                return;
            }
            if (!free && !player.HaveRequirements(recipe, discover: false, level))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_missingrequirement");
                return;
            }
            if (!free)
            {
                // At the Forge only the upgrader requirement (the idols) is taken, see Piece.Requirement.GetAmount
                player.ConsumeResources(recipe.m_resources, level);
            }
            bool cheated = player.NoCostCheat() && !PlayerProfile.s_bypassCheatChecks;

            // The item is refined in place, so it keeps its crafter, variant and any other mod's data
            item.m_quality = level;
            item.m_durability = item.GetMaxDurability();
            item.m_cheated |= cheated;
            ItemXp.Reset(item);
            player.GetInventory().Changed();

            station.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            player.Message(MessageHud.MessageType.Center,
                Localization.instance.Localize("$msg_upgrader_success", item.m_shared.m_name, level.ToString()),
                0, null, log: true);
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            profile.IncrementStat(PlayerStatType.CraftsOrUpgrades, 1f, cheated);
            profile.IncrementStat(PlayerStatType.Upgrades, 1f, cheated);
            gui.UpdateCraftingPanel();
        }
    }
}
