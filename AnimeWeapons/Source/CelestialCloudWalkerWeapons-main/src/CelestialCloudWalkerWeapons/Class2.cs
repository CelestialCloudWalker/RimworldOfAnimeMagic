using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;



namespace CelestialCloudWalkerWeapons
{
    [StaticConstructorOnStartup]
    internal class GeneGizmo_ResourceAstralPulse : GeneGizmo_Resource
    {
        private static readonly Texture2D IchorCostTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.78f, 0.72f, 0.66f));

        private const float TotalPulsateTime = 0.85f;

        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes = new List<Pair<IGeneResourceDrain, float>>();

        public object RegenAmount { get; private set; }

        public GeneGizmo_ResourceAstralPulse(Gene_Resource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barhighlightColor)
            : base(gene, drainGenes, barColor, barhighlightColor)
        {
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float num = Mathf.Repeat(Time.time, 0.85f);
            if (gene is Resource_Gene  AstralPulse)
            {
                Target = num;
            }
            return result;
        }

        protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
        {
            if ((gene.pawn.IsColonistPlayerControlled || gene.pawn.IsPrisonerOfColony) && gene is Resource_Gene resourceGene)
            {
                headerRect.xMax -= 24f;
                Rect rect = new Rect(headerRect.xMax, headerRect.y, 24f, 24f);
                Widgets.DefIcon(rect, FleckDefOf.Heart);
                if (Mouse.IsOver(rect))
                {
                    Widgets.DrawHighlight(rect);
                }
            }
            base.DrawHeader(headerRect, ref mouseOverElement);
        }

        protected override string GetTooltip()
        {
            tmpDrainGenes.Clear();

            int RegenRateStatValue = 1;

            if (this.gene is Resource_Gene resourceGene)
            {
                RegenRateStatValue = (int)this.gene.pawn.GetStatValue(resourceGene.Def.regenStat);
            }
      
            string Regen = $" Regenerates {RegenAmount} every {GenDate.ToStringTicksToPeriod(RegenRateStatValue)}";
            string text = "";
            if (!gene.def.resourceDescription.NullOrEmpty())
            {
                text = text + "\n\n" + gene.def.resourceDescription.Formatted(gene.pawn.Named("PAWN")).Resolve();
            }
            return text + Regen;
        }
    }
}
