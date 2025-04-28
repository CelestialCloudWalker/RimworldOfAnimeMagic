using Mono.Unix.Native;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompAbilityEffect_BaseStance : CompAbilityEffect
    {
        private int maxJumps = 4;
        private int jumps = 0;
        private int jumpDistance = 5; // Distance in cells for each additional jump

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (parent.pawn?.Map == null)
                return;

            // Reset the jump counter each time the ability is used
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

                // Pick a random cardinal direction (north, east, south, west)
                IntVec3 currentPosition = pawn.Position;
                IntVec3 nextPosition = GetNextPositionInRandomCardinalDirection(currentPosition, map);

                // Create a new flyer to the next position
                CreateFlyerToTargetPosition(currentPosition, nextPosition, map);
            }
            else
            {
                // Reset jumps for next usage of the ability
                jumps = 0;
            }
        }

        private IntVec3 GetNextPositionInRandomCardinalDirection(IntVec3 currentPosition, Map map)
        {
            // Define the four cardinal directions
            IntVec3[] cardinalDirections = new IntVec3[]
            {
            new IntVec3(0, 0, jumpDistance),  // North
            new IntVec3(jumpDistance, 0, 0),  // East
            new IntVec3(0, 0, -jumpDistance), // South
            new IntVec3(-jumpDistance, 0, 0)  // West
            };

            // Pick a random direction
            int directionIndex = Rand.Range(0, cardinalDirections.Length);
            IntVec3 direction = cardinalDirections[directionIndex];

            // Calculate the target position
            IntVec3 targetPosition = currentPosition + direction;

            // Ensure the position is valid within the map
            targetPosition = EnsurePositionIsValid(targetPosition, map);

            return targetPosition;
        }

        private IntVec3 EnsurePositionIsValid(IntVec3 position, Map map)
        {
            // Clamp the position to be within map bounds
            position.x = Mathf.Clamp(position.x, 0, map.Size.x - 1);
            position.z = Mathf.Clamp(position.z, 0, map.Size.z - 1);

            // Check if the position is walkable, if not, find the nearest walkable cell
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

}

