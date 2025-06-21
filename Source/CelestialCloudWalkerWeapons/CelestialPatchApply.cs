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
            if (__instance.EquipmentSource != null && target.Thing is Pawn targetPawn && __instance.CasterPawn != null)
            {
                IEnumerable<EnchantComp> enchantments = __instance.CasterPawn.health.hediffSet.hediffs
                    .Select(x => x.TryGetComp<EnchantComp>())
                    .Where(x => x != null);

                foreach (var item in enchantments)
                    item.ApplyEnchant(targetPawn);
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAiming")]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming
    {
        [HarmonyPrefix]
        public static void Prefix(Thing eq, ref Vector3 drawLoc, ref float aimAngle)
        {
            if (eq?.def?.HasModExtension<DrawOffsetExt>() == true)
            {
                drawLoc += eq.def.GetModExtension<DrawOffsetExt>().GetOffsetForRot(eq.Rotation);
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_Verb_MeleeAttack_TryCastShot
    {
        public static void Postfix(Verb_MeleeAttack __instance)
        {
            ThingWithComps weapon = __instance.EquipmentSource;
            Pawn pawn = __instance.CasterPawn;

            if (weapon == null || pawn == null) return;

            CompDemonSlayerWeapon comp = weapon.GetComp<CompDemonSlayerWeapon>();
            comp?.CheckOnWeaponUse(pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "CheckForStateChange")]
    public static class Patch_PreventVitalDeath
    {
        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> pawnField = AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        static bool Prefix(Pawn_HealthTracker __instance)
        {
            Pawn pawn = pawnField(__instance);
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null) return true;

            if (!(pawn.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) ?? false))
                return true;

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");

            if (neck != null && pawn.health.hediffSet.PartIsMissing(neck))
                return true;

            bool vitalMissing = pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
            {
                string defName = part.Part.def.defName;
                return defName == "Head" || defName == "Skull" || defName == "Brain" || defName == "Heart" ||
                       defName == "AA_DemonHead" || defName == "AA_DemonSkull" ||
                       defName == "AA_DemonBrain" || defName == "AA_DemonHeart";
            });

            return !vitalMissing;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    public static class Patch_ShouldBeDead
    {
        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> pawnField = AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        static void Postfix(Pawn_HealthTracker __instance, ref bool __result)
        {
            Pawn pawn = pawnField(__instance);
            if (pawn?.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) == true)
            {
                var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                    p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");

                if (neck != null && !pawn.health.hediffSet.PartIsMissing(neck))
                {
                    __result = false;
                }
            }
        }
    }
}
