using System.Collections.Generic;
using HarmonyLib;

namespace ValheimFOPReworked
{
    /// <summary>
    /// A few recipes take idols two or more tiers away from the biome their item is crafted in. Most look copied from
    /// an earlier recipe: the Nord Atgeir takes bronze idols. These items are refined with idols of the right tier
    /// instead, of the same kind (battle or protection), and the experience they need follows the same tier.
    /// Found by comparing every recipe's idol with its item's crafting biome. The Scythe, Snow Shovel and Fishing Hat are
    /// off too, but keep the game's cheap idols: they are tools more than gear.
    /// </summary>
    internal static class IdolFixes
    {
        // Item name -> the idol tier it should take: 0 Meadows to 7 Deep North
        public static readonly Dictionary<string, int> Tiers = new Dictionary<string, int>
        {
            { "$item_atgeir_gold", 7 }, // Nord Atgeir: idol tier 1, crafted in the Deep North
            { "$item_helmet_berserker", 1 }, // Headdress of the Bear: idol tier 5, crafted in the Black Forest
            { "$item_axe_early", 0 }, // Early Axes: idol tier 2, crafted in the Meadows
            { "$item_fistweapon_gold", 7 }, // Nord Knucklechains: idol tier 2, crafted in the Deep North
            { "$item_axe_gold", 7 }, // Nord Axe: idol tier 4, crafted in the Deep North
            { "$item_axe_gold_frostfire", 7 }, // Frostfire Axe: idol tier 4, crafted in the Deep North
        };

        /// <summary>Points the upgrader requirement of these recipes at the idol of the right tier.</summary>
        private static void Apply(ObjectDB db)
        {
            // The game's idols are named "$item_upgrader_tier3 $item_upgrader_weapon $item_upgrader_name"
            var idols = new Dictionary<string, ItemDrop>();
            foreach (var prefab in db.m_items)
            {
                ItemDrop item = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (item && item.m_itemData.m_shared.m_name.StartsWith("$item_upgrader_tier"))
                {
                    idols[item.m_itemData.m_shared.m_name] = item;
                }
            }
            foreach (Recipe recipe in db.m_recipes)
            {
                if (!recipe || !recipe.m_item || recipe.m_resources == null ||
                    !Tiers.TryGetValue(recipe.m_item.m_itemData.m_shared.m_name, out int tier))
                {
                    continue;
                }
                foreach (Piece.Requirement requirement in recipe.m_resources)
                {
                    if (!requirement.m_upgraderResource || !requirement.m_resItem)
                    {
                        continue;
                    }
                    string current = requirement.m_resItem.m_itemData.m_shared.m_name;
                    string wanted = ItemXp.WithTier(current, tier);
                    if (wanted == current)
                    {
                        continue;
                    }
                    if (idols.TryGetValue(wanted, out ItemDrop idol))
                    {
                        requirement.m_resItem = idol;
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"{recipe.name}: no idol {wanted} to replace {current} with");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class OnAwake
        {
            private static void Postfix(ObjectDB __instance) => Apply(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class OnCopy
        {
            private static void Postfix(ObjectDB __instance) => Apply(__instance);
        }
    }
}
