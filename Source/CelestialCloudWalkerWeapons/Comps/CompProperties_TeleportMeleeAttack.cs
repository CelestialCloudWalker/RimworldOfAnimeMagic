using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_TeleportMeleeAttack : CompProperties_AbilityEffect
    {
        public CompProperties_TeleportMeleeAttack()
        {
            compClass = typeof(CompAbilityEffect_TeleportMeleeAttack);
        }
    }
    public class CompAbilityEffect_TeleportMeleeAttack : CompAbilityEffect
    {
        private Thing TargetThing;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            TargetThing = target.Thing;

            if (TargetThing != null)
            {
                IntVec3 destination = TargetThing.Position;

                PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_TPStrikeFlyer, parent.pawn, destination, null, null);
                DelegateFlyer flyer = (DelegateFlyer)GenSpawn.Spawn(pawnFlyer, destination, TargetThing.Map);

                flyer.OnRespawnPawn += Flyer_OnRespawnPawn;
            }
        }

        private void Flyer_OnRespawnPawn(Pawn arg1, PawnFlyer arg2)
        {
            DelegateFlyer flyer = (DelegateFlyer)arg2;
            flyer.OnRespawnPawn -= Flyer_OnRespawnPawn;

            if (TargetThing != null)
            {
                parent.pawn.SetPositionDirect(TargetThing.RandomAdjacentCell8Way());
                parent.pawn.Notify_Teleported();
                parent.pawn.meleeVerbs.TryMeleeAttack(TargetThing, null, false);
            }

        }
    }
}