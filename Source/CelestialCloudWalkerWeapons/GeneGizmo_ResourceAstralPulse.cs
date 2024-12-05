using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;



namespace CelestialCloudWalkerWeapons
{
    internal class GeneGizmo_ResourceAstral : GeneGizmo_Resource
    {
        private static readonly Texture2D IchorCostTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.78f, 0.72f, 0.66f));
        private const float TotalPulsateTime = 0.85f;
        private List<Pair<IGeneResourceDrain, float>> tmpDrainGenes = new List<Pair<IGeneResourceDrain, float>>();

        public GeneGizmo_ResourceAstral(Gene_Resource gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor)
            : base(gene, drainGenes, barColor, barHighlightColor)
        {
            if (gene == null)
            {
                Log.Error("GeneGizmo_ResourceAstral created with null gene");
                return;
            }
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (gene == null) return new GizmoResult(GizmoState.Clear);

            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float num = Mathf.Repeat(Time.time, TotalPulsateTime);

            if (gene is Resource_Gene cursedEnergy)
            {
                Log.Message(cursedEnergy.Def);
                Target = num;
            }
            return result;
        }

        protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
        {
            if (gene == null || gene.pawn == null) return;

            if ((gene.pawn.IsColonistPlayerControlled || gene.pawn.IsPrisonerOfColony) && gene is Resource_Gene)
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
            return string.Empty;
        }
    }
}
