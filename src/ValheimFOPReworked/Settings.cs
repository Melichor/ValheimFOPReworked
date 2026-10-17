using BepInEx.Configuration;

namespace ValheimFOPReworked
{
    internal static class Settings
    {
        public static ConfigEntry<int> MaxLevel;
        public static ConfigEntry<int> LevelsPerIdol;
        public static ConfigEntry<float> XpBase;
        public static ConfigEntry<float> XpExponent;
        public static ConfigEntry<string> TierXpScale;
        public static ConfigEntry<float> DamageXp;
        public static ConfigEntry<float> KillXp;
        public static ConfigEntry<float> ArmorXp;
        public static ConfigEntry<float> BlockXp;
        public static ConfigEntry<float> ParryMultiplier;
        public static ConfigEntry<float> ResourceXp;
        public static ConfigEntry<float> ToolUseXp;
        public static ConfigEntry<float> CastXp;

        public static void Bind(ConfigFile config)
        {
            MaxLevel = config.Bind("Forge", "MaxLevel", 25,
                "The highest level the Forge of Potential can refine an item to.");
            LevelsPerIdol = config.Bind("Forge", "LevelsPerIdol", 5,
                "Idols a refinement takes: the new level divided by this, rounded up. With 5, refining to levels 2-5 " +
                "takes 1 idol, 6-10 takes 2, 11-15 takes 3, 16-20 takes 4 and 21-25 takes 5.");

            XpBase = config.Bind("Experience", "XpBase", 1000f,
                "Experience an item needs before it can be refined: " +
                "XpBase * (its tier's TierXpScale) * (current level ^ XpExponent).");
            XpExponent = config.Bind("Experience", "XpExponent", 1.5f,
                "See XpBase. For a tier with scale 1, a level 1 item needs 1,000, level 4 needs 8,000, level 10 " +
                "needs 31,600 and level 24 needs 117,600.");
            TierXpScale = config.Bind("Experience", "TierXpScale", "0.15, 0.4, 0.45, 0.55, 0.85, 0.9, 1.6, 4",
                "How much experience items of each tier need, as multipliers of XpBase, for the idol tiers 0 to 7 " +
                "(Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North). Experience " +
                "comes from creatures' health, which grows with the biome, so these follow how much experience a " +
                "typical kill in that biome gives: refining an item with the creatures of its own biome takes about " +
                "as many kills at every tier. Items whose tier is not known use 1.");

            DamageXp = config.Bind("Experience", "DamageXp", 1f,
                "Weapons: experience per point of damage dealt to a creature (after its resistances, no overkill).");
            KillXp = config.Bind("Experience", "KillXp", 0.5f,
                "Weapons: extra experience for the killing blow, per point of the creature's maximum health.");
            ArmorXp = config.Bind("Experience", "ArmorXp", 2f,
                "Armour: experience per point of damage from a creature's hit, taken or blocked, for every worn piece.");
            BlockXp = config.Bind("Experience", "BlockXp", 2f,
                "Shields: experience per point of damage blocked.");
            ParryMultiplier = config.Bind("Experience", "ParryMultiplier", 2f,
                "Shields: a parry (perfect block) gives this many times the block experience.");
            ResourceXp = config.Bind("Experience", "ResourceXp", 1f,
                "Pickaxes and axes: experience per point of pickaxe or chop damage dealt to rocks, ore and trees.");
            ToolUseXp = config.Bind("Experience", "ToolUseXp", 20f,
                "Hammer, hoe, cultivator, serving tray, scythe and snow shovel: experience for each piece placed, " +
                "piece repaired, plant harvested or shovelful of snow, times the tool's TierXpScale, so a use is " +
                "worth the same part of the bar for every tool.");
            CastXp = config.Bind("Experience", "CastXp", 2f,
                "Staffs that deal no damage themselves (summoning, shielding): experience per point of eitr or health " +
                "a cast costs.");
        }
    }
}
