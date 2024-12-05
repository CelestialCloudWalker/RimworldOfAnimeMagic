using RimWorld;
using Verse;
using Verse.AI;

namespace CelestialCloudWalkerWeapons
{
    public class Verb_ExtendedMeleeAttack : Verb_MeleeAttackDamage
    {
        private float currentRange = 4.9f;

        public override float EffectiveRange => currentRange;

        public void SetRange(float newRange)
        {
            currentRange = newRange;
            Log.Message($"[ExtendedMeleeAttack] Range changed to: {currentRange}");
        }

        protected override bool TryCastShot()
        {
            Log.Message($"[ExtendedMeleeAttack] TryCastShot called. Can hit target: {CanHitTarget(currentTarget)}");
            if (!CanHitTarget(currentTarget))
            {
                return false;
            }
            return base.TryCastShot();
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            Job verbJob = this.GetVerbJob(target);
            this.CasterPawn.jobs.TryTakeOrderedJob(verbJob, new JobTag?(JobTag.Misc), false);
        }

        public Job GetVerbJob(LocalTargetInfo target)
        {
            Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ExtendedMeleeAttack"), target);
            job.playerForced = true;
            job.verbToUse = this;
            job.maxNumMeleeAttacks = 1;
            Pawn pawn = target.Thing as Pawn;
            if (pawn != null)
            {
                job.killIncappedTarget = pawn.Downed;
            }
            return job;
        }

        public override bool CanHitTarget(LocalTargetInfo target)
        {
            var baseCanHit = base.CanHitTarget(target);
            var distance = caster.Position.DistanceTo(target.Cell);
            var lineOfSight = GenSight.LineOfSight(caster.Position, target.Cell, caster.Map);

            Log.Message($"[ExtendedMeleeAttack] CanHitTarget check - Base: {baseCanHit}, Distance: {distance}, Range: {currentRange}, LoS: {lineOfSight}");
            return baseCanHit && distance <= currentRange && lineOfSight;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }

            float distance = caster.Position.DistanceTo(target.Cell);
            if (distance > currentRange)
            {
                if (showMessages)
                {
                    Messages.Message("OutOfRange".Translate(), MessageTypeDefOf.RejectInput);
                }
                return false;
            }

            return true;
        }
    }
}
