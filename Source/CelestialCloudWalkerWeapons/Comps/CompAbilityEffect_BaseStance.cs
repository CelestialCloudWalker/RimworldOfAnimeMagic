using AnimeArsenal;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompAbilityEffect_BaseStance : CompAbilityEffect
    {
        private int maxJumps = 4;
        private int jumps = 0;
        private int jumpDistance = 5;
        private float targetSearchRadius = 18f;
        private List<Pawn> alreadyTargeted = new List<Pawn>();

        public virtual CompProperties_BaseStance Props => (CompProperties_BaseStance)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (parent.pawn?.Map == null)
                return;

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
            // Deal damage at landing position
            DealDamageAtPosition(pawn.Position);

            if (CanJumpAgain())
            {
                jumps++;
                IntVec3 currentPosition = pawn.Position;

                // Find the next target instead of random direction
                IntVec3 nextPosition = FindNextTargetPosition(currentPosition, map);

                if (nextPosition.IsValid)
                {
                    CreateFlyerToTargetPosition(currentPosition, nextPosition, map);
                }
                else
                {
                    // If no targets found, end the dash
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
            // Find all hostile pawns within search radius that haven't been targeted yet
            List<Pawn> potentialTargets = new List<Pawn>();

            foreach (Pawn mapPawn in map.mapPawns.AllPawnsSpawned)
            {
                if (mapPawn == parent.pawn) continue; // Skip self
                if (mapPawn.Dead || mapPawn.Downed) continue; // Skip dead/downed
                if (alreadyTargeted.Contains(mapPawn)) continue; // Skip already targeted

                float distance = currentPosition.DistanceTo(mapPawn.Position);
                if (distance > targetSearchRadius) continue; // Too far

                // Check if hostile
                if (mapPawn.HostileTo(parent.pawn))
                {
                    potentialTargets.Add(mapPawn);
                }
            }

            if (potentialTargets.Count == 0)
            {
                return IntVec3.Invalid; // No targets found
            }

            // Sort by distance and pick the closest
            Pawn closestTarget = potentialTargets.OrderBy(p => currentPosition.DistanceTo(p.Position)).First();
            alreadyTargeted.Add(closestTarget);

            // Calculate position near the target
            IntVec3 targetPosition = GetPositionNearTarget(closestTarget.Position, currentPosition, map);

            return targetPosition;
        }

        private IntVec3 GetPositionNearTarget(IntVec3 targetPos, IntVec3 currentPos, Map map)
        {
            // Calculate direction to target
            Vector3 direction = (targetPos - currentPos).ToVector3();
            float distance = direction.magnitude;

            if (distance <= jumpDistance)
            {
                // Can reach the target directly
                return EnsurePositionIsValid(targetPos, map);
            }
            else
            {
                // Jump toward the target at max jump distance
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

        public CompProperties_BaseStance()
        {
            compClass = typeof(CompAbilityEffect_DashStance);
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

            // Use the base class implementation which now has proper targeting
            base.Apply(target, dest);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (activeDash != null)
            {
                activeDash?.Tick();
                if (activeDash.IsFinished)
                {
                    //always make sure if subscribe to an event that unsubscribe from it properly, it will cause memory leaks otherwise
                    activeDash.onJumpComplete -= OnDashLand;
                    activeDash = null;
                }
            }
        }

        private void OnDashLand(IntVec3 vec)
        {
            Pawn firstPawn = vec.GetFirstPawn(this.parent.pawn.Map);

            if (firstPawn == null || firstPawn == this.parent.pawn)
                return;

            if (Props.jumpDamageDef != null)
            {
                DamageInfo damageInfo = new DamageInfo(
                    def: Props.jumpDamageDef,
                    amount: Props.jumpDamage.RandomInRange,
                    armorPenetration: 0f,
                    angle: -1f,
                    instigator: this.parent.pawn,
                    hitPart: null,
                    weapon: null,
                    category: DamageInfo.SourceCategory.ThingOrUnknown,
                    intendedTarget: firstPawn,
                    instigatorGuilty: true,
                    spawnFilth: true,
                    weaponQuality: QualityCategory.Normal,
                    checkForJobOverride: true,
                    preventCascade: false
                );

                firstPawn.TakeDamage(damageInfo);
            }
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