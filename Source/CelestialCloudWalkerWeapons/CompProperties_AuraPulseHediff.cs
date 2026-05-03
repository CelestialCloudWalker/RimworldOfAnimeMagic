using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AuraPulseHediff : HediffCompProperties
    {
        public int intervalTicks = 60;
        public float radius = 8f;
        public HediffDef hediffDef;
        public float severityPerPulse = 0.15f;
        public float maxSeverity = 1.0f;
        public float damagePerPulse = 0f;
        public DamageDef damageDef;
        public float armorPen = 0f;
        public bool affectAllies = false;
        public bool affectSummoner = false;
        public bool affectNeutrals = false;
        public bool affectDowned = true;
        public EffecterDef pulseEffecter;
        public EffecterDef sourceEffecter;
        public List<GeneDef> requiredGenes = new List<GeneDef>();
        public List<HediffDef> requiredHediffs = new List<HediffDef>();
        public bool invertTargetFilter = false;

        public CompProperties_AuraPulseHediff()
        {
            compClass = typeof(HediffComp_AuraPulse);
        }
    }

    public class HediffComp_AuraPulse : HediffComp
    {
        public CompProperties_AuraPulseHediff Props => (CompProperties_AuraPulseHediff)props;

        private int tickCounter = 0;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn == null || Pawn.Map == null || !Pawn.Spawned)
                return;

            tickCounter++;

            if (tickCounter < Props.intervalTicks)
                return;

            tickCounter = 0;
            DoPulse();
        }

        private void DoPulse()
        {
            Map map = Pawn.Map;
            if (map == null) return;

            if (Props.sourceEffecter != null)
            {
                Effecter e = Props.sourceEffecter.Spawn(Pawn.Position, map);
                e.Trigger(new TargetInfo(Pawn.Position, map), TargetInfo.Invalid);
                e.Cleanup();
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                Pawn.Position, map, Props.radius, true))
            {
                if (thing is Pawn target && ShouldAffect(target))
                {
                    if (Props.hediffDef != null)
                        ApplyHediff(target);

                    if (Props.damagePerPulse > 0f)
                        ApplyDamage(target);

                    if (Props.pulseEffecter != null)
                    {
                        Effecter e = Props.pulseEffecter.Spawn(target.Position, map);
                        e.Trigger(new TargetInfo(target.Position, map), TargetInfo.Invalid);
                        e.Cleanup();
                    }
                }
            }
        }

        private void ApplyDamage(Pawn target)
        {
            if (target == null || target.Dead || target.Destroyed) return;

            DamageInfo dinfo = new DamageInfo(
                Props.damageDef ?? DamageDefOf.Blunt,
                Props.damagePerPulse,
                Props.armorPen,
                -1f,
                Pawn
            );
            target.TakeDamage(dinfo);
        }

        private bool ShouldAffect(Pawn target)
        {
            if (target == null || target.Dead || target.Destroyed)
                return false;

            if (!target.Spawned)
                return false;

            if (!Props.affectDowned && target.Downed)
                return false;

            bool hasFilter = Props.requiredGenes.Count > 0 || Props.requiredHediffs.Count > 0;
            if (hasFilter)
            {
                bool matchesFilter = MatchesGeneOrHediffFilter(target);
                if (Props.invertTargetFilter ? matchesFilter : !matchesFilter)
                    return false;
            }

            Faction sourceFaction = Pawn.Faction;

            if (sourceFaction == null)
                return true;

            if (target == Pawn && !Props.affectSummoner)
                return false;

            if (target.Faction == null)
                return Props.affectNeutrals;

            if (target.Faction == sourceFaction)
                return Props.affectAllies;

            if (!target.Faction.HostileTo(sourceFaction))
                return Props.affectNeutrals;

            return true;
        }

        private bool MatchesGeneOrHediffFilter(Pawn target)
        {
            if (Props.requiredGenes.Count > 0 && target.genes != null)
            {
                foreach (GeneDef geneDef in Props.requiredGenes)
                {
                    if (target.genes.HasActiveGene(geneDef))
                        return true;
                }
            }

            if (Props.requiredHediffs.Count > 0)
            {
                foreach (HediffDef hediffDef in Props.requiredHediffs)
                {
                    if (target.health.hediffSet.HasHediff(hediffDef))
                        return true;
                }
            }

            return false;
        }

        private void ApplyHediff(Pawn target)
        {
            if (Props.hediffDef == null) return;

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);

            if (existing != null)
            {
                if (existing.Severity < Props.maxSeverity)
                    existing.Severity = System.Math.Min(
                        existing.Severity + Props.severityPerPulse,
                        Props.maxSeverity);
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, target);
                hediff.Severity = Props.severityPerPulse;
                target.health.AddHediff(hediff);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref tickCounter, "tickCounter", 0);
        }
    }
}