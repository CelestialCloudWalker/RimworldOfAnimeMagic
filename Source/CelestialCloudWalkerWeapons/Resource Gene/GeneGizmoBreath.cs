using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    [StaticConstructorOnStartup]
    public class GeneGizmoBreath : GeneGizmo_ResourceAstral
    {
        private BreathingTechniqueGene BreathingTechniqueGene => (BreathingTechniqueGene)gene;
        protected override bool IsDraggable => false;
        private static readonly Texture2D DragBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.74f, 0.97f, 0.8f));

        public GeneGizmoBreath(BreathingTechniqueGene gene, List<IGeneResourceDrain> drainGenes, Color barColor, Color barHighlightColor) : base(gene, drainGenes, barColor, barHighlightColor)
        {

        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float progress = BreathingTechniqueGene.ExhaustionProgress;
            DrawDraggableBarTarget(barRect, progress, DragBarTex);
            return result;
        }

        private static void DrawDraggableBarTarget(Rect rect, float percent, Texture2D targetTex)
        {
            float num = Mathf.Round((rect.width - 8f) * percent);
            GUI.DrawTexture(new Rect
            {
                x = rect.x + 3f + num,
                y = rect.y,
                width = 2f,
                height = rect.height
            }, targetTex);
            float num2 = UIScaling.AdjustCoordToUIScalingFloor(rect.x + 2f + num);
            float xMax = UIScaling.AdjustCoordToUIScalingCeil(num2 + 4f);
            Rect rect2 = new Rect
            {
                y = rect.y - 3f,
                height = 5f,
                xMin = num2,
                xMax = xMax
            };
            GUI.DrawTexture(rect2, targetTex);
            Rect position = rect2;
            position.y = rect.yMax - 2f;
            GUI.DrawTexture(position, targetTex);
        }

    }
}
