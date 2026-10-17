using System.Globalization;
using HarmonyLib;

namespace ValheimFOPReworked
{
    /// <summary>Console commands for testing. They are cheats: they need devcommands.</summary>
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
    internal static class Commands
    {
        private static void Postfix()
        {
            new Terminal.ConsoleCommand("fopxp",
                "[fill|clear|amount] - fill, clear or add to the Forge of Potential experience of your equipped items",
                args =>
                {
                    Player player = Player.m_localPlayer;
                    if (!player)
                    {
                        return;
                    }
                    string what = args.Length > 1 ? args[1] : "fill";
                    float amount = 0f;
                    if (what != "fill" && what != "clear" &&
                        !float.TryParse(what, NumberStyles.Float, CultureInfo.InvariantCulture, out amount))
                    {
                        args.Context.AddString("Usage: fopxp [fill|clear|amount]");
                        return;
                    }
                    foreach (ItemDrop.ItemData item in player.GetInventory().GetEquippedItems())
                    {
                        if (!ItemXp.IsRefinable(item))
                        {
                            continue;
                        }
                        if (what == "clear")
                        {
                            ItemXp.Reset(item);
                        }
                        else
                        {
                            ItemXp.Add(item, what == "fill" ? ItemXp.Required(item) : amount);
                        }
                        args.Context.AddString($"{Texts.Name(item)} (level {item.m_quality}): " +
                                               $"{ItemXp.Get(item):N0} / {ItemXp.Required(item):N0}");
                    }
                }, isCheat: true);
        }
    }
}
