using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class AbilityCompProperties_DemonConsume : CompProperties_AbilityEffect
    {
        public HediffDef hediffToApplyOnSelf;
        public HediffDef hediffToApplyOnTarget;
        public float hediffSeverityOnSelf = 1f;
        public float hediffSeverityOnTarget = 1f;
        public float hediffChanceOnSelf = 1f;
        public float hediffChanceOnTarget = 1f;

        public float nutritionGain = 0.8f;
        public bool canTargetCorpses = true;

        public bool canTargetLiving = false;
        public float healAmount = 15f;
        public float resourceRestore = 10f;
        public float bloodlustSeverity = 1f;
        public float drainSeverity = 1f;
        public float range = 1.5f;
        public int maxTargets = 1;
        public bool areaOfEffect = false;
        public float instantKillChance = 0f;

        public AbilityCompProperties_DemonConsume()
        {
            compClass = typeof(CompAbilityEffect_DemonConsume);
        }
    }

    public class CompAbilityEffect_DemonConsume : CompAbilityEffect
    {
        public new AbilityCompProperties_DemonConsume Props => (AbilityCompProperties_DemonConsume)props;

        private BloodDemonArtsGene BloodGene => parent.pawn.genes?.GetFirstGeneOfType<BloodDemonArtsGene>();

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            List<LocalTargetInfo> targets = new List<LocalTargetInfo>();

            if (Props.areaOfEffect && Props.maxTargets > 1)
            {
                targets = FindTargetsInArea(target, Props.maxTargets);
            }
            else
            {
                targets.Add(target);
            }

            foreach (var tgt in targets)
            {
                ProcessSingleTarget(tgt);
            }
        }

        private List<LocalTargetInfo> FindTargetsInArea(LocalTargetInfo centerTarget, int maxTargets)
        {
            List<LocalTargetInfo> targets = new List<LocalTargetInfo>();
            targets.Add(centerTarget);

            if (centerTarget.Thing?.Map == null) return targets;

            var cellsInRange = GenRadial.RadialCellsAround(centerTarget.Cell, Props.range, true)
                .Take(100) 
                .ToList();

            foreach (var cell in cellsInRange)
            {
                if (targets.Count >= maxTargets) break;

                var things = cell.GetThingList(centerTarget.Thing.Map);
                foreach (var thing in things)
                {
                    if (targets.Count >= maxTargets) break;

                    LocalTargetInfo potentialTarget = new LocalTargetInfo(thing);
                    if (thing != centerTarget.Thing && Valid(potentialTarget, false))
                    {
                        targets.Add(potentialTarget);
                    }
                }
            }

            return targets;
        }

        private void ProcessSingleTarget(LocalTargetInfo target)
        {
            Pawn targetPawn = null;
            Corpse targetCorpse = null;

            if (target.Thing is Pawn pawn)
            {
                targetPawn = pawn;
            }
            else if (target.Thing is Corpse corpse)
            {
                targetCorpse = corpse;
                targetPawn = corpse.InnerPawn;
            }

            if (targetPawn == null) return;

            ConsumeTarget(targetPawn, targetCorpse);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn targetPawn = null;
            Corpse targetCorpse = null;

            if (target.Thing is Pawn pawn)
            {
                targetPawn = pawn;
            }
            else if (target.Thing is Corpse corpse && Props.canTargetCorpses)
            {
                targetCorpse = corpse;
                targetPawn = corpse.InnerPawn;
            }
            else
            {
                if (throwMessages)
                    Messages.Message("Must target a pawn or corpse", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (targetPawn == parent.pawn)
            {
                if (throwMessages)
                    Messages.Message("Cannot target self", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            bool isValidTarget = false;

            if (targetCorpse != null)
            {
                isValidTarget = true;
            }
            else if (targetPawn != null)
            {
                if (Props.canTargetLiving && !targetPawn.Dead)
                {
                    isValidTarget = true;
                }
                else if (targetPawn.Dead || targetPawn.Downed)
                {
                    isValidTarget = true;
                }
                else if (targetPawn.health.InPainShock || targetPawn.health.capacities.CanBeAwake == false)
                {
                    isValidTarget = true;
                }
            }

            if (!isValidTarget)
            {
                string message = Props.canTargetLiving ?
                    "Cannot target this pawn" :
                    "Can only consume downed, incapacitated, or dead pawns";

                if (throwMessages)
                    Messages.Message(message, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (targetCorpse != null && targetCorpse.GetRotStage() == RotStage.Dessicated)
            {
                if (throwMessages)
                    Messages.Message("Corpse too dessicated to consume", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }

        private void ConsumeTarget(Pawn target, Corpse corpse = null)
        {
            Pawn caster = parent.pawn;

            if (!target.Dead && Props.instantKillChance > 0f && Rand.Range(0f, 1f) <= Props.instantKillChance)
            {
                PerformInstantKill(target, corpse);
                return;
            }

            float healAmount = CalculateHealAmount(target, corpse);
            float resourceGain = CalculateResourceGain(target, corpse);
            float nutritionGain = CalculateNutritionGain(target, corpse);

            var bloodLossHediff = caster.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLossHediff != null)
            {
                bloodLossHediff.Severity = Mathf.Max(0f, bloodLossHediff.Severity - (healAmount * 0.01f));
                if (bloodLossHediff.Severity <= 0f)
                {
                    caster.health.RemoveHediff(bloodLossHediff);
                }
            }

            var injuries = caster.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.Severity > 0)
                .ToList();

            float remainingHeal = healAmount;

            for (int i = injuries.Count - 1; i >= 0; i--)
            {
                if (remainingHeal <= 0) break;

                var injury = injuries[i];
                float healThisInjury = Mathf.Min(remainingHeal, injury.Severity);
                injury.Severity = Mathf.Max(0f, injury.Severity - healThisInjury);
                remainingHeal -= healThisInjury;

                if (injury.Severity <= 0f)
                {
                    injury.PostRemoved();
                }
            }

            if (BloodGene != null)
            {
                BloodGene.Value = Mathf.Min(BloodGene.Max, BloodGene.Value + Props.resourceRestore);
            }

            RestoreHunger(caster, nutritionGain);

            ApplyConsumptionHediffs(caster, target);

            CreateConsumptionEffects(corpse != null ? corpse.Position : target.Position, corpse?.Map ?? target.Map);

            ProcessTargetAfterConsumption(target, corpse);

            AddConsumptionThoughts(caster, target, corpse != null);
        }

        private void PerformInstantKill(Pawn target, Corpse corpse = null)
        {
            Pawn caster = parent.pawn;

            if (target.Dead) return;

            Messages.Message($"{caster.LabelShort} devours {target.LabelShort} completely!",
                caster, MessageTypeDefOf.NeutralEvent, false);

            DamageInfo killDamage = new DamageInfo(
                DamageDefOf.Bite,
                target.MaxHitPoints * 2f,
                999f,
                -1f,
                caster,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
            );

            target.TakeDamage(killDamage);

            float healAmount = Props.healAmount * 1.5f; 
            float resourceGain = Props.resourceRestore * 1.5f;
            float nutritionGain = Props.nutritionGain * 1.2f;

            var injuries = caster.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.Severity > 0)
                .ToList();

            foreach (var injury in injuries.Take(3))
            {
                injury.Severity = Mathf.Max(0f, injury.Severity - (healAmount / 3f));
                if (injury.Severity <= 0f)
                {
                    injury.PostRemoved();
                }
            }

            if (BloodGene != null)
            {
                BloodGene.Value = Mathf.Min(BloodGene.Max, BloodGene.Value + resourceGain);
            }

            RestoreHunger(caster, nutritionGain);

            CreateConsumptionEffects(target.Position, target.Map);

            if (target.Dead)
            {
                Corpse newCorpse = target.Corpse;
                if (newCorpse != null && !newCorpse.Destroyed)
                {
                    newCorpse.Destroy();
                }
            }

            AddConsumptionThoughts(caster, target, false);
        }

        private void RestoreHunger(Pawn caster, float nutritionAmount)
        {
            if (caster.needs?.food != null)
            {
                caster.needs.food.CurLevel = Mathf.Min(caster.needs.food.MaxLevel,
                    caster.needs.food.CurLevel + nutritionAmount);

                if (nutritionAmount > 0.1f)
                {
                    Messages.Message($"{caster.LabelShort} feels satisfied from the consumption.",
                        caster, MessageTypeDefOf.PositiveEvent, false);
                }
            }
        }

        private void ApplyConsumptionHediffs(Pawn caster, Pawn target)
        {
            if (Props.hediffToApplyOnSelf != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnSelf)
            {
                Hediff hediff = caster.health.GetOrAddHediff(Props.hediffToApplyOnSelf);
                hediff.Severity = Props.bloodlustSeverity; 
            }

            if (!target.Dead && Props.hediffToApplyOnTarget != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnTarget)
            {
                Hediff hediff = target.health.GetOrAddHediff(Props.hediffToApplyOnTarget);
                hediff.Severity = Props.drainSeverity; 
            }
        }

        private float CalculateHealAmount(Pawn target, Corpse corpse = null)
        {
            float baseHeal = Props.healAmount; 

            float sizeFactor = target.BodySize;
            float healthFactor = corpse != null ? 0.7f : target.health.summaryHealth.SummaryHealthPercent;

            if (corpse != null)
            {
                switch (corpse.GetRotStage())
                {
                    case RotStage.Fresh:
                        healthFactor = 0.9f;
                        break;
                    case RotStage.Rotting:
                        healthFactor = 0.6f;
                        break;
                    case RotStage.Dessicated:
                        healthFactor = 0.2f;
                        break;
                }
            }

            return baseHeal * sizeFactor * (0.5f + healthFactor * 0.5f);
        }

        private float CalculateResourceGain(Pawn target, Corpse corpse = null)
        {
            float baseResource = Props.resourceRestore / 100f;

            if (target.RaceProps.BloodDef != null)
                baseResource += 0.1f;

            if (target.RaceProps.IsFlesh)
                baseResource += 0.1f;

            if (corpse != null)
            {
                switch (corpse.GetRotStage())
                {
                    case RotStage.Fresh:
                        baseResource *= 0.9f;
                        break;
                    case RotStage.Rotting:
                        baseResource *= 0.6f;
                        break;
                    case RotStage.Dessicated:
                        baseResource *= 0.3f;
                        break;
                }
            }

            return baseResource * 100f; 
        }

        private float CalculateNutritionGain(Pawn target, Corpse corpse = null)
        {
            float baseNutrition = Props.nutritionGain;

            float sizeFactor = target.BodySize;
            baseNutrition *= sizeFactor;

            if (corpse != null)
            {
                switch (corpse.GetRotStage())
                {
                    case RotStage.Fresh:
                        baseNutrition *= 0.95f;
                        break;
                    case RotStage.Rotting:
                        baseNutrition *= 0.7f;
                        break;
                    case RotStage.Dessicated:
                        baseNutrition *= 0.4f;
                        break;
                }
            }

            return Mathf.Min(baseNutrition, 1.0f);
        }

        private void CreateConsumptionEffects(IntVec3 position, Map map)
        {
            if (map != null)
            {
                FilthMaker.TryMakeFilth(position, map, ThingDefOf.Filth_Blood, 3);

                SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(position, map));
            }
        }

        private void ProcessTargetAfterConsumption(Pawn target, Corpse corpse = null)
        {
            if (corpse != null)
            {
                if (!corpse.Destroyed)
                {
                    corpse.Destroy();
                }
            }
            else if (target.Dead)
            {
                Corpse targetCorpse = target.Corpse;
                if (targetCorpse != null && !targetCorpse.Destroyed)
                {
                    targetCorpse.Destroy();
                }
            }
            else
            {
                
                float drainDamage = target.MaxHitPoints * 0.6f * (Props.drainSeverity / 2f); 

                DamageInfo damage = new DamageInfo(
                    DamageDefOf.Bite,
                    drainDamage,
                    999f,
                    -1f,
                    parent.pawn,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown
                );

                target.TakeDamage(damage);

                if (!target.Dead)
                {
                    Hediff bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, target);
                    bloodLoss.Severity = Props.drainSeverity * 0.3f; 
                    target.health.AddHediff(bloodLoss);
                }
                else
                {
                    Corpse newCorpse = target.Corpse;
                    if (newCorpse != null && !newCorpse.Destroyed)
                    {
                        newCorpse.Destroy();
                    }
                }
            }
        }

        private void AddConsumptionThoughts(Pawn caster, Pawn target, bool wasCorpse)
        {
            if (caster.needs?.mood?.thoughts?.memories == null) return;

            try
            {
                string thoughtName = wasCorpse ? "DemonCorpseConsumptionMemory" : "DemonConsumptionMemory";
                var consumeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
                if (consumeThought != null)
                {
                    caster.needs.mood.thoughts.memories.TryGainMemory(consumeThought);
                }
                else
                {
                    consumeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("DemonConsumptionMemory");
                    if (consumeThought != null)
                    {
                        caster.needs.mood.thoughts.memories.TryGainMemory(consumeThought);
                    }
                }

                if (caster.Map != null)
                {
                    string witnessThoughtName = wasCorpse ? "WitnessedCorpseConsumptionMemory" : "WitnessedDemonConsumptionMemory";
                    var witnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail(witnessThoughtName);
                    if (witnessThought == null)
                    {
                        witnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WitnessedDemonConsumptionMemory");
                    }

                    if (witnessThought != null)
                    {
                        var witnessRadius = GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, 10f, true);
                        foreach (var thing in witnessRadius)
                        {
                            if (thing is Pawn witness && witness != caster && witness.RaceProps.Humanlike)
                            {
                                if (GenSight.LineOfSight(caster.Position, witness.Position, caster.Map))
                                {
                                    if (witness.needs?.mood?.thoughts?.memories != null)
                                    {
                                        witness.needs.mood.thoughts.memories.TryGainMemory(witnessThought, caster);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[AnimeArsenal] Error adding consumption thoughts: {ex.Message}");
            }
        }
    }
}