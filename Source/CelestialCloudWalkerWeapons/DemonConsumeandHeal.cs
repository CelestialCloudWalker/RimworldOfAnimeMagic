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
            Pawn targetPawn = target.Pawn;
            if (targetPawn == null) return;

            ConsumeTarget(targetPawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!(target.Thing is Pawn targetPawn))
            {
                if (throwMessages)
                    Messages.Message("Must target a pawn", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (targetPawn == parent.pawn)
            {
                if (throwMessages)
                    Messages.Message("Cannot target self", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (!targetPawn.Dead && targetPawn.Faction == parent.pawn.Faction)
            {
                if (throwMessages)
                    Messages.Message("Cannot target living allies", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }

        private void ConsumeTarget(Pawn target)
        {
            Pawn caster = parent.pawn;

            // Calculate healing and resource gain based on target
            float healAmount = CalculateHealAmount(target);
            float resourceGain = CalculateResourceGain(target);

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

            // Apply hediffs
            ApplyConsumptionHediffs(caster, target);

            // Create consumption effects
            CreateConsumptionEffects(target);

            // Remove or damage the target
            ProcessTargetAfterConsumption(target);

            // Add mood/thought effects
            AddConsumptionThoughts(caster, target);
        }

        private void ApplyConsumptionHediffs(Pawn caster, Pawn target)
        {
            // Apply hediff to caster (self)
            if (Props.hediffToApplyOnSelf != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnSelf)
            {
                Hediff hediff = caster.health.GetOrAddHediff(Props.hediffToApplyOnSelf);
                hediff.Severity = Props.hediffSeverityOnSelf;
            }

            // Apply hediff to target
            if (Props.hediffToApplyOnTarget != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnTarget)
            {
                Hediff hediff = target.health.GetOrAddHediff(Props.hediffToApplyOnTarget);
                hediff.Severity = Props.hediffSeverityOnTarget;
            }
        }

        private float CalculateHealAmount(Pawn target)
        {
            float baseHeal = 50f; // Base healing amount

            // More healing from larger/healthier targets
            float sizeFactor = target.BodySize;
            float healthFactor = target.health.summaryHealth.SummaryHealthPercent;

            return baseHeal * sizeFactor * (0.5f + healthFactor * 0.5f);
        }

        private float CalculateResourceGain(Pawn target)
        {
            float baseResource = 0.3f; // 30% of max resource

            // Bonus from target's blood or life force
            if (target.RaceProps.BloodDef != null)
                baseResource += 0.1f;

            if (target.RaceProps.IsFlesh)
                baseResource += 0.1f;

            return baseResource;
        }

        private void CreateConsumptionEffects(Pawn target)
        {
            // Blood splatter effect
            if (target.Map != null)
            {
                FilthMaker.TryMakeFilth(target.Position, target.Map, ThingDefOf.Filth_Blood, 3);

                // Sound effect
                SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(target.Position, target.Map));
            }
        }

        private void ProcessTargetAfterConsumption(Pawn target)
        {
            if (target.Dead)
            {
                // Consume corpse completely
                Corpse corpse = target.Corpse;
                if (corpse != null && !corpse.Destroyed)
                {
                    // Trigger disappearance logic for the corpse
                    BodyDisappearUtility.ProcessCorpseDisappearance(corpse);
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
                    // If the target died from the damage, register it for disappearance
                    BodyDisappearUtility.RegisterPawnForDisappearance(target);
                }
            }
        }

        private void AddConsumptionThoughts(Pawn caster, Pawn target)
        {
            // Only add thoughts if caster has needs
            if (caster.needs?.mood?.thoughts?.memories == null) return;

            try
            {
                // Add custom demon consumption thought using TryGainMemory directly
                var consumeThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("DemonConsumptionMemory");
                if (consumeThought != null)
                {
                    caster.needs.mood.thoughts.memories.TryGainMemory(consumeThought);
                }

                // Witnesses get horrified - use custom witness thought
                if (caster.Map != null)
                {
                    var witnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("WitnessedDemonConsumptionMemory");
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