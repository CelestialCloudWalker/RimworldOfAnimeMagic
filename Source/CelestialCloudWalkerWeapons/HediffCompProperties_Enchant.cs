using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_Enchant : HediffCompProperties
    {
        public DamageDef damageType;
        public float damageValue;
        public Hediff hediffToApply;
    }

    public class EnchantComp : HediffComp
    {
        HediffCompProperties_Enchant Props => (HediffCompProperties_Enchant)props;

        public void ApplyEnchant(Pawn TargetPawn)
        {
            if (Props.damageType != null && Props.damageValue > 0)
            {
                TargetPawn.TakeDamage(new DamageInfo(Props.damageType, Props.damageValue));
            }

            if (Props.hediffToApply != null)
            {
                TargetPawn.health.AddHediff(Props.hediffToApply);
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage))]
    [HarmonyPatch("ApplyMeleeDamageToTarget")]
    public static class Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget
    {
        public static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo target, ref DamageWorker.DamageResult __result)
        {
            if (__instance.EquipmentSource != null && target.Thing is Pawn targetPawn)
            {
                //find ALL EnchantComps on this weapon
                IEnumerable<EnchantComp> enchantments = __instance.EquipmentSource.GetComps<EnchantComp>();

                //check its not null
                if (enchantments != null)
                {
                    //loop over each one of them and call their ApplyEnchant method
                    foreach (var item in enchantments)
                    {
                        item.ApplyEnchant(targetPawn);
                    }
                }
            }
        }
    }
}
