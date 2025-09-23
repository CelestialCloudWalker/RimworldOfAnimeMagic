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

            Map map = parent.pawn.Map;
            IntVec3 center = target.Cell;

            DoVisualEffects(center, map);

            var targets = GetNearbyTargets(center, map);
            ProcessTargets(targets);

            if (Props.createCrater) MakeCrater(center, map);
            if (Props.blackHoleEffect) DoBlackHoleStuff(center, map);
        }

        private List<Thing> GetNearbyTargets(IntVec3 center, Map map)
        {
            var targets = new List<Thing>();
            var cells = GenRadial.RadialCellsAround(center, Props.radius, true);

            foreach (var cell in cells.Where(c => c.InBounds(map)))
            {
                var things = cell.GetThingList(map);
                foreach (var thing in things)
                {
                    if (thing == parent.pawn) continue;

                    if (thing is Pawn p && !p.Dead)
                        targets.Add(thing);
                    else if (Props.canCrushBuildings && thing.def.category == ThingCategory.Building)
                        targets.Add(thing);
                }
            }

            return targets.OrderBy(t => t.Position.DistanceTo(center)).Take(Props.maxTargets).ToList();
        }

        private void ProcessTargets(List<Thing> targets)
        {
            foreach (var target in targets)
            {
                if (target is Pawn pawn)
                    CrushPawn(pawn);
                else if (target.def.category == ThingCategory.Building)
                    CrushBuilding(target);
            }
        }

        private void CrushPawn(Pawn target)
        {
            
            if (Props.instantKillChance > 0f && Rand.Chance(Props.instantKillChance))
            {
                var killDamage = new DamageInfo(CelestialDefof.TwistDamage, target.MaxHitPoints * 3f,
                    Props.armorPenetration, -1f, parent.pawn);
                target.TakeDamage(killDamage);
                Messages.Message($"{target.LabelShort} is utterly crushed by gravitational forces!",
                    target, MessageTypeDefOf.NegativeEvent);
                return;
            }

            
            float damage = Props.baseDamage * target.BodySize * Rand.Range(0.8f, 1.2f);
            var parts = target.health.hediffSet.GetNotMissingParts().Where(p =>
                p.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) ||
                p.def.tags.Contains(BodyPartTagDefOf.MovingLimbSegment) ||
                p.def == BodyPartDefOf.Torso).ToList();

            if (!parts.Any()) parts = target.health.hediffSet.GetNotMissingParts().ToList();

            int hitCount = Mathf.Min(3, parts.Count);
            float damagePerHit = damage / hitCount;

            for (int i = 0; i < hitCount; i++)
            {
                var part = parts.RandomElement();
                var dinfo = new DamageInfo(CelestialDefof.TwistDamage, damagePerHit,
                    Props.armorPenetration, -1f, parent.pawn, part);
                target.TakeDamage(dinfo);
            }

            
            if (Props.stunChance > 0f && Rand.Chance(Props.stunChance) && !target.Dead)
            {
                var stun = HediffMaker.MakeHediff(HediffDefOf.Anesthetic, target);
                stun.Severity = Props.stunDuration / 10f;
                target.health.AddHediff(stun);
                Messages.Message($"{target.LabelShort} is stunned by the crushing force!",
                    target, MessageTypeDefOf.NeutralEvent);
            }

            if (Props.knockdownChance > 0f && Rand.Chance(Props.knockdownChance) &&
                !target.Dead && !target.Downed)
            {
                target.stances.stunner.StunFor(Props.stunDuration * 60, parent.pawn);
                Messages.Message($"{target.LabelShort} is knocked down by gravitational force!",
                    target, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void CrushBuilding(Thing building)
        {
            if (Props.buildingDamage <= 0f) return;

            var damage = new DamageInfo(DamageDefOf.Crush, Props.buildingDamage,
                Props.armorPenetration, -1f, parent.pawn);
            building.TakeDamage(damage);

            if (building.Destroyed)
                Messages.Message($"{building.LabelShort} is crushed to rubble!",
                    building, MessageTypeDefOf.NeutralEvent);
        }

        private void DoVisualEffects(IntVec3 center, Map map)
        {
            for (int i = 0; i < 5; i++)
            {
                var randomCell = center + GenRadial.RadialPattern[Rand.Range(0, GenRadial.RadialPattern.Length)];
                if (randomCell.InBounds(map))
                    FleckMaker.ThrowDustPuffThick(randomCell.ToVector3(), map, 2f, Color.gray);
            }
        }

        private void MakeCrater(IntVec3 center, Map map)
        {
            var cells = GenRadial.RadialCellsAround(center, 2f, true);
            foreach (var cell in cells.Where(c => c.InBounds(map)))
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_RubbleRock, 2);

            Messages.Message("The gravitational force leaves a crater in the ground!",
                MessageTypeDefOf.NeutralEvent);
        }

        private void DoBlackHoleStuff(IntVec3 center, Map map)
        {
            var centerVec = center.ToVector3Shifted();
            for (int i = 0; i < 10; i++)
            {
                var offset = new Vector3(Rand.Range(-Props.radius, Props.radius), 0f,
                    Rand.Range(-Props.radius, Props.radius));
                var pos = centerVec + offset;
                FleckMaker.ThrowSmoke(pos, map, 3f);
                FleckMaker.ThrowFireGlow(pos, map, 2f);
            }

            
            var cells = GenRadial.RadialCellsAround(center, Props.radius, true);
            foreach (var cell in cells.Where(c => c.InBounds(map)))
            {
                var items = cell.GetThingList(map).Where(t => t.def.category == ThingCategory.Item &&
                    t.stackCount < 10).ToList();

                foreach (var item in items)
                {
                    var direction = center - cell;
                    var pullTarget = cell + direction / 2;
                    if (pullTarget.InBounds(map) && pullTarget.Standable(map))
                        item.Position = pullTarget;
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
            var limb = AnimeArsenalUtility.GetRandomLimb(Target);
            if (limb != null)
            {
                var damage = BaseDamage * Scale;
                var dinfo = new DamageInfo(CelestialDefof.TwistDamage, damage, 1f, -1f, Caster, limb);
                Target.TakeDamage(dinfo);
            }
        }
    }
}