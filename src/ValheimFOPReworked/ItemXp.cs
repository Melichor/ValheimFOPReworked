using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ValheimFOPReworked
{
    /// <summary>
    /// The experience an item has gathered towards its next refinement. It is stored in the item's custom data, so it
    /// is saved and sent over the network with the item wherever it goes. It stops at what the current level needs:
    /// experience never carries over into the next level.
    /// </summary>
    internal static class ItemXp
    {
        private const string Key = "melichor.ValheimFOPReworked.xp";

        // The items the Forge of Potential refines, by name, with their tier: the tier of the idol their recipe takes
        // (-1 when the idol does not say), or the one IdolFixes gives them. An item is refinable when its recipe has an
        // upgrader (idol) requirement and it has levels at all: the Forge skips items whose maximum quality is 1.
        private static readonly Dictionary<string, int> s_tiers = new Dictionary<string, int>();
        private static ObjectDB s_tiersDb;
        private static int s_tiersRecipes = -1;

        // The game's idols are named "$item_upgrader_tier3 ..." (prefab Upgrader3Weapon): 0 Meadows to 7 Deep North
        private static readonly Regex TierInName = new Regex(@"tier(\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex TierInPrefab = new Regex(@"^Upgrader(\d+)", RegexOptions.IgnoreCase);

        private static string s_scaleText;
        private static float[] s_scales = new float[0];

        private static Dictionary<string, int> Tiers()
        {
            ObjectDB db = ObjectDB.instance;
            if (db && (db != s_tiersDb || db.m_recipes.Count != s_tiersRecipes))
            {
                s_tiers.Clear();
                foreach (Recipe recipe in db.m_recipes)
                {
                    if (!recipe || !recipe.m_item || recipe.m_resources == null ||
                        recipe.m_item.m_itemData.m_shared.m_maxQuality <= 1)
                    {
                        continue;
                    }
                    foreach (Piece.Requirement requirement in recipe.m_resources)
                    {
                        if (requirement.m_upgraderResource && requirement.m_resItem)
                        {
                            string name = recipe.m_item.m_itemData.m_shared.m_name;
                            s_tiers[name] = IdolFixes.Tiers.TryGetValue(name, out int tier)
                                ? tier
                                : IdolTier(requirement.m_resItem);
                            break;
                        }
                    }
                }
                s_tiersDb = db;
                s_tiersRecipes = db.m_recipes.Count;
            }
            return s_tiers;
        }

        /// <summary>The name of the same kind of idol (battle or protection) of another tier.</summary>
        public static string WithTier(string idolName, int tier) =>
            TierInName.Replace(idolName, "tier" + tier.ToString(CultureInfo.InvariantCulture), 1);

        private static int IdolTier(ItemDrop idol)
        {
            Match match = TierInName.Match(idol.m_itemData.m_shared.m_name);
            if (!match.Success)
            {
                match = TierInPrefab.Match(idol.name);
            }
            return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        }

        /// <summary>The item's tier's multiplier from TierXpScale, 1 when the tier is not known.</summary>
        public static float TierScale(ItemDrop.ItemData item)
        {
            string text = Settings.TierXpScale.Value;
            if (text != s_scaleText)
            {
                var scales = new List<float>();
                foreach (string part in text.Split(','))
                {
                    scales.Add(float.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) &&
                               v > 0f ? v : 1f);
                }
                s_scales = scales.ToArray();
                s_scaleText = text;
            }
            return Tiers().TryGetValue(item.m_shared.m_name, out int tier) && tier >= 0 && tier < s_scales.Length
                ? s_scales[tier]
                : 1f;
        }

        public static bool IsRefinable(ItemDrop.ItemData item) =>
            item != null && Tiers().ContainsKey(item.m_shared.m_name);

        public static bool IsMaxLevel(ItemDrop.ItemData item) => item.m_quality >= Settings.MaxLevel.Value;

        public static bool CanEarn(ItemDrop.ItemData item) => IsRefinable(item) && !IsMaxLevel(item);

        /// <summary>Experience the item needs at its current level before it can be refined.</summary>
        public static float Required(ItemDrop.ItemData item) =>
            Mathf.Round(Settings.XpBase.Value * TierScale(item) *
                        Mathf.Pow(Mathf.Max(1, item.m_quality), Settings.XpExponent.Value));

        public static float Get(ItemDrop.ItemData item)
        {
            if (item.m_customData != null && item.m_customData.TryGetValue(Key, out string text) &&
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float xp))
            {
                return xp;
            }
            return 0f;
        }

        public static bool IsFull(ItemDrop.ItemData item) => Get(item) >= Required(item);

        public static void Reset(ItemDrop.ItemData item)
        {
            item.m_customData?.Remove(Key);
        }

        /// <summary>One use of a tool: ToolUseXp, scaled by the tool's tier like the experience it needs.</summary>
        public static void AddUse(ItemDrop.ItemData item)
        {
            if (CanEarn(item))
            {
                Add(item, Settings.ToolUseXp.Value * TierScale(item));
            }
        }

        public static void Add(ItemDrop.ItemData item, float amount)
        {
            if (amount <= 0f || !CanEarn(item))
            {
                return;
            }
            float required = Required(item);
            float had = Get(item);
            if (had >= required)
            {
                return;
            }
            float now = Mathf.Min(required, had + amount);
            if (item.m_customData == null)
            {
                item.m_customData = new Dictionary<string, string>();
            }
            item.m_customData[Key] = now.ToString("0.##", CultureInfo.InvariantCulture);
            if (now >= required)
            {
                Player.m_localPlayer?.Message(MessageHud.MessageType.Center, Texts.ReadyMessage(item));
            }
        }
    }
}
