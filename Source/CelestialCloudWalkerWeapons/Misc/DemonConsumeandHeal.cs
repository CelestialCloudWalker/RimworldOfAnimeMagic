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
            var targets = new List<LocalTargetInfo>();

            if (Props.areaOfEffect && Props.maxTargets > 1)
                targets = GetNearbyTargets(target, Props.maxTargets);
            else
                targets.Add(target);

            foreach (var t in targets)
                ConsumeTarget(t);
        }

        private List<LocalTargetInfo> GetNearbyTargets(LocalTargetInfo center, int max)
        {
            var targets = new List<LocalTargetInfo> { center };

            if (center.Thing?.Map == null) return targets;

            var cells = GenRadial.RadialCellsAround(center.Cell, Props.range, true).Take(100);

            foreach (var cell in cells)
            {
                if (targets.Count >= max) break;

                foreach (var thing in cell.GetThingList(center.Thing.Map))
                {
                    if (targets.Count >= max) break;

                    var potential = new LocalTargetInfo(thing);
                    if (thing != center.Thing && Valid(potential, false))
                        targets.Add(potential);
                }
            }

            return targets;
        }

        private void ConsumeTarget(LocalTargetInfo target)
        {
            Pawn pawn = null;
            Corpse corpse = null;

            if (target.Thing is Pawn p)
                pawn = p;
            else if (target.Thing is Corpse c)
            {
                corpse = c;
                pawn = c.InnerPawn;
            }

            if (pawn == null) return;

            DoConsume(pawn, corpse);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = null;
            Corpse corpse = null;

            if (target.Thing is Pawn p)
                pawn = p;
            else if (target.Thing is Corpse c && Props.canTargetCorpses)
            {
                corpse = c;
                pawn = c.InnerPawn;
            }
            else
            {
                if (throwMessages)
                    Messages.Message("Must target a pawn or corpse", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (pawn == parent.pawn)
            {
                if (throwMessages)
                    Messages.Message("Cannot target self", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            bool canConsume = false;

            if (corpse != null)
            {
                canConsume = true;
            }
            else if (pawn != null)
            {
                if (Props.canTargetLiving && !pawn.Dead)
                    canConsume = true;
                else if (pawn.Dead || pawn.Downed)
                    canConsume = true;
                else if (pawn.health.InPainShock || !pawn.health.capacities.CanBeAwake)
                    canConsume = true;
            }

            if (!canConsume)
            {
                if (throwMessages)
                {
                    string msg = Props.canTargetLiving ?
                        "Cannot target this pawn" :
                        "Can only consume downed, incapacitated, or dead pawns";
                    Messages.Message(msg, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }

            if (corpse?.GetRotStage() == RotStage.Dessicated)
            {
                if (throwMessages)
                    Messages.Message("Corpse too dessicated to consume", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }

        private void DoConsume(Pawn target, Corpse corpse = null)
        {
            var caster = parent.pawn;

            
            if (!target.Dead && Props.instantKillChance > 0f && Rand.Range(0f, 1f) <= Props.instantKillChance)
            {
                DevourCompletely(target, corpse);
                return;
            }

            float heal = GetHealAmount(target, corpse);
            float resource = GetResourceGain(target, corpse);
            float nutrition = GetNutritionGain(target, corpse);

            
            var bloodLoss = caster.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss != null)
            {
                bloodLoss.Severity = Mathf.Max(0f, bloodLoss.Severity - (heal * 0.01f));
                if (bloodLoss.Severity <= 0f)
                    caster.health.RemoveHediff(bloodLoss);
            }

            
            var injuries = caster.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.Severity > 0)
                .ToList();

            float healLeft = heal;
            for (int i = injuries.Count - 1; i >= 0; i--)
            {
                if (healLeft <= 0) break;

                var injury = injuries[i];
                float healThis = Mathf.Min(healLeft, injury.Severity);
                injury.Severity = Mathf.Max(0f, injury.Severity - healThis);
                healLeft -= healThis;

                if (injury.Severity <= 0f)
                    injury.PostRemoved();
            }

            
            if (BloodGene != null)
                BloodGene.Value = Mathf.Min(BloodGene.Max, BloodGene.Value + Props.resourceRestore);

            
            FeedCaster(caster, nutrition);

            
            TryApplyHediffs(caster, target);

            
            MakeEffects(corpse?.Position ?? target.Position, corpse?.Map ?? target.Map);

            
            ProcessTarget(target, corpse);

            
            HandleThoughts(caster, target, corpse != null);
        }

        private void DevourCompletely(Pawn target, Corpse corpse = null)
        {
            var caster = parent.pawn;

            if (target.Dead) return;

            Messages.Message($"{caster.LabelShort} devours {target.LabelShort} completely!",
                caster, MessageTypeDefOf.NeutralEvent, false);

            var killDmg = new DamageInfo(DamageDefOf.Bite, target.MaxHitPoints * 2f, 999f, -1f, caster, null, null, DamageInfo.SourceCategory.ThingOrUnknown);
            target.TakeDamage(killDmg);

            float heal = Props.healAmount * 1.5f;
            float resource = Props.resourceRestore * 1.5f;
            float nutrition = Props.nutritionGain * 1.2f;

            var injuries = caster.health.hediffSet.hediffs
                .OfType<Hediff_Injury>()
                .Where(x => x.Severity > 0)
                .ToList();

            foreach (var injury in injuries.Take(3))
            {
                injury.Severity = Mathf.Max(0f, injury.Severity - (heal / 3f));
                if (injury.Severity <= 0f)
                    injury.PostRemoved();
            }

            if (BloodGene != null)
                BloodGene.Value = Mathf.Min(BloodGene.Max, BloodGene.Value + resource);

            FeedCaster(caster, nutrition);
            MakeEffects(target.Position, target.Map);

            if (target.Dead)
            {
                var newCorpse = target.Corpse;
                if (newCorpse != null && !newCorpse.Destroyed)
                    newCorpse.Destroy();
            }

            HandleThoughts(caster, target, false);
        }

        private void FeedCaster(Pawn caster, float amount)
        {
            if (caster.needs?.food == null) return;

            caster.needs.food.CurLevel = Mathf.Min(caster.needs.food.MaxLevel,
                caster.needs.food.CurLevel + amount);

            if (amount > 0.1f)
                Messages.Message($"{caster.LabelShort} feels satisfied from the consumption.",
                    caster, MessageTypeDefOf.PositiveEvent, false);
        }

        private void TryApplyHediffs(Pawn caster, Pawn target)
        {
            if (Props.hediffToApplyOnSelf != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnSelf)
            {
                var h = caster.health.GetOrAddHediff(Props.hediffToApplyOnSelf);
                h.Severity = Props.bloodlustSeverity;
            }

            if (!target.Dead && Props.hediffToApplyOnTarget != null && Rand.Range(0f, 1f) <= Props.hediffChanceOnTarget)
            {
                var h = target.health.GetOrAddHediff(Props.hediffToApplyOnTarget);
                h.Severity = Props.drainSeverity;
            }
        }

        private float GetHealAmount(Pawn target, Corpse corpse = null)
        {
            float heal = Props.healAmount;
            float size = target.BodySize;
            float health = corpse != null ? 0.7f : target.health.summaryHealth.SummaryHealthPercent;

            if (corpse != null)
            {
                switch (corpse.GetRotStage())
                {
                    case RotStage.Fresh: health = 0.9f; break;
                    case RotStage.Rotting: health = 0.6f; break;
                    case RotStage.Dessicated: health = 0.2f; break;
                }
            }

            return heal * size * (0.5f + health * 0.5f);
        }

        private float GetResourceGain(Pawn target, Corpse corpse = null)
        {
            float resource = Props.resourceRestore / 100f;

            if (target.RaceProps.BloodDef != null) resource += 0.1f;
            if (target.RaceProps.IsFlesh) resource += 0.1f;

            if (corpse != null)
            {
                switch (corpse.GetRotStage())
                {
                    case RotStage.Fresh: resource *= 0.9f; break;
                    case RotStage.Rotting: resource *= 0.6f; break;
                    case RotStage.Dessicated: resource *= 0.3f; break;
                }
            }

            return resource * 100f;
        }

        private float GetNutritionGain(Pawn target, Corpse corpse = null)
        {
            float nutrition = Props.nutritionGain * target.BodySize;

            if (corpse != null)
            {
                switch (corpse.GetRotStage())
                {
                    case RotStage.Fresh: nutrition *= 0.95f; break;
                    case RotStage.Rotting: nutrition *= 0.7f; break;
                    case RotStage.Dessicated: nutrition *= 0.4f; break;
                }
            }

            return Mathf.Min(nutrition, 1.0f);
        }

        private void MakeEffects(IntVec3 pos, Map map)
        {
            if (map != null)
            {
                FilthMaker.TryMakeFilth(pos, map, ThingDefOf.Filth_Blood, 3);
                SoundDefOf.Pawn_Melee_Punch_HitPawn.PlayOneShot(new TargetInfo(pos, map));
            }
        }

        private void ProcessTarget(Pawn target, Corpse corpse = null)
        {
            if (corpse != null)
            {
                if (!corpse.Destroyed)
                    corpse.Destroy();
            }
            else if (target.Dead)
            {
                var targetCorpse = target.Corpse;
                if (targetCorpse != null && !targetCorpse.Destroyed)
                    targetCorpse.Destroy();
            }
            else
            {
                float drainDmg = target.MaxHitPoints * 0.6f * (Props.drainSeverity / 2f);
                var dmg = new DamageInfo(DamageDefOf.Bite, drainDmg, 999f, -1f, parent.pawn, null, null, DamageInfo.SourceCategory.ThingOrUnknown);
                target.TakeDamage(dmg);

                if (!target.Dead)
                {
                    var bloodLoss = HediffMaker.MakeHediff(HediffDefOf.BloodLoss, target);
                    bloodLoss.Severity = Props.drainSeverity * 0.3f;
                    target.health.AddHediff(bloodLoss);
                }
                else
                {
                    var newCorpse = target.Corpse;
                    if (newCorpse != null && !newCorpse.Destroyed)
                        newCorpse.Destroy();
                }
            }
        }

        private void HandleThoughts(Pawn caster, Pawn target, bool wasCorpse)
        {
            if (caster.needs?.mood?.thoughts?.memories == null) return;

            try
            {
                string thoughtName = wasCorpse ? "DemonCorpseConsumptionMemory" : "DemonConsumptionMemory";
                var thought = DefDatabase<ThoughtDef>.GetNamedSilentFail(thoughtName) ??
                             DefDatabase<ThoughtDef>.GetNamedSilentFail("DemonConsumptionMemory");

                if (thought != null)
                    caster.needs.mood.thoughts.memories.TryGainMemory(thought);

                if (caster.Map != null)
                {
                    string witnessName = wasCorpse ? "WitnessedCorpseConsumptionMemory" : "WitnessedDemonConsumptionMemory";
                    var witnessThought = DefDatabase<ThoughtDef>.GetNamedSilentFail(witnessName) ??
                                       DefDatabase<ThoughtDef>.GetNamedSilentFail("WitnessedDemonConsumptionMemory");

                    if (witnessThought != null)
                    {
                        var nearby = GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, 10f, true);
                        foreach (var thing in nearby)
                        {
                            if (thing is Pawn witness && witness != caster && witness.RaceProps.Humanlike)
                            {
                                if (GenSight.LineOfSight(caster.Position, witness.Position, caster.Map))
                                {
                                    witness.needs?.mood?.thoughts?.memories?.TryGainMemory(witnessThought, caster);
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