using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace ValheimFOPReworked
{
    /// <summary>
    /// A few recipes take idols two or more tiers away from the biome their item is crafted in. Most look copied from
    /// an earlier recipe: the Nord Atgeir takes bronze idols. These items are refined with idols of the right tier
    /// instead, of the same kind (battle or protection), and the experience they need follows the same tier.
    /// Found by comparing every recipe's idol with its item's crafting biome. The Scythe, Snow Shovel and Fishing Hat are
    /// off too, but keep the game's cheap idols: they are tools more than gear.
    ///
    /// One recipe has levels but no idol at all, so the Forge never lists its item: it gets the idol of the rest of
    /// its set.
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

        // Item name -> the item whose idol requirement it gets a copy of
        private static readonly Dictionary<string, string> MissingIdols = new Dictionary<string, string>
        {
            // Helmet of the Protector: its breastplate and trousers take Bloodgold protection idols
            { "$item_helmet_heavy_deepnorth", "$item_chest_heavy_deepnorth" },
        };

        private static void Apply(ObjectDB db)
        {
            AddMissingIdols(db);
            FixIdolTiers(db);
        }

        /// <summary>
        /// Gives the MissingIdols items a copy of their set's idol requirement: the same idol, the same amounts. Only
        /// while the recipe has none, so it is added once, and not at all should the game add one itself.
        /// </summary>
        private static void AddMissingIdols(ObjectDB db)
        {
            foreach (Recipe recipe in db.m_recipes)
            {
                if (!recipe || !recipe.m_item || recipe.m_resources == null ||
                    !MissingIdols.TryGetValue(recipe.m_item.m_itemData.m_shared.m_name, out string sibling) ||
                    recipe.m_resources.Any(requirement => requirement.m_upgraderResource))
                {
                    continue;
                }
                Piece.Requirement idol = db.m_recipes
                    .Where(other => other && other.m_item && other.m_resources != null &&
                                    other.m_item.m_itemData.m_shared.m_name == sibling)
                    .SelectMany(other => other.m_resources)
                    .FirstOrDefault(requirement => requirement.m_upgraderResource && requirement.m_resItem);
                if (idol == null)
                {
                    Plugin.Log.LogWarning($"{recipe.name}: {sibling} has no idol to copy");
                    continue;
                }
                recipe.m_resources = recipe.m_resources.Append(new Piece.Requirement
                {
                    m_resItem = idol.m_resItem,
                    m_amount = idol.m_amount,
                    m_extraAmountOnlyOneIngredient = idol.m_extraAmountOnlyOneIngredient,
                    m_amountPerLevel = idol.m_amountPerLevel,
                    m_upgraderResource = true,
                    m_recover = idol.m_recover,
                }).ToArray();
            }
        }

        /// <summary>Points the upgrader requirement of the Tiers recipes at the idol of the right tier.</summary>
        private static void FixIdolTiers(ObjectDB db)
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
