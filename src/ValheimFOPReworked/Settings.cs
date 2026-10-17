using System.Collections.Generic;
using BepInEx.Configuration;

namespace ValheimFOPReworked
{
    /// <summary>
    /// The mod's settings. In multiplayer the server's settings apply to everyone (see ServerSync); each player's own
    /// config file is used again when they leave.
    /// </summary>
    internal static class Settings
    {
        /// <summary>Every setting, in the order the server sends them.</summary>
        public static readonly List<ISyncedSetting> All = new List<ISyncedSetting>();

        public static Setting<int> MaxLevel;
        public static Setting<int> LevelsPerIdol;
        public static Setting<float> XpBase;
        public static Setting<float> XpExponent;
        public static Setting<string> TierXpScale;
        public static Setting<float> DamageXp;
        public static Setting<float> KillXp;
        public static Setting<float> ArmorXp;
        public static Setting<float> BlockXp;
        public static Setting<float> ParryMultiplier;
        public static Setting<float> ResourceXp;
        public static Setting<float> ToolUseXp;
        public static Setting<float> CastXp;

        public static void Bind(ConfigFile config)
        {
            All.Clear();
            MaxLevel = Add(config.Bind("Forge", "MaxLevel", 25,
                "The highest level the Forge of Potential can refine an item to."));
            LevelsPerIdol = Add(config.Bind("Forge", "LevelsPerIdol", 5,
                "Idols a refinement takes: the new level divided by this, rounded up. With 5, refining to levels 2-5 " +
                "takes 1 idol, 6-10 takes 2, 11-15 takes 3, 16-20 takes 4 and 21-25 takes 5."));

            XpBase = Add(config.Bind("Experience", "XpBase", 1000f,
                "Experience an item needs before it can be refined: " +
                "XpBase * (its tier's TierXpScale) * (current level ^ XpExponent)."));
            XpExponent = Add(config.Bind("Experience", "XpExponent", 1.5f,
                "See XpBase. For a tier with scale 1, a level 1 item needs 1,000, level 4 needs 8,000, level 10 " +
                "needs 31,600 and level 24 needs 117,600."));
            TierXpScale = Add(config.Bind("Experience", "TierXpScale", "0.15, 0.4, 0.45, 0.55, 0.85, 0.9, 1.6, 4",
                "How much experience items of each tier need, as multipliers of XpBase, for the idol tiers 0 to 7 " +
                "(Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North). Experience " +
                "comes from creatures' health, which grows with the biome, so these follow how much experience a " +
                "typical kill in that biome gives: refining an item with the creatures of its own biome takes about " +
                "as many kills at every tier. Items whose tier is not known use 1."));

            DamageXp = Add(config.Bind("Experience", "DamageXp", 1f,
                "Weapons: experience per point of damage dealt to a creature (after its resistances, no overkill)."));
            KillXp = Add(config.Bind("Experience", "KillXp", 0.5f,
                "Weapons: extra experience for the killing blow, per point of the creature's maximum health."));
            ArmorXp = Add(config.Bind("Experience", "ArmorXp", 2f,
                "Armour: experience per point of damage from a creature's hit, taken or blocked, for every worn piece."));
            BlockXp = Add(config.Bind("Experience", "BlockXp", 2f,
                "Shields: experience per point of damage blocked."));
            ParryMultiplier = Add(config.Bind("Experience", "ParryMultiplier", 2f,
                "Shields: a parry (perfect block) gives this many times the block experience."));
            ResourceXp = Add(config.Bind("Experience", "ResourceXp", 1f,
                "Pickaxes and axes: experience per point of pickaxe or chop damage dealt to rocks, ore and trees."));
            ToolUseXp = Add(config.Bind("Experience", "ToolUseXp", 20f,
                "Hammer, hoe, cultivator, serving tray, scythe and snow shovel: experience for each piece placed, " +
                "piece repaired, plant harvested or shovelful of snow, times the tool's TierXpScale, so a use is " +
                "worth the same part of the bar for every tool."));
            CastXp = Add(config.Bind("Experience", "CastXp", 2f,
                "Staffs that deal no damage themselves (summoning, shielding): experience per point of eitr or health " +
                "a cast costs."));
        }

        private static Setting<T> Add<T>(ConfigEntry<T> entry)
        {
            var setting = new Setting<T>(entry);
            All.Add(setting);
            return setting;
        }
    }

    internal interface ISyncedSetting
    {
        /// <summary>"Section.Key", the same on every machine.</summary>
        string Key { get; }

        /// <summary>This machine's own value, as the config file writes it.</summary>
        string OwnValue { get; }

        void UseServerValue(string value);

        void UseOwnValue();
    }

    /// <summary>A setting from the config file, which the server's value replaces while connected to it.</summary>
    internal sealed class Setting<T> : ISyncedSetting
    {
        private readonly ConfigEntry<T> _entry;
        private T _serverValue;
        private bool _fromServer;

        public Setting(ConfigEntry<T> entry)
        {
            _entry = entry;
        }

        public T Value => _fromServer ? _serverValue : _entry.Value;

        public string Key => _entry.Definition.Section + "." + _entry.Definition.Key;

        public string OwnValue => _entry.GetSerializedValue();

        public void UseServerValue(string value)
        {
            _serverValue = (T)TomlTypeConverter.ConvertToValue(value, typeof(T));
            _fromServer = true;
        }

        public void UseOwnValue()
        {
            _fromServer = false;
        }
    }
}
