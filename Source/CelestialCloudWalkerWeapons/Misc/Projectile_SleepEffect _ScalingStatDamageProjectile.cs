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
        public ProjectileProperties_SleepEffect Props => (ProjectileProperties_SleepEffect)def.projectile;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var targets = AnimeArsenalUtility.GetThingsInRange(Position, MapHeld, Props.SleepRadius, IsValidTarget)
                .OfType<Pawn>()
                .Where(p => !p.Destroyed)
                .ToList();

            foreach (var pawn in targets)
            {
                MakePawnSleep(pawn);
            }

            Props.SleepVisualEffect?.Spawn(Position, MapHeld);
            base.Impact(hitThing, blockedByShield);
        }

        private bool IsValidTarget(Thing target)
        {
            if (target == this || (launcher != null && target == launcher && !Props.CanHitCaster))
                return false;

            if (target.Faction != null && !target.Faction.HostileTo(launcher.Faction) && !Props.CanHitFriendly)
                return false;

            return true;
        }

        private void MakePawnSleep(Pawn pawn)
        {
            if (!CanSleep(pawn)) return;

            if (pawn.needs?.rest != null)
                pawn.needs.rest.CurLevel = 0f;

            if (Props.useSleepHediff)
            {
                var hediff = HediffMaker.MakeHediff(Props.sleepHediffDef ?? HediffDefOf.Anesthetic, pawn);
                hediff.Severity = Props.sleepSeverity;
                pawn.health.AddHediff(hediff);
            }

            if (Props.forceImmediateSleep)
            {
                var job = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }

            if (Props.showMessage && pawn.IsColonist)
            {
                Messages.Message($"{pawn.LabelShort} has fallen asleep from sleep projectile!", pawn, MessageTypeDefOf.NeutralEvent);
            }

            Props.effectSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
        }

        private bool CanSleep(Pawn pawn)
        {
            if (pawn.health.InPainShock || pawn.Downed || pawn.RaceProps.IsMechanoid)
                return false;

            return pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) > 0.1f;
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