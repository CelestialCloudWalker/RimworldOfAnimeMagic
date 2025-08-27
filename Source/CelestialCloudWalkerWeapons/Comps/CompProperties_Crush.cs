using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;
using UnityEngine;

namespace AnimeArsenal
{
    public class CompProperties_Crush : CompProperties_AbilityEffect
    {
        public float radius = 10f;
        public int maxTargets = 10;
        public float baseDamage = 10f;

        public float stunChance = 0f;
        public int stunDuration = 1;
        public bool canCrushBuildings = false;
        public float buildingDamage = 0f;
        public float armorPenetration = 0f;
        public float range = 20f;
        public float knockdownChance = 0f;
        public float instantKillChance = 0f;
        public bool createCrater = false;
        public bool blackHoleEffect = false;

        public CompProperties_Crush()
        {
            compClass = typeof(CompAbilityEffect_Crush);
        }
    }

    public class CompAbilityEffect_Crush : CompAbilityEffect
    {
        public new CompProperties_Crush Props => (CompProperties_Crush)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (parent.pawn?.Map == null) return;

            IntVec3 targetCell = target.Cell;
            Map map = parent.pawn.Map;

            CreateCrushEffects(targetCell, map);

            List<Thing> targets = FindTargetsInRadius(targetCell, map);

            foreach (Thing thing in targets.Take(Props.maxTargets))
            {
                if (thing is Pawn pawn)
                {
                    ApplyCrushToPawn(pawn);
                }
                else if (Props.canCrushBuildings && thing.def.category == ThingCategory.Building)
                {
                    ApplyCrushToBuilding(thing);
                }
            }

            if (Props.createCrater)
            {
                CreateCrater(targetCell, map);
            }

            if (Props.blackHoleEffect)
            {
                CreateBlackHoleEffects(targetCell, map);
            }
        }

