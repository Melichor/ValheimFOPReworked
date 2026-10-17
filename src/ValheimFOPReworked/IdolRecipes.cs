using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ValheimFOPReworked
{
    /// <summary>
    /// Recipes for the idols. Each tier's idols are made from that tier's boss item and a trophy of the next tier, at the
    /// best station that tier reaches just after its boss. Bloodgold has no next tier, so it takes a rare Ashlands
    /// trophy instead. The recipe shows up once the player has picked up both ingredients and seen the station at that
    /// level, as with any recipe.
    /// </summary>
    internal static class IdolRecipes
    {
        private struct Tier
        {
            public string Station;
            public int Level;
            public string BossItem;
            public int BossItems;
            public string BattleTrophy;
            public string ProtectionTrophy;
        }

        // Prefab names. The Black Forest troll's trophy is called TrophyFrostTroll in the game files.
        private static readonly Tier[] Tiers =
        {
            // 0 Wooden: Eikthyr's Hard Antler (3 per kill)
            new Tier { Station = "piece_workbench", Level = 3, BossItem = "HardAntler", BossItems = 1,
                BattleTrophy = "TrophyFrostTroll", ProtectionTrophy = "TrophyGreydwarfShaman" },
            // 1 Bronze: the Elder's Swamp Key (1 per player per kill)
            new Tier { Station = "forge", Level = 3, BossItem = "CryptKey", BossItems = 1,
                BattleTrophy = "TrophyDraugrElite", ProtectionTrophy = "TrophyAbomination" },
            // 2 Iron: Bonemass's Wishbone (1 per player per kill)
            new Tier { Station = "forge", Level = 7, BossItem = "Wishbone", BossItems = 1,
                BattleTrophy = "TrophyHatchling", ProtectionTrophy = "TrophyWolf" },
            // 3 Silver: Moder's Dragon Tear (10 per kill), 5 per idol
            new Tier { Station = "piece_artisanstation", Level = 1, BossItem = "DragonTear", BossItems = 5,
                BattleTrophy = "TrophyDeathsquito", ProtectionTrophy = "TrophyLox" },
            // 4 Black Metal: Yagluth's Torn Spirit (3 per kill)
            new Tier { Station = "forge", Level = 7, BossItem = "YagluthDrop", BossItems = 1,
                BattleTrophy = "TrophyGjall", ProtectionTrophy = "TrophyHare" },
            // 5 Black Marble: the Queen's Majestic Carapace (5 per kill), 2 per idol
            new Tier { Station = "blackforge", Level = 3, BossItem = "QueenDrop", BossItems = 2,
                BattleTrophy = "TrophyBonemawSerpent", ProtectionTrophy = "TrophyAsksvin" },
            // 6 Flametal: Fader's Kindled Ribs (5 per kill), 2 per idol
            new Tier { Station = "blackforge", Level = 5, BossItem = "FaderDrop", BossItems = 2,
                BattleTrophy = "TrophyBarka", ProtectionTrophy = "TrophyJotunWarrior" },
            // 7 Bloodgold: the Frozen King's Crown Jewel (1 per kill), with a rare Ashlands trophy (5% drops)
            new Tier { Station = "blackforge", Level = 6, BossItem = "CrownJewel", BossItems = 1,
                BattleTrophy = "TrophyFallenValkyrie", ProtectionTrophy = "TrophyMorgen" },
        };

        private const string RecipePrefix = "Recipe_FOPReworked_";

        private static void Add(ObjectDB db)
        {
            if (db.m_items.Count == 0)
            {
                return;
            }
            // Stations are pieces, not items: take them from the recipes that already use them
            var stations = new Dictionary<string, CraftingStation>();
            var existing = new HashSet<string>();
            foreach (Recipe recipe in db.m_recipes)
            {
                if (!recipe)
                {
                    continue;
                }
                existing.Add(recipe.name);
                if (recipe.m_craftingStation)
                {
                    stations[recipe.m_craftingStation.name] = recipe.m_craftingStation;
                }
            }
            for (int tier = 0; tier < Tiers.Length; tier++)
            {
                Tier t = Tiers[tier];
                AddRecipe(db, existing, stations, $"Upgrader{tier}Weapon", t, t.BattleTrophy);
                AddRecipe(db, existing, stations, $"Upgrader{tier}Armor", t, t.ProtectionTrophy);
            }
        }

        private static void AddRecipe(ObjectDB db, HashSet<string> existing, Dictionary<string, CraftingStation> stations,
            string idolName, Tier tier, string trophyName)
        {
            string name = RecipePrefix + idolName;
            if (existing.Contains(name))
            {
                return;
            }
            ItemDrop idol = Item(db, idolName);
            ItemDrop bossItem = Item(db, tier.BossItem);
            ItemDrop trophy = Item(db, trophyName);
            stations.TryGetValue(tier.Station, out CraftingStation station);
            var missing = new List<string>();
            void Need(Object found, string what)
            {
                if (!found)
                {
                    missing.Add(what);
                }
            }
            Need(idol, idolName);
            Need(bossItem, tier.BossItem);
            Need(trophy, trophyName);
            Need(station, tier.Station);
            if (missing.Count > 0)
            {
                Plugin.Log.LogWarning($"{name} is not added, the game has no {string.Join(", ", missing)}");
                return;
            }
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = name;
            recipe.m_item = idol;
            recipe.m_amount = 1;
            recipe.m_craftingStation = station;
            recipe.m_minStationLevel = tier.Level;
            recipe.m_resources = new[]
            {
                new Piece.Requirement { m_resItem = bossItem, m_amount = tier.BossItems, m_amountPerLevel = 0 },
                new Piece.Requirement { m_resItem = trophy, m_amount = 1, m_amountPerLevel = 0 },
            };
            db.m_recipes.Add(recipe);
            existing.Add(name);
        }

        private static ItemDrop Item(ObjectDB db, string prefab)
        {
            GameObject go = db.GetItemPrefab(prefab);
            return go ? go.GetComponent<ItemDrop>() : null;
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class OnAwake
        {
            private static void Postfix(ObjectDB __instance) => Add(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class OnCopy
        {
            private static void Postfix(ObjectDB __instance) => Add(__instance);
        }
    }
}
