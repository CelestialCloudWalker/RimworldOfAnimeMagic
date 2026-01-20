using AnimeArsenal;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
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

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_Verb_MeleeAttack_ApplyMeleeDamageToTarget
    {
        public static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo target, ref DamageWorker.DamageResult __result)
        {
            if (__instance.EquipmentSource == null || !(target.Thing is Pawn targetPawn) || __instance.CasterPawn == null)
                return;

            var enchantments = __instance.CasterPawn.health.hediffSet.hediffs
                .Select(x => x.TryGetComp<EnchantComp>())
                .Where(x => x != null);

            foreach (var enchant in enchantments)
                enchant.ApplyEnchant(targetPawn);
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), "DrawEquipmentAiming")]
    public static class Patch_PawnRenderUtility_DrawEquipmentAiming
    {
        [HarmonyPrefix]
        public static void Prefix(Thing eq, ref Vector3 drawLoc, ref float aimAngle)
        {
            if (eq?.def?.HasModExtension<DrawOffsetExt>() == true)
                drawLoc += eq.def.GetModExtension<DrawOffsetExt>().GetOffsetForRot(eq.Rotation);
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
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
                return true;

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
            if (pawn?.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) != true)
                return;

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");

            if (neck != null && !pawn.health.hediffSet.PartIsMissing(neck))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(PawnCapacitiesHandler), "GetLevel")]
    public static class Patch_CapacitiesAfterRegeneration
    {
        static void Postfix(PawnCapacitiesHandler __instance, PawnCapacityDef capacity, ref float __result)
        {
            var pawnField = typeof(PawnCapacitiesHandler).GetField("pawn",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (pawnField == null) return;

            var pawn = (Pawn)pawnField.GetValue(__instance);
            if (pawn?.genes?.HasActiveGene(DefDatabase<GeneDef>.GetNamed("BloodDemonArt")) != true)
                return;

            if (capacity != PawnCapacityDefOf.BloodPumping &&
                capacity != PawnCapacityDefOf.Consciousness &&
                capacity != PawnCapacityDefOf.Moving)
                return;

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");

            if (neck != null && pawn.health.hediffSet.PartIsMissing(neck))
                return;

            bool hasHeart = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                part.Part.def.defName == "Heart" || part.Part.def.defName == "AA_DemonHeart");
            bool hasBrain = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                part.Part.def.defName == "Brain" || part.Part.def.defName == "AA_DemonBrain");
            bool hasSkull = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                part.Part.def.defName == "Skull" || part.Part.def.defName == "AA_DemonSkull");
            bool hasHead = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
                part.Part.def.defName == "Head");

            if (capacity == PawnCapacityDefOf.BloodPumping && hasHeart)
                __result = Mathf.Max(__result, 0.5f);
            else if (capacity == PawnCapacityDefOf.Consciousness && hasBrain && hasHead && hasSkull)
                __result = Mathf.Max(__result, 0.5f);
            else if (capacity == PawnCapacityDefOf.Moving && hasHeart && hasBrain && hasHead)
                __result = Mathf.Max(__result, 0.3f);
        }
    }

    [HarmonyPatch(typeof(Thing), "TakeDamage")]
    public static class TakeDamage_GeneDamage_Patch
    {
        private static bool isProcessingGeneDamage = false;

        public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (isProcessingGeneDamage || __result.totalDamageDealt <= 0)
                return;

            if (!(__instance is Pawn victim) || victim.genes == null)
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
                if (victim.genes.GenesListForReading.Any(gene => gene.def.defName == modExt.targetGene))
                    ApplyGeneDamage(victim, modExt, dinfo, totalDamageDealt);
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
                __result = damageInfos.Concat(bonusDamageInfos);
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Projectile_Impact_Patch
    {
        public static void Prefix(Projectile __instance, Thing hitThing)
        {
            var launcher = __instance.Launcher;
            if (launcher == null || !(launcher is Pawn pawn) || pawn.equipment?.Primary?.Stuff == null)
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
                    continue;

                yield return gizmo;
            }

            if (__instance.def is TalentedGeneDef talentedDef &&
                !isCustomGene &&
                Prefs.DevMode &&
                DebugSettings.godMode)
            {
                string resourceLabel = !string.IsNullOrEmpty(talentedDef.resourceLabel) ?
                    talentedDef.resourceLabel : "Resource";

                yield return new Command_Action
                {
                    defaultLabel = "DEV: +10 " + resourceLabel,
                    defaultDesc = "Add 10 " + resourceLabel.ToLower(),
                    action = () => __instance.Value += 10f
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: -10 " + resourceLabel,
                    defaultDesc = "Remove 10 " + resourceLabel.ToLower(),
                    action = () => __instance.Value -= 10f
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Fill " + resourceLabel,
                    defaultDesc = "Fill " + resourceLabel.ToLower() + " to max",
                    action = () => __instance.Value = __instance.Max
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Empty " + resourceLabel,
                    defaultDesc = "Empty " + resourceLabel.ToLower() + " to 0",
                    action = () => __instance.Value = 0f
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
            var geneField = typeof(BaseTreeHandler).GetField("gene", BindingFlags.NonPublic | BindingFlags.Instance);
            if (geneField == null)
                return true;

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
        }
    }

    [HarmonyPatch]
    public static class CorpseEatingPatches
    {
        private static readonly HashSet<int> processedCorpses = new HashSet<int>();

        [HarmonyPatch(typeof(Thing), "Ingested")]
        [HarmonyPostfix]
        public static void Postfix_Ingested(Thing __instance, Pawn ingester)
        {
            if (ingester?.genes == null)
                return;

            if (!(__instance is Corpse corpse) || corpse.InnerPawn?.RaceProps?.Humanlike != true)
                return;

            int corpseId = corpse.thingIDNumber;
            if (processedCorpses.Contains(corpseId))
                return;

            var demonGene = ingester.genes.GenesListForReading
                .FirstOrDefault(g => g.def.defName == "BloodDemonArt") as BloodDemonArtsGene;

            if (demonGene != null)
            {
                processedCorpses.Add(corpseId);
                demonGene.AddPawnEaten();
                Messages.Message($"{ingester.Name.ToStringShort} consumed {corpse.InnerPawn.Name.ToStringShort}!",
                               ingester, MessageTypeDefOf.PositiveEvent);

                if (!corpse.Destroyed)
                    corpse.Destroy(DestroyMode.Vanish);

                if (processedCorpses.Count > 100)
                    processedCorpses.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class StatWorker_BloodPoolBonus
    {
        private static readonly FieldInfo statField = AccessTools.Field(typeof(StatWorker), "stat");

        public static void Postfix(StatWorker __instance, StatRequest req, ref float __result)
        {
            if (!(req.Thing is Pawn pawn) || pawn.genes == null)
                return;

            var demonGene = pawn.genes.GenesListForReading
                .OfType<BloodDemonArtsGene>()
                .FirstOrDefault();

            if (demonGene != null)
            {
                StatDef currentStat = (StatDef)statField.GetValue(__instance);
                float offset = demonGene.GetStatOffset(currentStat);
                if (offset != 0f)
                    __result += offset;
            }
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ChooseHitPart")]
    public static class Patch_ChooseHitPart
    {
        public static void Postfix(ref BodyPartRecord __result, DamageInfo dinfo, Pawn pawn)
        {
            if (!(dinfo.Instigator is Pawn attacker) || attacker.health?.hediffSet == null)
                return;

            var transparentWorldHediff = attacker.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName.StartsWith("TransparentWorld_"));

            if (transparentWorldHediff == null)
                return;

            var props = transparentWorldHediff.def.GetModExtension<TransparentWorldProperties>();
            if (props == null || props.organHitChanceBonus <= 0f)
                return;

            var organs = pawn.health.hediffSet.GetNotMissingParts()
                .Where(part => part.def.tags?.Contains(BodyPartTagDefOf.BloodPumpingSource) == true ||
                              part.def.tags?.Contains(BodyPartTagDefOf.BreathingSource) == true ||
                              part.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true ||
                              part.def.tags?.Contains(BodyPartTagDefOf.BloodFiltrationSource) == true ||
                              part.def.tags?.Contains(BodyPartTagDefOf.MetabolismSource) == true)
                .ToList();

            if (organs.Any() && Rand.Chance(Mathf.Clamp01(props.organHitChanceBonus)))
                __result = organs.RandomElement();
        }
    }

    [StaticConstructorOnStartup]
    public static class StatPatches
    {
        static StatPatches()
        {
            var harmony = new Harmony("com.animearsenal.trainingitems");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class StatWorker_GetValueUnfinalized_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(StatRequest req, ref float __result, StatDef ___stat)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn))
                return;

            TrainingComp comp = pawn.TryGetComp<TrainingComp>();
            if (comp == null)
                return;

            float boost = comp.GetStatBoost(___stat.defName);
            if (boost != 0f)
                __result += boost;
        }
    }

    [HarmonyPatch(typeof(PawnCapacityUtility), "CalculateCapacityLevel")]
    public static class PawnCapacityUtility_CalculateCapacityLevel_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
        {
            Pawn pawn = diffSet?.pawn;
            if (pawn == null)
                return;

            TrainingComp comp = pawn.TryGetComp<TrainingComp>();
            if (comp == null)
                return;

            float boost = comp.GetCapacityBoost(capacity.defName);
            if (boost != 0f)
                __result = Mathf.Clamp(__result + boost, 0f, 999f);
        }
    }

    [StaticConstructorOnStartup]
    public static class QuestGeneFilterPatches
    {
        static QuestGeneFilterPatches()
        {
            var harmony = new Harmony("AnimeArsenal.QuestGeneFilter");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "DoBottomButtons")]
    public static class Dialog_FormCaravan_DoBottomButtons_Patch
    {
        public static bool Prefix(Dialog_FormCaravan __instance, Rect rect)
        {
            var transferablesField = typeof(Dialog_FormCaravan).GetField("transferables",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (transferablesField == null)
                return true;

            var transferables = transferablesField.GetValue(__instance) as List<TransferableOneWay>;
            if (transferables == null)
                return true;

            var quest = GetActiveQuestWithGeneRestrictions();
            if (quest == null)
                return true;

            var questPart = quest.PartsListForReading?.OfType<QuestPart_GetGene>().FirstOrDefault();
            if (questPart?.excludedGenes == null || !questPart.excludedGenes.Any())
                return true;

            var pawnsToSend = new List<Pawn>();
            foreach (var transferable in transferables)
            {
                if (transferable.CountToTransfer > 0 && transferable.AnyThing is Pawn pawn)
                    pawnsToSend.Add(pawn);
            }

            foreach (var pawn in pawnsToSend)
            {
                if (pawn.genes == null)
                    continue;

                foreach (var excludedGene in questPart.excludedGenes)
                {
                    if (pawn.genes.HasActiveGene(excludedGene))
                    {
                        Messages.Message($"{pawn.LabelShort} cannot participate due to having the {excludedGene.label} gene. They would die instantly in the wisteria barrier.",
                                       MessageTypeDefOf.RejectInput, false);
                        return true;
                    }
                }
            }

            return true;
        }

        private static Quest GetActiveQuestWithGeneRestrictions()
        {
            foreach (var quest in Find.QuestManager.QuestsListForReading)
            {
                if (quest.State != QuestState.Ongoing)
                    continue;

                var genePart = quest.PartsListForReading?.OfType<QuestPart_GetGene>().FirstOrDefault();
                if (genePart?.excludedGenes != null && genePart.excludedGenes.Any())
                {
                    var lendPart = quest.PartsListForReading?.OfType<QuestPart_LendColonistsToFaction>().FirstOrDefault();
                    if (lendPart != null)
                        return quest;
                }
            }
            return null;
        }
    }

    public static class QuestGeneFilterUtility
    {
        public static bool ShouldDisablePawn(Pawn pawn)
        {
            if (pawn?.genes == null)
                return false;

            var activeQuest = Find.QuestManager.QuestsListForReading
                .FirstOrDefault(q => q.State == QuestState.Ongoing &&
                               q.PartsListForReading?.OfType<QuestPart_GetGene>().Any() == true);

            if (activeQuest == null)
                return false;

            var questPart = activeQuest.PartsListForReading.OfType<QuestPart_GetGene>().FirstOrDefault();
            if (questPart?.excludedGenes == null)
                return false;

            foreach (var excludedGene in questPart.excludedGenes)
            {
                if (pawn.genes.HasActiveGene(excludedGene))
                    return true;
            }

            return false;
        }

        public static string GetExclusionReason(Pawn pawn)
        {
            if (pawn?.genes == null)
                return null;

            var activeQuest = Find.QuestManager.QuestsListForReading
                .FirstOrDefault(q => q.State == QuestState.Ongoing &&
                     q.PartsListForReading?.OfType<QuestPart_GetGene>().Any() == true);
            if (activeQuest == null)
                return null;

            var questPart = activeQuest.PartsListForReading.OfType<QuestPart_GetGene>().FirstOrDefault();
            if (questPart?.excludedGenes == null)
                return null;

            foreach (var excludedGene in questPart.excludedGenes)
            {
                if (pawn.genes.HasActiveGene(excludedGene))
                    return $"Cannot participate: Has {excludedGene.label} gene - would die in wisteria barrier";
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(TransferableOneWayWidget), "DoRow")]
    public static class TransferableOneWayWidget_DoRow_Patch
    {
        public static void Postfix(TransferableOneWay trad, Rect rect)
        {
            if (!(trad?.AnyThing is Pawn pawn))
                return;

            if (QuestGeneFilterUtility.ShouldDisablePawn(pawn))
            {
                Color oldColor = GUI.color;
                GUI.color = new Color(1f, 0f, 0f, 0.15f);
                Widgets.DrawHighlight(rect);
                GUI.color = oldColor;

                if (Mouse.IsOver(rect))
                {
                    string reason = QuestGeneFilterUtility.GetExclusionReason(pawn);
                    if (reason != null)
                        TooltipHandler.TipRegion(rect, reason);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TransferableOneWay), "CountToTransfer", MethodType.Setter)]
    public static class TransferableOneWay_CountToTransfer_Patch
    {
        public static bool Prefix(TransferableOneWay __instance, int value)
        {
            if (value <= 0)
                return true;

            if (!(__instance.AnyThing is Pawn pawn))
                return true;

            if (QuestGeneFilterUtility.ShouldDisablePawn(pawn))
            {
                Messages.Message($"{pawn.LabelShort} cannot be selected - has excluded gene",
                               MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }
    }
    [HarmonyPatch(typeof(Pawn), "ThreatDisabled")]
    public static class Patch_SelflessState_ThreatDisabled
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result || __instance?.health?.hediffSet == null) return;

            var selflessHediff = __instance.health.hediffSet.hediffs
                .OfType<Hediff_SelflessState>()
                .FirstOrDefault(h => h.IsInvisible);

            if (selflessHediff != null)
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Patch_SelflessState_ClearTarget
    {
        public static void Postfix(Pawn __instance)
        {
            if (!__instance.IsHashIntervalTick(5)) return;

            if (__instance?.mindState?.enemyTarget is Pawn targetPawn && targetPawn.health?.hediffSet != null)
            {
                var selflessHediff = targetPawn.health.hediffSet.hediffs
                    .OfType<Hediff_SelflessState>()
                    .FirstOrDefault(h => h.IsInvisible);

                if (selflessHediff != null)
                {
                    __instance.mindState.enemyTarget = null;

                    if (__instance.CurJobDef?.defName?.Contains("AttackMelee") == true ||
                        __instance.CurJobDef?.defName?.Contains("AttackStatic") == true ||
                        __instance.CurJobDef?.defName?.Contains("Wait_Combat") == true)
                    {
                        __instance.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    }
                }
            }
        }
    }
    [StaticConstructorOnStartup]
    public static class TalentedCompatibilityPatch
    {
        static TalentedCompatibilityPatch()
        {
            var harmony = new Harmony("AnimeArsenal.TalentedPatch");
            harmony.Patch(
                AccessTools.Method(typeof(ExperienceHandler), "HandleJobEnded"),
                prefix: new HarmonyMethod(typeof(TalentedCompatibilityPatch), nameof(HandleJobEnded_Prefix))
            );
        }

        private static bool HandleJobEnded_Prefix(Pawn pawn, Job job, JobCondition condition)
        {
            try
            {
                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    return false;
                }

                if (job == null)
                {
                    return false;
                }

                if (pawn.genes == null)
                {
                    return false;
                }

                if (job.def == null)
                {
                    return false;
                }

                return true;
            }
            catch (System.Exception e)
            {
                Log.Warning($"[AnimeArsenal] Caught exception in Talented HandleJobEnded for {pawn?.Name?.ToStringShort ?? "null"}: {e.Message}");
                return false;
            }
        }
    }
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyToPawn")]
    [HarmonyPriority(Priority.Low)]
    public static class Patch_FixTalentedDamageDealt
    {
        public static void Postfix(DamageWorker.DamageResult __result, DamageInfo dinfo, Pawn pawn)
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing) return;

                if (!(dinfo.Instigator is Pawn attackerPawn)) return;
                if (!attackerPawn.Spawned) return;

                var genes = attackerPawn.genes?.GenesListForReading;
                if (genes == null) return;

                foreach (var gene in genes)
                {
                    var geneType = gene.GetType();
                    if (geneType.Name != "Gene_TalentBase" && !geneType.IsSubclassOf(typeof(Talented.Gene_TalentBase)))
                        continue;

                    var geneDef = gene.def as TalentedGeneDef;
                    if (geneDef?.experienceGainSettings?.experienceTypes == null) continue;

                    foreach (var expType in geneDef.experienceGainSettings.experienceTypes)
                    {
                        if (expType is DamageDealtExperienceTypeDef damageExpType)
                        {
                            float xp = damageExpType.GetExperience(attackerPawn, __result);

                            var gainXpMethod = geneType.GetMethod("GainExperience");
                            if (gainXpMethod != null)
                            {
                                gainXpMethod.Invoke(gene, new object[] { xp });

                                var onExpMethod = geneType.GetMethod("OnExperienceGained");
                                if (onExpMethod != null)
                                {
                                    onExpMethod.Invoke(gene, new object[] { xp, "combat_damage_dealt" });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AnimeArsenal] Error in Talented damage fix: {ex}");
            }
        }
    }
}