        private List<Thing> FindTargetsInRadius(IntVec3 center, Map map)
        {
            List<Thing> targets = new List<Thing>();

            var cellsInRadius = GenRadial.RadialCellsAround(center, Props.radius, true);

            foreach (IntVec3 cell in cellsInRadius)
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> thingsInCell = cell.GetThingList(map);
                foreach (Thing thing in thingsInCell)
                {
                    if (IsValidTarget(thing))
                    {
                        targets.Add(thing);
                    }
                }
            }

            
            return targets.OrderBy(t => t.Position.DistanceTo(center)).ToList();
        }

        private bool IsValidTarget(Thing thing)
        {
            if (thing == parent.pawn) return false; 

            if (thing is Pawn pawn)
            {
                return !pawn.Dead;
            }

            if (Props.canCrushBuildings && thing.def.category == ThingCategory.Building)
            {
                return true;
            }

            return false;
        }

        private void ApplyCrushToPawn(Pawn target)
        {
            if (Props.instantKillChance > 0f && Rand.Range(0f, 1f) <= Props.instantKillChance)
            {
                PerformInstantKill(target);
                return;
            }

            float damage = CalculateDamage(target);

            ApplyCrushDamage(target, damage);

            ApplyStunEffect(target);
            ApplyKnockdownEffect(target);
        }

        private void PerformInstantKill(Pawn target)
        {
            Messages.Message($"{target.LabelShort} is utterly crushed by gravitational forces!",
                target, MessageTypeDefOf.NegativeEvent);

            DamageInfo killDamage = new DamageInfo(
                CelestialDefof.TwistDamage,
                target.MaxHitPoints * 3f,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
            );

            target.TakeDamage(killDamage);
        }

        private float CalculateDamage(Pawn target)
        {
            float damage = Props.baseDamage;

            damage *= target.BodySize;

            damage *= Rand.Range(0.8f, 1.2f);

            return damage;
        }

        private void ApplyCrushDamage(Pawn target, float totalDamage)
        {
            var bodyParts = target.health.hediffSet.GetNotMissingParts().ToList();

            var crushTargets = bodyParts.Where(part =>
                part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) ||
                part.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment) ||
                part.def == BodyPartDefOf.Torso).ToList();

            if (!crushTargets.Any())
                crushTargets = bodyParts;

            int partsToHit = Mathf.Min(3, crushTargets.Count);
            float damagePerPart = totalDamage / partsToHit;

            for (int i = 0; i < partsToHit; i++)
            {
                BodyPartRecord part = crushTargets.RandomElement();

                DamageInfo dinfo = new DamageInfo(
                    CelestialDefof.TwistDamage,
                    damagePerPart,
                    Props.armorPenetration,
                    -1f,
                    parent.pawn,
                    part,
                    null,
                    DamageInfo.SourceCategory.ThingOrUnknown
                );

                target.TakeDamage(dinfo);
            }
        }

        private void ApplyStunEffect(Pawn target)
        {
            if (Props.stunChance <= 0f || target.Dead) return;

            if (Rand.Range(0f, 1f) <= Props.stunChance)
            {
                HediffDef stunHediff = HediffDefOf.Anesthetic; 
                Hediff stun = HediffMaker.MakeHediff(stunHediff, target);
                stun.Severity = Props.stunDuration / 10f; 
                target.health.AddHediff(stun);

                Messages.Message($"{target.LabelShort} is stunned by the crushing force!",
                    target, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void ApplyKnockdownEffect(Pawn target)
        {
            if (Props.knockdownChance <= 0f || target.Dead || target.Downed) return;

            if (Rand.Range(0f, 1f) <= Props.knockdownChance)
            {
                target.stances.stunner.StunFor(Props.stunDuration * 60, parent.pawn); // Convert to ticks

                Messages.Message($"{target.LabelShort} is knocked down by gravitational force!",
                    target, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void ApplyCrushToBuilding(Thing building)
        {
            if (Props.buildingDamage <= 0f) return;

            DamageInfo buildingDamage = new DamageInfo(
                DamageDefOf.Crush,
                Props.buildingDamage,
                Props.armorPenetration,
                -1f,
                parent.pawn,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
            );

            building.TakeDamage(buildingDamage);

            if (building.Destroyed)
            {
                Messages.Message($"{building.LabelShort} is crushed to rubble!",
                    building, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void CreateCrushEffects(IntVec3 center, Map map)
        {
            
            for (int i = 0; i < 5; i++)
            {
                IntVec3 randomCell = center + GenRadial.RadialPattern[Rand.Range(0, GenRadial.RadialPattern.Length)];
                if (randomCell.InBounds(map))
                {
                    FleckMaker.ThrowDustPuffThick(randomCell.ToVector3(), map, 2f, Color.gray);
                }
            }
        }

        private void CreateCrater(IntVec3 center, Map map)
        {
            
            var craterCells = GenRadial.RadialCellsAround(center, 2f, true);

            foreach (IntVec3 cell in craterCells)
            {
                if (cell.InBounds(map))
                {
                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_RubbleRock, 2);
                }
            }

            Messages.Message("The gravitational force leaves a crater in the ground!",
                MessageTypeDefOf.NeutralEvent);
        }

        private void CreateBlackHoleEffects(IntVec3 center, Map map)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector3 centerVec = center.ToVector3Shifted();
                Vector3 randomOffset = new Vector3(
                    Rand.Range(-Props.radius, Props.radius),
                    0f,
                    Rand.Range(-Props.radius, Props.radius)
                );
                Vector3 randomPos = centerVec + randomOffset;

                FleckMaker.ThrowSmoke(randomPos, map, 3f);

                FleckMaker.ThrowFireGlow(randomPos, map, 2f);
            }

            ApplyBlackHolePull(center, map);
        }

        private void ApplyBlackHolePull(IntVec3 center, Map map)
        {
            var affectedCells = GenRadial.RadialCellsAround(center, Props.radius, true);

            foreach (IntVec3 cell in affectedCells)
            {
                if (!cell.InBounds(map)) continue;

                var items = cell.GetThingList(map).Where(t => t.def.category == ThingCategory.Item).ToList();

                foreach (Thing item in items)
                {
                    if (item.stackCount < 10) 
                    {
                        IntVec3 direction = center - cell;
                        IntVec3 pullTarget = cell + direction / 2; 
                        if (pullTarget.InBounds(map) && pullTarget.Standable(map))
                        {
                            item.Position = pullTarget;
                        }
                    }
                }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            if (target.IsValid) return true;

            if (throwMessages)
                Messages.Message("Must target a valid location", MessageTypeDefOf.RejectInput);

            return false;
        }

        public static void TwistTargetLimb(Pawn Target, Pawn Caster, float BaseDamage, float Scale)
        {
            BodyPartRecord targetLimb = AnimeArsenalUtility.GetRandomLimb(Target);
            if (targetLimb != null)
            {
                float damage = BaseDamage * Scale;
                DamageInfo dinfo = new DamageInfo(CelestialDefof.TwistDamage, damage, 1f, -1f, Caster, targetLimb);
                Target.TakeDamage(dinfo);
            }
        }
    }
}