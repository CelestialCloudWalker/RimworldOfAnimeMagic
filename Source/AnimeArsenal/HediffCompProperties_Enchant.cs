using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{

    [StaticConstructorOnStartup]
    public class CelestialPatchApply
    {
        static CelestialPatchApply()
        {
            var harmony = new Harmony("com.AnimeArsenal.patches");
            harmony.PatchAll();
        }
    }


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
                if (__instance.CasterPawn != null)
                {
                    ///find all hediffs, then get any with an EnchantComp, then turn them all into one list
                    IEnumerable<EnchantComp> enchantments = __instance.CasterPawn.health.hediffSet.hediffs
                        .Select(x => x.TryGetComp<EnchantComp>())
                        .Where(x => x != null);

                    //check the list isnt nul
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
}
