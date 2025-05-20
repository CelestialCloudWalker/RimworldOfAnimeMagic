using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAiming")]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming
    {
        [HarmonyPrefix]
        public static void Prefix(Thing eq, ref Vector3 drawLoc, ref float aimAngle)
        {
            if (eq != null && eq.def != null && eq.def.HasModExtension<DrawOffsetExt>())
            {
                drawLoc += eq.def.GetModExtension<DrawOffsetExt>().GetOffsetForRot(eq.Rotation);
            }
        }
    }
    // Harmony patch to catch weapon usage
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Verb_MeleeAttack_TryCastShot
    {
        public static void Postfix(Verb_MeleeAttack __instance)
        {
            // Get the weapon being used
            ThingWithComps weapon = __instance.EquipmentSource;
            if (weapon == null) return;

            // Get the pawn using the weapon
            Pawn pawn = __instance.CasterPawn;
            if (pawn == null) return;

            // Check if the weapon has our component
            CompDemonSlayerWeapon comp = weapon.GetComp<CompDemonSlayerWeapon>();
            if (comp != null)
            {
                // Trigger our check method
                comp.CheckOnWeaponUse(pawn);
            }
        }
    }
}
