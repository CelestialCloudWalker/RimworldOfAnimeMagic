using AnimeArsenal;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EMF;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{

    [StaticConstructorOnStartup]
    public static class AnimeArsenalHarmony
    {
        static AnimeArsenalHarmony()
        {
            new Harmony("com.AnimeArsenal").PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    internal static class DemonGeneHelper
    {
        private static readonly List<string> DemonGeneNames = new List<string>
            { "BloodDemonArt_LowerMoon", "BloodDemonArt_UpperMoon", "BloodDemonArt" };

        public static bool HasDemonGene(Pawn pawn)
        {
            if (pawn?.genes == null) return false;
            return pawn.genes.GenesListForReading.Any(g => g is BloodDemonArtsGene);
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
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null) return true;

            if (pawn.genes == null || !pawn.genes.GenesListForReading.Any(g => g is BloodDemonArtsGene)) return true;

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
            if (neck != null && pawn.health.hediffSet.PartIsMissing(neck)) return true;

            bool vitalMissing = pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(part =>
            {
                string dn = part.Part.def.defName;
                return dn == "Head" || dn == "Skull" || dn == "AA_DemonSkull" ||
                       dn == "Brain" || dn == "AA_DemonBrain" ||
                       dn == "Heart" || dn == "AA_DemonHeart";
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

            if (pawn?.genes == null || !pawn.genes.GenesListForReading.Any(g => g is BloodDemonArtsGene)) return;

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
            if (neck != null && !pawn.health.hediffSet.PartIsMissing(neck))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(PawnCapacitiesHandler), "GetLevel")]
    public static class Patch_CapacitiesAfterRegeneration
    {
        private static readonly FieldInfo pawnField =
            typeof(PawnCapacitiesHandler).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

        static void Postfix(PawnCapacitiesHandler __instance, PawnCapacityDef capacity, ref float __result)
        {
            if (pawnField == null) return;
            var pawn = (Pawn)pawnField.GetValue(__instance);

            if (pawn?.genes == null || !pawn.genes.GenesListForReading.Any(g => g is BloodDemonArtsGene)) return;

            if (capacity != PawnCapacityDefOf.BloodPumping &&
                capacity != PawnCapacityDefOf.Consciousness &&
                capacity != PawnCapacityDefOf.Moving) return;

            var neck = pawn.RaceProps?.body?.AllParts?.FirstOrDefault(p =>
                p.def.defName == "Neck" || p.def.defName == "AA_DemonNeck");
            if (neck != null && pawn.health.hediffSet.PartIsMissing(neck)) return;

            bool hasHeart = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(p =>
                p.Part.def.defName == "Heart" || p.Part.def.defName == "AA_DemonHeart");
            bool hasBrain = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(p =>
                p.Part.def.defName == "Brain" || p.Part.def.defName == "AA_DemonBrain");
            bool hasSkull = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(p =>
                p.Part.def.defName == "Skull" || p.Part.def.defName == "AA_DemonSkull");
            bool hasHead = !pawn.health.hediffSet.GetMissingPartsCommonAncestors().Any(p =>
                p.Part.def.defName == "Head");

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
            if (isProcessingGeneDamage || __result.totalDamageDealt <= 0) return;
            if (!(__instance is Pawn victim) || victim.genes == null) return;

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
            var modExtensions = new List<GeneDamageModExtension>();

            void TryAdd(DefModExtension ext)
            {
                if (ext is GeneDamageModExtension g && g.damageOnHit) modExtensions.Add(g);
            }

            if (dinfo.Weapon != null) TryAdd(dinfo.Weapon.GetModExtension<GeneDamageModExtension>());
            if (dinfo.Instigator is Pawn atk && atk.equipment?.Primary != null)
                TryAdd(atk.equipment.Primary.def.GetModExtension<GeneDamageModExtension>());
            if (dinfo.Def != null) TryAdd(dinfo.Def.GetModExtension<GeneDamageModExtension>());
            if (dinfo.Instigator is Pawn atkPawn && atkPawn.genes != null)
                foreach (var gene in atkPawn.genes.GenesListForReading)
                    TryAdd(gene.def.GetModExtension<GeneDamageModExtension>());

            foreach (var modExt in modExtensions)
                if (victim.genes.GenesListForReading.Any(g => g.def.defName == modExt.targetGene))
                    ApplyGeneDamage(victim, modExt, dinfo, totalDamageDealt);
        }

        private static void ApplyGeneDamage(Pawn victim, GeneDamageModExtension modExt, DamageInfo orig, float total)
        {
            float dmg = modExt.useMultiplier ? total * modExt.damageMultiplier : modExt.damageAmount;

            if (modExt.targetBodyParts?.Count > 0)
            {
                foreach (var bpDef in modExt.targetBodyParts)
                {
                    var bp = victim.RaceProps.body.AllParts.FirstOrDefault(p => p.def == bpDef);
                    if (bp != null)
                        modExt.damageType.Worker.Apply(new DamageInfo(modExt.damageType, dmg,
                            modExt.armorPenetration, orig.Angle, orig.Instigator, bp, orig.Weapon), victim);
                }
            }
            else
            {
                modExt.damageType.Worker.Apply(new DamageInfo(modExt.damageType, dmg,
                    modExt.armorPenetration, orig.Angle, orig.Instigator, orig.HitPart, orig.Weapon), victim);
            }
        }
    }
    
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    public static class Verb_MeleeAttackDamage_DamageInfosToApply_Patch
    {
        public static void Postfix(ref IEnumerable<DamageInfo> __result,
            Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            var weapon = __instance.EquipmentSource;
            if (weapon?.Stuff == null) return;
            string mat = weapon.Stuff.defName;
            if (mat != "Scarlet_Crimson_Iron_Sand" && mat != "Scarlet_Ore") return;

            var targetPawn = target.Pawn;
            if (!DemonGeneHelper.HasDemonGene(targetPawn)) return;

            var neckPart = targetPawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(bp => bp.def.defName == "Neck" || bp.def.defName == "AA_DemonNeck");
            if (neckPart == null) return;

            float dmg = mat == "Scarlet_Ore" ? 50f : 40f;
            __result = __result.ToList().Append(new DamageInfo(DamageDefOf.Cut, dmg, 0.8f, -1f,
                __instance.caster, neckPart, weapon.def, DamageInfo.SourceCategory.ThingOrUnknown, weapon));
        }
    }

    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Projectile_Impact_Patch
    {
        public static void Prefix(Projectile __instance, Thing hitThing)
        {
            var launcher = __instance.Launcher;
            if (!(launcher is Pawn pawn) || pawn.equipment?.Primary?.Stuff == null) return;
            var weapon = pawn.equipment.Primary;
            string mat = weapon.Stuff.defName;
            if (mat != "Scarlet_Crimson_Iron_Sand" && mat != "Scarlet_Ore") return;

            if (!(hitThing is Pawn targetPawn) || !DemonGeneHelper.HasDemonGene(targetPawn)) return;

            var neckPart = targetPawn.health.hediffSet.GetNotMissingParts()
                .FirstOrDefault(bp => bp.def.defName == "Neck" || bp.def.defName == "AA_DemonNeck");
            if (neckPart == null) return;

            float dmg = mat == "Scarlet_Ore" ? 50f : 40f;
            targetPawn.TakeDamage(new DamageInfo(DamageDefOf.Cut, dmg, 0.8f, -1f, pawn,
                neckPart, weapon.def, DamageInfo.SourceCategory.ThingOrUnknown, weapon));
        }
    }

    [HarmonyPatch(typeof(Thing), "Ingested")]
    public static class CorpseEatingPatches
    {
        private static readonly HashSet<int> processedCorpses = new HashSet<int>();

        [HarmonyPostfix]
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            if (ingester?.genes == null) return;
            if (!(__instance is Corpse corpse) || corpse.InnerPawn?.RaceProps?.Humanlike != true) return;

            int id = corpse.thingIDNumber;
            if (processedCorpses.Contains(id)) return;

            var demonGene = ingester.genes.GenesListForReading
                .OfType<BloodDemonArtsGene>()
                .FirstOrDefault();

            if (demonGene != null)
            {
                processedCorpses.Add(id);
                demonGene.AddPawnEaten();
                Messages.Message($"{ingester.Name.ToStringShort} consumed {corpse.InnerPawn.Name.ToStringShort}!",
                    ingester, MessageTypeDefOf.PositiveEvent);
                if (!corpse.Destroyed) corpse.Destroy(DestroyMode.Vanish);
                if (processedCorpses.Count > 100) processedCorpses.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class StatWorker_BloodPoolBonus
    {
        private static readonly FieldInfo statField = AccessTools.Field(typeof(StatWorker), "stat");

        public static void Postfix(StatWorker __instance, StatRequest req, ref float __result)
        {
            if (!(req.Thing is Pawn pawn) || pawn.genes == null) return;
            var demonGene = pawn.genes.GenesListForReading.OfType<BloodDemonArtsGene>().FirstOrDefault();
            if (demonGene == null) return;
            float offset = demonGene.GetStatOffset((StatDef)statField.GetValue(__instance));
            if (offset != 0f) __result += offset;
        }
    }

    [HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
    public static class StatWorker_TrainingCompBonus
    {
        [HarmonyPostfix]
        public static void Postfix(StatRequest req, ref float __result, StatDef ___stat)
        {
            if (!req.HasThing || !(req.Thing is Pawn pawn)) return;
            TrainingComp comp = pawn.TryGetComp<TrainingComp>();
            if (comp == null) return;
            float boost = comp.GetStatBoost(___stat.defName);
            if (boost != 0f) __result += boost;
        }
    }

    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ChooseHitPart")]
    public static class Patch_ChooseHitPart
    {
        public static void Postfix(ref BodyPartRecord __result, DamageInfo dinfo, Pawn pawn)
        {
            if (!(dinfo.Instigator is Pawn attacker) || attacker.health?.hediffSet == null) return;
            var twHediff = attacker.health.hediffSet.hediffs
                .FirstOrDefault(h => h.def.defName.StartsWith("TransparentWorld_"));
            if (twHediff == null) return;
            var props = twHediff.def.GetModExtension<TransparentWorldProperties>();
            if (props == null || props.organHitChanceBonus <= 0f) return;

            var organs = pawn.health.hediffSet.GetNotMissingParts()
                .Where(part =>
                    part.def.tags?.Contains(BodyPartTagDefOf.BloodPumpingSource) == true ||
                    part.def.tags?.Contains(BodyPartTagDefOf.BreathingSource) == true ||
                    part.def.tags?.Contains(BodyPartTagDefOf.ConsciousnessSource) == true ||
                    part.def.tags?.Contains(BodyPartTagDefOf.BloodFiltrationSource) == true ||
                    part.def.tags?.Contains(BodyPartTagDefOf.MetabolismSource) == true)
                .ToList();

            if (organs.Any() && Rand.Chance(Mathf.Clamp01(props.organHitChanceBonus)))
                __result = organs.RandomElement();
        }
    }

    [HarmonyPatch(typeof(PawnCapacityUtility), "CalculateCapacityLevel")]
    public static class PawnCapacityUtility_CalculateCapacityLevel_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HediffSet diffSet, PawnCapacityDef capacity, ref float __result)
        {
            Pawn pawn = diffSet?.pawn;
            if (pawn == null) return;

            TrainingComp comp = pawn.TryGetComp<TrainingComp>();
            if (comp != null)
            {
                float boost = comp.GetCapacityBoost(capacity.defName);
                if (boost != 0f) __result = Mathf.Clamp(__result + boost, 0f, 999f);
            }

            if (pawn.genes != null)
            {
                var demonGene = pawn.genes.GenesListForReading
                    .FirstOrDefault(g => g is BloodDemonArtsGene) as BloodDemonArtsGene;
                if (demonGene != null)
                {
                    float demonBoost = demonGene.GetCapacityOffset(capacity);
                    if (demonBoost != 0f) __result = Mathf.Max(0f, __result + demonBoost);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Dialog_FormCaravan), "DoBottomButtons")]
    public static class Dialog_FormCaravan_DoBottomButtons_Patch
    {
        private static readonly FieldInfo transferablesField =
            typeof(Dialog_FormCaravan).GetField("transferables", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool Prefix(Dialog_FormCaravan __instance, Rect rect)
        {
            if (transferablesField == null) return true;
            var transferables = transferablesField.GetValue(__instance) as List<TransferableOneWay>;
            if (transferables == null) return true;

            var quest = GetActiveQuestWithGeneRestrictions();
            if (quest == null) return true;

            var questPart = quest.PartsListForReading?.OfType<QuestPart_GetGene>().FirstOrDefault();
            if (questPart?.excludedGenes == null || !questPart.excludedGenes.Any()) return true;

            foreach (var transferable in transferables)
            {
                if (transferable.CountToTransfer <= 0 || !(transferable.AnyThing is Pawn pawn)) continue;
                if (pawn.genes == null) continue;
                foreach (var excludedGene in questPart.excludedGenes)
                {
                    if (pawn.genes.HasActiveGene(excludedGene))
                    {
                        Messages.Message(
                            $"{pawn.LabelShort} cannot participate — has the {excludedGene.label} gene.",
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
                if (quest.State != QuestState.Ongoing) continue;
                var part = quest.PartsListForReading?.OfType<QuestPart_GetGene>().FirstOrDefault();
                if (part?.excludedGenes != null && part.excludedGenes.Any() &&
                    quest.PartsListForReading?.OfType<QuestPart_LendColonistsToFaction>().Any() == true)
                    return quest;
            }
            return null;
        }
    }

    public static class QuestGeneFilterUtility
    {
        public static bool ShouldDisablePawn(Pawn pawn)
        {
            if (pawn?.genes == null) return false;
            var part = GetActiveQuestPart();
            return part?.excludedGenes?.Any(g => pawn.genes.HasActiveGene(g)) == true;
        }

        public static string GetExclusionReason(Pawn pawn)
        {
            if (pawn?.genes == null) return null;
            var part = GetActiveQuestPart();
            var gene = part?.excludedGenes?.FirstOrDefault(g => pawn.genes.HasActiveGene(g));
            return gene == null ? null : $"Cannot participate: Has {gene.label} gene — would die in wisteria barrier";
        }

        private static QuestPart_GetGene GetActiveQuestPart() =>
            Find.QuestManager.QuestsListForReading
                .Where(q => q.State == QuestState.Ongoing)
                .SelectMany(q => q.PartsListForReading.OfType<QuestPart_GetGene>())
                .FirstOrDefault(p => p.excludedGenes != null);
    }

    [HarmonyPatch(typeof(TransferableOneWayWidget), "DoRow")]
    public static class TransferableOneWayWidget_DoRow_Patch
    {
        public static void Postfix(TransferableOneWay trad, Rect rect)
        {
            if (!(trad?.AnyThing is Pawn pawn) || !QuestGeneFilterUtility.ShouldDisablePawn(pawn)) return;
            Color old = GUI.color;
            GUI.color = new Color(1f, 0f, 0f, 0.15f);
            Widgets.DrawHighlight(rect);
            GUI.color = old;
            if (Mouse.IsOver(rect))
            {
                string reason = QuestGeneFilterUtility.GetExclusionReason(pawn);
                if (reason != null) TooltipHandler.TipRegion(rect, reason);
            }
        }
    }

    [HarmonyPatch(typeof(TransferableOneWay), "CountToTransfer", MethodType.Setter)]
    public static class TransferableOneWay_CountToTransfer_Patch
    {
        public static bool Prefix(TransferableOneWay __instance, int value)
        {
            if (value <= 0 || !(__instance.AnyThing is Pawn pawn)) return true;
            if (QuestGeneFilterUtility.ShouldDisablePawn(pawn))
            {
                Messages.Message($"{pawn.LabelShort} cannot be selected — has excluded gene",
                    MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return true;
        }
    }
    public static class SelflessStateUtility
    {
        public static bool HasActiveInvisibility(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_SelflessState)
                {
                    var comp = hediff.TryGetComp<HediffComp_Invisibility>();
                    if (comp != null && !comp.PsychologicallyVisible)
                        return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), "ThreatDisabled")]
    public static class Patch_SelflessState_ThreatDisabled
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result) return;
            if (SelflessStateUtility.HasActiveInvisibility(__instance))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Pawn), "Tick")]
    public static class Patch_SelflessState_ClearTarget
    {
        public static void Postfix(Pawn __instance)
        {
            if (!__instance.IsHashIntervalTick(5)) return;
            if (!(__instance?.mindState?.enemyTarget is Pawn targetPawn)) return;
            if (!SelflessStateUtility.HasActiveInvisibility(targetPawn)) return;
            __instance.mindState.enemyTarget = null;
            string job = __instance.CurJobDef?.defName;
            if (job != null && (job.Contains("AttackMelee") || job.Contains("AttackStatic") || job.Contains("Wait_Combat")))
                __instance.jobs?.EndCurrentJob(JobCondition.InterruptForced);
        }
    }


    [HarmonyPatch(typeof(Pawn), nameof(Pawn.HealthScale), MethodType.Getter)]
    public static class Pawn_HealthScale_Patch
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            if (__instance?.genes == null) return;
            var demonGene = __instance.genes.GenesListForReading
                .FirstOrDefault(g => g is BloodDemonArtsGene) as BloodDemonArtsGene;
            if (demonGene == null || !(demonGene.def is BloodDemonArtsGeneDef demonDef)) return;

            float mult = 1f;
            if (demonDef.bodyPartHealthPerRank?.Count > 0)
            {
                int idx = Mathf.Min((int)demonGene.CurrentRank, demonDef.bodyPartHealthPerRank.Count - 1);
                mult = demonDef.bodyPartHealthPerRank[idx];
            }
            else if (demonDef.bodyPartHealthBonusPerPawnEaten > 0f)
            {
                mult = demonDef.bodyPartHealthBaseMultiplier +
                       demonGene.TotalPawnsEaten * demonDef.bodyPartHealthBonusPerPawnEaten;
                if (demonDef.bodyPartHealthMaxMultiplier > 0f)
                    mult = Mathf.Min(mult, demonDef.bodyPartHealthMaxMultiplier);
            }
            __result *= mult;
        }
    }

    [HarmonyPatch(typeof(FoodUtility), "WillEat",
        new Type[] { typeof(Pawn), typeof(Thing), typeof(Pawn), typeof(bool), typeof(bool) })]
    public static class Patch_FoodUtility_WillEat_DemonsOnlyEatHumans
    {
        public static void Postfix(ref bool __result, Pawn p, Thing food)
        {
            if (!__result || p?.genes == null) return;
            if (!p.genes.GenesListForReading.Any(g => g is BloodDemonArtsGene)) return;
            __result = food is Corpse c && c.InnerPawn?.RaceProps?.Humanlike == true;
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Patch_JobGiver_GetFood_PreventDemonNormalFood
    {
        public static bool Prefix(Pawn pawn, ref Job __result)
        {
            if (pawn?.genes == null) return true;
            if (!pawn.genes.GenesListForReading.Any(g => g is BloodDemonArtsGene)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Need_Food), "NeedInterval")]
    public static class Patch_Need_Food_SyncWithDemonSanity
    {
        private static readonly FieldInfo curLevelInt = typeof(Need)
            .GetField("curLevelInt", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo pawnField = typeof(Need)
            .GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Postfix(Need_Food __instance)
        {
            Pawn pawn = (Pawn)pawnField.GetValue(__instance);
            if (pawn?.genes == null) return;
            var demonGene = pawn.genes.GenesListForReading
                .OfType<BloodDemonArtsGene>()
                .FirstOrDefault();
            if (demonGene == null || demonGene.def.GetModExtension<DemonSanityExtension>() == null) return;
            curLevelInt.SetValue(__instance, demonGene.SanityPercent * __instance.MaxLevel);
        }
    }

    [HarmonyPatch(typeof(Need_Food), "FoodFallPerTick", MethodType.Getter)]
    public static class Patch_Need_Food_PreventDemonHungerDecay
    {
        private static readonly FieldInfo pawnField = typeof(Need)
            .GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Postfix(Need_Food __instance, ref float __result)
        {
            Pawn pawn = (Pawn)pawnField.GetValue(__instance);
            if (pawn?.genes?.GenesListForReading.Any(g => g is BloodDemonArtsGene) == true)
                __result = 0f;
        }
    }

    [HarmonyPatch(typeof(Need_Food), "GetTipString")]
    public static class Patch_Need_Food_DemonTooltip
    {
        private static readonly FieldInfo pawnField = typeof(Need)
            .GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Postfix(Need_Food __instance, ref string __result)
        {
            Pawn pawn = (Pawn)pawnField.GetValue(__instance);
            if (pawn?.genes == null) return;
            var demonGene = pawn.genes.GenesListForReading
                .OfType<BloodDemonArtsGene>()
                .FirstOrDefault();
            if (demonGene == null || demonGene.def.GetModExtension<DemonSanityExtension>() == null) return;

            float sp = demonGene.SanityPercent;
            Color col = sp <= 0.1f ? Color.red : sp <= 0.25f ? new Color(1f, 0.5f, 0f) :
                        sp <= 0.5f ? Color.yellow : Color.green;
            string status = sp <= 0.1f ? "Ravenous" : sp <= 0.25f ? "Starving" :
                            sp <= 0.5f ? "Hungry" : sp >= 0.9f ? "Satiated" : "Normal";

            __result = (__instance.def.LabelCap + ": " + __instance.CurLevelPercentage.ToStringPercent())
                           .Colorize(ColoredText.TipSectionTitleColor)
                       + $" ({__instance.CurLevel:0.##} / {__instance.MaxLevel:0.##})"
                       + "\n\nDemon Hunger (Sanity-based)"
                       + "\nStatus: " + status.Colorize(col)
                       + $"\nSanity: {demonGene.CurrentSanity:F0} / {demonGene.MaxSanity:F0}"
                       + "\n\nDemons must consume humans to maintain sanity.";
        }
    }

    [HarmonyPatch(typeof(Need_Food), "GUIChangeArrow", MethodType.Getter)]
    public static class Patch_Need_Food_DemonArrow
    {
        private static readonly FieldInfo pawnField = typeof(Need)
            .GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Postfix(Need_Food __instance, ref int __result)
        {
            Pawn pawn = (Pawn)pawnField.GetValue(__instance);
            if (pawn?.genes == null) return;
            var demonGene = pawn.genes.GenesListForReading
                .OfType<BloodDemonArtsGene>()
                .FirstOrDefault();
            if (demonGene != null)
                __result = demonGene.SanityPercent < 0.9f ? -1 : 0;
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_MeleeAttack_BreathingKillTracker
    {
        internal static readonly HashSet<int> _creditedKills = new HashSet<int>();

        public static void Prefix(LocalTargetInfo target, Verb_MeleeAttackDamage __instance,
            out (Pawn attacker, Pawn target, bool wasDead) __state)
        {
            Pawn targetPawn = target.Thing as Pawn;
            __state = (__instance.CasterPawn, targetPawn, targetPawn?.Dead ?? true);
        }

        public static void Postfix((Pawn attacker, Pawn target, bool wasDead) __state)
        {
            if (__state.attacker == null || __state.target == null) return;
            if (__state.wasDead || !__state.target.Dead) return;
            if (__state.attacker.genes == null) return;
            if (!_creditedKills.Add(__state.target.thingIDNumber)) return;
            if (_creditedKills.Count > 200) _creditedKills.Clear();

            foreach (var gene in __state.attacker.genes.GenesListForReading)
            {
                if (gene is BreathingTechniqueGene bg && bg.Active && !bg.IsStyleGene) { bg.NotifyKill(__state.target); return; }
                if (gene is HybridDemonBreathGene hg && hg.Active) { hg.NotifyKill(__state.target); return; }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_BreathingKillTracker
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (__instance == null || !__instance.Dead) return;
            if (dinfo == null) return;

            Pawn attacker = dinfo.Value.Instigator as Pawn;
            if (attacker?.genes == null) return;

            if (!Patch_MeleeAttack_BreathingKillTracker._creditedKills.Add(__instance.thingIDNumber)) return;
            if (Patch_MeleeAttack_BreathingKillTracker._creditedKills.Count > 200)
                Patch_MeleeAttack_BreathingKillTracker._creditedKills.Clear();

            foreach (var gene in attacker.genes.GenesListForReading)
            {
                if (gene is BreathingTechniqueGene bg && bg.Active && !bg.IsStyleGene) { bg.NotifyKill(__instance); return; }
                if (gene is HybridDemonBreathGene hg && hg.Active) { hg.NotifyKill(__instance); return; }
            }
        }
    }

    public static class ResetTreeTracker
    {
        public static bool AllowCustomTreeReset = false;
    }
     [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.ScaleFor))]
    public static class Patch_PawnRenderNodeWorker_ScaleFor
    {
        public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (node?.Props?.tagDef?.defName != "Root") return;
 
            Pawn pawn = parms.pawn;
            if (pawn?.health?.hediffSet == null) return;
 
            float lowestFactor = 1.0f;
 
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                HediffExtension_RenderScale ext =
                    hediff.def.GetModExtension<HediffExtension_RenderScale>();
                if (ext == null) continue;
                if (ext.bodyScaleFactor < lowestFactor)
                    lowestFactor = ext.bodyScaleFactor;
            }
 
            if (lowestFactor < 1.0f)
                __result *= lowestFactor;
        }
    }

    [HarmonyPatch(typeof(HediffComp_Invisibility), "GetAlpha")]
    public static class Patch_Invisibility_PlayerGhost
    {
        public static void Postfix(HediffComp_Invisibility __instance, ref float __result)
        {
            if (__instance.Props.visibleToPlayer
                && __instance.parent.pawn.Faction?.IsPlayer == true)
            {
                __result = 0.2f;
            }
        }
    }
}