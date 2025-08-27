using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Noise;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompProperties_Cleave : CompProperties_AbilityEffect
    {
        public int NumberOfCuts = 8;
        public float BaseDamage = 8f;
        public int TicksBetweenCuts = 10;
        public DamageDef DamageDef = DamageDefOf.Cut;
        public StatDef ScaleStat = StatDefOf.MeleeDamageFactor;
        public EffecterDef CleaveDamageEffecter;

        public float radius = 8f;
        public int maxTargets = 5;
        public float range = 20f;

        public CompProperties_Cleave()
        {
            compClass = typeof(CompAbilityEffect_Cleave);
        }
    }

    public class CompAbilityEffect_Cleave : CompAbilityEffect
    {
        public new CompProperties_Cleave Props => (CompProperties_Cleave)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (parent.pawn?.Map == null) return;

            IntVec3 targetCell = target.Cell;
            Map map = parent.pawn.Map;

            List<Pawn> targets = FindPawnsInRadius(targetCell, map);

            foreach (Pawn pawn in targets.Take(Props.maxTargets))
            {
                AppleCleaveEffects(pawn);
            }
        }

        private List<Pawn> FindPawnsInRadius(IntVec3 center, Map map)
        {
            List<Pawn> targets = new List<Pawn>();

            var cellsInRadius = GenRadial.RadialCellsAround(center, Props.radius, true);

            foreach (IntVec3 cell in cellsInRadius)
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> thingsInCell = cell.GetThingList(map);
                foreach (Thing thing in thingsInCell)
                {
                    if (thing is Pawn pawn && IsValidTarget(pawn))
                    {
                        targets.Add(pawn);
                    }
                }
            }

            return targets.OrderBy(p => p.Position.DistanceTo(center)).ToList();
        }

        private bool IsValidTarget(Pawn pawn)
        {
            if (pawn == parent.pawn) return false; 
            return !pawn.Dead;
        }

        private void AppleCleaveEffects(Pawn targetPawn)
        {
            
            parent.pawn.Map.GetComponent<CleaveManager>()?.StartCleaveSequence(targetPawn, Props, parent.pawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            
            if (target.IsValid) return true;

            if (throwMessages)
                Messages.Message("Must target a valid location", MessageTypeDefOf.RejectInput);

            return false;
        }
    }

    
    public class CleaveManager : MapComponent
    {
        private List<CleaveSequence> activeSequences = new List<CleaveSequence>();

        public CleaveManager(Map map) : base(map) { }

        public void StartCleaveSequence(Pawn target, CompProperties_Cleave props, Pawn caster)
        {
            activeSequences.Add(new CleaveSequence(target, props, caster));
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            for (int i = activeSequences.Count - 1; i >= 0; i--)
            {
                var sequence = activeSequences[i];
                sequence.Tick();

                if (sequence.IsComplete)
                {
                    activeSequences.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref activeSequences, "activeSequences", LookMode.Deep);
            if (activeSequences == null)
                activeSequences = new List<CleaveSequence>();
        }
    }

    
    public class CleaveSequence : IExposable
    {
        private Pawn targetPawn;
        private CompProperties_Cleave props;
        private Pawn caster;
        private int ticksElapsed;
        private int cutsApplied;
        private int nextCutTick;

        public bool IsComplete => cutsApplied >= props.NumberOfCuts || targetPawn?.Dead == true || targetPawn?.Destroyed == true;

        public CleaveSequence() { } 

        public CleaveSequence(Pawn target, CompProperties_Cleave properties, Pawn casterPawn)
        {
            targetPawn = target;
            props = properties;
            caster = casterPawn;
            ticksElapsed = 0;
            cutsApplied = 0;
            nextCutTick = props.TicksBetweenCuts;
        }

        public void Tick()
        {
            if (IsComplete) return;

            ticksElapsed++;

            if (ticksElapsed >= nextCutTick)
            {
                ApplyCut();
                cutsApplied++;
                nextCutTick = ticksElapsed + props.TicksBetweenCuts;
            }
        }

        private void ApplyCut()
        {
            if (targetPawn == null || targetPawn.Dead || targetPawn.Destroyed)
                return;

            if (props.CleaveDamageEffecter != null)
            {
                props.CleaveDamageEffecter.SpawnMaintained(targetPawn, targetPawn.MapHeld);
            }

            float damageMultiplier = 1f;
            if (props.ScaleStat != null && caster != null)
            {
                damageMultiplier = caster.GetStatValue(props.ScaleStat);
            }

            float actualDamage = props.BaseDamage * damageMultiplier;

            DamageInfo damageInfo = new DamageInfo(
                props.DamageDef ?? DamageDefOf.Cut,
                actualDamage,
                0f, 
                -1f,
                caster,
                null,
                null,
                DamageInfo.SourceCategory.ThingOrUnknown
            );

            targetPawn.TakeDamage(damageInfo);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref targetPawn, "targetPawn");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Deep.Look(ref props, "props");
            Scribe_Values.Look(ref ticksElapsed, "ticksElapsed");
            Scribe_Values.Look(ref cutsApplied, "cutsApplied");
            Scribe_Values.Look(ref nextCutTick, "nextCutTick");
        }
    }
}