using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompProjectileEffect_CauseSleep : ThingComp
    {
        public CompProperties_ProjectileEffect_CauseSleep Props => (CompProperties_ProjectileEffect_CauseSleep)props;

        public void OnProjectileImpact(Thing hitThing, IntVec3 impactPos, Thing launcher)
        {
            if (hitThing is Pawn targetPawn && !targetPawn.Dead)
            {
                ApplySleepEffectToTarget(targetPawn);
            }

            if (Props.areaEffect && Props.effectRadius > 0)
            {
                ApplyAreaSleepEffect(impactPos, launcher?.Map);
            }
        }

        private void ApplySleepEffectToTarget(Pawn targetPawn)
        {
            if (CanFallAsleep(targetPawn))
                ApplySleepEffect(targetPawn);
        }

        private void ApplyAreaSleepEffect(IntVec3 center, Map map)
        {
            if (map == null) return;

            var affectedCells = GenRadial.RadialCellsAround(center, Props.effectRadius, true);
            foreach (var cell in affectedCells)
            {
                if (!cell.InBounds(map)) continue;

                var thingsInCell = cell.GetThingList(map);
                foreach (var thing in thingsInCell)
                {
                    if (thing is Pawn pawn && !pawn.Dead && CanFallAsleep(pawn))
                    {
                        ApplySleepEffect(pawn);
                    }
                }
            }
        }

        private bool CanFallAsleep(Pawn pawn)
        {
            if (pawn.health.InPainShock || pawn.Downed) return false;
            if (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) <= 0.1f) return false;
            if (pawn.RaceProps.IsMechanoid) return false;
            if (pawn.CurJob?.def == JobDefOf.LayDown) return false;
            return true;
        }

        private void ApplySleepEffect(Pawn pawn)
        {
            if (pawn.needs?.rest != null)
            {
                pawn.needs.rest.CurLevel = 0f;
            }

            if (Props.useSleepHediff)
            {
                var hediffToUse = Props.sleepHediffDef ?? HediffDefOf.Anesthetic;
                var existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffToUse);

                if (existingHediff != null)
                {
                    existingHediff.Severity = Math.Max(existingHediff.Severity, Props.sleepSeverity);
                }
                else
                {
                    var sleepHediff = HediffMaker.MakeHediff(hediffToUse, pawn);
                    sleepHediff.Severity = Props.sleepSeverity;
                    pawn.health.AddHediff(sleepHediff);
                }
            }

            if (Props.forceImmediateSleep)
            {
                if (pawn.jobs.curJob != null)
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                }

                var sleepSpot = pawn.Position;
                var bed = RestUtility.FindBedFor(pawn);
                if (bed != null && pawn.CanReach(bed, PathEndMode.OnCell, Danger.Some))
                {
                    sleepSpot = bed.Position;
                }

                var sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, sleepSpot);
                sleepJob.forceSleep = true;
                pawn.jobs.TryTakeOrderedJob(sleepJob, JobTag.SatisfyingNeeds);
            }

            if (Props.showMessage && (pawn.IsColonist || pawn.IsPrisonerOfColony))
            {
                Messages.Message($"{pawn.LabelShort} has fallen asleep from projectile impact!", pawn, MessageTypeDefOf.NeutralEvent);
            }

            Props.effectSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }
    }

    public class CompProperties_ProjectileEffect_CauseSleep : CompProperties
    {
        public bool useSleepHediff = true;
        public HediffDef sleepHediffDef = null;
        public float sleepSeverity = 1.0f;
        public bool forceImmediateSleep = true;
        public bool showMessage = true;
        public SoundDef effectSound = null;
        public bool areaEffect = false;
        public float effectRadius = 0f;

        public CompProperties_ProjectileEffect_CauseSleep()
        {
            compClass = typeof(CompProjectileEffect_CauseSleep);
        }
    }

    public class Projectile_WithSleepComp : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var impactPos = this.Position;
            var projectileLauncher = this.launcher;

            var sleepComp = this.GetComp<CompProjectileEffect_CauseSleep>();
            if (sleepComp != null && !blockedByShield)
            {
                sleepComp.OnProjectileImpact(hitThing, impactPos, projectileLauncher);
            }

            base.Impact(hitThing, blockedByShield);
        }
    }
}