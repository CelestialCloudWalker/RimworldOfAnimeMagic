using AnimeArsenal;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Talented;
using UnityEngine;
using Verse;
using Verse.AI;

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

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
            if (neck != null && pawn.health.hediffSet.PartIsMissing(neck))
                return true;

            bool vitalMissing = pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
            {
                string defName = part.Part.def.defName;
                return defName == "Head" || defName == "Skull" || defName == "AA_DemonSkull" ||
                       defName == "Brain" || defName == "AA_DemonBrain" ||
                       defName == "Heart" || defName == "AA_DemonHeart";
            });

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
                var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                    p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
                if (neck != null && !pawn.health.hediffSet.PartIsMissing(neck))
                {
                    __result = false;
                }
            }
        }
    }

    [HarmonyPatch(typeof(PawnCapacitiesHandler), "GetLevel")]
    public static class Patch_CapacitiesAfterRegeneration
    {
        static void Postfix(PawnCapacitiesHandler __instance, PawnCapacityDef capacity, ref float __result)
        {
            try
            {
                var pawnField = typeof(PawnCapacitiesHandler).GetField("pawn",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (pawnField == null) return;

                var pawn = (Pawn)pawnField.GetValue(__instance);
                if (pawn?.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) != true)
                    return;

                if (capacity == PawnCapacityDefOf.BloodPumping ||
                    capacity == PawnCapacityDefOf.Consciousness ||
                    capacity == PawnCapacityDefOf.Moving)
                {
                    var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                        p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");

                    if (neck == null || !pawn.health.hediffSet.PartIsMissing(neck))
                    {
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
                            __result = Mathf.Max(__result, 0.5f);
                        }
                        else if (capacity == PawnCapacityDefOf.Consciousness && hasBrain && hasHead && hasSkull)
                        {
                            __result = Mathf.Max(__result, 0.5f);
                        }
                        else if (capacity == PawnCapacityDefOf.Moving && hasHeart && hasBrain && hasHead)
                        {
                            __result = Mathf.Max(__result, 0.3f);
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

    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    public static class TakeDamage_GeneDamage_Patch
    {
        private static bool isProcessingGeneDamage = false;

        public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (isProcessingGeneDamage)
                return;

            if (!(__instance is Pawn victim) || victim.genes == null)
                return;

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

            if (dinfo.Weapon != null)
            {
                var weaponExt = dinfo.Weapon.GetModExtension<GeneDamageModExtension>();
                if (weaponExt != null && weaponExt.damageOnHit)
                    modExtensions.Add(weaponExt);
            }

            if (dinfo.Instigator is Pawn attacker && attacker.equipment?.Primary != null)
            {
                var weaponExt = attacker.equipment.Primary.def.GetModExtension<GeneDamageModExtension>();
                if (weaponExt != null && weaponExt.damageOnHit)
                    modExtensions.Add(weaponExt);
            }

            if (dinfo.Def != null)
            {
                var damageExt = dinfo.Def.GetModExtension<GeneDamageModExtension>();
                if (damageExt != null && damageExt.damageOnHit)
                    modExtensions.Add(damageExt);
            }

            if (dinfo.Instigator is Pawn attackerPawn && attackerPawn.genes != null)
            {
                foreach (var gene in attackerPawn.genes.GenesListForReading)
                {
                    var geneExt = gene.def.GetModExtension<GeneDamageModExtension>();
                    if (geneExt != null && geneExt.damageOnHit)
                        modExtensions.Add(geneExt);
                }
            }

            foreach (var modExt in modExtensions)
            {
                bool hasTargetGene = victim.genes.GenesListForReading.Any(gene => gene.def.defName == modExt.targetGene);
                if (hasTargetGene)
                {
                    ApplyGeneDamage(victim, modExt, dinfo, totalDamageDealt);
                }
            }
        }

        private static void ApplyGeneDamage(Pawn victim, GeneDamageModExtension modExt, DamageInfo originalDinfo, float totalDamageDealt)
        {
            float extraDamage = modExt.useMultiplier ?
                totalDamageDealt * modExt.damageMultiplier :
                modExt.damageAmount;

            if (modExt.targetBodyParts != null && modExt.targetBodyParts.Count > 0)
            {
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

                        modExt.damageType.Worker.Apply(extraDamageInfo, victim);
                    }
                }
            }
            else
            {
                DamageInfo extraDamageInfo = new DamageInfo(
                    modExt.damageType,
                    extraDamage,
                    modExt.armorPenetration,
                    originalDinfo.Angle,
                    originalDinfo.Instigator,
                    originalDinfo.HitPart,
                    originalDinfo.Weapon
                );

                modExt.damageType.Worker.Apply(extraDamageInfo, victim);
            }
        }
    }
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("ScarletMaterials.Patches");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    public static class Verb_MeleeAttackDamage_DamageInfosToApply_Patch
    {
        public static void Postfix(ref IEnumerable<DamageInfo> __result, Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            try
            {
                var weapon = __instance.EquipmentSource;
                if (weapon?.Stuff == null)
                    return;

                string materialDefName = weapon.Stuff.defName;
                if (materialDefName != "Scarlet_Crimson_Iron_Sand" && materialDefName != "Scarlet_Ore")
                    return;

                var targetPawn = target.Pawn;
                if (targetPawn?.genes == null)
                    return;

                List<string> targetGenes = new List<string>
                {
                    "BloodDemonArt_LowerMoon",
                    "BloodDemonArt_UpperMoon",
                    "BloodDemonArt",
                };

                bool hasDemonGene = false;
                foreach (string geneDefName in targetGenes)
                {
                    GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(geneDefName);
                    if (gene != null && targetPawn.genes.HasActiveGene(gene))
                    {
                        hasDemonGene = true;
                        break;
                    }
                }

                if (!hasDemonGene)
                    return;

                var damageInfos = __result.ToList();
                var bonusDamageInfos = new List<DamageInfo>();

                foreach (var bodyPart in targetPawn.health.hediffSet.GetNotMissingParts())
                {
                    if (bodyPart.def.defName == "Neck" || bodyPart.def.defName == "AA_DemonNeck")
                    {
                        float damageAmount = materialDefName == "Scarlet_Ore" ? 50f : 40f;

                        var bonusDamage = new DamageInfo(
                            DamageDefOf.Cut,
                            damageAmount,
                            0.8f,
                            -1f,
                            __instance.caster,
                            bodyPart,
                            weapon.def,
                            DamageInfo.SourceCategory.ThingOrUnknown,
                            weapon
                        );
                        bonusDamageInfos.Add(bonusDamage);
                        break;
                    }
                }

                if (bonusDamageInfos.Any())
                {
                    __result = damageInfos.Concat(bonusDamageInfos);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ScarletMaterials] Error in demon damage patch: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Projectile_Impact_Patch
    {
        public static void Prefix(Projectile __instance, Thing hitThing)
        {
            try
            {
                var launcher = __instance.Launcher;
                if (launcher == null)
                    return;

                if (!(launcher is Pawn pawn) || pawn.equipment?.Primary?.Stuff == null)
                    return;

                var weapon = pawn.equipment.Primary;
                string materialDefName = weapon.Stuff.defName;

                if (materialDefName != "Scarlet_Crimson_Iron_Sand" && materialDefName != "Scarlet_Ore")
                    return;

                if (!(hitThing is Pawn targetPawn) || targetPawn.genes == null)
                    return;

                List<string> targetGenes = new List<string>
                {
                    "BloodDemonArt_LowerMoon",
                    "BloodDemonArt_UpperMoon",
                    "BloodDemonArt",
                };

                bool hasDemonGene = false;
                foreach (string geneDefName in targetGenes)
                {
                    GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(geneDefName);
                    if (gene != null && targetPawn.genes.HasActiveGene(gene))
                    {
                        hasDemonGene = true;
                        break;
                    }
                }

                if (!hasDemonGene)
                    return;

                var neckPart = targetPawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(
                    bp => bp.def.defName == "Neck" || bp.def.defName == "AA_DemonNeck");

                if (neckPart != null)
                {
                    float damageAmount = materialDefName == "Scarlet_Ore" ? 50f : 40f;

                    var bonusDamage = new DamageInfo(
                        DamageDefOf.Cut,
                        damageAmount,
                        0.8f,
                        -1f,
                        pawn,
                        neckPart,
                        weapon.def,
                        DamageInfo.SourceCategory.ThingOrUnknown,
                        weapon
                    );

                    targetPawn.TakeDamage(bonusDamage);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ScarletMaterials] Error in ranged demon damage patch: {ex}");
            }
        }
    }
    [HarmonyPatch(typeof(Gene_TalentBase), "GetGizmos")]
    public static class Gene_TalentBase_GetGizmos_Patch
    {
        static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Gene_TalentBase __instance)
        {
            bool isCustomGene = __instance.GetType() == typeof(BloodDemonArtsGene) ||
                               __instance.GetType() == typeof(BreathingTechniqueGene) ||
                               __instance.GetType() == typeof(BreathingPotentialGene);

            foreach (var gizmo in __result)
            {
                if (isCustomGene && gizmo is Command_Action cmd && Prefs.DevMode &&
                    cmd.defaultLabel.Contains("Refund all trees"))
                {
                    continue;
                }
                yield return gizmo;
            }

            if (__instance.def is TalentedGeneDef talentedDef &&
                !isCustomGene &&
                Prefs.DevMode &&
                DebugSettings.godMode)
            {
                string resourceLabel = !string.IsNullOrEmpty(talentedDef.resourceLabel) ? talentedDef.resourceLabel : "Resource";

                yield return new Command_Action
                {
                    defaultLabel = "DEV: +10 " + resourceLabel,
                    defaultDesc = "Add 10 " + resourceLabel.ToLower() + " (Debug)",
                    action = () =>
                    {
                        __instance.Value += 10f;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: -10 " + resourceLabel,
                    defaultDesc = "Remove 10 " + resourceLabel.ToLower() + " (Debug)",
                    action = () =>
                    {
                        __instance.Value -= 10f;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Fill " + resourceLabel,
                    defaultDesc = "Fill " + resourceLabel.ToLower() + " to max (Debug)",
                    action = () =>
                    {
                        __instance.Value = __instance.Max;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Empty " + resourceLabel,
                    defaultDesc = "Empty " + resourceLabel.ToLower() + " to 0 (Debug)",
                    action = () =>
                    {
                        __instance.Value = 0f;
                    }
                };
            }
        }
    }
    public static class ResetTreeTracker
    {
        public static bool AllowCustomTreeReset = false;
    }

    [HarmonyPatch(typeof(BaseTreeHandler), "ResetTree")]
    public static class BaseTreeHandler_ResetTree_Patch
    {
        static bool Prefix(BaseTreeHandler __instance)
        {
            try
            {
                var geneField = typeof(BaseTreeHandler).GetField("gene", BindingFlags.NonPublic | BindingFlags.Instance);

                if (geneField != null)
                {
                    var ownerGene = geneField.GetValue(__instance);

                    if (ownerGene is BreathingTechniqueGene || ownerGene is BloodDemonArtsGene || ownerGene is BreathingPotentialGene)
                    {
                        string treeName = __instance.TreeDef?.defName ?? "";

                        if (treeName.Contains("Breathing") || treeName.Contains("Demon") ||
                            treeName.Contains("Blood") || treeName.Contains("Art") ||
                            treeName.Contains("Potential"))
                        {
                            return ResetTreeTracker.AllowCustomTreeReset;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"BaseTreeHandler_ResetTree_Patch failed: {ex.Message}");
            }

            return true;
        }
    }
    [StaticConstructorOnStartup]
    public static class HarmonyPatcher
    {
        static HarmonyPatcher()
        {
            var harmony = new Harmony("rimworld.animearsenal.demonslayer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[AnimeArsenal] Harmony patches applied successfully!");
        }
    }
}
[HarmonyPatch]
public static class CorpseEatingPatches
{
    private static readonly HashSet<int> processedCorpses = new HashSet<int>();

    [HarmonyPatch(typeof(Thing), "Ingested")]
    [HarmonyPostfix]
    public static void Postfix_Ingested(Thing __instance, Pawn ingester, float nutritionWanted)
    {
        try
        {
            if (ingester?.genes == null) return;

            if (__instance is Corpse corpse &&
                corpse.InnerPawn?.RaceProps?.Humanlike == true)
            {
                int corpseId = corpse.thingIDNumber;
                if (!processedCorpses.Contains(corpseId))
                {
                    var demonGene = ingester.genes.GenesListForReading
                        .FirstOrDefault(g => g.def.defName == "BloodDemonArt") as BloodDemonArtsGene;

                    if (demonGene != null)
                    {
                        processedCorpses.Add(corpseId);
                        demonGene.AddPawnEaten();
                        Messages.Message($"{ingester.Name.ToStringShort} consumed the essence of {corpse.InnerPawn.Name.ToStringShort}!",
                                       ingester, MessageTypeDefOf.PositiveEvent);
                        Log.Message($"[AnimeArsenal] Demon {ingester.Name.ToStringShort} ate corpse via Ingested patch");


                        if (processedCorpses.Count > 100)
                        {
                            processedCorpses.Clear();
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[AnimeArsenal] Error in ingestion tracking: {ex}");
        }
    }

    [HarmonyPatch(typeof(Corpse), "Destroy")]
    [HarmonyPrefix]
    public static void Prefix_CorpseDestroyed(Corpse __instance, DestroyMode mode)
    {
        try
        {
            if (__instance.InnerPawn?.RaceProps?.Humanlike != true) return;
            if (mode != DestroyMode.Vanish && mode != DestroyMode.KillFinalize) return;

            int corpseId = __instance.thingIDNumber;
            if (processedCorpses.Contains(corpseId)) return;

            var map = __instance.Map;
            if (map?.mapPawns?.AllPawns == null) return;

            foreach (var pawn in map.mapPawns.AllPawns)
            {
                if (pawn?.CurJob?.def == JobDefOf.Ingest &&
                    pawn.CurJob.GetTarget(TargetIndex.A).Thing == __instance &&
                    pawn.genes != null)
                {
                    var demonGene = pawn.genes.GenesListForReading
                        .FirstOrDefault(g => g.def.defName == "BloodDemonArt") as BloodDemonArtsGene;

                    if (demonGene != null)
                    {
                        processedCorpses.Add(corpseId);
                        demonGene.AddPawnEaten();
                        Messages.Message($"{pawn.Name.ToStringShort} consumed the essence of {__instance.InnerPawn.Name.ToStringShort}!",
                                       pawn, MessageTypeDefOf.PositiveEvent);
                        Log.Message($"[AnimeArsenal] Demon {pawn.Name.ToStringShort} ate corpse via Destroy patch");

                        if (processedCorpses.Count > 100)
                        {
                            processedCorpses.Clear();
                        }
                        break;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[AnimeArsenal] Error in corpse destruction tracking: {ex}");
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), "EndCurrentJob")]
    [HarmonyPostfix]
    public static void Postfix_JobEnded(Pawn_JobTracker __instance, JobCondition condition)
    {
        try
        {
            var pawnField = AccessTools.Field(typeof(Pawn_JobTracker), "pawn");
            var pawn = (Pawn)pawnField?.GetValue(__instance);

            if (pawn?.genes == null) return;
            if (condition != JobCondition.Succeeded) return;
            if (__instance.curJob?.def != JobDefOf.Ingest) return;

            var target = __instance.curJob.GetTarget(TargetIndex.A).Thing;
            if (target is Corpse corpse &&
                corpse.InnerPawn?.RaceProps?.Humanlike == true)
            {
                int corpseId = corpse.thingIDNumber;
                if (!processedCorpses.Contains(corpseId))
                {
                    var demonGene = pawn.genes.GenesListForReading
                        .FirstOrDefault(g => g.def.defName == "BloodDemonArt") as BloodDemonArtsGene;

                    if (demonGene != null)
                    {
                        processedCorpses.Add(corpseId);
                        demonGene.AddPawnEaten();
                        Messages.Message($"{pawn.Name.ToStringShort} consumed the essence of {corpse.InnerPawn.Name.ToStringShort}!",
                                       pawn, MessageTypeDefOf.PositiveEvent);
                        Log.Message($"[AnimeArsenal] Demon {pawn.Name.ToStringShort} ate corpse via Job End patch");

                        if (processedCorpses.Count > 100)
                        {
                            processedCorpses.Clear();
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Error($"[AnimeArsenal] Error in job completion tracking: {ex}");
        }
    }
    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class StatWorker_BloodPoolBonus
    {
        private static readonly FieldInfo statField = AccessTools.Field(typeof(StatWorker), "stat");

        public static void Postfix(StatWorker __instance, StatRequest req, ref float __result)
        {
            if (req.Thing is Pawn pawn && pawn.genes != null)
            {
                var demonGene = pawn.genes.GenesListForReading
                    .OfType<BloodDemonArtsGene>()
                    .FirstOrDefault();

                if (demonGene != null)
                {
                    StatDef currentStat = (StatDef)statField.GetValue(__instance);
                    float offset = demonGene.GetStatOffset(currentStat);
                    if (offset != 0f)
                    {
                        __result += offset;
                    }
                }
            }
        }
    }
}