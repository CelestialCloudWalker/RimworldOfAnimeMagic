using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class MentalState_DemonCannibalism : MentalState
    {
        private int checkInterval = 150;
        private int ticksSinceLastCheck = 0;

        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            TryStartHunting();
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);

            ticksSinceLastCheck += delta;
            if (ticksSinceLastCheck >= checkInterval)
            {
                ticksSinceLastCheck = 0;

                if (pawn.CurJob == null || pawn.CurJob.def != CelestialDefof.AA_DemonHuntAndConsume)
                {
                    TryStartHunting();
                }
            }
        }

        private void TryStartHunting()
        {
            Pawn victim = FindVictim();
            if (victim != null)
            {
                Job huntJob = JobMaker.MakeJob(CelestialDefof.AA_DemonHuntAndConsume, victim);
                huntJob.playerForced = true;
                huntJob.expiryInterval = 999999;
                huntJob.collideWithPawns = true;

                pawn.jobs.StartJob(huntJob, JobCondition.InterruptForced, null, false, true);
            }
        }

        private Pawn FindVictim()
        {
            if (pawn.Map == null) return null;

            var ext = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g.def.GetModExtension<DemonSanityExtension>() != null)
                ?.def.GetModExtension<DemonSanityExtension>();

            int searchRadius = ext?.searchRadiusForVictims ?? 40;

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Deadly),
                searchRadius,
                p => p is Pawn target &&
                     target != pawn &&
                     target.RaceProps.Humanlike &&
                     !target.Dead &&
                     pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)
            ) as Pawn;
        }

        public override RandomSocialMode SocialModeMax()
        {
            return RandomSocialMode.Off;
        }
    }

    public class MentalStateWorker_DemonCannibalism : MentalStateWorker
    {
        public override bool StateCanOccur(Pawn pawn)
        {
            if (!base.StateCanOccur(pawn))
                return false;

            var demonGene = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g is BloodDemonArtsGene);

            return demonGene != null;
        }
    }

    public class JobGiver_DemonHunt : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.MentalState is MentalState_DemonCannibalism)
            {
                Pawn victim = FindVictim(pawn);
                if (victim != null)
                {
                    Job huntJob = JobMaker.MakeJob(CelestialDefof.AA_DemonHuntAndConsume, victim);
                    huntJob.playerForced = true;
                    huntJob.expiryInterval = 999999;
                    huntJob.collideWithPawns = true;
                    return huntJob;
                }
            }
            return null;
        }

        private Pawn FindVictim(Pawn pawn)
        {
            if (pawn.Map == null) return null;

            var ext = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g.def.GetModExtension<DemonSanityExtension>() != null)
                ?.def.GetModExtension<DemonSanityExtension>();

            int searchRadius = ext?.searchRadiusForVictims ?? 40;

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Deadly),
                searchRadius,
                p => p is Pawn target &&
                     target != pawn &&
                     target.RaceProps.Humanlike &&
                     !target.Dead &&
                     pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)
            ) as Pawn;
        }
    }

    public class JobDriver_DemonHuntAndConsume : JobDriver
    {
        private Pawn Victim => (Pawn)job.targetA.Thing;
        private const TargetIndex VictimInd = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil chaseAndAttack = new Toil();
            chaseAndAttack.initAction = delegate
            {
                if (Victim != null && Victim.Spawned && !Victim.Dead)
                {
                    pawn.pather.StartPath(Victim, PathEndMode.Touch);
                }
            };

            chaseAndAttack.tickAction = delegate
            {
                if (Victim == null || Victim.Dead)
                {
                    ReadyForNextToil();
                    return;
                }

                if (!Victim.Spawned)
                {
                    ReadyForNextToil();
                    return;
                }

                if (pawn.Position.AdjacentTo8WayOrInside(Victim.Position))
                {
                    if (pawn.IsHashIntervalTick(60))
                    {
                        pawn.meleeVerbs.TryMeleeAttack(Victim);
                    }

                    pawn.rotationTracker.FaceTarget(Victim);

                    if (pawn.pather.Moving)
                    {
                        pawn.pather.StopDead();
                    }
                }
                else
                {
                    if (!pawn.pather.Moving || pawn.pather.Destination.Thing != Victim)
                    {
                        pawn.pather.StartPath(Victim, PathEndMode.Touch);
                    }
                }
            };

            chaseAndAttack.defaultCompleteMode = ToilCompleteMode.Never;
            chaseAndAttack.socialMode = RandomSocialMode.Off;
            yield return chaseAndAttack;

            Toil consumeToil = new Toil();
            consumeToil.initAction = delegate
            {
                ConsumeVictim();
            };
            consumeToil.defaultCompleteMode = ToilCompleteMode.Instant;
            consumeToil.AddFinishAction(delegate
            {
                if (pawn.MentalState != null && pawn.MentalState is MentalState_DemonCannibalism)
                {
                    pawn.MentalState.RecoverFromState();
                }
            });
            yield return consumeToil;
        }

        private void ConsumeVictim()
        {
            if (Victim == null)
            {
                Log.Warning("DemonHuntAndConsume: Victim is null during consumption");
                return;
            }

            var demonGene = pawn.genes?.GenesListForReading
                .FirstOrDefault(g => g is BloodDemonArtsGene) as BloodDemonArtsGene;

            if (demonGene == null)
            {
                Log.Warning("DemonHuntAndConsume: No BloodDemonArtsGene found");
                return;
            }

            demonGene.AddPawnEaten();

            if (pawn.Map != null)
            {
                IntVec3 effectPos = pawn.Position;

                if (Victim.Corpse != null && Victim.Corpse.Spawned)
                {
                    effectPos = Victim.Corpse.Position;
                }
                else if (Victim.Spawned)
                {
                    effectPos = Victim.Position;
                }

                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Consumed!", Color.red, 4f);
                FilthMaker.TryMakeFilth(effectPos, pawn.Map, ThingDefOf.Filth_Blood, 10);

                for (int i = 0; i < 5; i++)
                {
                    FleckMaker.ThrowSmoke(effectPos.ToVector3(), pawn.Map, 1f);
                }
            }

            Messages.Message(
                $"{pawn.LabelShort} has consumed {Victim.LabelShort}!",
                pawn,
                MessageTypeDefOf.NegativeEvent
            );

            if (Victim.Corpse != null && !Victim.Corpse.Destroyed)
            {
                Victim.Corpse.Destroy(DestroyMode.Vanish);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }
    }
}