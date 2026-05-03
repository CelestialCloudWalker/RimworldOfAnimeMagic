using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{

    public class HediffCompProperties_RegenSuppression : HediffCompProperties
    {
        public float regenMultiplier = 0f;
        public bool suppressWoundHealing = true;
        public bool suppressLimbRegen = true;
        public bool suppressOrganRegen = true;
        public bool suppressScarHealing = true;
        public float suppressAboveSeverity = 0f;
        public float suppressionWeight = 1f;

        public HediffCompProperties_RegenSuppression()
        {
            compClass = typeof(HediffComp_RegenSuppression);
        }
    }

    public class HediffComp_RegenSuppression : HediffComp
    {
        public HediffCompProperties_RegenSuppression Props =>
            (HediffCompProperties_RegenSuppression)props;
    }

    public class HediffCompProperties_ApplyHediffOnRemoved : HediffCompProperties
    {
        public HediffDef hediffToApply;
        public string onlyIfGenePresent;

        public HediffCompProperties_ApplyHediffOnRemoved()
        {
            compClass = typeof(HediffComp_ApplyHediffOnRemoved);
        }
    }

    public class HediffComp_ApplyHediffOnRemoved : HediffComp
    {
        public HediffCompProperties_ApplyHediffOnRemoved Props =>
            (HediffCompProperties_ApplyHediffOnRemoved)props;

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();

            if (Pawn == null || Pawn.Dead || Props.hediffToApply == null) return;

            if (!Props.onlyIfGenePresent.NullOrEmpty())
            {
                bool hasGene = false;
                if (Pawn.genes != null)
                {
                    var genes = Pawn.genes.GenesListForReading;
                    for (int i = 0; i < genes.Count; i++)
                    {
                        if (genes[i].def.defName == Props.onlyIfGenePresent)
                        {
                            hasGene = true;
                            break;
                        }
                    }
                }
                if (!hasGene) return;
            }

            Hediff existing = Pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffToApply);
            if (existing != null)
                Pawn.health.RemoveHediff(existing);

            Pawn.health.AddHediff(Props.hediffToApply);
        }
    }

    public class MapComponent_RegenSuppressionPruner : MapComponent
    {
        public MapComponent_RegenSuppressionPruner(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 600 == 0)
                RegenSuppressionTracker.PruneCache();
        }
    }


    public static class RegenSuppressionTracker
    {
        private struct CachedState
        {
            public int tick;
            public RegenSuppressionState state;
        }

        private static readonly Dictionary<int, CachedState> _cache
            = new Dictionary<int, CachedState>();

        public static RegenSuppressionState GetState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
                return RegenSuppressionState.None;

            int now = Find.TickManager.TicksGame;
            int id = pawn.thingIDNumber;

            if (_cache.TryGetValue(id, out CachedState cached) && cached.tick == now)
                return cached.state;

            RegenSuppressionState state = ComputeState(pawn);
            _cache[id] = new CachedState { tick = now, state = state };
            return state;
        }

        private static RegenSuppressionState ComputeState(Pawn pawn)
        {
            bool anyActive = false;
            bool suppressWound = false;
            bool suppressLimb = false;
            bool suppressOrgan = false;
            bool suppressScar = false;
            float weightedSum = 0f;
            float totalWeight = 0f;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            int hediffCount = hediffs.Count;

            for (int i = 0; i < hediffCount; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.ShouldRemove) continue;

                HediffWithComps hwc = hediff as HediffWithComps;
                if (hwc == null) continue;

                HediffComp_RegenSuppression regen =
                    hwc.TryGetComp<HediffComp_RegenSuppression>();
                if (regen == null) continue;

                HediffCompProperties_RegenSuppression p = regen.Props;
                if (hediff.Severity < p.suppressAboveSeverity) continue;

                anyActive = true;
                suppressWound |= p.suppressWoundHealing;
                suppressLimb |= p.suppressLimbRegen;
                suppressOrgan |= p.suppressOrganRegen;
                suppressScar |= p.suppressScarHealing;
                weightedSum += p.regenMultiplier * p.suppressionWeight;
                totalWeight += p.suppressionWeight;
            }

            if (!anyActive)
                return RegenSuppressionState.None;

            return new RegenSuppressionState
            {
                isActive = true,
                isRecovering = false,
                regenMultiplier = totalWeight > 0f
                                    ? Mathf.Clamp01(weightedSum / totalWeight)
                                    : 0f,
                suppressWound = suppressWound,
                suppressLimb = suppressLimb,
                suppressOrgan = suppressOrgan,
                suppressScar = suppressScar
            };
        }

        public static void PruneCache()
        {
            int now = Find.TickManager.TicksGame;
            List<int> stale = new List<int>();

            foreach (KeyValuePair<int, CachedState> kvp in _cache)
            {
                if (now - kvp.Value.tick > 120)
                    stale.Add(kvp.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                _cache.Remove(stale[i]);
        }
    }


    public struct RegenSuppressionState
    {
        public bool isActive;
        public bool isRecovering;
        public float regenMultiplier;
        public bool suppressWound;
        public bool suppressLimb;
        public bool suppressOrgan;
        public bool suppressScar;

        public bool AnyEffect => isActive || isRecovering;

        public static readonly RegenSuppressionState None = new RegenSuppressionState
        {
            isActive = false,
            isRecovering = false,
            regenMultiplier = 1f,
            suppressWound = false,
            suppressLimb = false,
            suppressOrgan = false,
            suppressScar = false
        };
    }
}