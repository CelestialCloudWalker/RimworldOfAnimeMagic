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
    // Enhanced ability definition with hediff application
    public class AbilityCompProperties_DemonConsume : CompProperties_AbilityEffect
    {
        // Hediff application properties
        public HediffDef hediffToApplyOnSelf;
        public HediffDef hediffToApplyOnTarget;
        public float hediffSeverityOnSelf = 1f;
        public float hediffSeverityOnTarget = 1f;
        public float hediffChanceOnSelf = 1f;
        public float hediffChanceOnTarget = 1f;

        // Nutrition properties
        public float nutritionGain = 0.8f; // How much nutrition to restore (0.8 = 80% of max hunger)
        public bool canTargetCorpses = true; // Allow targeting corpses

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
            Pawn targetPawn = null;
            Corpse targetCorpse = null;

            // Handle both living pawns and corpses
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

            // Check if target is a pawn or corpse
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

            // Fixed logic: Allow consuming corpses, dead pawns, or downed pawns
            bool isValidTarget = false;

            if (targetCorpse != null)
            {
                // Targeting a corpse directly - always valid (except for dessication check below)
                isValidTarget = true;
            }
            else if (targetPawn != null)
            {
                // For living pawns, check if they're downed or dead
                if (targetPawn.Dead || targetPawn.Downed)
                {
                    isValidTarget = true;
                }
                // Also allow targeting incapacitated pawns (unconscious, etc.)
                else if (targetPawn.health.InPainShock || targetPawn.health.capacities.CanBeAwake == false)
                {
                    isValidTarget = true;
                }
            }

            if (!isValidTarget)
            {
                if (throwMessages)
                    Messages.Message("Can only consume downed, incapacitated, or dead pawns", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            // Check if corpse is too rotten (optional restriction)
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

            // Calculate healing and resource gain based on target
            float healAmount = CalculateHealAmount(target, corpse);
            float resourceGain = CalculateResourceGain(target, corpse);
            float nutritionGain = CalculateNutritionGain(target, corpse);

            // Apply healing to caster - fix blood loss
            var bloodLossHediff = caster.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLossHediff != null)
            {
                bloodLossHediff.Severity = Mathf.Max(0f, bloodLossHediff.Severity - (healAmount * 0.01f));
                if (bloodLossHediff.Severity <= 0f)
                {
                    caster.health.RemoveHediff(bloodLossHediff);
                }
            }

            // Heal injuries
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

            // Restore blood demon arts resource
            if (BloodGene != null)
            {
                BloodGene.Value = Mathf.Min(BloodGene.Max, BloodGene.Value + (BloodGene.Max * resourceGain));
            }

            // Restore hunger/nutrition
            RestoreHunger(caster, nutritionGain);

            // Apply hediffs
            ApplyConsumptionHediffs(caster, target);

            // Create consumption effects
            CreateConsumptionEffects(corpse != null ? corpse.Position : target.Position, corpse?.Map ?? target.Map);

            // Remove or damage the target
            ProcessTargetAfterConsumption(target, corpse);

            // Add mood/thought effects
            AddConsumptionThoughts(caster, target, corpse != null);
        }

        private void RestoreHunger(Pawn caster, float nutritionAmount)
        {
            if (caster.needs?.food != null)
            {
                // Add nutrition - this will reduce hunger
                caster.needs.food.CurLevel = Mathf.Min(caster.needs.food.MaxLevel,
                    caster.needs.food.CurLevel + nutritionAmount);

                // Create a message about being fed
                if (nutritionAmount > 0.1f)
                {
                    Messages.Message($"{caster.LabelShort} feels satisfied from the consumption.",
                        caster, MessageTypeDefOf.PositiveEvent, false);
                }
            }
        }

        private void ApplyConsumptionHediffs(Pawn caster, Pawn target)
        {
            // Apply hediff to caster (self)
            if (Props.hediffToApplyOnSelf != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnSelf)
            {
                Hediff hediff = caster.health.GetOrAddHediff(Props.hediffToApplyOnSelf);
                hediff.Severity = Props.hediffSeverityOnSelf;
            }

            // Apply hediff to target (only if alive)
            if (!target.Dead && Props.hediffToApplyOnTarget != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnTarget)
            {
                Hediff hediff = target.health.GetOrAddHediff(Props.hediffToApplyOnTarget);
                hediff.Severity = Props.hediffSeverityOnTarget;
            }
        }

        private float CalculateHealAmount(Pawn target, Corpse corpse = null)
        {
            float baseHeal = 50f; // Base healing amount

            // More healing from larger/healthier targets
            float sizeFactor = target.BodySize;
            float healthFactor = corpse != null ? 0.7f : target.health.summaryHealth.SummaryHealthPercent;

            // Reduce healing from rotten corpses
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
            float baseResource = 0.3f; // 30% of max resource

            // Bonus from target's blood or life force
            if (target.RaceProps.BloodDef != null)
                baseResource += 0.1f;

            if (target.RaceProps.IsFlesh)
                baseResource += 0.1f;

            // Reduce from corpses based on rot stage
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

            return baseResource;
        }

        private float CalculateNutritionGain(Pawn target, Corpse corpse = null)
        {
            float baseNutrition = Props.nutritionGain;

            // Scale by body size - larger creatures provide more nutrition
            float sizeFactor = target.BodySize;
            baseNutrition *= sizeFactor;

            // Reduce nutrition from rotten corpses
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

            // Ensure we don't exceed maximum nutrition
            return Mathf.Min(baseNutrition, 1.0f);
        }

        private void CreateConsumptionEffects(IntVec3 position, Map map)
        {
            // Blood splatter effect
            if (map != null)
            {
                FilthMaker.TryMakeFilth(position, map, ThingDefOf.Filth_Blood, 3);

                // Sound effect
                SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(position, map));
            }
        }

        private void ProcessTargetAfterConsumption(Pawn target, Corpse corpse = null)
        {
            if (corpse != null)
            {
                // Consume corpse completely
                if (!corpse.Destroyed)
                {
                    corpse.Destroy();
                }
            }
            else if (target.Dead)
            {
                // Consume dead pawn's corpse
                Corpse targetCorpse = target.Corpse;
                if (targetCorpse != null && !targetCorpse.Destroyed)
                {
                    targetCorpse.Destroy();
                }
            }
            else
            {
                // Deal massive damage to living target
                DamageInfo damage = new DamageInfo(
                    DamageDefOf.Bite,
                    target.MaxHitPoints * 0.8f,
                    999f,
                    -1f,
                    parent.pawn,
                    null,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown
                );

                target.TakeDamage(damage);

                // Add blood loss
                if (!target.Dead)
                {
                    Hediff bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, target);
                    bloodLoss.Severity = 0.8f;
                    target.health.AddHediff(bloodLoss);
                }
                else
                {
                    // If the target died from the damage, consume the resulting corpse
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
            // Only add thoughts if caster has needs
            if (caster.needs?.mood?.thoughts?.memories == null) return;

            try
            {
                // Add different thoughts based on whether it was a corpse or living target
                string thoughtName = wasCorpse ? "DemonCorpseConsumptionMemory" : "DemonConsumptionMemory";
                var consumeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName);
                if (consumeThought != null)
                {
                    caster.needs.mood.thoughts.memories.TryGainMemory(consumeThought);
                }
                else
                {
                    // Fallback to generic consumption thought
                    consumeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("DemonConsumptionMemory");
                    if (consumeThought != null)
                    {
                        caster.needs.mood.thoughts.memories.TryGainMemory(consumeThought);
                    }
                }

                // Witnesses get horrified - use custom witness thought
                if (caster.Map != null)
                {
                    string witnessThoughtName = wasCorpse ? "WitnessedCorpseConsumptionMemory" : "WitnessedDemonConsumptionMemory";
                    var witnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail(witnessThoughtName);
                    if (witnessThought == null)
                    {
                        // Fallback to generic witness thought
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