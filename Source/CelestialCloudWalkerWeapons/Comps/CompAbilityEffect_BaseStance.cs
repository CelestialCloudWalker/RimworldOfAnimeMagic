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
            try
            {
                if (pawn?.genes == null)
                    return false;

                return pawn.genes.GenesListForReading.Any(x => x.def == CelestialDefof.BloodDemonArt);
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[Demon AI] Error checking if {pawn?.LabelShort} is demon: {ex.Message}", pawn?.thingIDNumber ?? 0);
                return false;
            }
        }
    }

    public class ThinkNode_ConditionalIsColonist : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            return pawn?.Faction != null && pawn.Faction == Faction.OfPlayer;
        }
    }

    public class ThinkNode_ConditionalIsDaylight : ThinkNode_Conditional
    {
        public float lightThreshold = 0.4f;

        protected override bool Satisfied(Pawn pawn)
        {
            try
            {
                if (pawn?.Map?.skyManager == null)
                    return false;

                return pawn.Map.skyManager.CurSkyGlow >= lightThreshold;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ThinkNode_ConditionalIsUnderRoof : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            try
            {
                if (pawn?.Map == null || !pawn.Spawned)
                    return false;

                return pawn.Position.Roofed(pawn.Map);
            }
            catch
            {
                return false;
            }
        }
    }

    public class JobGiver_GoToRoof : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn?.Map == null || !pawn.Spawned)
                return null;

            if (pawn.Position.Roofed(pawn.Map))
                return null;

            var sunlightComp = pawn.Map.GetComponent<MapComponent_SunlightDamage>();
            if (sunlightComp == null)
                return null;

            float tolerancePercent = sunlightComp.GetSunTolerancePercentage(pawn);
            float damagePercent = sunlightComp.GetSunlightDamagePercentage(pawn);

            if (tolerancePercent > 50f && damagePercent < 10f)
                return null;

            LocomotionUrgency urgency = LocomotionUrgency.Walk;
            if (damagePercent > 50f || tolerancePercent < 20f)
                urgency = LocomotionUrgency.Sprint;
            else if (damagePercent > 20f || tolerancePercent < 40f)
                urgency = LocomotionUrgency.Jog;

            IntVec3 shelterCell = IntVec3.Invalid;

            int maxRadius = urgency == LocomotionUrgency.Sprint ? 100 : 50;

            if (!TryFindRoofedCell(pawn, 20, out shelterCell))
            {
                if (!TryFindRoofedCell(pawn, maxRadius, out shelterCell))
                {
                    IntVec3 wanderCell = CellFinder.RandomClosewalkCellNear(pawn.Position, pawn.Map, 8);
                    if (wanderCell.IsValid)
                    {
                        return JobMaker.MakeJob(JobDefOf.GotoWander, wanderCell);
                    }
                    return null;
                }
            }

            if (shelterCell.IsValid)
            {
                Job job = JobMaker.MakeJob(JobDefOf.Goto, shelterCell);
                job.locomotionUrgency = urgency;
                job.expiryInterval = 2000;
                job.checkOverrideOnExpire = true;
                return job;
            }

            return null;
        }

        private bool TryFindRoofedCell(Pawn pawn, int radius, out IntVec3 cell)
        {
            return CellFinder.TryFindRandomCellNear(
                pawn.Position,
                pawn.Map,
                radius,
                (IntVec3 c) => c.Roofed(pawn.Map) &&
                              c.Standable(pawn.Map) &&
                              !c.IsForbidden(pawn) &&
                              pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly),
                out cell
            );
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
                float baseDamage = Props.jumpDamage.RandomInRange;
                float scaledDamage = DamageScalingUtility.GetScaledDamage(
                    baseDamage,
                    parent.pawn,
                    Props.scaleStat,
                    Props.scaleSkill,
                    Props.skillMultiplier,
                    Props.debugScaling
                );

                DamageInfo damageInfo = new DamageInfo(
                    def: Props.jumpDamageDef,
                    amount: scaledDamage,
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

        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;

        public CompProperties_BaseStance()
        {
            compClass = typeof(CompAbilityEffect_DashStance);
        }
    }

    public class CompAbilityEffect_DashStance : CompAbilityEffect_BaseStance
    {
    }
}