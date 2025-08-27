using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompAbilityEffect_CauseSleep : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_CauseSleep Props => (CompProperties_AbilityEffect_CauseSleep)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            List<Pawn> targets = new List<Pawn>();

            if (Props.areaOfEffect && Props.maxTargets > 1)
            {
                targets = FindTargetsInArea(target, Props.maxTargets);
            }
            else
            {
                if (target.Pawn != null && !target.Pawn.Dead && CanFallAsleep(target.Pawn))
                {
                    targets.Add(target.Pawn);
                }
            }

            foreach (Pawn targetPawn in targets)
            {
                ApplySleepEffect(targetPawn);
            }

            if (Props.effectSound != null)
            {
                Props.effectSound.PlayOneShot(new TargetInfo(target.Cell, parent.pawn.Map));
            }
        }

        private List<Pawn> FindTargetsInArea(LocalTargetInfo centerTarget, int maxTargets)
        {
            List<Pawn> targets = new List<Pawn>();

            if (centerTarget.Cell == IntVec3.Invalid || parent.pawn.Map == null)
                return targets;

            if (centerTarget.Pawn != null && !centerTarget.Pawn.Dead && CanFallAsleep(centerTarget.Pawn))
            {
                targets.Add(centerTarget.Pawn);
            }

            var cellsInRange = GenRadial.RadialCellsAround(centerTarget.Cell, Props.areaRadius, true)
                .Take(100) 
                .ToList();

            foreach (var cell in cellsInRange)
            {
                if (targets.Count >= maxTargets) break;
                if (!cell.InBounds(parent.pawn.Map)) continue;

                var things = cell.GetThingList(parent.pawn.Map);
                foreach (var thing in things)
                {
                    if (targets.Count >= maxTargets) break;

                    if (thing is Pawn pawn && !targets.Contains(pawn) && CanFallAsleep(pawn))
                    {
                        targets.Add(pawn);
                    }
                }
            }

            return targets;
        }

        private bool CanFallAsleep(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;

            if (pawn.health.InPainShock || pawn.Downed)
                return false;

            if (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) <= 0.1f)
                return false;

            if (pawn.RaceProps.IsMechanoid)
                return false;

            if (!Props.canPenetrateMindShields)
            {
                var psychicSensitivity = pawn.GetStatValue(StatDefOf.PsychicSensitivity);
                if (psychicSensitivity <= 0.1f)
                    return false;

                if (pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicHangover))
                    return false;
            }

            return true;
        }

        private void ApplySleepEffect(Pawn pawn)
        {
            try
            {
                if (pawn.needs?.rest != null)
                {
                    pawn.needs.rest.CurLevel = 0f;
                }

                if (Props.useSleepHediff)
                {
                    HediffDef hediffToUse = Props.sleepHediffDef ?? HediffDefOf.Anesthetic;
                    Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffToUse);

                    if (existingHediff != null)
                    {
                        existingHediff.Severity = Math.Max(existingHediff.Severity, Props.sleepSeverity);
                    }
                    else
                    {
                        Hediff sleepHediff = HediffMaker.MakeHediff(hediffToUse, pawn);
                        sleepHediff.Severity = Props.sleepSeverity;
                        pawn.health.AddHediff(sleepHediff);
                    }
                }

                if (Props.sleepDurationHours > 0)
                {
                    ApplyTimedSleep(pawn, Props.sleepDurationHours);
                }

                if (Props.forceImmediateSleep)
                {
                    if (pawn.jobs.curJob != null)
                    {
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    }

                    IntVec3 sleepSpot = pawn.Position;
                    Building_Bed bed = RestUtility.FindBedFor(pawn);
                    if (bed != null && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some))
                    {
                        sleepSpot = bed.Position;
                    }

                    Job sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, sleepSpot);
                    sleepJob.forceSleep = true;
                    pawn.jobs.TryTakeOrderedJob(sleepJob, JobTag.SatisfyingNeeds);
                }

                if (Props.deepSleep)
                {
                    ApplyDeepSleepEffects(pawn);
                }

                if (Props.showMessage && (pawn.IsColonist || pawn.IsPrisonerOfColony))
                {
                    string sleepType = Props.deepSleep ? "deep sleep" : "sleep";
                    Messages.Message($"{pawn.LabelShort} has fallen into {sleepType}!", pawn, MessageTypeDefOf.NeutralEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Error applying sleep effect to {pawn?.LabelShort}: {ex}");
            }
        }

        private void ApplyTimedSleep(Pawn pawn, float hours)
        {
            HediffDef timedSleepHediff = Props.sleepHediffDef ?? HediffDefOf.Anesthetic;
            Hediff sleepEffect = HediffMaker.MakeHediff(timedSleepHediff, pawn);

            sleepEffect.Severity = Mathf.Clamp(hours / 8f, 0.5f, 2f); 
            pawn.health.AddHediff(sleepEffect);
        }

        private void ApplyDeepSleepEffects(Pawn pawn)
        {
            if (Props.wakeResistance > 0)
            {
                HediffDef deepSleepHediff = HediffDefOf.Anesthetic;
                Hediff deepSleep = HediffMaker.MakeHediff(deepSleepHediff, pawn);
                deepSleep.Severity = 1.5f + Props.wakeResistance; 
                pawn.health.AddHediff(deepSleep);
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && target.HasThing && target.Thing is Pawn pawn && CanFallAsleep(pawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                if (throwMessages)
                    Messages.Message("Must target a pawn", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (!CanFallAsleep(pawn))
            {
                if (throwMessages)
                {
                    string reason = "cannot fall asleep";
                    if (pawn.RaceProps.IsMechanoid)
                        reason = "is a mechanoid";
                    else if (pawn.health.InPainShock)
                        reason = "is in pain shock";
                    else if (pawn.Downed)
                        reason = "is already downed";

                    Messages.Message($"{pawn.LabelShort} {reason}", MessageTypeDefOf.RejectInput);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    public class CompProperties_AbilityEffect_CauseSleep : CompProperties_AbilityEffect
    {
        public bool useSleepHediff = false;
        public HediffDef sleepHediffDef = null;
        public float sleepSeverity = 1.0f;
        public bool forceImmediateSleep = true;
        public bool showMessage = true;
        public SoundDef effectSound = null;

        public float sleepDurationHours = 4f;
        public int maxTargets = 1;
        public float range = 12f;
        public bool areaOfEffect = false;
        public float areaRadius = 3f;
        public bool canPenetrateMindShields = false;
        public bool deepSleep = false;
        public float wakeResistance = 0f; 

        public CompProperties_AbilityEffect_CauseSleep()
        {
            compClass = typeof(CompAbilityEffect_CauseSleep);
        }
    }
}