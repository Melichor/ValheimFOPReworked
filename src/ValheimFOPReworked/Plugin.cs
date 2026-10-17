using BepInEx;
using HarmonyLib;
using ValheimFOPReworked.Patches;

namespace ValheimFOPReworked
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "melichor.ValheimFOPReworked";
        public const string Name = "Forge of Potential Reworked";
        public const string Version = "0.1.0";

        private readonly Harmony _harmony = new Harmony(Guid);

        private void Awake()
        {
            Settings.Bind(Config);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded");
        }

        private void OnDestroy()
        {
            _harmony.UnpatchSelf();
        }

        /// <summary>The game registers its own routed RPCs here, once per session.</summary>
        [HarmonyPatch(typeof(Game), nameof(Game.Start))]
        private static class RegisterRpcs
        {
            private static void Postfix() => WeaponXp.RegisterRpc();
        }
    }
}
