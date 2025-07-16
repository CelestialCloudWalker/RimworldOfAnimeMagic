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

            
            for (int dashCount = 0; dashCount < Props.numberOfDashes; dashCount++)
            {
                IntVec3 currentPos = caster.Position;

                
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

                
                IntVec3 dashTarget = CalculateForwardPosition(caster, targetCell);

                if (dashTarget.IsValid)
                {
                   
                    caster.Position = dashTarget;
                    caster.Notify_Teleported(false, false);

                   
                    if (Props.landEffecter != null)
                    {
                        Effecter landEffect = Props.landEffecter.Spawn();
                        landEffect.Trigger(new TargetInfo(dashTarget, caster.Map), new TargetInfo(dashTarget, caster.Map));
                        landEffect.Cleanup();
                    }

                    
                    if (dashCount == 0 && Props.castSound != null)
                    {
                        Props.castSound.PlayOneShot(new TargetInfo(currentPos, caster.Map));
                    }

                    if (Props.landSound != null)
                    {
                        Props.landSound.PlayOneShot(new TargetInfo(dashTarget, caster.Map));
                    }

                    
                    if (dashCount < Props.numberOfDashes - 1 && Props.dashDelay > 0)
                    {
                       
                    }
                }
                else
                {
                    
                    break;
                }
            }
        }

        private IntVec3 CalculateForwardPosition(Pawn caster, IntVec3 targetCell)
        {
            Map map = caster.Map;
            IntVec3 casterPos = caster.Position;

            
            Vector3 forwardDirection = (targetCell - casterPos).ToVector3Shifted().normalized;

           
            for (int dist = Props.minDashDistance; dist <= Props.maxDashDistance; dist++)
            {
                Vector3 targetVector = casterPos.ToVector3Shifted() + (forwardDirection * dist);
                IntVec3 candidate = targetVector.ToIntVec3();

                if (IsValidForwardCell(candidate, caster, map, targetCell))
                {
                    return candidate;
                }

                
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

            
            if (Props.maintainMinDistanceFromTarget && cell.DistanceTo(originalTarget) < Props.minDistanceFromTarget)
                return false;

            
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
        public bool avoidEnemies = false; 
        public float enemyAvoidanceRadius = 5f;
        public int numberOfDashes = 1; 
        public int dashDelay = 0; 

        
        public bool allowRandomDirection = false;
        public bool maintainMinDistanceFromTarget = false; 
        public float minDistanceFromTarget = 2f; 

        
        public EffecterDef castEffecter; 
        public EffecterDef landEffecter; 
        public EffecterDef retryEffecter; 
        public SoundDef castSound;
        public SoundDef landSound;

        
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