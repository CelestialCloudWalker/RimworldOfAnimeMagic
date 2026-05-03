using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_ConstantResoundingSlashes : CompProperties_AbilityEffect
    {
        public DamageDef damageType;
        public FloatRange strikeDamage = new FloatRange(18f, 24f);
        public float armorPen = 0.3f;
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;
        public bool spawnADBAlongPath = false;
        public ThingDef adbProjectileDef;
        public int adbSpawnInterval = 3;
        public EffecterDef casterEffecter;
        public EffecterDef pathEffecter;
        public EffecterDef impactEffecter;
        public ThingDef dashTrailMote;

        public CompProperties_ConstantResoundingSlashes()
        {
            compClass = typeof(CompAbilityEffect_ConstantResoundingSlashes);
        }
    }

    public class CompAbilityEffect_ConstantResoundingSlashes : CompAbilityEffect
    {
        public new CompProperties_ConstantResoundingSlashes Props =>
            (CompProperties_ConstantResoundingSlashes)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster == null || caster.Map == null) return;

            Map map = caster.Map;
            IntVec3 startPos = caster.Position;
            IntVec3 endPos = target.Cell;

            if (Props.casterEffecter != null)
            {
                Effecter e = Props.casterEffecter.Spawn();
                e.Trigger(new TargetInfo(startPos, map), TargetInfo.Invalid);
                e.Cleanup();
            }

            DamageAlongPath(caster, startPos, endPos, map);

            DelegateFlyer flyer = (DelegateFlyer)PawnFlyer.MakeFlyer(
                CelestialDefof.AnimeArsenal_DelegateFlyer, caster, endPos, null, null);
            flyer.OnRespawnPawn += OnFlyerLand;
            GenSpawn.Spawn(flyer, startPos, map);
        }

        private void OnFlyerLand(Pawn pawn, PawnFlyer flyer, Map map)
        {
            if (Props.impactEffecter != null)
            {
                Effecter e = Props.impactEffecter.Spawn();
                e.Trigger(new TargetInfo(pawn.Position, map), TargetInfo.Invalid);
                e.Cleanup();
            }
        }

        private void DamageAlongPath(Pawn caster, IntVec3 from, IntVec3 to, Map map)
        {
            List<IntVec3> path = BuildPath(from, to, map);
            HashSet<Pawn> alreadyHit = new HashSet<Pawn>();

            for (int i = 0; i < path.Count; i++)
            {
                IntVec3 cell = path[i];

                if (Props.dashTrailMote != null)
                    MoteMaker.MakeStaticMote(cell, map, Props.dashTrailMote);

                if (Props.pathEffecter != null)
                {
                    Effecter e = Props.pathEffecter.Spawn();
                    e.Trigger(new TargetInfo(cell, map), TargetInfo.Invalid);
                    e.Cleanup();
                }

                foreach (Thing thing in cell.GetThingList(map).ToList())
                {
                    if (thing is Pawn p && p != caster && p.HostileTo(caster) && !alreadyHit.Contains(p))
                    {
                        alreadyHit.Add(p);
                        ApplyDamage(caster, p);
                    }
                }

                if (Props.spawnADBAlongPath && Props.adbProjectileDef != null && i % Props.adbSpawnInterval == 0)
                    SpawnADBAt(cell, caster, map);
            }
        }

        private List<IntVec3> BuildPath(IntVec3 from, IntVec3 to, Map map)
        {
            List<IntVec3> path = new List<IntVec3>();
            Vector3 dir = (to - from).ToVector3();
            float dist = dir.magnitude;
            dir.Normalize();
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist));

            for (int i = 1; i <= steps; i++)
            {
                IntVec3 cell = (from.ToVector3() + dir * dist * ((float)i / steps)).ToIntVec3();
                if (cell.InBounds(map))
                    path.Add(cell);
            }
            return path;
        }

        private void ApplyDamage(Pawn instigator, Pawn target)
        {
            if (target == null || target.Dead) return;

            float scaled = DamageScalingUtility.GetScaledDamage(
                Props.strikeDamage.RandomInRange,
                instigator, Props.scaleStat, Props.scaleSkill,
                Props.skillMultiplier, Props.debugScaling);

            DamageInfo dinfo = new DamageInfo(
                Props.damageType ?? DamageDefOf.Cut,
                scaled, Props.armorPen, -1f, instigator);
            target.TakeDamage(dinfo);
        }

        private void SpawnADBAt(IntVec3 cell, Pawn instigator, Map map)
        {
            Thing thing = ThingMaker.MakeThing(Props.adbProjectileDef);
            if (thing is Projectile proj)
            {
                GenSpawn.Spawn(proj, instigator.Position, map);
                proj.Launch(instigator, cell, cell, ProjectileHitFlags.All);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!target.IsValid)
            {
                if (throwMessages)
                    Messages.Message("Must select a target", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            Pawn caster = parent?.pawn;
            if (caster == null) return false;

            if (target.Pawn == caster)
            {
                if (throwMessages)
                    Messages.Message("Cannot target self", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }
    }
}