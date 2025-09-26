using System;
using System.Collections.Generic;
using RimWorld;
using Talented;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class GeneGizmo_ResourceAstral : GeneGizmo_Resource
    {
        protected override bool DraggingBar
        {
            get
            {
                return this.IsDraggingBar;
            }
            set
            {
                this.IsDraggingBar = value;
            }
        }

        protected override string Title
        {
            get
            {
                Gene_BasicResource resourceGene = this.gene as Gene_BasicResource;
                bool flag = resourceGene != null && resourceGene.Def != null;
                string result;
                if (flag)
                {
                    result = resourceGene.Def.resourceLabel;
                }
                else
                {
                    result = base.Title;
                }
                return result;
            }
        }

        public GeneGizmo_ResourceAstral(Gene_Resource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor) : base(gene, drainGenes, barColor, barHighlightColor)
        {
            bool flag = gene == null;
            if (flag)
            {
                Log.Error("GeneGizmo_ResourceAstral created with null gene");
            }
        }

        protected override IEnumerable<float> GetBarThresholds()
        {
            Gene_BasicResource resourceGene = this.gene as Gene_BasicResource;
            if (resourceGene != null)
            {
                // Return thresholds at 25%, 50%, 75% of max resource
                yield return resourceGene.Max * 0.25f;
                yield return resourceGene.Max * 0.5f;
                yield return resourceGene.Max * 0.75f;
            }
        }

        protected override string GetTooltip()
        {
            Gene_BasicResource resourceGene = this.gene as Gene_BasicResource;
            bool flag = resourceGene == null;
            string result;
            if (flag)
            {
                result = "";
            }
            else
            {
                string text = string.Format("{0}: {1} / {2}\n",
                    resourceGene.Def.resourceLabel.CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor),
                    resourceGene.ValueForDisplay,
                    resourceGene.MaxForDisplay);

                // Only add demon info if this is actually a BloodDemonArtsGene and everything is initialized
                if (resourceGene.def.defName == "BloodDemonArt" && resourceGene is BloodDemonArtsGene demonGene && resourceGene.pawn != null)
                {
                    try
                    {
                        text += "\n" + "Demon Progression".Colorize(ColoredText.TipSectionTitleColor);
                    }
                    catch
                    {
                        // Skip demon info if there's any error
                    }
                }

                string regen = string.Format("\nRegenerates {0} every {1}",
                    resourceGene.RegenAmount,
                    resourceGene.RegenTicks.ToStringTicksToPeriod(true, false, true, true, false));
                bool flag2 = !resourceGene.def.resourceDescription.NullOrEmpty();
                if (flag2)
                {
                    text = text + "\n\n" + resourceGene.def.resourceDescription.Formatted(resourceGene.pawn.Named("PAWN")).Resolve();
                }
                result = text + regen;
            }
            return result;
        }

        private const float TotalPulsateTime = 0.85f;
        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes = new List<Pair<IGeneResourceDrain, float>>();
        protected bool IsDraggingBar = false;
    }
}