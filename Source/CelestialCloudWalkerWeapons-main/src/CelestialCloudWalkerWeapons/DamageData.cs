using Verse;

namespace CelestialCloudWalkerWeapons
{
    //public class JobDriver_ExtendedMeleeAttack : JobDriver
    //{
    //    public override bool TryMakePreToilReservations(bool errorOnFailed)
    //    {
    //        IAttackTarget attackTarget = job.targetA.Thing as IAttackTarget;
    //        if (attackTarget != null)
    //        {
    //            pawn.Map.attackTargetReservationManager.Reserve(pawn, job, attackTarget);
    //        }
    //        return true;
    //    }

    //    protected override IEnumerable<Toil> MakeNewToils()
    //    {
    //        yield return Toils_Misc.ThrowColonistAttackingMote(TargetIndex.A);
    //        yield return Toils_Combat.GotoCastPosition(TargetIndex.A, true, job.verbToUse.verbProps.range).FailOnDespawnedOrNull(TargetIndex.A);
    //        yield return Toils_Combat.CastVerb(TargetIndex.A, false);
    //    }
    //}

    public class DamageData
    {
        public DamageDef damage;
        public float multiplier = 1f;
    }
}
