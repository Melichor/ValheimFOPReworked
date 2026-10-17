using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ValheimFOPReworked
{
    /// <summary>
    /// Other Forge of Potential mods change the refinement roll, which this mod replaces altogether: with both, their
    /// changes do nothing or the two fight over the result. The known ones are declared with BepInIncompatibility on
    /// the plugin, so BepInEx does not load this mod next to them. Any other mod that patches the refinement is named in
    /// a warning at start.
    /// </summary>
    internal static class Compatibility
    {
        // Plugin IDs of known Forge mods (Thunderstore package in the comment). These five are from the config file
        // names their READMEs give, which BepInEx names after the plugin ID.
        public const string PotentialReforged = "Dyls123.PotentialReforged"; // Dyls123-PotentialReforged
        public const string JGFoP = "com.jgmods.jgfop"; // JGSTUDIOS-JGFoP
        public const string OdinBet = "OdinBet.ForgeOfPotential"; // Igao-OdinBet_ForgeOfPotential
        public const string ForgeRework = "MerlinofMines.ForgeRework"; // MerlinofMines-ForgeRework
        public const string ReforgedPotential = "akuichi.ReforgedPotential"; // Akuichi-ReforgedPotential

        // These are read from the mods' plugins. All of them patch InventoryGui.DoCrafting.
        public const string MySix = "com.mysix.forgeofpotential"; // MysixValheimMods-MySix_ForgeOfPotential
        public const string Redux = "com.dreich.fopredux"; // Dreich-ForgeOfPotentialRedux
        public const string Goodtential = "wire.valheim.forgeofgoodtential"; // Wire-WiresForgeOfGoodtential
        public const string ForgeOdds = "sansara.ForgeOdds"; // Sansara-ForgeOdds
        public const string TruePotential = "beijingcarlo.forgeoftruepotential"; // beijing_carlo-ForgeOfTruePotential
        public const string PotentialDraught = "com.forgepotential.potentialdraught"; // LOSDC-PotentialDraught
        // Not listed: TeflonTed-SurePotential only sets every idol to 100% success and no breaking, which does nothing
        // with this mod (it has no roll) and does no harm either.

        /// <summary>Runs once every plugin has loaded and applied its patches.</summary>
        public static void WarnAboutOtherForgeMods()
        {
            MethodBase[] refinement =
            {
                AccessTools.Method(typeof(Player), nameof(Player.GetItemUpgradeChance)),
                AccessTools.Method(typeof(InventoryGui), nameof(InventoryGui.DoCrafting)),
            };
            string[] others = refinement
                .Select(Harmony.GetPatchInfo)
                .Where(info => info != null)
                .SelectMany(info => info.Owners)
                .Where(owner => owner != Plugin.Guid)
                .Distinct()
                .ToArray();
            if (others.Length > 0)
            {
                Plugin.Log.LogWarning("These mods also change crafting or refining, and may not work at the Forge of " +
                                      $"Potential together with this one: {string.Join(", ", others)}");
            }
        }
    }
}
