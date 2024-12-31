using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDemonifyWorkSlave : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityDemonifyWorkSlave()
        {
            compClass = typeof(CompAbilityEffect_DemonifyWorkSlave);
        }
    }

    public class CompAbilityEffect_DemonifyWorkSlave : CompAbilityEffect
    {
        public new CompProperties_AbilityDemonifyWorkSlave Props => (CompProperties_AbilityDemonifyWorkSlave)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn targetPawn = target.Pawn;
            if (targetPawn != null)
            {
                DemonUtility.ForceIntoDemonSlave(targetPawn, this.parent.pawn);

                Hediff zombieHediff = HediffMaker.MakeHediff(CelestialDefof.DemonWorkSlaveHediff, targetPawn);
                targetPawn.health.AddHediff(zombieHediff);


                Hediff addedHeDiff = targetPawn.health.hediffSet.GetFirstHediffOfDef(CelestialDefof.DemonWorkSlaveHediff);


                if (addedHeDiff != null)
                {
                    HediffComp_DemonWorkSlaveEffect zombieWorkSlaveEffect = addedHeDiff.TryGetComp<HediffComp_DemonWorkSlaveEffect>();
                    if (zombieWorkSlaveEffect != null)
                    {
                        zombieWorkSlaveEffect.SetSlaveMaster(parent.pawn);
                        Log.Message($"Zombify: Master set for {targetPawn.LabelShort} to {parent.pawn.LabelShort}");
                    }
                    else
                    {
                        Log.Error($"Zombify: Failed to get HediffComp_ZombieWorkSlaveEffect for {targetPawn.LabelShort}");
                    }
                }


            }
        }
    }

}