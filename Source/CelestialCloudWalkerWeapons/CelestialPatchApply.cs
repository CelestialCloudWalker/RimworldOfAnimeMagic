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
        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> pawnField =
            AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        static bool Prefix(Pawn_HealthTracker __instance)
        {
            Pawn pawn = pawnField(__instance);
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null) return true;
            if (!(pawn.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) ?? false))
                return true;

            // Check if neck is destroyed - if so, allow death
            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
            if (neck != null && pawn.health.hediffSet.PartIsMissing(neck))
                return true;

            // Check if vital parts are missing
            bool vitalMissing = pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
            {
                string defName = part.Part.def.defName;
                return defName == "Head" || defName == "Skull" || defName == "AA_DemonSkull" ||
                       defName == "Brain" || defName == "AA_DemonBrain" ||
                       defName == "Heart" || defName == "AA_DemonHeart";
            });

            // Prevent death if vital parts are missing (they will regenerate)
            return !vitalMissing;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    public static class Patch_ShouldBeDead
    {
        private static readonly AccessTools.FieldRef<Pawn_HealthTracker, Pawn> pawnField =
            AccessTools.FieldRefAccess<Pawn_HealthTracker, Pawn>("pawn");

        static void Postfix(Pawn_HealthTracker __instance, ref bool __result)
        {
            Pawn pawn = pawnField(__instance);
            if (pawn?.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) == true)
            {
                // Check if neck is intact - if neck is destroyed, they should die
                var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                    p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
                if (neck != null && !pawn.health.hediffSet.PartIsMissing(neck))
                {
                    __result = false; // Don't die if neck is intact
                }
            }
        }
    }

    // Additional patch to handle consciousness and movement issues after regeneration
    [HarmonyPatch(typeof(PawnCapacitiesHandler), "GetLevel")]
    public static class Patch_CapacitiesAfterRegeneration
    {
        static void Postfix(PawnCapacitiesHandler __instance, PawnCapacityDef capacity, ref float __result)
        {
            try
            {
                // Access the pawn through reflection since it's a private field
                var pawnField = typeof(PawnCapacitiesHandler).GetField("pawn",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pawnField == null) return;

                var pawn = (Pawn)pawnField.GetValue(__instance);
                if (pawn?.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) != true)
                    return;

                // If the pawn has blood demon art and their heart was recently regenerated,
                // ensure they don't have movement/consciousness issues
                if (capacity == PawnCapacityDefOf.BloodPumping ||
                    capacity == PawnCapacityDefOf.Consciousness ||
                    capacity == PawnCapacityDefOf.Moving)
                {
                    var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                        p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");

                    // Only apply if neck is intact
                    if (neck == null || !pawn.health.hediffSet.PartIsMissing(neck))
                    {
                        // Check if vital organs are present (not missing)
                        bool hasHeart = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                            part.Part.def.defName == "Heart" || part.Part.def.defName == "AA_DemonHeart");
                        bool hasBrain = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                            part.Part.def.defName == "Brain" || part.Part.def.defName == "AA_DemonBrain");
                        bool hasSkull = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                            part.Part.def.defName == "Skull" || part.Part.def.defName == "AA_DemonSkull");
                        bool hasHead = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                            part.Part.def.defName == "Head");

                        if (capacity == PawnCapacityDefOf.BloodPumping && hasHeart)
                        {
                            __result = Mathf.Max(__result, 0.5f); // Ensure minimum blood pumping
                        }
                        else if (capacity == PawnCapacityDefOf.Consciousness && hasBrain && hasHead && hasSkull)
                        {
                            __result = Mathf.Max(__result, 0.5f); // Ensure minimum consciousness
                        }
                        else if (capacity == PawnCapacityDefOf.Moving && hasHeart && hasBrain && hasHead)
                        {
                            __result = Mathf.Max(__result, 0.3f); // Ensure minimum movement
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"Error in Patch_CapacitiesAfterRegeneration: {ex}");
            }
        }
    }
    // Main Harmony patch - handles all weapon/projectile damage
    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    public static class TakeDamage_GeneDamage_Patch
    {
        // Flag to prevent infinite recursion
        private static bool isProcessingGeneDamage = false;

        public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            // Prevent infinite recursion
            if (isProcessingGeneDamage)
                return;

            // Only process if the target is a pawn with genes
            if (!(__instance is Pawn victim) || victim.genes == null)
                return;

            // Only process if there was actual damage dealt
            if (__result.totalDamageDealt <= 0)
                return;

            try
            {
                isProcessingGeneDamage = true;
                ProcessGeneDamageFromWeapon(victim, dinfo, __result.totalDamageDealt);
            }
            finally
            {
                isProcessingGeneDamage = false;
            }
        }

        private static void ProcessGeneDamageFromWeapon(Pawn victim, DamageInfo dinfo, float totalDamageDealt)
        {
            List<GeneDamageModExtension> modExtensions = new List<GeneDamageModExtension>();

            // 1. Check weapon for mod extensions
            if (dinfo.Weapon != null)
            {
                var weaponExt = dinfo.Weapon.GetModExtension<GeneDamageModExtension>();
                if (weaponExt != null && weaponExt.damageOnHit)
                    modExtensions.Add(weaponExt);
            }

            // 2. Check instigator's equipped weapon
            if (dinfo.Instigator is Pawn attacker && attacker.equipment?.Primary != null)
            {
                var weaponExt = attacker.equipment.Primary.def.GetModExtension<GeneDamageModExtension>();
                if (weaponExt != null && weaponExt.damageOnHit)
                    modExtensions.Add(weaponExt);
            }

            // 3. Check damage def itself
            if (dinfo.Def != null)
            {
                var damageExt = dinfo.Def.GetModExtension<GeneDamageModExtension>();
                if (damageExt != null && damageExt.damageOnHit)
                    modExtensions.Add(damageExt);
            }

            // 4. Check attacker's genes for mod extensions
            if (dinfo.Instigator is Pawn attackerPawn && attackerPawn.genes != null)
            {
                foreach (var gene in attackerPawn.genes.GenesListForReading)
                {
                    var geneExt = gene.def.GetModExtension<GeneDamageModExtension>();
                    if (geneExt != null && geneExt.damageOnHit)
                        modExtensions.Add(geneExt);
                }
            }

            // Apply damage for each relevant mod extension
            foreach (var modExt in modExtensions)
            {
                // Check if victim has the target gene for this mod extension
                bool hasTargetGene = victim.genes.GenesListForReading.Any(gene => gene.def.defName == modExt.targetGene);
                if (hasTargetGene)
                {
                    ApplyGeneDamage(victim, modExt, dinfo, totalDamageDealt);
                }
            }
        }

        private static void ApplyGeneDamage(Pawn victim, GeneDamageModExtension modExt, DamageInfo originalDinfo, float totalDamageDealt)
        {
            // Calculate extra damage
            float extraDamage = modExt.useMultiplier ?
                totalDamageDealt * modExt.damageMultiplier :
                modExt.damageAmount;

            // Apply the extra damage
            if (modExt.targetBodyParts != null && modExt.targetBodyParts.Count > 0)
            {
                // Target specific body parts
                foreach (BodyPartDef bodyPartDef in modExt.targetBodyParts)
                {
                    BodyPartRecord bodyPart = victim.RaceProps.body.AllParts.FirstOrDefault(p => p.def == bodyPartDef);
                    if (bodyPart != null)
                    {
                        DamageInfo extraDamageInfo = new DamageInfo(
                            modExt.damageType,
                            extraDamage,
                            modExt.armorPenetration,
                            originalDinfo.Angle,
                            originalDinfo.Instigator,
                            bodyPart,
                            originalDinfo.Weapon
                        );

                        // Use DamageWorker directly to avoid triggering patches again
                        modExt.damageType.Worker.Apply(extraDamageInfo, victim);
                    }
                }
            }
            else
            {
                // Apply to same hit part as original damage
                DamageInfo extraDamageInfo = new DamageInfo(
                    modExt.damageType,
                    extraDamage,
                    modExt.armorPenetration,
                    originalDinfo.Angle,
                    originalDinfo.Instigator,
                    originalDinfo.HitPart,
                    originalDinfo.Weapon
                );

                // Use DamageWorker directly to avoid triggering patches again
                modExt.damageType.Worker.Apply(extraDamageInfo, victim);
            }
        }
    }
}