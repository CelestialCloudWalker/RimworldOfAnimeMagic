using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class CompAbilityEffect_CauseSleep : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_CauseSleep Props => (CompProperties_AbilityEffect_CauseSleep)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn.Dead)
                return;

            
            if (!CanFallAsleep(targetPawn))
                return;

            
            ApplySleepEffect(targetPawn);
        }

        private bool CanFallAsleep(Pawn pawn)
        {
            
            if (pawn.health.InPainShock || pawn.Downed)
                return false;

            
            if (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) <= 0.1f)
                return false;

            
            if (pawn.RaceProps.IsMechanoid)
                return false;

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
                Hediff sleepHediff = HediffMaker.MakeHediff(Props.sleepHediffDef ?? HediffDefOf.Anesthetic, pawn);
                sleepHediff.Severity = Props.sleepSeverity;
                pawn.health.AddHediff(sleepHediff);
            }

            if (Props.forceImmediateSleep)
            {
                Job sleepJob = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
                pawn.jobs.StartJob(sleepJob, JobCondition.InterruptForced);
            }

            
            if (Props.showMessage && pawn.IsColonist)
            {
                Messages.Message($"{pawn.LabelShort} has fallen asleep!", pawn, MessageTypeDefOf.NeutralEvent);
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
                    Messages.Message($"{pawn.LabelShort} cannot fall asleep", MessageTypeDefOf.RejectInput);
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

        public CompProperties_AbilityEffect_CauseSleep()
        {
            compClass = typeof(CompAbilityEffect_CauseSleep);
        }
    }
}