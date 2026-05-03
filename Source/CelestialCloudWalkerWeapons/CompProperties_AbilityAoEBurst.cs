using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityAoEBurst : CompProperties_AbilityEffect
    {
        public float radius = 5f;
        public int damage = 0;
        public DamageDef damageDef;
        public float armorPen = 0f;
        public int repeatCount = 1;
        public int damageInterval = 6;
        public int maxTargets = 99;
        public HediffDef hediff;
        public float hediffSeverity = 0f;
        public EffecterDef effecter;
        public EffecterDef hitEffecter;
        public FleckDef fleck;
        public bool affectCaster = false;
        public bool onlyAffectHostiles = false;

        public CompProperties_AbilityAoEBurst()
        {
            compClass = typeof(CompAbilityEffect_AoEBurst);
        }
    }

    public class CompAbilityEffect_AoEBurst : CompAbilityEffect
    {
        public new CompProperties_AbilityAoEBurst Props =>
            (CompProperties_AbilityAoEBurst)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent.pawn;
            if (caster?.Map == null) return;

            if (Props.repeatCount <= 1)
            {
                AoEBurstHit.FireAt(caster.Position, caster, caster.Map, Props);
                return;
            }

            var manager = caster.Map.GetComponent<AoEBurstTickManager>();
            if (manager == null)
            {
                Log.Warning("[AnimeArsenal] CompAbilityEffect_AoEBurst: AoEBurstTickManager " +
                            "MapComponent not found. Firing once as fallback.");
                AoEBurstHit.FireAt(caster.Position, caster, caster.Map, Props);
                return;
            }

            IntVec3 origin = caster.Position;
            int startTick = Find.TickManager.TicksGame;

            for (int i = 0; i < Props.repeatCount; i++)
            {
                manager.Schedule(new AoEBurstHit(
                    origin: origin,
                    caster: caster,
                    map: caster.Map,
                    props: Props,
                    fireTick: startTick + (Props.damageInterval * i)
                ));
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn pawn = parent.pawn;
            if (pawn?.Map == null) return;

            List<IntVec3> cells = GenRadial
                .RadialCellsAround(pawn.Position, Props.radius, true)
                .Where(c => c.InBounds(pawn.Map))
                .ToList();

            GenDraw.DrawFieldEdges(cells, Color.cyan);
        }
    }

    public class CompProperties_AbilityAoEBurst_Inner : CompProperties_AbilityAoEBurst
    {
        public CompProperties_AbilityAoEBurst_Inner()
        {
            compClass = typeof(CompAbilityEffect_AoEBurst_Inner);
        }
    }
    public class CompAbilityEffect_AoEBurst_Inner : CompAbilityEffect_AoEBurst { }

    public class CompProperties_AbilityAoEBurst_Outer : CompProperties_AbilityAoEBurst
    {
        public CompProperties_AbilityAoEBurst_Outer()
        {
            compClass = typeof(CompAbilityEffect_AoEBurst_Outer);
        }
    }
    public class CompAbilityEffect_AoEBurst_Outer : CompAbilityEffect_AoEBurst { }
    public class AoEBurstTickManager : MapComponent
    {
        private List<AoEBurstHit> pending = new List<AoEBurstHit>();

        public AoEBurstTickManager(Map map) : base(map) { }

        public void Schedule(AoEBurstHit hit) => pending.Add(hit);

        public override void MapComponentTick()
        {
            if (pending.Count == 0) return;

            int now = Find.TickManager.TicksGame;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (now >= pending[i].FireTick)
                {
                    pending[i].Fire();
                    pending.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "aoeBurstHits", LookMode.Deep);
            if (pending == null)
                pending = new List<AoEBurstHit>();
        }
    }

    public class AoEBurstHit : IExposable
    {
        private IntVec3 origin;
        private Pawn caster;
        private Map map;
        private CompProperties_AbilityAoEBurst props;
        public int FireTick;

        public AoEBurstHit() { }

        public AoEBurstHit(IntVec3 origin, Pawn caster, Map map,
            CompProperties_AbilityAoEBurst props, int fireTick)
        {
            this.origin = origin;
            this.caster = caster;
            this.map = map;
            this.props = props;
            this.FireTick = fireTick;
        }

        public void Fire() => FireAt(origin, caster, map, props);

        public static void FireAt(IntVec3 origin, Pawn caster, Map map,
            CompProperties_AbilityAoEBurst props)
        {
            if (map == null || !origin.InBounds(map)) return;

            if (props.effecter != null)
            {
                Effecter e = props.effecter.Spawn(origin, map);
                e.EffectTick(new TargetInfo(origin, map), TargetInfo.Invalid);
                e.EffectTick(new TargetInfo(origin, map), TargetInfo.Invalid);
                e.EffectTick(new TargetInfo(origin, map), TargetInfo.Invalid);
                e.Cleanup();
            }

            if (props.fleck != null)
                FleckMaker.Static(origin.ToVector3Shifted(), map, props.fleck);

            DamageDef dmgDef = props.damageDef ?? DamageDefOf.Cut;
            int hits = 0;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                origin, map, props.radius, true))
            {
                if (hits >= props.maxTargets) break;
                if (!(thing is Pawn hitPawn)) continue;
                if (hitPawn.Dead || hitPawn.Destroyed
                    || !hitPawn.Spawned) continue;
                if (!props.affectCaster && hitPawn == caster) continue;
                if (props.onlyAffectHostiles
                    && caster != null
                    && !hitPawn.HostileTo(caster)) continue;

                if (props.damage > 0)
                {
                    DamageInfo dinfo = new DamageInfo(
                        dmgDef, props.damage, props.armorPen, -1f, caster);
                    hitPawn.TakeDamage(dinfo);
                }

                if (props.hediff != null && props.hediffSeverity > 0f)
                {
                    Hediff existing = hitPawn.health.hediffSet
                        .GetFirstHediffOfDef(props.hediff);
                    if (existing != null)
                        existing.Severity += props.hediffSeverity;
                    else
                    {
                        Hediff newHediff = HediffMaker.MakeHediff(props.hediff, hitPawn);
                        newHediff.Severity = props.hediffSeverity;
                        hitPawn.health.AddHediff(newHediff);
                    }
                }

                if (props.hitEffecter != null)
                {
                    Effecter he = props.hitEffecter.Spawn(hitPawn.Position, map);
                    he.EffectTick(hitPawn, hitPawn);
                    he.EffectTick(hitPawn, hitPawn);
                    he.Cleanup();
                }

                hits++;
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref origin, "origin");
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref map, "map");
            Scribe_Deep.Look(ref props, "props");
            Scribe_Values.Look(ref FireTick, "fireTick");
        }
    }
}