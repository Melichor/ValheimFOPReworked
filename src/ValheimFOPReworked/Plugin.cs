using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ValheimFOPReworked.Patches;

namespace ValheimFOPReworked
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInIncompatibility(Compatibility.PotentialReforged)]
    [BepInIncompatibility(Compatibility.JGFoP)]
    [BepInIncompatibility(Compatibility.OdinBet)]
    [BepInIncompatibility(Compatibility.ForgeRework)]
    [BepInIncompatibility(Compatibility.ReforgedPotential)]
    [BepInIncompatibility(Compatibility.MySix)]
    [BepInIncompatibility(Compatibility.Redux)]
    [BepInIncompatibility(Compatibility.Goodtential)]
    [BepInIncompatibility(Compatibility.ForgeOdds)]
    [BepInIncompatibility(Compatibility.TruePotential)]
    [BepInIncompatibility(Compatibility.PotentialDraught)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "melichor.ValheimFOPReworked";
        public const string Name = "Forge of Potential Reworked";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;

        private readonly Harmony _harmony = new Harmony(Guid);
        private FileSystemWatcher _configWatcher;
        private volatile bool _configFileChanged;

        private void Awake()
        {
            Log = Logger;
            Settings.Bind(Config);
            Config.SettingChanged += (sender, args) => ServerSync.SettingsChanged();
            WatchConfigFile();
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Translations.AddToCurrentLanguage();
            Logger.LogInfo($"{Name} {Version} loaded");
        }

        private void Start()
        {
            Compatibility.WarnAboutOtherForgeMods();
        }

        private void Update()
        {
            if (_configFileChanged)
            {
                _configFileChanged = false;
                Config.Reload();
            }
            ServerSync.Update();
        }

        private void OnDestroy()
        {
            _configWatcher?.Dispose();
            _harmony.UnpatchSelf();
        }

        /// <summary>
        /// Reloads the config when its file is edited, so a server admin can change settings without a restart. The
        /// watcher reports on another thread; the reload happens in Update, on the game's thread.
        /// </summary>
        private void WatchConfigFile()
        {
            _configWatcher = new FileSystemWatcher(Path.GetDirectoryName(Config.ConfigFilePath),
                Path.GetFileName(Config.ConfigFilePath));
            _configWatcher.Changed += (sender, args) => _configFileChanged = true;
            _configWatcher.Created += (sender, args) => _configFileChanged = true;
            _configWatcher.Renamed += (sender, args) => _configFileChanged = true;
            _configWatcher.EnableRaisingEvents = true;
        }

        /// <summary>The game registers its own routed RPCs here, once per session.</summary>
        [HarmonyPatch(typeof(Game), nameof(Game.Start))]
        private static class RegisterRpcs
        {
            private static void Postfix() => WeaponXp.RegisterRpc();
        }
    }
}
