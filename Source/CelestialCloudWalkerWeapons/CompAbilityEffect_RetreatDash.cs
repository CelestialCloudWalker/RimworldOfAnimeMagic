using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace AnimeArsenal
{
    public class CompAbilityEffect_RetreatDash : CompAbilityEffect
    {
        public new CompProperties_AbilityRetreatDash Props => (CompProperties_AbilityRetreatDash)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null) return;

            // Calculate retreat direction (away from target)
            IntVec3 retreatTarget = CalculateRetreatPosition(caster, target.Cell);

            if (retreatTarget.IsValid)
            {
                // Just move the pawn
                caster.Position = retreatTarget;
                caster.Notify_Teleported(false, false);
            }
        }

        private IntVec3 CalculateRetreatPosition(Pawn caster, IntVec3 fromCell)
        {
            Map map = caster.Map;
            IntVec3 casterPos = caster.Position;

            // Find direction away from threat
            Vector3 retreatDirection = (casterPos - fromCell).ToVector3Shifted().normalized;

            // Try to find valid cells in retreat direction
            for (int dist = Props.minDashDistance; dist <= Props.maxDashDistance; dist++)
            {
                Vector3 targetVector = casterPos.ToVector3Shifted() + (retreatDirection * dist);
                IntVec3 candidate = targetVector.ToIntVec3();

                if (IsValidRetreatCell(candidate, caster, map))
                {
                    return candidate;
                }

                // Try slight variations if direct path doesn't work
                for (int angle = -45; angle <= 45; angle += 15)
                {
                    Vector3 rotatedDir = retreatDirection.RotatedBy(angle);
                    Vector3 rotatedVector = casterPos.ToVector3Shifted() + (rotatedDir * dist);
                    IntVec3 rotatedCandidate = rotatedVector.ToIntVec3();

                    if (IsValidRetreatCell(rotatedCandidate, caster, map))
                    {
                        return rotatedCandidate;
                    }
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsValidRetreatCell(IntVec3 cell, Pawn caster, Map map)
        {
            if (!cell.InBounds(map) || !cell.Standable(map))
                return false;

            if (cell.GetEdifice(map)?.def.passability == Traversability.Impassable)
                return false;

            if (Props.avoidEnemies)
            {
                // Check if cell is too close to enemies
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

            // Check if there are valid retreat positions
            IntVec3 retreatPos = CalculateRetreatPosition(caster, target.Cell);
            if (!retreatPos.IsValid)
            {
                if (throwMessages)
                    Messages.Message("No valid retreat position found.", MessageTypeDefOf.RejectInput);
                return false;
            }

            return true;
        }
    }

    public class CompProperties_AbilityRetreatDash : CompProperties_AbilityEffect
    {
        public int minDashDistance = 3;
        public int maxDashDistance = 8;
        public bool avoidEnemies = true;
        public float enemyAvoidanceRadius = 5f;

        // Disabled features (keeping properties for potential future use)
        public bool grantTemporaryImmunity = false;
        public bool grantSpeedBoost = false;
        public bool grantMoodBoost = false;
        public bool restoreStamina = false;

        public CompProperties_AbilityRetreatDash()
        {
            compClass = typeof(CompAbilityEffect_RetreatDash);
        }
    }
}