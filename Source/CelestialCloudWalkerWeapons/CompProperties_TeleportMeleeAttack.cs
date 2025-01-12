using RimWorld;
using Verse;
using System.Collections.Generic;

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
        private bool teleportQueued = false;
        private IntVec3 queuedDestination;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!target.HasThing || target.Thing == null || target.Thing.Map == null || parent?.pawn == null)
            {
                Log.Warning("TeleportMeleeAttack - Invalid target or state");
                return;
            }

            TargetThing = target.Thing;
            queuedDestination = TargetThing.Position;
            teleportQueued = true;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (teleportQueued)
            {
                teleportQueued = false;
                ExecuteTeleport();
            }
        }

        private void ExecuteTeleport()
        {
            if (TargetThing == null || parent?.pawn == null || TargetThing.Map == null)
            {
                return;
            }

            try
            {
                PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_TPStrikeFlyer, parent.pawn, queuedDestination, null, null);
                if (pawnFlyer != null)
                {
                    DelegateFlyer flyer = GenSpawn.Spawn(pawnFlyer, queuedDestination, TargetThing.Map) as DelegateFlyer;
                    if (flyer != null)
                    {
                        flyer.OnRespawnPawn += Flyer_OnRespawnPawn;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"TeleportMeleeAttack - Error during teleport: {ex}");
            }
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return target.HasThing && target.Thing != null;
        }

        private void Flyer_OnRespawnPawn(Pawn pawn, PawnFlyer flyer)
        {
            if (flyer is DelegateFlyer delegateFlyer)
            {
                delegateFlyer.OnRespawnPawn -= Flyer_OnRespawnPawn;
            }

            if (TargetThing == null || parent?.pawn == null || TargetThing.Map == null)
            {
                return;
            }

            try
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    IntVec3 adjacentCell = TargetThing.RandomAdjacentCell8Way();
                    if (adjacentCell.InBounds(TargetThing.Map))
                    {
                        parent.pawn.Position = adjacentCell;
                        parent.pawn.meleeVerbs.TryMeleeAttack(TargetThing, null, false);
                    }
                });
            }
            catch (System.Exception ex)
            {
                Log.Error($"TeleportMeleeAttack - Error during respawn: {ex}");
            }
        }
    }
}