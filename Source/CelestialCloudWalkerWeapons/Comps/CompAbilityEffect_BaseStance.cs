using AnimeArsenal;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompAbilityEffect_BaseStance : CompAbilityEffect
    {
        private int maxJumps = 4;
        private int jumps = 0;
        private int jumpDistance = 5;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (parent.pawn?.Map == null)
                return;

            jumps = 0;

            Map map = parent.pawn.Map;
            if (target.Cell.IsValid)
            {
                CreateFlyerToTargetPosition(parent.pawn.Position, target.Cell, parent.pawn.Map);
            }
        }

        private void CreateFlyerToTargetPosition(IntVec3 start, IntVec3 target, Map map)
        {
            DelegateFlyer pawnFlyer = (DelegateFlyer)PawnFlyer.MakeFlyer(CelestialDefof.AnimeArsenal_DelegateFlyer, parent.pawn, target, null, null);
            pawnFlyer.OnRespawnPawn += OnFlyerLand;
            GenSpawn.Spawn(pawnFlyer, start, map);
        }

        private void OnFlyerLand(Pawn pawn, PawnFlyer flyer, Map map)
        {
            if (CanJumpAgain())
            {
                jumps++;
                IntVec3 currentPosition = pawn.Position;
                IntVec3 nextPosition = GetNextPositionInRandomCardinalDirection(currentPosition, map);
                CreateFlyerToTargetPosition(currentPosition, nextPosition, map);
            }
            else
            {
                jumps = 0;
            }
        }

        private IntVec3 GetNextPositionInRandomCardinalDirection(IntVec3 currentPosition, Map map)
        {
            IntVec3[] cardinalDirections = new IntVec3[]
            {
            new IntVec3(0, 0, jumpDistance),  // North
            new IntVec3(jumpDistance, 0, 0),  // East
            new IntVec3(0, 0, -jumpDistance), // South
            new IntVec3(-jumpDistance, 0, 0)  // West
            };

            int directionIndex = Rand.Range(0, cardinalDirections.Length);
            IntVec3 direction = cardinalDirections[directionIndex];
            IntVec3 targetPosition = currentPosition + direction;
            targetPosition = EnsurePositionIsValid(targetPosition, map);

            return targetPosition;
        }

        private IntVec3 EnsurePositionIsValid(IntVec3 position, Map map)
        {
            position.x = Mathf.Clamp(position.x, 0, map.Size.x - 1);
            position.z = Mathf.Clamp(position.z, 0, map.Size.z - 1);


            if (!position.Walkable(map))
            {
                IntVec3 fallbackPosition = CellFinder.StandableCellNear(position, map, jumpDistance / 2);
                if (fallbackPosition.IsValid)
                {
                    return fallbackPosition;
                }
            }

            return position;
        }

        private bool CanJumpAgain()
        {
            return jumps < maxJumps;
        }
    }

    public class CompProperties_BaseStance : CompProperties_AbilityEffect
    {
        public List<IntVec3> jumpOffsets;

        public CompProperties_BaseStance()
        {
            compClass = typeof(CompAbilityEffect_DashStance);

            jumpOffsets = new List<IntVec3>
            {
                new IntVec3(0, 0, 5),   // North
                new IntVec3(5, 0, 0),   // East
                new IntVec3(0, 0, -5),  // South
                new IntVec3(-5, 0, 0)   // West
            };
        }
    }

    public class CompAbilityEffect_DashStance : CompAbilityEffect_BaseStance
    {
        private DashBehaviour activeDash;

        public new CompProperties_BaseStance Props => (CompProperties_BaseStance)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (parent.pawn?.Map == null || !target.Cell.IsValid)
                return;

            if (activeDash != null && activeDash.IsRunning)
                return;

            activeDash = new DashBehaviour(parent.pawn, target.Cell);
            activeDash.Initialize(
                maxJumps: 10,
                jumpDistance: 5,
                delayBetweenJumps: 5,
                actionDuration: 30,
                onJumpStart: (pos) => CreateJumpEffect(pos),
                onJumpComplete: (pos) => { },
                onDashComplete: () => SpawnTrailingEffects(),
                customJumpOffsets: Props.jumpOffsets
            );

            activeDash.Start();
        }

        public override void CompTick()
        {
            base.CompTick();
            activeDash?.Tick();
        }

        private void CreateJumpEffect(IntVec3 position)
        {
            FleckMaker.ThrowDustPuff(position.ToVector3Shifted(), parent.pawn.Map, 1.0f);
        }

        private void SpawnTrailingEffects()
        {
           
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref activeDash, "activeDash");
        }
    }
}