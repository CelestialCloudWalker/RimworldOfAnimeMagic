using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class Projectile_SleepEffect : ScalingStatDamageProjectile
    {
        public ProjectileProperties_SleepEffect Props => (ProjectileProperties_SleepEffect)this.def.projectile;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            
            List<Thing> ThingsToHit = AnimeArsenalUtility.GetThingsInRange(this.Position, this.MapHeld, this.Props.SleepRadius, TargetValidator).ToList();

            
            foreach (var thingToHit in ThingsToHit)
            {
                if (!thingToHit.Destroyed && thingToHit is Pawn pawnToHit)
                {
                    ApplySleepEffect(pawnToHit);
                }
            }

            
            if (this.Props.SleepVisualEffect != null)
            {
                this.Props.SleepVisualEffect.Spawn(this.Position, this.MapHeld);
            }

            
            base.Impact(hitThing, blockedByShield);
        }

        private bool TargetValidator(Thing HitThing)
        {
            if (HitThing == this)
            {
                return false;
            }
            if (this.launcher != null && HitThing == this.launcher && !Props.CanHitCaster)
            {
                return false;
            }
            if (HitThing.Faction != null)
            {
                if (!HitThing.Faction.HostileTo(this.launcher.Faction) && !Props.CanHitFriendly)
                {
                    return false;
                }
            }
            return true;
        }

        private void ApplySleepEffect(Pawn pawn)
        {
            
            if (!CanFallAsleep(pawn))
                return;

            
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
                Messages.Message($"{pawn.LabelShort} has fallen asleep from sleep projectile!", pawn, MessageTypeDefOf.NeutralEvent);
            }

            
            if (Props.effectSound != null)
            {
                Props.effectSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }
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
    }

    public class ProjectileProperties_SleepEffect : ProjectileProperties
    {
        public float SleepRadius = 3f;
        public bool useSleepHediff = false;
        public HediffDef sleepHediffDef = null;
        public float sleepSeverity = 1.0f;
        public bool forceImmediateSleep = true;
        public bool showMessage = true;
        public SoundDef effectSound = null;
        public EffecterDef SleepVisualEffect;
        public bool CanHitFriendly = true;
        public bool CanHitCaster = false;
    }
}