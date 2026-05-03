using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityLeapStrike : EMF.CompProperties_BaseJumpEffect
    {
        public int strikeDamage = 28;
        public DamageDef strikeDamageDef;
        public float strikeRadius = 2f;
        public float armorPen = 0f;
        public EffecterDef strikeEffecter;

        public EffecterDef castEffecter;

        public bool hasCone = false;
        public int coneDamage = 0;
        public DamageDef coneDamageDef;
        public float coneLength = 12f;
        public float coneWidth = 3f;
        public float coneArmorPen = 0f;
        public bool coneForward = true;
        public EffecterDef coneEffecter;
        public EffecterDef coneHitEffecter;

        public int coneCount = 1;
        public bool coneSpreadFull = false;
        public float coneSpreadArc = 180f;

        public ThingDef midHazardDef;
        public ThingDef endHazardDef;
        public int endHazardSpawnDelay = 10;

        public int burstDamage = 0;
        public float burstRadius = 6f;
        public DamageDef burstDamageDef;
        public int burstRepeatCount = 2;
        public int burstInterval = 10;

        public float delayedBurstDamage = 0f;
        public float delayedBurstRadius = 6f;
        public DamageDef delayedBurstDamageDef;
        public float delayedBurstArmorPen = 0.25f;
        public int delayedBurstDelayTicks = 25;
        public int delayedBurstMaxTargets = 8;
        public int delayedBurstWaveCount = 1;
        public int delayedBurstTicksBetweenWaves = 15;
        public float delayedBurstDamageDecayPerWave = 1f;
        public EffecterDef delayedBurstEffecter;
        public EffecterDef delayedBurstHitEffecter;

        public bool requiresSecondTarget = false;

        public CompProperties_AbilityLeapStrike()
        {
            compClass = typeof(CompAbilityEffect_LeapStrike);
        }
    }

    public class CompAbilityEffect_LeapStrike : EMF.CompAbilityEffect_BaseJumpEffect
    {
        private new CompProperties_AbilityLeapStrike Props =>
            (CompProperties_AbilityLeapStrike)props;

        private CompProperties_AbilityLeapStrike ActiveProps
        {
            get
            {
                if (parent is EMF.LevelingResourceAbility lra)
                {
                    var activeComp = lra.EffectComps
                        .OfType<CompAbilityEffect_LeapStrike>()
                        .FirstOrDefault();
                    if (activeComp != null)
                        return activeComp.Props;
                }
                return Props;
            }
        }

        private IntVec3 launchCell;
        private IntVec3 burstTargetCell;
        private IntVec3 coneAimCell;
        private bool pendingConeAim;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            launchCell = parent.pawn.Position;
            burstTargetCell = target.Cell;
            coneAimCell = IntVec3.Invalid;

            if (Props.requiresSecondTarget)
                pendingConeAim = true;

            if (Props.castEffecter != null && parent.pawn.Map != null)
            {
                Effecter e = Props.castEffecter.Spawn(parent.pawn.Position, parent.pawn.Map);
                e.EffectTick(parent.pawn, parent.pawn);
                e.EffectTick(parent.pawn, parent.pawn);
                e.Cleanup();
            }

            base.Apply(target, dest);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
                yield return g;

            if (!pendingConeAim || thingFlyer == null)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = "Aim cone",
                defaultDesc = "Choose the direction of the cone.",
                icon = parent.def.uiIcon,
                action = () =>
                {
                    Find.Targeter.BeginTargeting(
                        new TargetingParameters
                        {
                            canTargetLocations = true,
                            canTargetPawns = true,
                        },
                        t =>
                        {
                            coneAimCell = t.Cell;
                            pendingConeAim = false;
                        },
                        parent.pawn,
                        () => { }
                    );
                }
            };
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);

            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;

            if (pendingConeAim && thingFlyer != null)
            {
                GenDraw.DrawFieldEdges(
                    GenRadial.RadialCellsAround(burstTargetCell, ActiveProps.strikeRadius, true)
                             .Where(c => c.InBounds(pawn.Map)).ToList(),
                    Color.red);

                if (ActiveProps.hasCone && ActiveProps.coneDamage > 0)
                {
                    Vector3 landV = burstTargetCell.ToVector3Shifted();
                    Vector3 dir = target.Cell.ToVector3Shifted() - landV;
                    if (dir == Vector3.zero) dir = Vector3.forward;
                    float baseAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    DrawConePreview(pawn.Map, landV, baseAngle,
                        ActiveProps.coneCount, ActiveProps.coneSpreadFull, ActiveProps.coneSpreadArc);
                }
            }
            else
            {
                GenDraw.DrawFieldEdges(
                    GenRadial.RadialCellsAround(target.Cell, ActiveProps.strikeRadius, true)
                             .Where(c => c.InBounds(pawn.Map)).ToList(),
                    Color.red);

                if (ActiveProps.hasCone && ActiveProps.coneDamage > 0 && !ActiveProps.requiresSecondTarget)
                {
                    Vector3 origin = pawn.DrawPos;
                    Vector3 tgt = target.Cell.ToVector3Shifted();
                    Vector3 dir = ActiveProps.coneForward ? (tgt - origin) : (origin - tgt);
                    if (dir == Vector3.zero) dir = Vector3.forward;
                    float baseAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                    DrawConePreview(pawn.Map, tgt, baseAngle,
                        ActiveProps.coneCount, ActiveProps.coneSpreadFull, ActiveProps.coneSpreadArc);
                }

                if (ActiveProps.burstDamage > 0)
                    GenDraw.DrawFieldEdges(
                        GenRadial.RadialCellsAround(target.Cell, ActiveProps.burstRadius, true)
                                 .Where(c => c.InBounds(pawn.Map)).ToList(),
                        Color.cyan);

                if (ActiveProps.delayedBurstDamage > 0)
                    GenDraw.DrawFieldEdges(
                        GenRadial.RadialCellsAround(target.Cell, ActiveProps.delayedBurstRadius, true)
                                 .Where(c => c.InBounds(pawn.Map)).ToList(),
                        Color.magenta);
            }
        }

        private void DrawConePreview(Map map, Vector3 origin, float baseAngle,
            int count, bool spreadFull, float spreadArc)
        {
            foreach (float angle in GetConeAngles(baseAngle, count, spreadFull, spreadArc))
            {
                GenDraw.DrawFieldEdges(
                    RotatedRectTargetFinder.GetCellsInRotatedRect(
                        map, origin, ActiveProps.coneWidth, ActiveProps.coneLength, angle)
                    .Where(c => c.InBounds(map)).ToList(),
                    Color.yellow);
            }
        }

        protected override void OnLand(IntVec3 landCell, Thing flyer, Pawn pawn)
        {
            base.OnLand(landCell, flyer, pawn);
            pendingConeAim = false;

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            ApplySweetSpot(caster, landCell);

            if (Props.hasCone && Props.coneDamage > 0)
                ApplyCone(caster, landCell);

            if (Props.burstDamage > 0)
                ApplyBurst(caster, landCell);

            if (Props.delayedBurstDamage > 0)
                ScheduleDelayedBurst(caster, burstTargetCell);
        }

        private void ApplySweetSpot(Pawn caster, IntVec3 origin)
        {
            DamageDef dmg = Props.strikeDamageDef ?? DamageDefOf.Cut;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                origin, caster.Map, Props.strikeRadius, true))
            {
                if (!(thing is Pawn hit)) continue;
                if (hit == caster || hit.Dead || !hit.Spawned) continue;
                if (!hit.HostileTo(caster)) continue;

                hit.TakeDamage(new DamageInfo(dmg, Props.strikeDamage, Props.armorPen, -1f, caster));

                if (Props.strikeEffecter != null)
                {
                    Effecter e = Props.strikeEffecter.Spawn(hit.Position, caster.Map);
                    e.EffectTick(hit, hit);
                    e.EffectTick(hit, hit);
                    e.Cleanup();
                }
            }
        }

        private void ApplyCone(Pawn caster, IntVec3 landCell)
        {
            Map map = caster.Map;
            Vector3 landV = landCell.ToVector3Shifted();

            Vector3 dir;
            if (Props.requiresSecondTarget && coneAimCell.IsValid)
                dir = coneAimCell.ToVector3Shifted() - landV;
            else
            {
                Vector3 launchV = launchCell.ToVector3Shifted();
                dir = Props.coneForward ? (landV - launchV) : (launchV - landV);
            }
            if (dir == Vector3.zero) dir = Vector3.forward;
            float baseAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            if (Props.coneEffecter != null)
            {
                Effecter e = Props.coneEffecter.Spawn(landCell, map);
                e.EffectTick(new TargetInfo(landCell, map), TargetInfo.Invalid);
                e.EffectTick(new TargetInfo(landCell, map), TargetInfo.Invalid);
                e.EffectTick(new TargetInfo(landCell, map), TargetInfo.Invalid);
                e.Cleanup();
            }

            DamageDef dmg = Props.coneDamageDef ?? DamageDefOf.Cut;
            HashSet<Pawn> alreadyHit = new HashSet<Pawn>();

            foreach (float angle in GetConeAngles(baseAngle, Props.coneCount,
                Props.coneSpreadFull, Props.coneSpreadArc))
            {
                foreach (IntVec3 cell in RotatedRectTargetFinder.GetCellsInRotatedRect(
                    map, landV, Props.coneWidth, Props.coneLength, angle))
                {
                    if (!cell.InBounds(map)) continue;
                    foreach (Thing thing in cell.GetThingList(map).ToList())
                    {
                        if (!(thing is Pawn target)) continue;
                        if (target == caster || target.Dead || !target.Spawned) continue;
                        if (!target.HostileTo(caster)) continue;
                        if (!alreadyHit.Add(target)) continue;

                        target.TakeDamage(new DamageInfo(
                            dmg, Props.coneDamage, Props.coneArmorPen, -1f, caster));

                        if (Props.coneHitEffecter != null)
                        {
                            Effecter e = Props.coneHitEffecter.Spawn(target.Position, map);
                            e.EffectTick(target, target);
                            e.EffectTick(target, target);
                            e.Cleanup();
                        }
                    }
                }

               
                if (Props.midHazardDef != null || Props.endHazardDef != null)
                    SpawnConeHazards(caster, map, landV, angle);
            }
        }

        private void SpawnConeHazards(Pawn caster, Map map, Vector3 landV, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            if (Props.midHazardDef != null)
            {
                IntVec3 midCell = (landV + dir * (Props.coneLength * 0.5f)).ToIntVec3();
                if (midCell.InBounds(map))
                    SpawnHazard(Props.midHazardDef, midCell, map, caster, 0);
            }

            if (Props.endHazardDef != null)
            {
                IntVec3 endCell = (landV + dir * (Props.coneLength * 0.9f)).ToIntVec3();
                if (endCell.InBounds(map))
                    SpawnHazard(Props.endHazardDef, endCell, map, caster, Props.endHazardSpawnDelay);
            }
        }

        private void SpawnHazard(ThingDef def, IntVec3 cell, Map map, Pawn caster, int delayTicks)
        {
            DoSpawnHazard(def, cell, map, caster);
        }

        private static void DoSpawnHazard(ThingDef def, IntVec3 cell, Map map, Pawn caster)
        {
            Thing hazard = ThingMaker.MakeThing(def);
            var hazardComp = hazard.TryGetComp<CompGaleHazard>();
            if (hazardComp != null)
                hazardComp.caster = caster;
            GenSpawn.Spawn(hazard, cell, map);
        }

        private static IEnumerable<float> GetConeAngles(float baseAngle, int count,
            bool spreadFull, float spreadArc)
        {
            if (count <= 1)
            {
                yield return baseAngle;
                yield break;
            }

            float totalArc = spreadFull ? 360f : spreadArc;
            float step = totalArc / count;
            float arcOffset = spreadFull ? 0f : -(totalArc / 2f) + (step / 2f);

            for (int i = 0; i < count; i++)
                yield return baseAngle + arcOffset + (step * i);
        }

        private void ApplyBurst(Pawn caster, IntVec3 origin)
        {
            var manager = caster.Map.GetComponent<AoEBurstTickManager>();
            DamageDef dmg = Props.burstDamageDef ?? DamageDefOf.Cut;

            var burstProps = new CompProperties_AbilityAoEBurst
            {
                damage = Props.burstDamage,
                radius = Props.burstRadius,
                damageDef = dmg,
                repeatCount = Props.burstRepeatCount,
                damageInterval = Props.burstInterval,
                affectCaster = false,
                onlyAffectHostiles = true,
            };

            if (Props.burstRepeatCount <= 1 || manager == null)
            {
                AoEBurstHit.FireAt(origin, caster, caster.Map, burstProps);
                return;
            }

            int startTick = Find.TickManager.TicksGame;
            for (int i = 0; i < Props.burstRepeatCount; i++)
                manager.Schedule(new AoEBurstHit(
                    origin: origin, caster: caster, map: caster.Map,
                    props: burstProps, fireTick: startTick + (Props.burstInterval * i)));
        }

        private void ScheduleDelayedBurst(Pawn caster, IntVec3 burstOrigin)
        {
            Map map = caster.Map;
            var manager = map.GetComponent<DelayedBurstManager>();
            if (manager == null)
            {
                Log.Warning("[AnimeArsenal] LeapStrike: DelayedBurstManager not found on map.");
                return;
            }

            manager.Schedule(new DelayedBurstJob(
                origin: burstOrigin, caster: caster, map: map,
                props: new CompProperties_DelayedAoEBurst
                {
                    delayTicks = Props.delayedBurstDelayTicks,
                    radius = Props.delayedBurstRadius,
                    maxTargets = Props.delayedBurstMaxTargets,
                    damageAmount = Props.delayedBurstDamage,
                    damageDef = Props.delayedBurstDamageDef ?? DamageDefOf.Cut,
                    armourPen = Props.delayedBurstArmorPen,
                    waveCount = Props.delayedBurstWaveCount,
                    ticksBetweenWaves = Props.delayedBurstTicksBetweenWaves,
                    damageDecayPerWave = Props.delayedBurstDamageDecayPerWave,
                    canHitFriendly = false,
                    burstEffecter = Props.delayedBurstEffecter,
                    hitEffecter = Props.delayedBurstHitEffecter,
                },
                startTick: Find.TickManager.TicksGame + Props.delayedBurstDelayTicks));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref launchCell, "launchCell");
            Scribe_Values.Look(ref burstTargetCell, "targetCell");
            Scribe_Values.Look(ref coneAimCell, "coneAimCell");
        }
    }
}
