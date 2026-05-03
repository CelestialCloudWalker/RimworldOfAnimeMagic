using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompAbilityEffect_CauseSleep : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_CauseSleep Props =>
            (CompProperties_AbilityEffect_CauseSleep)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            IntVec3 center = Props.selfCentered ? parent.pawn.Position : target.Cell;

            List<Pawn> targets = new List<Pawn>();

            if (Props.areaOfEffect)
            {
                targets = FindTargetsInArea(center, Props.maxTargets);
            }
            else
            {
                if (target.Pawn != null && !target.Pawn.Dead && CanFallAsleep(target.Pawn))
                    targets.Add(target.Pawn);
            }

            if (Props.shockwaveEffecter != null)
            {
                Effecter e = Props.shockwaveEffecter.Spawn();
                e.Trigger(new TargetInfo(center, parent.pawn.Map), TargetInfo.Invalid);
                e.Cleanup();
            }

            if (Props.effectSound != null)
                Props.effectSound.PlayOneShot(new TargetInfo(center, parent.pawn.Map));

            foreach (Pawn pawn in targets)
                ApplySleepEffect(pawn);
        }

        private List<Pawn> FindTargetsInArea(IntVec3 center, int maxTargets)
        {
            List<Pawn> targets = new List<Pawn>();

            if (!center.IsValid || parent.pawn.Map == null)
                return targets;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                center, parent.pawn.Map, Props.areaRadius, true))
            {
                if (targets.Count >= maxTargets) break;

                if (thing is Pawn pawn
                    && pawn != parent.pawn
                    && !targets.Contains(pawn)
                    && CanFallAsleep(pawn))
                {
                    targets.Add(pawn);
                }
            }

            return targets;
        }

        private bool CanFallAsleep(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed) return false;

            if (pawn.health.InPainShock) return false;

            if (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) <= 0.1f)
                return false;

            if (pawn.RaceProps.IsMechanoid) return false;

            if (Props.onlyWeakWilled)
            {
                float will = pawn.GetStatValue(StatDefOf.PsychicSensitivity);
                if (will >= Props.willThreshold)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Resisted", Color.white);
                    return false;
                }
            }

            return true;
        }

        private void ApplySleepEffect(Pawn pawn)
        {
            if (pawn.needs?.rest != null)
                pawn.needs.rest.CurLevel = 0f;

            if (Props.useSleepHediff)
            {
                HediffDef hediffToUse = Props.sleepHediffDef ?? HediffDefOf.Anesthetic;
                Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(hediffToUse);

                if (existing != null)
                    existing.Severity = Math.Max(existing.Severity, Props.sleepSeverity);
                else
                {
                    Hediff h = HediffMaker.MakeHediff(hediffToUse, pawn);
                    h.Severity = Props.sleepSeverity;
                    pawn.health.AddHediff(h);
                }
            }

            if (Props.sleepDurationHours > 0)
                ApplyTimedSleep(pawn, Props.sleepDurationHours);

            if (Props.forceImmediateSleep)
            {
                if (pawn.jobs.curJob != null)
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);

                IntVec3 sleepSpot = pawn.Position;
                Building_Bed bed = RestUtility.FindBedFor(pawn);
                if (bed != null && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some))
                    sleepSpot = bed.Position;

                Job sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, sleepSpot);
                sleepJob.forceSleep = true;
                pawn.jobs.TryTakeOrderedJob(sleepJob, JobTag.SatisfyingNeeds);
            }

            if (Props.deepSleep)
                ApplyDeepSleepEffects(pawn);

            if (Props.showMessage && (pawn.IsColonist || pawn.IsPrisonerOfColony))
            {
                string defaultMsg = Props.deepSleep
                    ? $"{pawn.LabelShort} has fallen into deep sleep!"
                    : $"{pawn.LabelShort} has fallen asleep!";

                string msg = !Props.overrideMessage.NullOrEmpty()
                    ? Props.overrideMessage.Replace("{pawn}", pawn.LabelShort)
                                           .Replace("{caster}", parent.pawn.LabelShort)
                    : defaultMsg;

                Messages.Message(msg, pawn, MessageTypeDefOf.NeutralEvent);
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
                Hediff deepSleep = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, pawn);
                deepSleep.Severity = 1.5f + Props.wakeResistance;
                pawn.health.AddHediff(deepSleep);
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (Props.selfCentered) return true;

            if (Props.areaOfEffect) return base.CanApplyOn(target, dest);

            return base.CanApplyOn(target, dest)
                && target.HasThing
                && target.Thing is Pawn pawn
                && CanFallAsleep(pawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (Props.selfCentered) return true;

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
                    Messages.Message("Target cannot sleep", MessageTypeDefOf.RejectInput);
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
        public string overrideMessage = null;
        public SoundDef effectSound = null;
        public float sleepDurationHours = 4f;
        public int maxTargets = 1;
        public float range = 12f;
        public bool areaOfEffect = false;
        public float areaRadius = 3f;
        public bool deepSleep = false;
        public float wakeResistance = 0f;
        public bool selfCentered = false;
        public bool onlyWeakWilled = false;
        public float willThreshold = 0.6f;
        public EffecterDef shockwaveEffecter = null;

        public CompProperties_AbilityEffect_CauseSleep()
        {
            compClass = typeof(CompAbilityEffect_CauseSleep);
        }
    }
}