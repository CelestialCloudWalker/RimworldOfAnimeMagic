using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{

    public class CompProperties_DelayedAoEBurst : CompProperties_AbilityEffect
    {
        public int delayTicks = 20;

        public float radius = 6f;
        public int maxTargets = 20;     

        public float damageAmount = 15f;
        public DamageDef damageDef;      
        public float armourPen = 0.3f;
        public bool canHitFriendly = false;

        public HediffDef hediffOnHit;
        public float hediffSeverity = 0.5f;

        public int stunTicksOnHit = 0;

        public int waveCount = 1;
        public int ticksBetweenWaves = 15;
        public float radiusGrowthPerWave = 0f;     
        public float damageDecayPerWave = 1f;       

        public bool fireAtCaster = false;

        public EffecterDef burstEffecter;
        public EffecterDef hitEffecter; 

        public CompProperties_DelayedAoEBurst()
        {
            compClass = typeof(CompAbilityEffect_DelayedAoEBurst);
        }
    }

    public class CompAbilityEffect_DelayedAoEBurst : CompAbilityEffect
    {
        public new CompProperties_DelayedAoEBurst Props =>
            (CompProperties_DelayedAoEBurst)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn pawn = parent?.pawn;
            if (pawn == null) return;

            Map map = pawn.Map;
            if (map == null) return;

            IntVec3 burstOrigin = Props.fireAtCaster
                ? pawn.Position
                : target.Cell;
            var manager = map.GetComponent<DelayedBurstManager>();
            if (manager == null)
            {
                Log.Warning("[AnimeArsenal] DelayedAoEBurst: DelayedBurstManager MapComponent not found. " +
                            "Make sure it is registered.");
                return;
            }

            manager.Schedule(new DelayedBurstJob(
                origin: burstOrigin,
                caster: pawn,
                map: map,
                props: Props,
                startTick: Find.TickManager.TicksGame + Props.delayTicks
            ));
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return parent?.pawn != null && target.IsValid;
        }
    }


    public class DelayedBurstManager : MapComponent
    {
        private List<DelayedBurstJob> pending = new List<DelayedBurstJob>();

        public DelayedBurstManager(Map map) : base(map) { }

        public void Schedule(DelayedBurstJob job)
        {
            pending.Add(job);
        }

        public override void MapComponentTick()
        {
            if (pending.Count == 0) return;

            int now = Find.TickManager.TicksGame;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                DelayedBurstJob job = pending[i];
                job.Tick(now);

                if (job.IsFinished)
                    pending.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pending, "delayedBurstJobs", LookMode.Deep);
            if (pending == null)
                pending = new List<DelayedBurstJob>();
        }
    }


    public class DelayedBurstJob : IExposable
    {
        private IntVec3 origin;
        private Pawn caster;
        private Map map;
        private CompProperties_DelayedAoEBurst props;
        private int nextWaveTick;
        private int wavesCompleted;

        public bool IsFinished => wavesCompleted >= props.waveCount;

        public DelayedBurstJob() { }

        public DelayedBurstJob(IntVec3 origin, Pawn caster, Map map,
            CompProperties_DelayedAoEBurst props, int startTick)
        {
            this.origin = origin;
            this.caster = caster;
            this.map = map;
            this.props = props;
            this.nextWaveTick = startTick;
            this.wavesCompleted = 0;
        }

        public void Tick(int now)
        {
            if (IsFinished) return;
            if (now < nextWaveTick) return;

            FireWave();
            wavesCompleted++;
            nextWaveTick = now + props.ticksBetweenWaves;
        }

        private void FireWave()
        {
            if (map == null || !origin.InBounds(map)) return;

            float waveRadius = props.radius
                + props.radiusGrowthPerWave * wavesCompleted;

            float waveDamage = props.damageAmount
                * Mathf.Pow(props.damageDecayPerWave, wavesCompleted);

            DamageDef dmgDef = props.damageDef ?? DamageDefOf.Cut;

            SpawnEffecter(props.burstEffecter, origin, origin);

            int hits = 0;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                origin, map, waveRadius, true))
            {
                if (hits >= props.maxTargets) break;
                if (!(thing is Pawn target)) continue;
                if (target.Dead || target.Destroyed || !target.Spawned) continue;
                if (target == caster) continue;
                if (!props.canHitFriendly
                    && caster != null
                    && target.Faction != null
                    && !target.Faction.HostileTo(caster.Faction))
                    continue;

                if (waveDamage > 0f)
                {
                    var dinfo = new DamageInfo(
                        dmgDef,
                        waveDamage,
                        props.armourPen,
                        -1f,
                        caster,
                        null, null,
                        DamageInfo.SourceCategory.ThingOrUnknown,
                        target
                    );
                    target.TakeDamage(dinfo);
                }

                if (props.hediffOnHit != null)
                {
                    Hediff existing = target.health.hediffSet
                        .GetFirstHediffOfDef(props.hediffOnHit);
                    if (existing != null)
                        existing.Severity += props.hediffSeverity;
                    else
                        target.health.AddHediff(props.hediffOnHit);
                }

                if (props.stunTicksOnHit > 0
                    && target.stances?.stunner != null)
                {
                    target.stances.stunner.StunFor(props.stunTicksOnHit, caster);
                }

                if (props.hitEffecter != null)
                    SpawnEffecter(props.hitEffecter, target.Position, target.Position);

                hits++;
            }
        }

        private void SpawnEffecter(EffecterDef def, IntVec3 a, IntVec3 b)
        {
            if (def == null || map == null) return;
            Effecter e = def.Spawn();
            e.Trigger(new TargetInfo(a, map), new TargetInfo(b, map));
            e.Cleanup();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref origin, "origin");
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref map, "map");
            Scribe_Deep.Look(ref props, "props");
            Scribe_Values.Look(ref nextWaveTick, "nextWaveTick");
            Scribe_Values.Look(ref wavesCompleted, "wavesCompleted");
        }
    }
}