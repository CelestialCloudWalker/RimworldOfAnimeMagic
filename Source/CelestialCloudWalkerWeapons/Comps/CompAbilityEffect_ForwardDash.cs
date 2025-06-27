using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompAbilityEffect_ForwardDash : CompAbilityEffect
    {
        public new CompProperties_AbilityForwardDash Props => (CompProperties_AbilityForwardDash)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null) return;

            IntVec3 targetCell = target.Cell;

            // Perform multiple dashes
            for (int dashCount = 0; dashCount < Props.numberOfDashes; dashCount++)
            {
                IntVec3 currentPos = caster.Position;

                // Cast effect at current position (first dash gets cast effect, others get retry effect)
                if (dashCount == 0 && Props.castEffecter != null)
                {
                    Effecter castEffect = Props.castEffecter.Spawn();
                    castEffect.Trigger(new TargetInfo(currentPos, caster.Map), new TargetInfo(currentPos, caster.Map));
                    castEffect.Cleanup();
                }
                else if (dashCount > 0 && Props.retryEffecter != null)
                {
                    Effecter retryEffect = Props.retryEffecter.Spawn();
                    retryEffect.Trigger(new TargetInfo(currentPos, caster.Map), new TargetInfo(currentPos, caster.Map));
                    retryEffect.Cleanup();
                }

                // Calculate next dash position (toward target)
                IntVec3 dashTarget = CalculateForwardPosition(caster, targetCell);

                if (dashTarget.IsValid)
                {
                    // Move the pawn
                    caster.Position = dashTarget;
                    caster.Notify_Teleported(false, false);

                    // Landing effect at new position
                    if (Props.landEffecter != null)
                    {
                        Effecter landEffect = Props.landEffecter.Spawn();
                        landEffect.Trigger(new TargetInfo(dashTarget, caster.Map), new TargetInfo(dashTarget, caster.Map));
                        landEffect.Cleanup();
                    }

                    // Play sounds
                    if (dashCount == 0 && Props.castSound != null)
                    {
                        Props.castSound.PlayOneShot(new TargetInfo(currentPos, caster.Map));
                    }

                    if (Props.landSound != null)
                    {
                        Props.landSound.PlayOneShot(new TargetInfo(dashTarget, caster.Map));
                    }

                    // Short delay between dashes for visual effect
                    if (dashCount < Props.numberOfDashes - 1 && Props.dashDelay > 0)
                    {
                        // Note: In RimWorld, we can't easily add delays in the middle of ability execution
                        // The visual effects will play rapidly in sequence
                    }
                }
                else
                {
                    // If we can't find a valid position, stop dashing
                    break;
                }
            }
        }

        private IntVec3 CalculateForwardPosition(Pawn caster, IntVec3 targetCell)
        {
            Map map = caster.Map;
            IntVec3 casterPos = caster.Position;

            // Find direction toward target
            Vector3 forwardDirection = (targetCell - casterPos).ToVector3Shifted().normalized;

            // Try to find valid cells in forward direction
            for (int dist = Props.minDashDistance; dist <= Props.maxDashDistance; dist++)
            {
                Vector3 targetVector = casterPos.ToVector3Shifted() + (forwardDirection * dist);
                IntVec3 candidate = targetVector.ToIntVec3();

                if (IsValidForwardCell(candidate, caster, map, targetCell))
                {
                    return candidate;
                }

                // Try slight variations if direct path doesn't work
                for (int angle = -30; angle <= 30; angle += 10)
                {
                    Vector3 rotatedDir = forwardDirection.RotatedBy(angle);
                    Vector3 rotatedVector = casterPos.ToVector3Shifted() + (rotatedDir * dist);
                    IntVec3 rotatedCandidate = rotatedVector.ToIntVec3();

                    if (IsValidForwardCell(rotatedCandidate, caster, map, targetCell))
                    {
                        return rotatedCandidate;
                    }
                }
            }

            // If forward direction fails and Props.allowRandomDirection is true, try random directions
            if (Props.allowRandomDirection)
            {
                for (int i = 0; i < 20; i++)
                {
                    float randomAngle = Rand.Range(0f, 360f);
                    Vector3 randomDir = Vector3.forward.RotatedBy(randomAngle);

                    for (int dist = Props.minDashDistance; dist <= Props.maxDashDistance; dist++)
                    {
                        Vector3 randomVector = casterPos.ToVector3Shifted() + (randomDir * dist);
                        IntVec3 randomCandidate = randomVector.ToIntVec3();

                        if (IsValidForwardCell(randomCandidate, caster, map, targetCell))
                        {
                            return randomCandidate;
                        }
                    }
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsValidForwardCell(IntVec3 cell, Pawn caster, Map map, IntVec3 originalTarget)
        {
            if (!cell.InBounds(map) || !cell.Standable(map))
                return false;

            if (cell.GetEdifice(map)?.def.passability == Traversability.Impassable)
                return false;

            // Don't dash too close to the original target if specified
            if (Props.maintainMinDistanceFromTarget && cell.DistanceTo(originalTarget) < Props.minDistanceFromTarget)
                return false;

            // Optional: avoid enemies (might be less relevant for forward dash)
            if (Props.avoidEnemies)
            {
                var enemies = map.mapPawns.AllPawnsSpawned.Where(p =>
                    p.HostileTo(caster) && p.Position.DistanceTo(cell) < Props.enemyAvoidanceRadius);
                if (enemies.Any())
                    return false;
            }

            return true;
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
                return false;

            Pawn caster = parent.pawn;
            if (caster == null)
                return false;

            // Check if there are valid forward positions for at least the first dash
            IntVec3 forwardPos = CalculateForwardPosition(caster, target.Cell);
            if (!forwardPos.IsValid)
            {
                if (throwMessages)
                    Messages.Message("No valid dash position found.", MessageTypeDefOf.RejectInput);
                return false;
            }

            return true;
        }
    }

    public class CompProperties_AbilityForwardDash : CompProperties_AbilityEffect
    {
        public int minDashDistance = 3;
        public int maxDashDistance = 8;
        public bool avoidEnemies = false; // Usually false for forward dash since you might want to dash toward enemies
        public float enemyAvoidanceRadius = 5f;
        public int numberOfDashes = 1; // Number of consecutive dashes to perform
        public int dashDelay = 0; // Delay between dashes (limited effectiveness in RimWorld)

        // Forward dash specific properties
        public bool allowRandomDirection = false; // If true, will try random directions if forward path blocked
        public bool maintainMinDistanceFromTarget = false; // Prevent dashing too close to target
        public float minDistanceFromTarget = 2f; // Minimum distance to maintain from target

        // Visual and audio effects
        public EffecterDef castEffecter; // Effect for the first dash
        public EffecterDef landEffecter; // Effect for each landing
        public EffecterDef retryEffecter; // Effect for subsequent dashes (2nd, 3rd, etc.)
        public SoundDef castSound;
        public SoundDef landSound;

        // Disabled features (keeping properties for potential future use)
        public bool grantTemporaryImmunity = false;
        public bool grantSpeedBoost = false;
        public bool grantMoodBoost = false;
        public bool restoreStamina = false;

        public CompProperties_AbilityForwardDash()
        {
            compClass = typeof(CompAbilityEffect_ForwardDash);
        }
    }
}