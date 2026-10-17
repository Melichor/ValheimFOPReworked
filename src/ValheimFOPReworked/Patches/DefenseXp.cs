using HarmonyLib;
using UnityEngine;

namespace ValheimFOPReworked.Patches
{
    /// <summary>
    /// Armour earns experience from creatures' hits, taken or blocked; shields from the damage they block, more for a
    /// parry. A player's character is always simulated on that player's own machine, so this is all local.
    /// </summary>
    internal static class DefenseXp
    {
        /// <summary>Hits from creatures only: not players, tamed creatures or the world (falling, fire, drowning).</summary>
        private static bool FromCreature(HitData hit, out Character attacker)
        {
            attacker = hit.GetAttacker();
            return attacker && !attacker.IsPlayer() && !attacker.IsTamed();
        }

        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
        private static class ArmorXp
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                Player player = Player.m_localPlayer;
                if (__instance != player || !player.m_nview.IsOwner() || player.IsDead() || player.GetHealth() <= 0f ||
                    player.IsTeleporting() || player.InCutscene() || player.IsDebugFlying() ||
                    (hit.m_dodgeable && player.IsDodgeInvincible()) || !FromCreature(hit, out _))
                {
                    return;
                }
                // The hit as the game scales it before blocking and armour
                float damage = hit.GetTotalDamage() *
                               Game.instance.GetDifficultyDamageScalePlayer(player.transform.position) *
                               Game.m_enemyDamageRate;
                float xp = damage * Settings.ArmorXp.Value;
                ItemXp.Earn(player.m_helmetItem, xp);
                ItemXp.Earn(player.m_chestItem, xp);
                ItemXp.Earn(player.m_legItem, xp);
                ItemXp.Earn(player.m_shoulderItem, xp);
            }
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
        private static class ShieldXp
        {
            private struct Block
            {
                public ItemDrop.ItemData Shield;
                public float Blockable;
                public bool Parry;
            }

            private static void Prefix(Humanoid __instance, HitData hit, Character attacker, out Block __state)
            {
                __state = default;
                if (__instance != Player.m_localPlayer || !attacker || attacker.IsPlayer() || attacker.IsTamed() ||
                    Vector3.Dot(hit.m_dir, __instance.transform.forward) > 0f)
                {
                    return;
                }
                ItemDrop.ItemData blocker = __instance.GetCurrentBlocker();
                if (blocker == null || blocker.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield ||
                    !ItemXp.CanEarn(blocker))
                {
                    return;
                }
                // The same test BlockAttack makes for a perfect block
                bool parry = blocker.m_shared.m_timedBlockBonus > 1f && __instance.m_blockTimer != -1f &&
                             __instance.m_blockTimer < 0.25f;
                __state = new Block { Shield = blocker, Blockable = hit.GetTotalBlockableDamage(), Parry = parry };
            }

            private static void Postfix(HitData hit, Block __state)
            {
                if (__state.Shield == null)
                {
                    return;
                }
                float blocked = __state.Blockable - hit.GetTotalBlockableDamage();
                if (blocked > 0f)
                {
                    float xp = blocked * Settings.BlockXp.Value * (__state.Parry ? Settings.ParryMultiplier.Value : 1f);
                    ItemXp.Earn(__state.Shield, xp);
                }
            }
        }
    }
}
