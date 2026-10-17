using HarmonyLib;
using TMPro;

namespace ValheimFOPReworked.Patches
{
    internal static class ForgePatches
    {
        /// <summary>
        /// The idols a refinement takes. Upgrader requirements only count at the Forge, and every check, display and
        /// consumption of them goes through GetAmount with the level the item is refined to.
        /// </summary>
        [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
        private static class IdolCost
        {
            private static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result)
            {
                if (__instance.m_upgraderResource)
                {
                    __result = Forge.IdolCost(qualityLevel);
                }
            }
        }

        /// <summary>Items that are not ready are listed greyed out at the Forge.</summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.AddRecipeToList))]
        private static class GreyOutUnready
        {
            private static void Prefix(Player player, ItemDrop.ItemData item, ref bool canCraft)
            {
                if (canCraft && item != null && Forge.AtForge(player) && !Forge.IsReady(item, player))
                {
                    canCraft = false;
                }
            }
        }

        /// <summary>Shows the experience instead of the breaking warning, and only lets a ready item be refined.</summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
        private static class ShowExperience
        {
            private static void Postfix(InventoryGui __instance, Player player)
            {
                ItemDrop.ItemData item = __instance.m_selectedRecipe.ItemData;
                if (!__instance.m_selectedRecipe.Recipe || item == null || !Forge.AtForge(player))
                {
                    return;
                }
                __instance.m_itemCraftType.text = Texts.ForgeStatus(item, Forge.IsFree(player));
                __instance.m_craftButton.GetComponentInChildren<TMP_Text>().text = Texts.RefineButton;
                if (!Forge.IsReady(item, player))
                {
                    __instance.m_craftButton.interactable = false;
                    __instance.m_craftButton.GetComponent<UITooltip>().m_text =
                        ItemXp.IsMaxLevel(item) ? Texts.MaxLevel : Texts.NotEnoughXp;
                }
            }
        }

        /// <summary>
        /// The game wants free inventory slots for the materials a broken item gives back. Nothing breaks any more, so
        /// the check is skipped by hiding the Forge's upgrader flag while the button press is handled.
        /// </summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftPressed))]
        private static class SkipFreeSlotCheck
        {
            private static void Prefix(out CraftingStation __state)
            {
                __state = null;
                CraftingStation station = Player.m_localPlayer ? Player.m_localPlayer.GetCurrentCraftingStation() : null;
                if (station && station.m_upgrader)
                {
                    station.m_upgrader = false;
                    __state = station;
                }
            }

            private static void Finalizer(CraftingStation __state)
            {
                if (__state)
                {
                    __state.m_upgrader = true;
                }
            }
        }

        /// <summary>The refinement itself: always succeeds, see Forge.Refine.</summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        private static class Refine
        {
            private static bool Prefix(InventoryGui __instance, Player player)
            {
                CraftingStation station = player.GetCurrentCraftingStation();
                if (!station || !station.m_upgrader)
                {
                    return true;
                }
                Forge.Refine(__instance, player, station);
                return false;
            }
        }
    }
}
