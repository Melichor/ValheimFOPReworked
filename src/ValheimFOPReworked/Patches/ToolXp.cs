using HarmonyLib;

namespace ValheimFOPReworked.Patches
{
    /// <summary>
    /// Tools earn experience from use: pickaxes and axes from mining and chopping, the build tools from placing and
    /// repairing pieces, the scythe from harvesting, the snow shovel from shovelling. Staffs that deal no damage
    /// themselves (summoning, shielding) earn it per cast.
    /// </summary>
    internal static class ToolXp
    {
        private static Player LocalPlayer => Player.m_localPlayer;

        /// <summary>A pickaxe or axe hitting a rock, ore vein, tree or log: the damage it would do, if its tier is enough.</summary>
        private static class Resources
        {
            // A destroyed rock can turn into an ore vein and pass the same hit on; count it once
            private static bool s_inHit;

            /// <summary>Returns whether this call is the outer one, which End has to close.</summary>
            public static bool Hit(HitData hit, HitData.DamageModifiers modifiers, int minToolTier, bool tierZeroAlways)
            {
                if (s_inHit)
                {
                    return false;
                }
                s_inHit = true;
                ItemDrop.ItemData tool = WeaponXp.LocalWeapon;
                if (tool != null && hit.m_attacker == LocalPlayer.GetZDOID() &&
                    hit.CheckToolTier(minToolTier, tierZeroAlways))
                {
                    HitData resisted = hit.Clone();
                    resisted.ApplyResistance(modifiers, out _);
                    float damage = resisted.m_damage.m_chop + resisted.m_damage.m_pickaxe;
                    ItemXp.Earn(tool, damage * Settings.ResourceXp.Value);
                }
                return true;
            }

            public static void End(bool outer)
            {
                if (outer)
                {
                    s_inHit = false;
                }
            }
        }

        // Trees and logs let tier 0 tools through (the last argument), as the game does
        [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
        private static class TreeHit
        {
            private static void Prefix(TreeBase __instance, HitData hit, out bool __state) =>
                __state = Resources.Hit(hit, __instance.m_damageModifiers, __instance.m_minToolTier, true);

            private static void Finalizer(bool __state) => Resources.End(__state);
        }

        [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
        private static class LogHit
        {
            private static void Prefix(TreeLog __instance, HitData hit, out bool __state) =>
                __state = Resources.Hit(hit, __instance.m_damages, __instance.m_minToolTier, true);

            private static void Finalizer(bool __state) => Resources.End(__state);
        }

        [HarmonyPatch(typeof(MineRock), nameof(MineRock.Damage))]
        private static class RockHit
        {
            private static void Prefix(MineRock __instance, HitData hit, out bool __state) =>
                __state = Resources.Hit(hit, __instance.m_damageModifiers, __instance.m_minToolTier, false);

            private static void Finalizer(bool __state) => Resources.End(__state);
        }

        [HarmonyPatch(typeof(MineRock5), nameof(MineRock5.Damage))]
        private static class VeinHit
        {
            private static void Prefix(MineRock5 __instance, HitData hit, out bool __state) =>
                __state = Resources.Hit(hit, __instance.m_damageModifiers, __instance.m_minToolTier, false);

            private static void Finalizer(bool __state) => Resources.End(__state);
        }

        [HarmonyPatch(typeof(Destructible), nameof(Destructible.Damage))]
        private static class DestructibleHit
        {
            private static void Prefix(Destructible __instance, HitData hit, out bool __state) =>
                __state = Resources.Hit(hit, __instance.m_damages, __instance.m_minToolTier, false);

            private static void Finalizer(bool __state) => Resources.End(__state);
        }

        /// <summary>
        /// Placing a piece, a plant or a terrain change. After a piece is removed the game raises no build skill for
        /// the next placements (its "remove debt"), and neither does this: placing and removing gives nothing.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static class Place
        {
            private static void Postfix(Player __instance, bool __result)
            {
                // The game takes off the debt after TryPlacePiece returns, so it still shows here
                if (__result && __instance == LocalPlayer && __instance.m_buildRemoveDebt == 0)
                {
                    ItemXp.AddUse(__instance.GetRightItem());
                }
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Repair))]
        private static class Repair
        {
            internal static ItemDrop.ItemData s_tool;

            private static void Prefix(ItemDrop.ItemData toolItem) => s_tool = toolItem;

            private static void Finalizer() => s_tool = null;
        }

        [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Repair))]
        private static class Repaired
        {
            private static void Postfix(bool __result)
            {
                if (__result && Repair.s_tool != null)
                {
                    ItemXp.AddUse(Repair.s_tool);
                }
            }
        }

        /// <summary>The scythe harvests by picking everything in reach during its swing.</summary>
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        private static class Harvest
        {
            private static void Postfix(Humanoid character, bool __result)
            {
                ItemDrop.ItemData tool = WeaponXp.LocalWeapon;
                if (__result && tool != null && character == LocalPlayer)
                {
                    ItemXp.AddUse(tool);
                }
            }
        }

        /// <summary>The snow shovel only clears snow in the Deep North, as part of its swing.</summary>
        [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
        private static class Shovel
        {
            private static void Postfix(Attack __instance)
            {
                if (__instance.m_snowShovel && __instance.m_snowShovelRadius > 0f && __instance.m_character == LocalPlayer &&
                    EnvMan.instance && EnvMan.instance.GetCurrentBiome() == Heightmap.Biome.DeepNorth)
                {
                    ItemXp.AddUse(__instance.m_weapon);
                }
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.Start))]
        private static class Cast
        {
            private static void Postfix(Attack __instance, bool __result, Humanoid character, ItemDrop.ItemData weapon)
            {
                if (!__result || character != LocalPlayer || weapon == null ||
                    weapon.m_shared.m_damages.GetTotalDamage() > 0f)
                {
                    return;
                }
                float cost = __instance.m_attackEitr + __instance.m_attackHealth;
                ItemXp.Earn(weapon, cost * Settings.CastXp.Value);
            }
        }
    }
}
