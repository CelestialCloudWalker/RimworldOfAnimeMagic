using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public enum TrailSelectMode
    {
        Full,
        Interval,
        Random,
        EvenSpread,
    }

    public enum TrailSpawnRegion
    {
        WholePath,
        NearImpact,
        NearOrigin,
    }

    public class Projectile_ImpactAOE : ScalingStatDamageProjectile
    {
        public ProjectileProperties_ImpactAOE Props =>
            (ProjectileProperties_ImpactAOE)def.projectile;

        private readonly List<IntVec3> travelledCells = new List<IntVec3>();
        private IntVec3 lastRecordedCell;

        protected override void Tick()
        {
            base.Tick();
            IntVec3 current = Position;
            if (current != lastRecordedCell && current.IsValid)
            {
                travelledCells.Add(current);
                lastRecordedCell = current;
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            IntVec3 impactCell;
            if (hitThing != null && hitThing.Spawned)
                impactCell = hitThing.Position;
            else if (intendedTarget.IsValid)
                impactCell = intendedTarget.Cell;
            else
                impactCell = Position;

            Map map = MapHeld;
            Pawn launcherPawn = launcher as Pawn;

            var targets = AnimeArsenalUtility.GetThingsInRange(
                impactCell, map, Props.ExplosionRadius, IsValidTarget).ToList();

            float scaledAoeDamage = DamageScalingUtility.GetScaledDamage(
                Props.aoeDamage, launcherPawn,
                Props.scaleStat, Props.scaleSkill,
                Props.skillMultiplier, Props.debugScaling);

            AnimeArsenalUtility.DealDamageToThingsInRange(
                targets, Props.damageDef, scaledAoeDamage,
                Props.GetArmorPenetration(launcher));

            foreach (var target in targets.OfType<Pawn>().Where(p => !p.Destroyed))
                TryPushBack(target);

            Props.ExplosionEffect?.Spawn(impactCell, map);

            if (Props.leaveHazardDef != null && map != null)
                SpawnHazard(Props.leaveHazardDef, impactCell, map, launcherPawn);

            if (Props.trailHazardDef != null && map != null && travelledCells.Count > 0)
                SpawnTrailHazards(impactCell, map, launcherPawn);

            base.Impact(hitThing, blockedByShield);
        }

        private void SpawnTrailHazards(IntVec3 impactCell, Map map, Pawn launcherPawn)
        {
            List<IntVec3> candidates = BuildTrailCandidates(impactCell);
            if (candidates.Count == 0) return;

            int max = Props.maxTrailHazards > 0
                ? Props.maxTrailHazards
                : candidates.Count;

            switch (Props.trailSelectMode)
            {
                case TrailSelectMode.Interval:
                    {
                        int interval = Mathf.Max(1, Props.trailHazardInterval);
                        int spawned = 0;
                        for (int i = 0; i < candidates.Count && spawned < max; i += interval)
                        {
                            SpawnHazard(Props.trailHazardDef, candidates[i], map, launcherPawn);
                            spawned++;
                        }
                        break;
                    }
                case TrailSelectMode.Random:
                    {
                        foreach (var cell in candidates.InRandomOrder().Take(max))
                            SpawnHazard(Props.trailHazardDef, cell, map, launcherPawn);
                        break;
                    }
                case TrailSelectMode.EvenSpread:
                    {
                        if (max == 1)
                        {
                            SpawnHazard(Props.trailHazardDef,
                                candidates[candidates.Count / 2], map, launcherPawn);
                        }
                        else
                        {
                            float step = (float)(candidates.Count - 1) / (max - 1);
                            for (int i = 0; i < max; i++)
                            {
                                int idx = Mathf.RoundToInt(step * i);
                                SpawnHazard(Props.trailHazardDef, candidates[idx], map, launcherPawn);
                            }
                        }
                        break;
                    }
                default: 
                    {
                        foreach (var cell in candidates.Take(max))
                            SpawnHazard(Props.trailHazardDef, cell, map, launcherPawn);
                        break;
                    }
            }
        }

        private List<IntVec3> BuildTrailCandidates(IntVec3 impactCell)
        {
            if (travelledCells.Count == 0) return new List<IntVec3>();
            int cutoff = travelledCells.Count;
            float closestDist = float.MaxValue;
            for (int i = 0; i < travelledCells.Count; i++)
            {
                float dist = (travelledCells[i] - impactCell).LengthHorizontalSquared;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    cutoff = i + 1;
                }
            }

            List<IntVec3> validPath = travelledCells
                .Take(cutoff)
                .Where(c => c != impactCell)
                .ToList();

            switch (Props.trailSpawnRegion)
            {
                case TrailSpawnRegion.NearImpact:
                    {
                        int take = Mathf.Max(1, Props.trailNearImpactCells);
                        int start = Mathf.Max(0, validPath.Count - take);
                        return validPath.Skip(start).ToList();
                    }
                case TrailSpawnRegion.NearOrigin:
                    {
                        int take = Mathf.Max(1, Props.trailNearImpactCells);
                        return validPath.Take(take).ToList();
                    }
                default:
                    return validPath;
            }
        }

        private void SpawnHazard(ThingDef hazardDef, IntVec3 cell, Map map, Pawn launcherPawn)
        {
            if (!cell.InBounds(map) || !cell.IsValid) return;
            Thing hazard = GenSpawn.Spawn(hazardDef, cell, map);
            CompGaleHazard comp = hazard.TryGetComp<CompGaleHazard>();
            if (comp != null)
                comp.caster = launcherPawn;
        }

        private bool IsValidTarget(Thing target)
        {
            if (target == this ||
                (launcher != null && target == launcher && !Props.CanHitCaster))
                return false;
            if (target.Faction != null &&
                !target.Faction.HostileTo(launcher.Faction) &&
                !Props.CanHitFriendly)
                return false;
            return true;
        }

        private void TryPushBack(Pawn target)
        {
            if (launcher?.Map == null) return;
            var direction = target.Position - launcher.Position;
            var knockback = target.Position + direction * Rand.Range(1, 4);
            knockback = knockback.ClampInsideMap(launcher.Map);
            if (GenAdj.TryFindRandomAdjacentCell8WayWithRoom(target, out var destination))
            {
                var map = launcher.Map;
                if (destination.IsValid && destination.InBounds(map) &&
                    destination.Walkable(map))
                {
                    var flyer = PawnFlyer.MakeFlyer(
                        CelestialDefof.AnimeArsenal_Flyer, target, destination, null, null);
                    GenSpawn.Spawn(flyer, target.Position, map);
                }
            }
        }
    }

    public class ProjectileProperties_ImpactAOE : ProjectileProperties
    {
        public float ExplosionRadius = 10f;
        public EffecterDef ExplosionEffect;
        public bool CanHitFriendly = true;
        public bool CanHitCaster = false;
        public float aoeDamage = 7f;
        public StatDef scaleStat;
        public SkillDef scaleSkill;
        public float skillMultiplier = 0.1f;
        public bool debugScaling = false;

        public ThingDef leaveHazardDef;

        public ThingDef trailHazardDef;
        public int maxTrailHazards = 0;
        public int trailHazardInterval = 3;
        public int trailNearImpactCells = 5;
        public TrailSelectMode trailSelectMode = TrailSelectMode.Full;
        public TrailSpawnRegion trailSpawnRegion = TrailSpawnRegion.WholePath;
    }
}