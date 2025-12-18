using AnimeArsenal;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{

    public class ThinkNode_ConditionalIsDemon : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            
            return pawn.genes.GenesListForReading.Any(x => x.def == CelestialDefof.BloodDemonArt);
        }
    }

    public class ThinkNode_ConditionalIsColonist : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            return pawn.Faction != null && pawn.Faction == Faction.OfPlayer;
        }
    }

    public class ThinkNode_ConditionalIsDaylight : ThinkNode_Conditional
    {
        public float lightThreshold = 0.4f;

        protected override bool Satisfied(Pawn pawn)
        {
            
            return pawn.Map != null && pawn.Map.skyManager.CurSkyGlow >= lightThreshold;
        }
    }

    public class ThinkNode_ConditionalIsUnderRoof : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            
            return pawn.Map != null && pawn.Position.Roofed(pawn.Map);
        }
    }

    public class JobGiver_GoToRoof : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Position.Roofed(pawn.Map))
            {
                return null;
            }

            if (CellFinder.TryFindRandomCellNear(pawn.Position, pawn.Map, 20,
                (IntVec3 c) => c.Roofed(pawn.Map) && pawn.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Deadly),
                out IntVec3 shelterCell))
            {

                return JobMaker.MakeJob(JobDefOf.Goto, shelterCell);
            }

            return null;
        }
    }

    public class CompAbilityEffect_BaseStance : CompAbilityEffect
    {
        private int maxJumps = 4;
        private int jumps = 0;
        private int jumpDistance = 5;
        private float targetSearchRadius = 18f;
        private List<Pawn> alreadyTargeted = new List<Pawn>();

        public new virtual CompProperties_BaseStance Props => (CompProperties_BaseStance)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (parent.pawn?.Map == null)
                return;

            
            if (Props.casterEffecter != null)
            {
                Effecter effecter = Props.casterEffecter.Spawn();
                effecter.Trigger(new TargetInfo(parent.pawn.Position, parent.pawn.Map), new TargetInfo(parent.pawn.Position, parent.pawn.Map));
                effecter.Cleanup();
            }

            jumps = 0;
            alreadyTargeted.Clear();
            maxJumps = Props.maxJumps;
            jumpDistance = Props.jumpDistance;
            targetSearchRadius = Props.targetSearchRadius;

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
            
            DealDamageAtPosition(pawn.Position);

            
            if (Props.impactEffecter != null)
            {
                Effecter effecter = Props.impactEffecter.Spawn();
                effecter.Trigger(new TargetInfo(pawn.Position, map), new TargetInfo(pawn.Position, map));
                effecter.Cleanup();
            }

            if (CanJumpAgain())
            {
                jumps++;
                IntVec3 currentPosition = pawn.Position;

                
                IntVec3 nextPosition = FindNextTargetPosition(currentPosition, map);

                if (nextPosition.IsValid)
                {
                    CreateFlyerToTargetPosition(currentPosition, nextPosition, map);
                }
                else
                {
                    
                    jumps = maxJumps;
                }
            }
            else
            {
                jumps = 0;
                alreadyTargeted.Clear();
            }
        }

        private IntVec3 FindNextTargetPosition(IntVec3 currentPosition, Map map)
        {
            
            List<Pawn> potentialTargets = new List<Pawn>();

            foreach (Pawn mapPawn in map.mapPawns.AllPawnsSpawned)
            {
                if (mapPawn == parent.pawn) continue;
                if (mapPawn.Dead || mapPawn.Downed) continue; 
                if (alreadyTargeted.Contains(mapPawn)) continue; 

                float distance = currentPosition.DistanceTo(mapPawn.Position);
                if (distance > targetSearchRadius) continue; 

                
                if (mapPawn.HostileTo(parent.pawn))
                {
                    potentialTargets.Add(mapPawn);
                }
            }

            if (potentialTargets.Count == 0)
            {
                return IntVec3.Invalid; 
            }

            
            Pawn closestTarget = potentialTargets.OrderBy(p => currentPosition.DistanceTo(p.Position)).First();
            alreadyTargeted.Add(closestTarget);

            IntVec3 targetPosition = GetPositionNearTarget(closestTarget.Position, currentPosition, map);

            return targetPosition;
        }

        private IntVec3 GetPositionNearTarget(IntVec3 targetPos, IntVec3 currentPos, Map map)
        {
            
            Vector3 direction = (targetPos - currentPos).ToVector3();
            float distance = direction.magnitude;

            if (distance <= jumpDistance)
            {
                
                return EnsurePositionIsValid(targetPos, map);
            }
            else
            {
                
                Vector3 normalized = direction.normalized;
                IntVec3 jumpVector = new IntVec3(
                    Mathf.RoundToInt(normalized.x * jumpDistance),
                    0,
                    Mathf.RoundToInt(normalized.z * jumpDistance)
                );

                IntVec3 newTargetPos = currentPos + jumpVector;
                return EnsurePositionIsValid(newTargetPos, map);
            }
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

        private void DealDamageAtPosition(IntVec3 position)
        {
            Pawn targetPawn = position.GetFirstPawn(parent.pawn.Map);

            if (targetPawn == null || targetPawn == parent.pawn)
                return;

            if (Props.jumpDamageDef != null)
            {
                DamageInfo damageInfo = new DamageInfo(
                    def: Props.jumpDamageDef,
                    amount: Props.jumpDamage.RandomInRange,
                    armorPenetration: 0f,
                    angle: -1f,
                    instigator: parent.pawn,
                    hitPart: null,
                    weapon: null,
                    category: DamageInfo.SourceCategory.ThingOrUnknown,
                    intendedTarget: targetPawn,
                    instigatorGuilty: true,
                    spawnFilth: true,
                    weaponQuality: QualityCategory.Normal,
                    checkForJobOverride: true,
                    preventCascade: false
                );

                targetPawn.TakeDamage(damageInfo);
            }
        }

        private bool CanJumpAgain()
        {
            return jumps < maxJumps;
        }
    }

    public class CompProperties_BaseStance : CompProperties_AbilityEffect
    {
        public List<IntVec3> jumpOffsets;
        public int maxJumps = 10;
        public int ticksBetweenJumps = 5;
        public int jumpDistance = 5;
        public int duration = 30;
        public float targetSearchRadius = 18f;

        public DamageDef jumpDamageDef;
        public FloatRange jumpDamage = new FloatRange(1f, 2f);

        
        public EffecterDef casterEffecter;  
        public EffecterDef impactEffecter;  

        public CompProperties_BaseStance()
        {
            compClass = typeof(CompAbilityEffect_DashStance);
        }
    }

    public class CompAbilityEffect_DashStance : CompAbilityEffect_BaseStance
    {
    }
}