using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace ValheimFOPReworked.Patches
{
    /// <summary>
    /// Weapons earn experience from the damage they deal to creatures and from kills.
    ///
    /// The damage a hit really does is only known where the creature is simulated: on whichever machine owns it, which
    /// in multiplayer is often not the attacker's. So the attacker remembers which of its weapons last hit each
    /// creature, and the owner reports the damage dealt (and the kill) back to the attacker, who credits that weapon.
    /// Damage over time (burning, poison) has no attacker in the game, so the owner remembers who set it.
    /// </summary>
    internal static class WeaponXp
    {
        private const string HitRpc = "melichor.ValheimFOPReworked.Hit";
        // How long a hit is remembered, long enough for burning and poison to finish
        private const float Memory = 60f;

        // The attack being processed right now: a melee swing, an area attack, a projectile or area effect hitting
        private static Character s_attacker;
        private static ItemDrop.ItemData s_weapon;

        // On the attacker: the local player's weapon that last hit each creature
        private static readonly Dictionary<ZDOID, Remembered<ItemDrop.ItemData>> s_lastWeapon =
            new Dictionary<ZDOID, Remembered<ItemDrop.ItemData>>();
        // On the owner: who last hit each creature with fire, poison or spirit damage
        private static readonly Dictionary<ZDOID, Remembered<ZDOID>> s_dotSource = new Dictionary<ZDOID, Remembered<ZDOID>>();

        private struct Remembered<T>
        {
            public T Value;
            public float Time;
        }

        /// <summary>The weapon of the attack being processed, if the local player is making it.</summary>
        public static ItemDrop.ItemData LocalWeapon =>
            s_attacker && s_attacker == Player.m_localPlayer ? s_weapon : null;

        /// <summary>Creatures that give experience: not players, tamed creatures, summons or training dummies.</summary>
        public static bool GivesXp(Character character) =>
            character && !character.IsPlayer() && !character.IsTamed() &&
            character.m_faction != Character.Faction.TrainingDummy;

        public static void RegisterRpc()
        {
            ZRoutedRpc.instance.Register<ZDOID, float, float>(HitRpc, (sender, target, damage, killedMaxHealth) =>
                Credit(target, damage, killedMaxHealth));
        }

        private static void Remember<T>(Dictionary<ZDOID, Remembered<T>> map, ZDOID key, T value)
        {
            if (map.Count > 256)
            {
                foreach (ZDOID old in map.Where(e => Time.time - e.Value.Time > Memory).Select(e => e.Key).ToList())
                {
                    map.Remove(old);
                }
            }
            map[key] = new Remembered<T> { Value = value, Time = Time.time };
        }

        private static bool Recall<T>(Dictionary<ZDOID, Remembered<T>> map, ZDOID key, out T value)
        {
            if (map.TryGetValue(key, out Remembered<T> entry) && Time.time - entry.Time <= Memory)
            {
                value = entry.Value;
                return true;
            }
            value = default;
            return false;
        }

        private static Character FindCharacter(ZDOID id)
        {
            GameObject go = ZNetScene.instance ? ZNetScene.instance.FindInstance(id) : null;
            return go ? go.GetComponent<Character>() : null;
        }

        /// <summary>On the owner of a creature that just lost health to a player's attack.</summary>
        private static void Report(Character target, Player attacker, float damage, bool killed)
        {
            float killedMaxHealth = killed ? target.GetMaxHealth() : 0f;
            if (attacker == Player.m_localPlayer)
            {
                Credit(target.GetZDOID(), damage, killedMaxHealth);
            }
            else
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(attacker.GetOwner(), HitRpc, target.GetZDOID(), damage, killedMaxHealth);
            }
        }

        /// <summary>On the attacker: gives the experience to the weapon that last hit the creature.</summary>
        private static void Credit(ZDOID target, float damage, float killedMaxHealth)
        {
            if (!Recall(s_lastWeapon, target, out ItemDrop.ItemData weapon))
            {
                return;
            }
            ItemXp.Earn(weapon, damage * Settings.DamageXp.Value + killedMaxHealth * Settings.KillXp.Value);
            if (killedMaxHealth > 0f)
            {
                s_lastWeapon.Remove(target);
            }
        }

        /// <summary>Saves the attack being processed and makes it the current one; Exit puts the saved one back.</summary>
        internal struct Context
        {
            private Character _attacker;
            private ItemDrop.ItemData _weapon;

            public static Context Enter(Character attacker, ItemDrop.ItemData weapon)
            {
                Context saved = new Context { _attacker = s_attacker, _weapon = s_weapon };
                s_attacker = attacker;
                s_weapon = weapon;
                return saved;
            }

            public void Exit()
            {
                s_attacker = _attacker;
                s_weapon = _weapon;
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
        private static class MeleeAttack
        {
            private static void Prefix(Attack __instance, out Context __state) =>
                __state = Context.Enter(__instance.m_character, __instance.m_weapon);

            private static void Finalizer(Context __state) => __state.Exit();
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.DoAreaAttack))]
        private static class AreaAttack
        {
            private static void Prefix(Attack __instance, out Context __state) =>
                __state = Context.Enter(__instance.m_character, __instance.m_weapon);

            private static void Finalizer(Context __state) => __state.Exit();
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
        private static class ProjectileHit
        {
            private static void Prefix(Projectile __instance, out Context __state) =>
                __state = Context.Enter(__instance.m_owner, __instance.m_weapon);

            private static void Finalizer(Context __state) => __state.Exit();
        }

        [HarmonyPatch(typeof(Projectile), nameof(Projectile.DoAOE))]
        private static class ProjectileArea
        {
            private static void Prefix(Projectile __instance, out Context __state) =>
                __state = Context.Enter(__instance.m_owner, __instance.m_weapon);

            private static void Finalizer(Context __state) => __state.Exit();
        }

        [HarmonyPatch(typeof(Aoe), nameof(Aoe.OnHit))]
        private static class AreaEffectHit
        {
            private static void Prefix(Aoe __instance, out Context __state) =>
                __state = Context.Enter(__instance.m_owner, __instance.m_itemData);

            private static void Finalizer(Context __state) => __state.Exit();
        }

        /// <summary>On the attacker, as the hit is sent to the creature's owner: remember the weapon.</summary>
        [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
        private static class RememberWeapon
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                ItemDrop.ItemData weapon = LocalWeapon;
                if (weapon != null && hit.m_attacker == Player.m_localPlayer.GetZDOID() && GivesXp(__instance))
                {
                    Remember(s_lastWeapon, __instance.GetZDOID(), weapon);
                }
            }
        }

        /// <summary>On the owner: remember who set the creature burning or poisoned.</summary>
        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
        private static class RememberDamageOverTime
        {
            private static void Prefix(Character __instance, HitData hit)
            {
                if (!__instance.m_nview.IsOwner() || !hit.HaveAttacker() || !GivesXp(__instance))
                {
                    return;
                }
                HitData.DamageTypes damage = hit.m_damage;
                if (damage.m_fire > 0f || damage.m_poison > 0f || damage.m_spirit > 0f)
                {
                    Remember(s_dotSource, __instance.GetZDOID(), hit.m_attacker);
                }
            }
        }

        /// <summary>On the owner: the health the creature lost, reported to the attacking player.</summary>
        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        private static class ReportDamage
        {
            private struct Before
            {
                public Player Attacker;
                public float Health;
            }

            private static void Prefix(Character __instance, HitData hit, out Before __state)
            {
                __state = default;
                if (!__instance.m_nview.IsOwner() || !GivesXp(__instance))
                {
                    return;
                }
                Character attacker = hit.GetAttacker();
                if (!attacker && (hit.m_hitType == HitData.HitType.Burning || hit.m_hitType == HitData.HitType.Poisoned) &&
                    Recall(s_dotSource, __instance.GetZDOID(), out ZDOID source))
                {
                    attacker = FindCharacter(source);
                }
                if (attacker is Player player)
                {
                    __state = new Before { Attacker = player, Health = __instance.GetHealth() };
                }
            }

            private static void Postfix(Character __instance, Before __state)
            {
                if (!__state.Attacker || __state.Health <= 0f)
                {
                    return;
                }
                float health = __instance.GetHealth();
                float damage = __state.Health - Mathf.Max(0f, health);
                if (damage > 0f)
                {
                    Report(__instance, __state.Attacker, damage, health <= 0f);
                }
            }
        }
    }
}
