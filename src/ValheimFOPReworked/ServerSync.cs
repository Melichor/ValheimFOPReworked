using System;
using System.Collections.Generic;
using HarmonyLib;

namespace ValheimFOPReworked
{
    /// <summary>
    /// In multiplayer the server's settings apply to everyone, so every player levels items at the same rates. The
    /// server sends them as it accepts a player, and again whenever they change (its config file edited, or a setting
    /// changed in game). A player uses them until they leave; in single player, and on the server or host, the
    /// player's own config file applies.
    /// </summary>
    internal static class ServerSync
    {
        private const string Rpc = "melichor.ValheimFOPReworked.Settings";
        // Bumped if the message changes, so an older client ignores what it cannot read
        private const int Format = 1;

        private static bool s_changed;

        /// <summary>Called when a setting changes; the server sends the new settings on its next update.</summary>
        public static void SettingsChanged()
        {
            s_changed = true;
        }

        public static void Update()
        {
            if (!s_changed)
            {
                return;
            }
            s_changed = false;
            if (!ZNet.instance || !ZNet.instance.IsServer())
            {
                return;
            }
            foreach (ZNetPeer peer in ZNet.instance.GetConnectedPeers())
            {
                Send(peer.m_rpc);
            }
        }

        private static void Send(ZRpc rpc)
        {
            var package = new ZPackage();
            package.Write(Format);
            package.Write(Settings.All.Count);
            foreach (ISyncedSetting setting in Settings.All)
            {
                package.Write(setting.Key);
                package.Write(setting.OwnValue);
            }
            rpc.Invoke(Rpc, package);
        }

        private static void Receive(ZRpc rpc, ZPackage package)
        {
            if (!ZNet.instance || ZNet.instance.IsServer())
            {
                return;
            }
            int format = package.ReadInt();
            if (format != Format)
            {
                Plugin.Log.LogWarning($"The server sent its settings in format {format}, this version reads {Format}. " +
                                      "Using this game's own settings; the server and this game need the same mod version.");
                return;
            }
            var values = new Dictionary<string, string>();
            for (int count = package.ReadInt(); count > 0; count--)
            {
                string key = package.ReadString();
                values[key] = package.ReadString();
            }
            foreach (ISyncedSetting setting in Settings.All)
            {
                if (!values.TryGetValue(setting.Key, out string value))
                {
                    continue;
                }
                try
                {
                    setting.UseServerValue(value);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Cannot read the server's {setting.Key} ({value}): {e.Message}");
                }
            }
            Plugin.Log.LogInfo("Using the server's settings");
        }

        /// <summary>A new session (a world started or joined) begins with the player's own settings.</summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NewSession
        {
            private static void Postfix()
            {
                foreach (ISyncedSetting setting in Settings.All)
                {
                    setting.UseOwnValue();
                }
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
        private static class RegisterRpc
        {
            private static void Postfix(ZNetPeer peer) => peer.m_rpc.Register<ZPackage>(Rpc, Receive);
        }

        /// <summary>
        /// On the server, after a player's peer info: the settings follow it, unless the server turned the player away
        /// (wrong version or password), which removes their peer.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
        private static class SendToNewPlayer
        {
            private static void Postfix(ZNet __instance, ZRpc rpc)
            {
                if (__instance.IsServer() && __instance.GetPeer(rpc) != null)
                {
                    Send(rpc);
                }
            }
        }
    }
}
