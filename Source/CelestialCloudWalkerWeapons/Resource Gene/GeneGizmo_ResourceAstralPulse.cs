using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Talented;
using UnityEngine;
using Verse;
using Verse.Sound;



namespace AnimeArsenal
{
    public class GeneGizmo_ResourceAstral : GeneGizmo_Resource
    {
        private const float TotalPulsateTime = 0.85f;
        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes = new List<Pair<IGeneResourceDrain, float>>();

        protected override string Title
        {
            get
            {
                if (gene is Gene_BasicResource resourceGene && resourceGene.Def != null)
                {
                    return resourceGene.Def.resourceLabel;
                }
                return base.Title;
            }
        }

        public GeneGizmo_ResourceAstral(Gene_Resource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor) : base(gene, drainGenes, barColor, barHighlightColor)
        {
            if (gene == null)
            {
                Log.Error("GeneGizmo_ResourceAstral created with null gene");
                return;
            }
        }

        //public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        //{
        //    if (gene == null) return new GizmoResult(GizmoState.Clear);

        //    GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
        //    float num = Mathf.Repeat(Time.time, TotalPulsateTime);

        //    if (gene is Resource_Gene cursedEnergy)
        //    {
        //        Target = num;
        //    }
        //    return result;
        //}

        //protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
        //{
        //    if (gene == null || gene.pawn == null) return;
        //    base.DrawHeader(headerRect, ref mouseOverElement);  
        //}

        protected override string GetTooltip()
        {
            if (!(gene is Gene_BasicResource resourceGene)) return "";

            string text = $"{resourceGene.Def.resourceLabel.CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor)}: {resourceGene.ValueForDisplay} / {resourceGene.MaxForDisplay}\n";

            string regen = $"\nRegenerates {resourceGene.RegenAmount} every {GenDate.ToStringTicksToPeriod(resourceGene.RegenTicks)}";

            if (!resourceGene.def.resourceDescription.NullOrEmpty())
            {
                text += $"\n\n{resourceGene.def.resourceDescription.Formatted(resourceGene.pawn.Named("PAWN")).Resolve()}";
            }

            return text + regen;
        }
    }
}
