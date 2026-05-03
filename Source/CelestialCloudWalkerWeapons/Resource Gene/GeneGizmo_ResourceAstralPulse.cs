using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using EMF;

namespace AnimeArsenal
{
    public class GeneGizmo_ResourceAstral : GeneGizmo_BasicResource
    {
        private const float TotalPulsateTime = 0.85f;
        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes = new List<Pair<IGeneResourceDrain, float>>();

        private Gene_BasicResource resourceGene;
        private readonly Color fallbackBarColor;
        private readonly Color fallbackBarHighlightColor;

        protected override string Title
        {
            get
            {
                if (resourceGene?.ResourceDef is EMF.AbilityResourceDef ard &&
                    !string.IsNullOrEmpty(ard.resourceName) &&
                    ard.resourceName != "unnamed resource")
                    return ard.resourceName;
                if (!string.IsNullOrEmpty(resourceGene?.def?.resourceLabel))
                    return resourceGene.def.resourceLabel;
                if (resourceGene?.def != null)
                    return resourceGene.def.label;
                return base.Title;
            }
        }

        protected override Color BarColor
        {
            get
            {
                if (resourceGene?.ResourceDef != null)
                    return resourceGene.ResourceDef.barColor;
                return fallbackBarColor;
            }
        }

        protected override Color BarHighlightColor
        {
            get
            {
                if (resourceGene?.ResourceDef != null)
                    return resourceGene.ResourceDef.barHighlightColor;
                return fallbackBarHighlightColor;
            }
        }




        protected override float ValuePercent
        {
            get
            {
                if (resourceGene != null)
                    return resourceGene.ValuePercent;
                return base.ValuePercent;
            }
        }

        protected override string BarLabel
        {
            get
            {
                if (resourceGene != null)
                    return $"{resourceGene.ValueForDisplay} / {resourceGene.MaxForDisplay}";
                return base.BarLabel;
            }
        }

        public GeneGizmo_ResourceAstral(Gene_BasicResource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor)
            : base(gene, drainGenes, barColor, barHighlightColor)
        {
            if (gene == null)
            {
                Log.Error("GeneGizmo_ResourceAstral created with null gene");
                return;
            }
            resourceGene = gene;
            fallbackBarColor = barColor;
            fallbackBarHighlightColor = barHighlightColor;
        }

        protected override IEnumerable<float> GetBarThresholds()
        {
            if (resourceGene != null)
            {
                yield return resourceGene.Max * 0.25f;
                yield return resourceGene.Max * 0.5f;
                yield return resourceGene.Max * 0.75f;
            }
        }

        protected override string GetTooltip()
        {
            if (resourceGene == null)
                return string.Empty;

            string resourceLabel = resourceGene.ResourceDef?.label ?? resourceGene.def?.label ?? "Resource";

            string text = $"{resourceLabel.CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor)}: {resourceGene.ValueForDisplay} / {resourceGene.MaxForDisplay}\n";

            BloodDemonArtsGene demonGene = resourceGene as BloodDemonArtsGene;
            if (resourceGene.def?.defName == "BloodDemonArt" && demonGene != null && resourceGene.pawn != null)
            {
                try { text += "\n" + "Demon Progression".Colorize(ColoredText.TipSectionTitleColor); }
                catch { }
            }

            if (resourceGene.ResourceDef?.regenStat != null)
            {
                text += $"\nRegenerates {resourceGene.ResourceDef.RegenStatValue(resourceGene.Pawn)} every " +
                        $"{resourceGene.ResourceDef.RegenTicksValue(resourceGene.Pawn).ToStringTicksToPeriod()}";
            }

            if (!(resourceGene.def?.resourceDescription.NullOrEmpty() ?? true))
            {
                text += $"\n\n{resourceGene.def.resourceDescription.Formatted(resourceGene.pawn.Named("PAWN")).Resolve()}";
            }

            return text;
        }
    }
}