using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AuraPulse : CompProperties
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

        public CompProperties_AuraPulse()
        {
            compClass = typeof(CompAuraPulseHediff);
        }
    }

    public class CompAuraPulseHediff : ThingComp
    {
        public CompProperties_AuraPulse Props => (CompProperties_AuraPulse)props;

        public Pawn summoner;
        private int tickCounter = 0;

        public override void CompTick()
        {
            base.CompTick();

            if (parent == null || parent.Map == null || !parent.Spawned)
                return;

            tickCounter++;

            if (tickCounter < Props.intervalTicks)
                return;

            tickCounter = 0;
            DoPulse();
        }

        private void DoPulse()
        {
            Map map = parent.Map;
            if (map == null) return;

            if (Props.sourceEffecter != null)
            {
                Effecter e = Props.sourceEffecter.Spawn();
                e.Trigger(new TargetInfo(parent.Position, map), TargetInfo.Invalid);
                e.Cleanup();
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                parent.Position, map, Props.radius, true))
            {
                if (thing is Pawn pawn && ShouldAffect(pawn))
                {
                    if (Props.hediffDef != null)
                        ApplyHediff(pawn);

                    if (Props.damagePerPulse > 0f)
                        ApplyDamage(pawn);

                    if (Props.pulseEffecter != null)
                    {
                        Effecter e = Props.pulseEffecter.Spawn();
                        e.Trigger(new TargetInfo(pawn.Position, map), TargetInfo.Invalid);
                        e.Cleanup();
                    }
                }
            }
        }

        private void ApplyDamage(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) return;

            DamageInfo dinfo = new DamageInfo(
                Props.damageDef ?? DamageDefOf.Blunt,
                Props.damagePerPulse,
                Props.armorPen,
                -1f,
                parent as Pawn
            );
            pawn.TakeDamage(dinfo);
        }

        private bool ShouldAffect(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed)
                return false;

            if (!pawn.Spawned)
                return false;

            if (!Props.affectDowned && pawn.Downed)
                return false;

            bool hasFilter = Props.requiredGenes.Count > 0 || Props.requiredHediffs.Count > 0;
            if (hasFilter)
            {
                bool matchesFilter = MatchesGeneOrHediffFilter(pawn);
                if (Props.invertTargetFilter ? matchesFilter : !matchesFilter)
                    return false;
            }

            Faction sourceFaction = summoner?.Faction ?? parent.Faction;

            if (sourceFaction == null)
                return true;

            if (!Props.affectSummoner && summoner != null && pawn == summoner)
                return false;

            if (pawn.Faction == null)
                return Props.affectNeutrals;

            if (pawn.Faction == sourceFaction)
                return Props.affectAllies;

            if (!pawn.Faction.HostileTo(sourceFaction))
                return Props.affectNeutrals;

            return true;
        }

        private bool MatchesGeneOrHediffFilter(Pawn pawn)
        {
            if (Props.requiredGenes.Count > 0 && pawn.genes != null)
            {
                foreach (GeneDef geneDef in Props.requiredGenes)
                {
                    if (pawn.genes.HasActiveGene(geneDef))
                        return true;
                }
            }

            if (Props.requiredHediffs.Count > 0)
            {
                foreach (HediffDef hediffDef in Props.requiredHediffs)
                {
                    if (pawn.health.hediffSet.HasHediff(hediffDef))
                        return true;
                }
            }

            return false;
        }

        private void ApplyHediff(Pawn pawn)
        {
            if (Props.hediffDef == null) return;

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);

            if (existing != null)
            {
                if (existing.Severity < Props.maxSeverity)
                {
                    existing.Severity = System.Math.Min(
                        existing.Severity + Props.severityPerPulse,
                        Props.maxSeverity
                    );
                }
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
                hediff.Severity = Props.severityPerPulse;
                pawn.health.AddHediff(hediff);
            }
        }

        public void SetSummoner(Pawn pawn)
        {
            summoner = pawn;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref summoner, "summoner");
            Scribe_Values.Look(ref tickCounter, "tickCounter", 0);
        }
    }
}